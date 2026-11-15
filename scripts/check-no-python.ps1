param(
    [string] $Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$git = [System.Diagnostics.ProcessStartInfo]::new()
$git.FileName = 'git'
$git.UseShellExecute = $false
$git.RedirectStandardOutput = $true
$git.RedirectStandardError = $true
foreach ($argument in @('-C', $Root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard')) {
    $git.ArgumentList.Add($argument)
}

$process = [System.Diagnostics.Process]::Start($git)
try {
    $output = $process.StandardOutput.ReadToEnd()
    $errorOutput = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Unable to enumerate repository files: $errorOutput"
    }
}
finally {
    $process.Dispose()
}

$namePattern = '(^|/)(?:[^/]+\.(?:py|pyc|pyo|pyi|pyw|pyd|whl)|requirements(?:[._-][^/]*)?\.txt|pyproject\.toml|Pipfile(?:\.lock)?|poetry\.lock|uv\.lock|environment\.ya?ml|\.python-version|tox\.ini|pytest\.ini)$|(^|/)\.venv(/|$)'
$commandPattern = '(?:^\s*(?:-\s*)?(?:run(?:\s+|:\s*)|cmd\s+)?|[;&|]\s*)(?:\$\s*)?(?:sudo\s+)?(?:python(?:\d+(?:\.\d+)*)?|pypy\d*|pip(?:\d+(?:\.\d+)*)?|conda|poetry|uv)(?:\.exe)?(?:\s|$)'
$integrationPattern = 'actions/setup-python@|devcontainers/features/python(?:[:/@]|$)|^\s*FROM\s+(?:--platform=\S+\s+)?(?:docker\.io/)?python(?::|@|\s|$)|(?:apt(?:-get)?|apk|dnf|yum)\s+(?:install|add)\b.*\bpython\d*\b|<PackageReference\s+Include\s*=\s*["''](?:pythonnet|python\.runtime)["'']|["'']type["'']\s*:\s*["'']python["'']'
$documentationPattern = '\b(?:install|run|execute|use)\s+(?:python(?:\d+(?:\.\d+)*)?|pip(?:\d+(?:\.\d+)*)?|conda)\b'
$textPattern = '\.(?:cs|csproj|props|targets|json|ya?ml|sh|ps1|md)$|(?:^|/)Dockerfile(?:\.[^/]*)?$'
$excluded = @('scripts/check-no-python.ps1', 'scripts/test-no-python.ps1')
$findings = [System.Collections.Generic.List[string]]::new()

foreach ($path in ($output -split "`0")) {
    if ([string]::IsNullOrEmpty($path)) {
        continue
    }

    if ($path -match $namePattern) {
        $findings.Add("${path}: prohibited file or environment")
        continue
    }

    if ($path -in $excluded -or $path -eq '.github/copilot-instructions.md' -or
        $path -like 'docs/migration/*' -or $path -notmatch $textPattern) {
        continue
    }

    $fullPath = Join-Path $Root ($path.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($fullPath)) {
        $lineNumber++
        if ($line -match '^\s*#' -and $path -notmatch '\.md$') {
            continue
        }

        if ($line -match $commandPattern -or $line -match $integrationPattern -or
            ($path -match '\.md$' -and $line -match $documentationPattern)) {
            $findings.Add("${path}:${lineNumber}: prohibited Python integration")
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Output $_ }
    exit 1
}

Write-Output 'No common prohibited Python artifacts or integrations found in scanned repository files.'
exit 0
