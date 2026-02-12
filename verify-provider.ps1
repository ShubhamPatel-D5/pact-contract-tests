<#
.SYNOPSIS
    Runs provider verification tests and publishes results to Pact Broker.

.DESCRIPTION
    This script sets up environment variables and runs the provider verification tests.
    Results are automatically published to the Pact Broker by the test code.

.PARAMETER ProviderVersion
    Version of the provider application (e.g., git SHA, build number).

.PARAMETER BrokerUrl
    Base URL of the Pact Broker (without trailing slash).

.PARAMETER Branch
    Branch name for the provider version (default: main).

.EXAMPLE
    .\verify-provider.ps1 -ProviderVersion "1.0.456"

.EXAMPLE
    .\verify-provider.ps1 -ProviderVersion "xyz789" -Branch "develop"
#>

param(
    [string]$ProviderVersion = "",
    [string]$BrokerUrl = "",
    [string]$Branch = ""
)

# Set defaults from environment variables if parameters not provided
if ([string]::IsNullOrEmpty($BrokerUrl)) {
    $BrokerUrl = if ($env:PACT_BROKER_BASE_URL) { $env:PACT_BROKER_BASE_URL } else { "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292" }
}

if ([string]::IsNullOrEmpty($ProviderVersion)) {
    $ProviderVersion = if ($env:BUILD_NUMBER) { $env:BUILD_NUMBER } elseif ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { "1.0.$(Get-Date -Format 'yyyyMMddHHmmss')" }
}

if ([string]::IsNullOrEmpty($Branch)) {
    $Branch = if ($env:BRANCH_NAME) { $env:BRANCH_NAME } else { "main" }
}

Write-Host "Running Provider Verification Tests" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Provider Version: $ProviderVersion"
Write-Host "Broker URL: $BrokerUrl"
Write-Host "Branch: $Branch"
Write-Host ""

# Set environment variables for the test process
$env:PACT_BROKER_BASE_URL = $BrokerUrl
$env:PROVIDER_VERSION = $ProviderVersion
$env:BRANCH_NAME = $Branch

# Run the provider tests
Write-Host "Running tests in Producer project..." -ForegroundColor Yellow
Push-Location Producer

try {
    dotnet test --logger "console;verbosity=detailed"
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "SUCCESS: Provider verification passed!" -ForegroundColor Green
        Write-Host "Verification results have been published to the broker." -ForegroundColor Green
        Write-Host "View them at: $BrokerUrl" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "FAILURE: Provider verification failed!" -ForegroundColor Red
        Write-Host "Check the test output above for details." -ForegroundColor Red
        exit $exitCode
    }
}
finally {
    Pop-Location
}
