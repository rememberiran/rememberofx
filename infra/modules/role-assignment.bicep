@description('Principal ID to assign the role to')
param principalId string

@description('Role definition ID (the GUID portion)')
param roleDefinitionId string

@description('A unique suffix for the role assignment name')
param uniqueSuffix string

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, uniqueSuffix, roleDefinitionId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
