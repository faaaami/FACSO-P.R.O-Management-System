# Deploying FASCO PRO to Azure

The app runs on **Azure Container Apps**, the same shape as `docker-compose.yml`:

| Part | Azure resource | Notes |
|---|---|---|
| Web (nginx + SPA) | Container App `<prefix>-web` | The only public endpoint; proxies `/api` and `/hubs` |
| API | Container App `<prefix>-api` | Internal only; exactly **one** replica |
| PostgreSQL | Azure Database for PostgreSQL – Flexible Server (B1ms, v16) | 7-day automatic backups |
| Redis | Azure Managed Redis (Balanced B0) | SignalR backplane and cache |
| Uploaded documents | Azure Files share mounted at `/app/storage` | 14-day soft delete |
| Images | Azure Container Registry (Basic) | Pulled with a managed identity |
| Logs | Log Analytics workspace | Container logs, 30 days |

Everything is defined in [`main.bicep`](main.bicep). Parameters come from
environment variables via [`main.bicepparam`](main.bicepparam), so no secret is
ever written to a file.

Commands below are for **PowerShell**. You need the
[Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), an Azure
subscription, and admin rights on the GitHub repository.

---

## 1. Sign in and create a resource group

```powershell
az login
az account set --subscription "<subscription name or id>"

$RG = "fasco-pro-prod"
$LOCATION = "uaenorth"   # pick the region closest to your users
az group create --name $RG --location $LOCATION

foreach ($p in "Microsoft.App","Microsoft.OperationalInsights","Microsoft.ContainerRegistry",
               "Microsoft.DBforPostgreSQL","Microsoft.Cache","Microsoft.Storage",
               "Microsoft.ManagedIdentity") {
  az provider register --namespace $p
}
```

Check that **Azure Managed Redis** and **PostgreSQL Flexible Server** are
offered in your region (Azure portal → Create a resource). If not, choose a
nearby region for the whole resource group.

## 2. Set the parameters

```powershell
function New-Secret { $b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b) }

$env:AZ_PREFIX               = "fascopro"          # 3–12 lowercase letters/digits
$env:POSTGRES_PASSWORD       = New-Secret
$env:JWT_SECRET              = New-Secret
$env:SMTP_HOST               = "smtp.example.com"
$env:SMTP_PORT               = "587"
$env:SMTP_USERNAME           = "..."
$env:SMTP_PASSWORD           = "..."
$env:SMTP_FROM               = "pro@fasco.ae"
$env:RAZORPAY_KEY_ID         = "rzp_live_..."
$env:RAZORPAY_KEY_SECRET     = "..."
$env:RAZORPAY_WEBHOOK_SECRET = New-Secret         # also entered in the Razorpay dashboard (step 7)
```

**Save the generated values in a password manager now.** Bicep needs them
again every time you re-run it, and `POSTGRES_PASSWORD` cannot be recovered.

## 3. Check the template before creating anything

```powershell
az bicep build --file deploy/azure/main.bicep
az deployment group what-if -g $RG --template-file deploy/azure/main.bicep --parameters deploy/azure/main.bicepparam
```

`what-if` lists every resource it would create without creating anything.

## 4. First pass: shared resources

With `API_IMAGE` / `WEB_IMAGE` unset, only the registry, database, Redis,
storage and the Container Apps environment are created (PostgreSQL and Redis
take 10–20 minutes).

```powershell
az deployment group create -g $RG --name infra `
  --template-file deploy/azure/main.bicep --parameters deploy/azure/main.bicepparam

$REGISTRY = az deployment group show -g $RG -n infra --query properties.outputs.registryName.value -o tsv
```

## 5. Let GitHub Actions deploy

Create an identity for the workflow that signs in with OIDC (no password is
stored in GitHub) and may push images and update the apps:

```powershell
$REPO = "faaaami/FACSO-P.R.O-Management-System"
$APP  = az ad app create --display-name "fasco-pro-github-deploy" --query appId -o tsv
az ad sp create --id $APP | Out-Null

@"
{ "name": "github-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:${REPO}:environment:production",
  "audiences": ["api://AzureADTokenExchange"] }
"@ | Out-File -Encoding utf8 fic.json
az ad app federated-credential create --id $APP --parameters fic.json
Remove-Item fic.json

$RG_ID  = az group show -n $RG --query id -o tsv
$ACR_ID = az acr show -n $REGISTRY --query id -o tsv
az role assignment create --assignee $APP --role "Contributor" --scope $RG_ID
az role assignment create --assignee $APP --role "AcrPush"     --scope $ACR_ID
```

Then add the repository settings (GitHub → Settings → Secrets and variables →
Actions, or with the GitHub CLI):

```powershell
gh secret set AZURE_CLIENT_ID       --body $APP
gh secret set AZURE_TENANT_ID       --body (az account show --query tenantId -o tsv)
gh secret set AZURE_SUBSCRIPTION_ID --body (az account show --query id -o tsv)
gh variable set AZURE_RESOURCE_GROUP --body $RG
gh variable set AZURE_REGISTRY_NAME  --body $REGISTRY
gh variable set AZURE_API_APP_NAME   --body "$env:AZ_PREFIX-api"
gh variable set AZURE_WEB_APP_NAME   --body "$env:AZ_PREFIX-web"
```

Create a GitHub environment named **production** (Settings → Environments).
Adding yourself as a required reviewer there makes every deploy wait for your
approval.

Run the workflow once by hand (Actions → **Deploy to Azure** → Run workflow).
The apps do not exist yet, so it pushes the images and prints their names
(`API_IMAGE=...`, `WEB_IMAGE=...`) in the run summary.

## 6. Second pass: the apps

```powershell
$env:API_IMAGE      = "<API_IMAGE from the workflow run>"
$env:WEB_IMAGE      = "<WEB_IMAGE from the workflow run>"
$env:ADMIN_EMAIL    = "you@fasco.ae"   # first Admin, created only if none exists
$env:ADMIN_PASSWORD = "<a strong password>"

az deployment group create -g $RG --name apps `
  --template-file deploy/azure/main.bicep --parameters deploy/azure/main.bicepparam

az deployment group show -g $RG -n apps --query properties.outputs -o table
```

The API applies all database migrations on its first start, then the site is
live at `siteUrl`. Sign in with the Admin account, then **remove
`ADMIN_EMAIL` / `ADMIN_PASSWORD`** and re-run this step so the password is no
longer stored on the app.

From now on, every push to `main` that passes CI is deployed automatically.

## 7. Razorpay webhook

In the Razorpay dashboard → Settings → Webhooks, add the `webhookUrl` output
(`https://<site>/api/v1/client/payments/webhook`) with the same secret as
`RAZORPAY_WEBHOOK_SECRET`, and enable at least `payment.captured`,
`order.paid` and `payment.failed`.

## 8. Your own domain (optional)

```powershell
az containerapp hostname add  -g $RG -n "$env:AZ_PREFIX-web" --hostname pro.fasco.ae
az containerapp hostname bind -g $RG -n "$env:AZ_PREFIX-web" --hostname pro.fasco.ae `
  --environment "$env:AZ_PREFIX-env" --validation-method CNAME
```

Create the DNS records the first command asks for, then set
`$env:PUBLIC_URL = "https://pro.fasco.ae"`, re-run step 6 (so links in emails
and CORS use the new address) and update the Razorpay webhook URL.

---

## Things to know

- **Re-running Bicep sets the images to `API_IMAGE` / `WEB_IMAGE`.** Always use
  the tags that are currently deployed (Azure portal → Container App →
  Containers), or you roll the apps back.
- **One API replica, on purpose.** Migrations run on start, Hangfire jobs and
  the OCR concurrency limit assume a single API. Do not raise `maxReplicas` for
  the API without revisiting those.
- **Client IP for the login rate limit.** Requests pass through the Container
  Apps ingress, nginx and the internal ingress. nginx forwards the address the
  public ingress saw in `X-Client-IP`, and the API reads it
  (`ForwardedHeaders__ClientIpHeader`). The API is never reachable from the
  internet, so that header cannot be forged by a client.
- **Backups.** PostgreSQL keeps 7 days of point-in-time restore; deleted files
  on the share are kept for 14 days. For longer retention, schedule
  `deploy/backup.sh` or Azure Backup for the file share.
- **Logs.** Azure portal → Container App → Log stream, or query the Log
  Analytics workspace.
- **Cost.** The B1ms database, Balanced B0 Redis, Basic registry and two small
  always-on Container Apps are the smallest production sizes. Check current
  prices for your region in the Azure pricing calculator before deploying.
