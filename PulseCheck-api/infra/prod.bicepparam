using './main.bicep'

param environmentName = 'prod'
param location = 'eastus2'
param tags = {
  application: 'PulseCheck'
  environment: 'prod'
  owner: 'platform'
}
param appServicePlanName = 'asp-swa-prod'
param appServicePlanSkuName = 'P1v3'
param appServicePlanSkuTier = 'PremiumV3'
param apiAppServiceName = 'swa-back-prod'
param staticWebAppName = 'swa-front-prod'
param applicationInsightsName = 'swa-back-prod'
param sqlServerName = 'sql-server-swa-prod'
param sqlDatabaseName = 'PulseCheckDb'
param sqlAdminUsername = 'pulsecheckadmin'
