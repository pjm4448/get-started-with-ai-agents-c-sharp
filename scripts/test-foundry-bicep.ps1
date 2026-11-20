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
    $parameters.modelCapacity.minValue -ne 1 -or
    $parameters.allowedIpAddress1.ContainsKey('defaultValue') -or
    $parameters.allowedIpAddress2.ContainsKey('defaultValue') -or
    $parameters.allowedIpAddress1.minLength -ne 7 -or
    $parameters.allowedIpAddress2.minLength -ne 7 -or
    $parameters.allowedIpAddress1.maxLength -ne 15 -or
    $parameters.allowedIpAddress2.maxLength -ne 15) {
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

$acl = $accounts[0].properties.networkAcls
$moduleParameters = $deployments[0].properties.parameters
if ($accounts[0].properties.publicNetworkAccess -ne 'Enabled' -or
    $acl.defaultAction -ne 'Deny' -or $acl.bypass -ne 'None' -or
    $acl.Count -ne 3 -or
    $acl.ipRules.Count -ne 2 -or
    $acl.ipRules[0].value -ne "[parameters('allowedIpAddress1')]" -or
    $acl.ipRules[1].value -ne "[parameters('allowedIpAddress2')]" -or
    $moduleParameters.allowedIpAddress1.value -ne "[parameters('allowedIpAddress1')]" -or
    $moduleParameters.allowedIpAddress2.value -ne "[parameters('allowedIpAddress2')]") {
    throw 'The compiled Foundry network rules are incorrect.'
}

$parameterFile = Get-Content -Raw (Join-Path $root 'infra' 'main.parameters.json') | ConvertFrom-Json -AsHashtable
if ($parameterFile.parameters.allowedIpAddress1.value -ne '${FOUNDRY_ALLOWED_IP_1}' -or
    $parameterFile.parameters.allowedIpAddress2.value -ne '${FOUNDRY_ALLOWED_IP_2}') {
    throw 'Foundry IP rules must use azd environment values.'
}

$ipValidator = Join-Path $root 'scripts' 'validate-foundry-ip-addresses.ps1'
& $ipValidator -First '1.1.1.1' -Second '8.8.8.8' | Out-Null
foreach ($pair in @(
    @('', '8.8.8.8'),
    @('1.1.1.1/24', '8.8.8.8'),
    @('999.1.1.1', '8.8.8.8'),
    @('1.1.1.1', '1.1.1.1'),
    @('::1', '8.8.8.8')
)) {
    $rejected = $false
    try {
        & $ipValidator -First $pair[0] -Second $pair[1] | Out-Null
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected) {
        throw 'Foundry IP validation accepted an invalid or duplicate input.'
    }
}

Write-Output 'Foundry Bicep lint, compilation and template contract checks passed.'
