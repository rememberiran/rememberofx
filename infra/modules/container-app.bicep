@description('Container app name')
param appName string

@description('Azure region')
param location string

@description('Container Apps Environment ID')
param containerEnvId string

@description('Container image to deploy')
param image string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Ingress type: external, internal, or disabled')
@allowed(['external', 'internal', 'disabled'])
param ingressType string

@description('Target port')
param targetPort int = 8080

@description('CPU cores')
param cpu string = '0.5'

@description('Memory')
param memory string = '1Gi'

@description('Min replicas')
param minReplicas int = 0

@description('Max replicas')
param maxReplicas int = 3

@description('Environment variables')
param envVars array = []

@description('Scale rules')
param scaleRules array = []

@description('Allow insecure (HTTP) connections. Use for internal-only apps where TLS is unnecessary within the ACA environment.')
param allowInsecure bool = false

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerEnvId
    configuration: {
      ingress: ingressType == 'disabled' ? null : {
        external: ingressType == 'external'
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: allowInsecure
      }
    }
    template: {
      containers: [
        {
          name: appName
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: envVars
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: empty(scaleRules) ? null : scaleRules
      }
    }
  }
}

output appName string = containerApp.name
output principalId string = containerApp.identity.principalId
output fqdn string = ingressType != 'disabled' && containerApp.properties.configuration.ingress != null ? containerApp.properties.configuration.ingress.fqdn : ''
