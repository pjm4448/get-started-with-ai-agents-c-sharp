Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("aiagents-scan-" + [guid]::NewGuid())
$scanner = Join-Path $PSScriptRoot 'check-no-python.ps1'
$created = $false

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $created = $true
    & git -C $root init --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to initialize the scanner test repository.'
    }

    function Assert-Scan([int] $expected) {
        & $scanner -Root $root | Out-Null
        if ($LASTEXITCODE -ne $expected) {
            throw "Scanner returned $LASTEXITCODE instead of $expected."
        }
    }

    Set-Content -LiteralPath (Join-Path $root 'README.md') -Value 'This .NET project does not require a Python runtime.'
    Set-Content -LiteralPath (Join-Path $root 'checks.yml') -Value '- name: Check for Python artifacts'
    Assert-Scan 0

    New-Item -ItemType File -Path (Join-Path $root 'example.py') | Out-Null
    Assert-Scan 1
    Remove-Item -LiteralPath (Join-Path $root 'example.py')
    Assert-Scan 0

    New-Item -ItemType File -Path (Join-Path $root 'requirements_dev.txt') | Out-Null
    Assert-Scan 1
    Remove-Item -LiteralPath (Join-Path $root 'requirements_dev.txt')

    Set-Content -LiteralPath (Join-Path $root 'Dockerfile') -Value 'FROM python:3.12'
    Assert-Scan 1
    Remove-Item -LiteralPath (Join-Path $root 'Dockerfile')

    Set-Content -LiteralPath (Join-Path $root 'build.sh') -Value 'python3 -m pip install example'
    Assert-Scan 1
    Remove-Item -LiteralPath (Join-Path $root 'build.sh')

    Set-Content -LiteralPath (Join-Path $root 'checks.yml') -Value '- run: python3 example.py'
    Assert-Scan 1

    Set-Content -LiteralPath (Join-Path $root 'checks.yml') -Value '- uses: actions/setup-python@v5'
    Assert-Scan 1
    Remove-Item -LiteralPath (Join-Path $root 'checks.yml')

    Set-Content -LiteralPath (Join-Path $root 'README.md') -Value 'Install Python before running the application.'
    Assert-Scan 1
}
finally {
    if ($created -and (Test-Path -LiteralPath $root)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}

Write-Output 'Zero-Python scanner tests passed.'
exit 0
