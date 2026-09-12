targetScope = 'resourceGroup'

@description('Environment short name. Expected values: dev or prod.')
param environmentName string

@description('Azure region for all resources.')
param location string

@description('Tags applied to all supported resources.')
param tags object = {}

@description('App Service plan name.')
param appServicePlanName string

@description('App Service plan sku name, for example B1 or P1v3.')
param appServicePlanSkuName string = 'B1'

@description('App Service plan sku tier.')
param appServicePlanSkuTier string = 'Basic'

@description('API App Service name.')
param apiAppServiceName string

@description('Static Web App name.')
param staticWebAppName string

@description('Application Insights component name.')
param applicationInsightsName string

@description('Azure SQL logical server name.')
param sqlServerName string

@description('Azure SQL database name.')
param sqlDatabaseName string

@description('Azure SQL admin username.')
param sqlAdminUsername string

@secure()
@description('Azure SQL admin password.')
param sqlAdminPassword string

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminUsername
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: appServicePlanSkuName
    tier: appServicePlanSkuTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource apiAppService 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppServiceName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'PulseCheck__EnvironmentName'
          value: environmentName
        }
        {
          name: 'PulseCheck__DatabaseProvider'
          value: 'SqlServer'
        }
        {
          name: 'ConnectionStrings__PulseCheckDb'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminUsername};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    publicNetworkAccess: 'Enabled'
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output apiAppServiceName string = apiAppService.name
output apiAppServiceUrl string = 'https://${apiAppService.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
