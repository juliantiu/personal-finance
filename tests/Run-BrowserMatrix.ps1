param(
    [ValidateSet('chrome', 'edge', 'firefox', 'webkit', 'brave')]
    [string[]] $Browsers = @('chrome', 'edge', 'firefox', 'webkit', 'brave'),
    [string] $BravePath = $env:PERSONAL_FINANCE_BRAVE_PATH
)

$ErrorActionPreference = 'Stop'
$previousBrowser = $env:PERSONAL_FINANCE_TEST_BROWSER
$previousBrave = $env:PERSONAL_FINANCE_BRAVE_PATH
$projectPath = Join-Path $PSScriptRoot 'PersonalFinance.EndToEndTests/PersonalFinance.EndToEndTests.csproj'
$logDirectory = Join-Path $PSScriptRoot '../TestResults/browser-matrix'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$results = @()
try {
    $env:PERSONAL_FINANCE_BRAVE_PATH = $BravePath
    foreach ($browserName in $Browsers) {
        $env:PERSONAL_FINANCE_TEST_BROWSER = $browserName
        Write-Output "Running local HTTPS account workflows in $browserName"
        & dotnet test --no-build --no-restore --project $projectPath 2>&1 |
            Tee-Object -FilePath (Join-Path $logDirectory "$browserName.log")
        $results += [pscustomobject]@{ Browser = $browserName; ExitCode = $LASTEXITCODE }
    }
}
finally {
    $env:PERSONAL_FINANCE_TEST_BROWSER = $previousBrowser
    $env:PERSONAL_FINANCE_BRAVE_PATH = $previousBrave
}
$results | Format-Table
if ($results | Where-Object { $_.ExitCode -ne 0 }) {
    throw 'Browser matrix failed. Check TestResults/browser-matrix. Missing browsers are failures, not skipped coverage.'
}
