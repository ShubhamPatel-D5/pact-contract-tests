<#
.SYNOPSIS
    Publishes a Pact file to the Pact Broker using HTTP API.

.DESCRIPTION
    This script publishes a pact file to a Pact Broker without requiring Docker.
    It uses Invoke-RestMethod to interact with the Broker's HTTP API directly.

.PARAMETER PactFile
    Path to the pact JSON file to publish.

.PARAMETER BrokerUrl
    Base URL of the Pact Broker (without trailing slash).

.PARAMETER ConsumerVersion
    Version of the consumer application (e.g., git SHA, build number).

.PARAMETER Branch
    Branch name to tag the consumer version with (default: main).

.PARAMETER Username
    Pact Broker username for basic authentication (optional).

.PARAMETER Password
    Pact Broker password for basic authentication (optional).

.PARAMETER Token
    Bearer token for authentication (optional, alternative to username/password).

.EXAMPLE
    .\publish-pact.ps1 -PactFile "Consumer\pacts\SF-Consumer-VAIS-Producer.json" -ConsumerVersion "1.0.123"

.EXAMPLE
    .\publish-pact.ps1 -PactFile "Consumer\pacts\SF-Consumer-VAIS-Producer.json" -ConsumerVersion "abc123" -Branch "feature-x"
#>

param(
    [string]$PactFile = "",
    [string]$BrokerUrl = "",
    [string]$ConsumerVersion = "",
    [string]$Branch = "",
    [string]$Username = "",
    [string]$Password = "",
    [string]$Token = ""
)

# Set defaults from environment variables if parameters not provided
if ([string]::IsNullOrEmpty($BrokerUrl)) {
    $BrokerUrl = if ($env:PACT_BROKER_BASE_URL) { $env:PACT_BROKER_BASE_URL } else { "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292" }
}

if ([string]::IsNullOrEmpty($ConsumerVersion)) {
    $ConsumerVersion = if ($env:BUILD_NUMBER) { $env:BUILD_NUMBER } elseif ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { "1.0.$(Get-Date -Format 'yyyyMMddHHmmss')" }
}

if ([string]::IsNullOrEmpty($Branch)) {
    $Branch = if ($env:BRANCH_NAME) { $env:BRANCH_NAME } else { "main" }
}

if ([string]::IsNullOrEmpty($Username)) {
    $Username = if ($env:PACT_BROKER_USERNAME) { $env:PACT_BROKER_USERNAME } else { "" }
}

if ([string]::IsNullOrEmpty($Password)) {
    $Password = if ($env:PACT_BROKER_PASSWORD) { $env:PACT_BROKER_PASSWORD } else { "" }
}

if ([string]::IsNullOrEmpty($Token)) {
    $Token = if ($env:PACT_BROKER_TOKEN) { $env:PACT_BROKER_TOKEN } else { "" }
}

# Default pact file path
if ([string]::IsNullOrEmpty($PactFile)) {
    $PactFile = "Consumer\pacts\SF-Consumer-VAIS-Producer.json"
}

Write-Host "Publishing Pact to Broker" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Pact File: $PactFile"
Write-Host "Broker URL: $BrokerUrl"
Write-Host "Consumer Version: $ConsumerVersion"
Write-Host "Branch: $Branch"
Write-Host ""

# Check if pact file exists
if (-not (Test-Path $PactFile)) {
    Write-Host "ERROR: Pact file not found at: $PactFile" -ForegroundColor Red
    exit 1
}

# Read and parse the pact file
try {
    $pactContent = Get-Content $PactFile -Raw
    $pact = $pactContent | ConvertFrom-Json
    $consumerName = $pact.consumer.name
    $providerName = $pact.provider.name
    Write-Host "Consumer: $consumerName"
    Write-Host "Provider: $providerName"
    Write-Host ""
}
catch {
    Write-Host "ERROR: Failed to parse pact file: $_" -ForegroundColor Red
    exit 1
}

# Set up authentication headers
$headers = @{
    "Content-Type" = "application/json"
}

if (-not [string]::IsNullOrEmpty($Token)) {
    $headers["Authorization"] = "Bearer $Token"
    Write-Host "Using bearer token authentication"
}
elseif (-not [string]::IsNullOrEmpty($Username) -and -not [string]::IsNullOrEmpty($Password)) {
    $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${Username}:${Password}"))
    $headers["Authorization"] = "Basic $base64Auth"
    Write-Host "Using basic authentication (user: $Username)"
}
else {
    Write-Host "No authentication configured"
}

Write-Host ""

# Step 1: Create consumer participant if it doesn't exist
Write-Host "Step 1: Ensuring consumer participant exists..." -ForegroundColor Yellow
$createConsumerUrl = "$BrokerUrl/pacticipants"
$consumerBody = @{
    name = $consumerName
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri $createConsumerUrl -Method Post -Headers $headers -Body $consumerBody -ErrorAction SilentlyContinue
    Write-Host "Consumer participant created or already exists" -ForegroundColor Green
}
catch {
    Write-Host "Consumer participant may already exist (this is OK): $_" -ForegroundColor Gray
}

# Step 2: Create provider participant if it doesn't exist
Write-Host "Step 2: Ensuring provider participant exists..." -ForegroundColor Yellow
$providerBody = @{
    name = $providerName
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri $createConsumerUrl -Method Post -Headers $headers -Body $providerBody -ErrorAction SilentlyContinue
    Write-Host "Provider participant created or already exists" -ForegroundColor Green
}
catch {
    Write-Host "Provider participant may already exist (this is OK): $_" -ForegroundColor Gray
}

# Step 3: Publish the pact
Write-Host "Step 3: Publishing pact..." -ForegroundColor Yellow
$publishUrl = "$BrokerUrl/pacts/provider/$providerName/consumer/$consumerName/version/$ConsumerVersion"
Write-Host "PUT: $publishUrl"

try {
    $response = Invoke-RestMethod -Uri $publishUrl -Method Put -Headers $headers -Body $pactContent
    Write-Host "Pact published successfully!" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Failed to publish pact: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

# Step 4: Tag the consumer version with branch
Write-Host "Step 4: Tagging consumer version with branch '$Branch'..." -ForegroundColor Yellow
$tagUrl = "$BrokerUrl/pacticipants/$consumerName/versions/$ConsumerVersion/tags/$Branch"
Write-Host "PUT: $tagUrl"

try {
    $null = Invoke-RestMethod -Uri $tagUrl -Method Put -Headers $headers -Body "{}"
    Write-Host "Consumer version tagged successfully with '$Branch'" -ForegroundColor Green
}
catch {
    Write-Host "WARNING: Failed to tag consumer version: $_" -ForegroundColor Yellow
    Write-Host "Pact was published but tagging failed. You can tag manually later." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "SUCCESS: Pact published to broker!" -ForegroundColor Green
Write-Host "View it at: $BrokerUrl"
