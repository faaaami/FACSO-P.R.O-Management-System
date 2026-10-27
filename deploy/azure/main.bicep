// FASCO PRO on Azure Container Apps.
//
// Mirrors docker-compose.yml: PostgreSQL, Redis, the API and the web app
// (nginx serving the SPA and proxying /api and /hubs to the API). Only the web
// app is public; the API is reachable inside the Container Apps environment
// only. Uploaded documents live on an Azure Files share mounted at
// /app/storage, so they survive restarts and redeploys.
//
// Deploy in two passes (see deploy/azure/README.md): first without images to
// create the registry, then with the image tags after pushing them.

targetScope = 'resourceGroup'

@description('Short lowercase name used in every resource name, e.g. "fascopro".')
@minLength(3)
@maxLength(12)
param prefix string

param location string = resourceGroup().location

@description('API image, e.g. "<registry>.azurecr.io/fasco-pro-api:<tag>". Leave empty on the first pass to create only the shared resources.')
param apiImage string = ''

@description('Web image, e.g. "<registry>.azurecr.io/fasco-pro-web:<tag>".')
param webImage string = ''

@description('Public URL of the site, e.g. "https://pro.fasco.ae". Leave empty to use the web app\'s default *.azurecontainerapps.io address.')
param publicUrl string = ''

// --- PostgreSQL ---------------------------------------------------------------

param postgresAdminLogin string = 'proadmin'

@secure()
param postgresAdminPassword string

// --- Application secrets and settings -----------------------------------------

@secure()
@minLength(32)
param jwtSecret string

param smtpHost string
param smtpPort int = 587
param smtpUsername string = ''

@secure()
param smtpPassword string = ''

param smtpFromAddress string

param razorpayKeyId string

@secure()
param razorpayKeySecret string

@secure()
param razorpayWebhookSecret string

@allowed([ 'Local', 'Mock' ])
param extractionProvider string = 'Local'

@description('First Admin account, created only when no Admin exists. Clear after the first start.')
param adminEmail string = ''

@secure()
param adminPassword string = ''

// --- Names --------------------------------------------------------------------

var suffix = uniqueString(resourceGroup().id, prefix)
var registryName = toLower('${prefix}acr${suffix}')
var storageAccountName = take(toLower('${prefix}st${suffix}'), 24)
var apiAppName = '${prefix}-api'
var webAppName = '${prefix}-web'
var databaseName = 'pro'
var shareName = 'storage'
var deployApps = !empty(apiImage) && !empty(webImage)

// The .NET images run as this non-root user; the share is mounted as it.
var appUid = '1654'

var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d')

// --- Monitoring ---------------------------------------------------------------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// --- Container registry and pull identity -------------------------------------

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-pull'
  location: location
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Document storage ---------------------------------------------------------

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Container Apps mounts Azure Files with the account key.
    allowSharedKeyAccess: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    shareDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
  }
}

resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: shareName
  properties: {
    shareQuota: 100
  }
}

// --- PostgreSQL ---------------------------------------------------------------

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${prefix}-pg-${suffix}'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 0.0.0.0 - 0.0.0.0 is Azure's "allow access from Azure services" rule. The
// Consumption Container Apps environment has no fixed outbound IP to list.
resource postgresAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// --- Redis (SignalR backplane and cache) --------------------------------------

resource redis 'Microsoft.Cache/redisEnterprise@2025-04-01' = {
  name: '${prefix}-redis-${suffix}'
  location: location
  sku: { name: 'Balanced_B0' }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-04-01' = {
  parent: redis
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    // A single endpoint, so the standard (non-cluster-aware) client works.
    clusteringPolicy: 'EnterpriseCluster'
    evictionPolicy: 'VolatileLRU'
    accessKeysAuthentication: 'Enabled'
  }
}

// --- Container Apps environment -----------------------------------------------

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource environmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: environment
  name: 'documents'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: share.name
      accessMode: 'ReadWrite'
    }
  }
}

// --- Apps ---------------------------------------------------------------------

resource web 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${pullIdentity.id}': {} }
  }
  dependsOn: [ acrPull ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: pullIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            // Internal calls address the API app by name; its ingress routes
            // on the Host header, so that must be the app name too.
            { name: 'API_UPSTREAM', value: 'http://${apiAppName}' }
            { name: 'API_HOST_HEADER', value: apiAppName }
            // TLS is terminated by the Container Apps ingress in front.
            { name: 'FORWARDED_PROTO', value: 'https' }
            // The ingress appends the real client address to X-Forwarded-For.
            { name: 'CLIENT_IP', value: '$xff_last' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/', port: 80 }
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

var siteUrl = !empty(publicUrl)
  ? publicUrl
  : (deployApps ? 'https://${web!.properties.configuration.ingress.fqdn}' : '')

var postgresConnection = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require'

var redisConnection = '${redis.properties.hostName}:10000,password=${redisDatabase.listKeys().primaryKey},ssl=True,abortConnect=False'

resource api 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${pullIdentity.id}': {} }
  }
  dependsOn: [ acrPull, environmentStorage, postgresDatabase, postgresAllowAzure ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        // Reachable from the web app only, never from the internet.
        external: false
        targetPort: 8080
        transport: 'auto'
        // nginx calls it over plain HTTP inside the environment.
        allowInsecure: true
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: pullIdentity.id
        }
      ]
      secrets: [
        { name: 'db-connection', value: postgresConnection }
        { name: 'redis-connection', value: redisConnection }
        { name: 'jwt-secret', value: jwtSecret }
        { name: 'smtp-password', value: empty(smtpPassword) ? 'unused' : smtpPassword }
        { name: 'razorpay-key-secret', value: razorpayKeySecret }
        { name: 'razorpay-webhook-secret', value: razorpayWebhookSecret }
        { name: 'admin-password', value: empty(adminPassword) ? 'unused' : adminPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          env: concat([
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'Database__MigrateOnStartup', value: 'true' }
            { name: 'ForwardedHeaders__Enabled', value: 'true' }
            { name: 'ForwardedHeaders__ClientIpHeader', value: 'X-Client-IP' }
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'db-connection' }
            { name: 'ConnectionStrings__Redis', secretRef: 'redis-connection' }
            { name: 'Jwt__Secret', secretRef: 'jwt-secret' }
            { name: 'App__FrontendBaseUrl', value: siteUrl }
            { name: 'Cors__AllowedOrigins__0', value: siteUrl }
            { name: 'Smtp__Host', value: smtpHost }
            { name: 'Smtp__Port', value: string(smtpPort) }
            { name: 'Smtp__Username', value: smtpUsername }
            { name: 'Smtp__FromAddress', value: smtpFromAddress }
            { name: 'Razorpay__KeyId', value: razorpayKeyId }
            { name: 'Razorpay__KeySecret', secretRef: 'razorpay-key-secret' }
            { name: 'Razorpay__WebhookSecret', secretRef: 'razorpay-webhook-secret' }
            { name: 'Storage__RootPath', value: '/app/storage' }
            { name: 'Extraction__Provider', value: extractionProvider }
          ], empty(smtpPassword) ? [] : [
            { name: 'Smtp__Password', secretRef: 'smtp-password' }
          ], empty(adminEmail) ? [] : [
            { name: 'ADMIN_EMAIL', value: adminEmail }
            { name: 'ADMIN_PASSWORD', secretRef: 'admin-password' }
          ])
          volumeMounts: [
            {
              volumeName: 'documents'
              mountPath: '/app/storage'
            }
          ]
          probes: [
            {
              // Migrations run before the app listens, so allow a slow start.
              type: 'Startup'
              tcpSocket: { port: 8080 }
              periodSeconds: 10
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              tcpSocket: { port: 8080 }
            }
            {
              type: 'Readiness'
              httpGet: { path: '/api/v1/health', port: 8080 }
              periodSeconds: 15
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'documents'
          storageType: 'AzureFile'
          storageName: environmentStorage.name
          mountOptions: 'uid=${appUid},gid=${appUid},dir_mode=0770,file_mode=0660'
        }
      ]
      // Exactly one replica: migrations run on start (one instance at a
      // time), and Hangfire jobs and the in-process OCR limit assume one API.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output apiAppName string = apiAppName
output webAppName string = webAppName
output siteUrl string = siteUrl
output webhookUrl string = empty(siteUrl) ? '' : '${siteUrl}/api/v1/client/payments/webhook'
output postgresHost string = postgres.properties.fullyQualifiedDomainName
