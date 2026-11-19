targetScope = 'subscription'

@description('Name of a dedicated development resource group.')
param resourceGroupName string

@description('Location of the resource group.')
param primaryLocation string = 'eastus'

@description('Location of the Foundry account and project.')
param aiLocation string = 'eastus'

@description('Globally unique Foundry account and custom subdomain name.')
@minLength(2)
@maxLength(64)
param accountName string

@description('Foundry project name.')
@minLength(2)
@maxLength(64)
param projectName string

@description('Name of the model deployment.')
param modelDeploymentName string

@description('Model catalog name.')
param modelName string = 'gpt-5-mini'

@description('Model catalog version.')
param modelVersion string = '2025-08-07'

@description('Model deployment SKU.')
param modelSku string = 'GlobalStandard'

@description('Explicit model capacity; verify supported values and quota before deployment.')
@minValue(1)
param modelCapacity int

@description('Non-secret tags applied to resources.')
param tags object = {}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: primaryLocation
  tags: tags
}

module foundry './modules/foundry.bicep' = {
  name: 'foundry'
  scope: resourceGroup
  params: {
    accountName: accountName
    projectName: projectName
    location: aiLocation
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    modelSku: modelSku
    modelCapacity: modelCapacity
    tags: tags
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_AI_PROJECT_RESOURCE_ID string = foundry.outputs.projectResourceId
