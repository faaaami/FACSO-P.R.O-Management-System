// Parameters for main.bicep. Every value is read from an environment variable,
// so nothing secret is ever written to this file. Set them in your shell (see
// deploy/azure/README.md) before running `az deployment group create`.
using './main.bicep'

param prefix = readEnvironmentVariable('AZ_PREFIX', 'fascopro')

// Empty on the first pass; the image references on the second.
param apiImage = readEnvironmentVariable('API_IMAGE', '')
param webImage = readEnvironmentVariable('WEB_IMAGE', '')

param publicUrl = readEnvironmentVariable('PUBLIC_URL', '')

param postgresAdminPassword = readEnvironmentVariable('POSTGRES_PASSWORD')

param jwtSecret = readEnvironmentVariable('JWT_SECRET')

param smtpHost = readEnvironmentVariable('SMTP_HOST')
param smtpPort = int(readEnvironmentVariable('SMTP_PORT', '587'))
param smtpUsername = readEnvironmentVariable('SMTP_USERNAME', '')
param smtpPassword = readEnvironmentVariable('SMTP_PASSWORD', '')
param smtpFromAddress = readEnvironmentVariable('SMTP_FROM')

param razorpayKeyId = readEnvironmentVariable('RAZORPAY_KEY_ID')
param razorpayKeySecret = readEnvironmentVariable('RAZORPAY_KEY_SECRET')
param razorpayWebhookSecret = readEnvironmentVariable('RAZORPAY_WEBHOOK_SECRET')

param extractionProvider = readEnvironmentVariable('EXTRACTION_PROVIDER', 'Local')

param adminEmail = readEnvironmentVariable('ADMIN_EMAIL', '')
param adminPassword = readEnvironmentVariable('ADMIN_PASSWORD', '')
