targetScope = 'resourceGroup'

param accountName string
param projectName string
param location string
param modelDeploymentName string
param modelName string
param modelVersion string
param modelSku string
param modelCapacity int
param allowedIpAddress1 string
param allowedIpAddress2 string
param tags object

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: accountName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'None'
      ipRules: [
        {
          value: allowedIpAddress1
        }
        {
          value: allowedIpAddress2
        }
      ]
    }
  }
  tags: tags
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  // The provider locks the account during child-resource provisioning.
  dependsOn: [
    model
  ]
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectName
  }
  tags: tags
}

resource model 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: modelDeploymentName
  sku: {
    name: modelSku
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

output projectResourceId string = project.id
