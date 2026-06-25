@description('Environment name (dev or prod)')
param environment string

@description('Azure region')
param location string

@description('Log Analytics workspace ID')
param logAnalyticsWorkspaceId string

var appInsightsName = 'appi-mox-${environment}'

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
}

output appInsightsName string = appInsights.name
output connectionString string = appInsights.properties.ConnectionString
output instrumentationKey string = appInsights.properties.InstrumentationKey
