using 'main.bicep'

param environment = readEnvironmentVariable('DEPLOY_ENVIRONMENT', 'dev')
param location = readEnvironmentVariable('DEPLOY_LOCATION', 'eastus')
param sqlAdminLogin = 'moxadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
