param(
    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $First,

    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $Second
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

foreach ($value in @($First, $Second)) {
    $address = $null
    if (-not [System.Net.IPAddress]::TryParse($value, [ref] $address) -or
        $address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork -or
        $address.ToString() -cne $value) {
        throw 'Each Foundry firewall input must be a canonical IPv4 host address, without a subnet mask.'
    }
}

if ($First -ceq $Second) {
    throw 'The two Foundry firewall addresses must be distinct.'
}

Write-Output 'Foundry firewall inputs are distinct canonical IPv4 host addresses. Confirm they are the intended public addresses before provisioning.'
