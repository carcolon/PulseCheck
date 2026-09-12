using './main.bicep'

param environmentName = 'dev'
param location = 'eastus2'
param tags = {
  application: 'PulseCheck'
  environment: 'dev'
  owner: 'platform'
}
param appServicePlanName = 'asp-swa-dev'
param appServicePlanSkuName = 'B1'
param appServicePlanSkuTier = 'Basic'
param apiAppServiceName = 'swa-back-dev'
param staticWebAppName = 'swa-front-dev'
param applicationInsightsName = 'swa-back-dev'
param sqlServerName = 'sql-server-swa-dev'
param sqlDatabaseName = 'PulseCheckDb'
param sqlAdminUsername = 'pulsecheckadmin'
