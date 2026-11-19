$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$entryPoint = Join-Path $root 'infra' 'main.bicep'
$module = Join-Path $root 'infra' 'modules' 'foundry.bicep'

foreach ($file in @($entryPoint, $module)) {
    & az bicep lint --file $file
    if ($LASTEXITCODE -ne 0) {
        throw "Bicep lint failed for $file."
    }
}

$json = & az bicep build --file $entryPoint --stdout
if ($LASTEXITCODE -ne 0) {
    throw 'Bicep compilation failed.'
}
$template = $json | ConvertFrom-Json -AsHashtable
$parameters = $template.parameters
if ($parameters.primaryLocation.defaultValue -ne 'eastus' -or
    $parameters.aiLocation.defaultValue -ne 'eastus' -or
    $parameters.modelName.defaultValue -ne 'gpt-5-mini' -or
    $parameters.modelVersion.defaultValue -ne '2025-08-07' -or
    $parameters.modelSku.defaultValue -ne 'GlobalStandard' -or
    $parameters.modelCapacity.ContainsKey('defaultValue') -or
    $parameters.modelCapacity.minValue -ne 1) {
    throw 'The compiled Foundry parameter contract is incorrect.'
}

$deployments = @($template.resources | Where-Object type -eq 'Microsoft.Resources/deployments')
if ($deployments.Count -ne 1) {
    throw 'Expected one nested Foundry deployment.'
}
$resources = @($deployments[0].properties.template.resources)
$accounts = @($resources | Where-Object type -eq 'Microsoft.CognitiveServices/accounts')
$projects = @($resources | Where-Object type -eq 'Microsoft.CognitiveServices/accounts/projects')
$models = @($resources | Where-Object type -eq 'Microsoft.CognitiveServices/accounts/deployments')
if ($resources.Count -ne 3 -or $accounts.Count -ne 1 -or $projects.Count -ne 1 -or
    $models.Count -ne 1) {
    throw 'The compiled Foundry resource set is incorrect.'
}
if ($accounts[0].kind -ne 'AIServices' -or
    $accounts[0].properties.disableLocalAuth -ne $true -or
    $accounts[0].identity.type -ne 'SystemAssigned' -or
    $projects[0].identity.type -ne 'SystemAssigned' -or
    $models[0].sku.capacity -ne "[parameters('modelCapacity')]") {
    throw 'The compiled Foundry security or model configuration is incorrect.'
}

Write-Output 'Foundry Bicep lint, compilation and template contract checks passed.'
