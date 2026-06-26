targetScope = 'resourceGroup'

@description('Environment name')
@allowed(['dev', 'prod'])
param environment string

@description('Azure region')
param location string = resourceGroup().location

@description('SQL admin login')
param sqlAdminLogin string = 'moxadmin'

@secure()
@description('SQL admin password')
param sqlAdminPassword string

// --- Well-known RBAC role definition GUIDs ---
var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorRole = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'

// --- SQL Server + Database ---
module sql 'modules/sql.bicep' = {
  name: 'sql-${environment}'
  params: {
    environment: environment
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

// --- Storage Account (Blob + Queue) ---
module storage 'modules/storage.bicep' = {
  name: 'storage-${environment}'
  params: {
    environment: environment
    location: location
  }
}

// --- Key Vault ---
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-${environment}'
  params: {
    environment: environment
    location: location
    tenantId: subscription().tenantId
  }
}

// --- Log Analytics ---
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics-${environment}'
  params: {
    environment: environment
    location: location
  }
}

// --- Application Insights ---
module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights-${environment}'
  params: {
    environment: environment
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// --- Container Registry ---
module acr 'modules/acr.bicep' = {
  name: 'acr-${environment}'
  params: {
    environment: environment
    location: location
  }
}

// --- Container Apps Environment ---
module containerEnv 'modules/container-env.bicep' = {
  name: 'container-env-${environment}'
  params: {
    environment: environment
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// --- Container App: Backend API ---
// Network isolation: internal ingress only — reachable by Frontend and Worker
// within the same Container Apps Environment via internal DNS (http://ca-mox-api-{env}).
// Not accessible from the public internet. No API keys or mTLS needed.
module apiApp 'modules/container-app.bicep' = {
  name: 'ca-api-${environment}'
  params: {
    appName: 'ca-mox-api-${environment}'
    location: location
    containerEnvId: containerEnv.outputs.envId
    ingressType: 'internal'
    allowInsecure: true // HTTP within ACA environment — no TLS overhead for internal traffic
    cpu: '0.5'
    memory: '1Gi'
    minReplicas: 0
    maxReplicas: 3
  }
}

// --- Container App: Frontend ---
module frontendApp 'modules/container-app.bicep' = {
  name: 'ca-frontend-${environment}'
  params: {
    appName: 'ca-mox-frontend-${environment}'
    location: location
    containerEnvId: containerEnv.outputs.envId
    ingressType: 'external'
    cpu: '0.5'
    memory: '1Gi'
    minReplicas: 0
    maxReplicas: 3
  }
}

// --- Container App: Worker ---
module workerApp 'modules/container-app.bicep' = {
  name: 'ca-worker-${environment}'
  params: {
    appName: 'ca-mox-worker-${environment}'
    location: location
    containerEnvId: containerEnv.outputs.envId
    ingressType: 'disabled'
    cpu: '1'
    memory: '2Gi'
    minReplicas: 0
    maxReplicas: 2
  }
}

// --- RBAC: Key Vault Secrets User ---
module apiKvRole 'modules/role-assignment.bicep' = {
  name: 'role-api-kv-${environment}'
  params: {
    principalId: apiApp.outputs.principalId
    roleDefinitionId: keyVaultSecretsUserRole
    uniqueSuffix: 'api-kv-${environment}'
  }
}

module frontendKvRole 'modules/role-assignment.bicep' = {
  name: 'role-frontend-kv-${environment}'
  params: {
    principalId: frontendApp.outputs.principalId
    roleDefinitionId: keyVaultSecretsUserRole
    uniqueSuffix: 'frontend-kv-${environment}'
  }
}

module workerKvRole 'modules/role-assignment.bicep' = {
  name: 'role-worker-kv-${environment}'
  params: {
    principalId: workerApp.outputs.principalId
    roleDefinitionId: keyVaultSecretsUserRole
    uniqueSuffix: 'worker-kv-${environment}'
  }
}

// --- RBAC: Storage Blob Data Contributor ---
module apiBlobRole 'modules/role-assignment.bicep' = {
  name: 'role-api-blob-${environment}'
  params: {
    principalId: apiApp.outputs.principalId
    roleDefinitionId: storageBlobDataContributorRole
    uniqueSuffix: 'api-blob-${environment}'
  }
}

module workerBlobRole 'modules/role-assignment.bicep' = {
  name: 'role-worker-blob-${environment}'
  params: {
    principalId: workerApp.outputs.principalId
    roleDefinitionId: storageBlobDataContributorRole
    uniqueSuffix: 'worker-blob-${environment}'
  }
}

// --- RBAC: Storage Queue Data Contributor ---
module apiQueueRole 'modules/role-assignment.bicep' = {
  name: 'role-api-queue-${environment}'
  params: {
    principalId: apiApp.outputs.principalId
    roleDefinitionId: storageQueueDataContributorRole
    uniqueSuffix: 'api-queue-${environment}'
  }
}

module workerQueueRole 'modules/role-assignment.bicep' = {
  name: 'role-worker-queue-${environment}'
  params: {
    principalId: workerApp.outputs.principalId
    roleDefinitionId: storageQueueDataContributorRole
    uniqueSuffix: 'worker-queue-${environment}'
  }
}

// --- Outputs ---
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlConnectionString string = sql.outputs.connectionString
output storageBlobEndpoint string = storage.outputs.blobEndpoint
output storageQueueEndpoint string = storage.outputs.queueEndpoint
output keyVaultUri string = keyVault.outputs.vaultUri
output appInsightsConnectionString string = appInsights.outputs.connectionString
output acrLoginServer string = acr.outputs.loginServer
output containerEnvName string = containerEnv.outputs.envName
output frontendFqdn string = frontendApp.outputs.fqdn
output apiAppName string = apiApp.outputs.appName
output frontendAppName string = frontendApp.outputs.appName
output workerAppName string = workerApp.outputs.appName
