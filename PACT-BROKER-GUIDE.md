# Pact Broker Integration Guide

This guide provides comprehensive instructions for publishing pacts to the Pact Broker and verifying providers using the generated scripts and test code.

## Table of Contents

1. [Overview](#overview)
2. [Broker Configuration](#broker-configuration)
3. [Publishing Pacts](#publishing-pacts)
4. [Provider Verification](#provider-verification)
5. [Verification Results Publishing](#verification-results-publishing)
6. [CI/CD Integration](#cicd-integration)
7. [Troubleshooting](#troubleshooting)

## Overview

The Pact Broker is a central repository for storing and managing pact contracts. It enables:

- **Contract Storage**: Central location for all pacts
- **Versioning**: Track different versions of contracts
- **Verification Status**: See which provider versions have been verified
- **Deployment Safety**: Check if it's safe to deploy using can-i-deploy
- **Webhooks**: Trigger CI builds when contracts change

**Default Broker URL**: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292

## Broker Configuration

### Environment Variables

Configure these environment variables for all scripts and tests:

```powershell
# Broker URL (required)
$env:PACT_BROKER_BASE_URL = "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292"

# Authentication (choose one method if broker requires auth)
# Option 1: Bearer Token
$env:PACT_BROKER_TOKEN = "your-bearer-token"

# Option 2: Basic Auth
$env:PACT_BROKER_USERNAME = "your-username"
$env:PACT_BROKER_PASSWORD = "your-password"

# Versioning (recommended to set in CI)
$env:BUILD_NUMBER = "1.0.123"           # Or use GITHUB_SHA for git-based versions
$env:BRANCH_NAME = "main"               # Current branch name
```

### pact-config.json

The generated [pact-config.json](pact-config.json) file contains default configuration values. These are used as fallbacks when environment variables are not set.

## Publishing Pacts

### Manual Publishing

Use the `publish-pact.ps1` script to publish pacts to the broker:

```powershell
# Basic usage with defaults
.\publish-pact.ps1 -ConsumerVersion "1.0.0"

# Specify custom pact file
.\publish-pact.ps1 -PactFile "Consumer\pacts\SF-Consumer-VAIS-Producer.json" -ConsumerVersion "1.0.123"

# Specify branch for tagging
.\publish-pact.ps1 -ConsumerVersion "1.0.123" -Branch "develop"

# With custom broker URL
.\publish-pact.ps1 -ConsumerVersion "1.0.123" -BrokerUrl "http://your-broker:9292"

# With authentication
.\publish-pact.ps1 -ConsumerVersion "1.0.123" -Token "your-bearer-token"

# Or with basic auth
.\publish-pact.ps1 -ConsumerVersion "1.0.123" -Username "user" -Password "pass"
```

### What the Script Does

The publish script performs these steps:

1. **Parses Pact File**: Extracts consumer and provider names
2. **Creates Participants**: Ensures consumer and provider exist in broker
3. **Publishes Pact**: Uploads the pact JSON via HTTP PUT
4. **Tags Version**: Tags the consumer version with the branch name

### Expected Output

```
Publishing Pact to Broker
=========================
Pact File: Consumer\pacts\SF-Consumer-VAIS-Producer.json
Broker URL: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
Consumer Version: 1.0.123
Branch: main

Consumer: SF-Consumer
Provider: VAIS-Producer

Step 1: Ensuring consumer participant exists...
Consumer participant created or already exists
Step 2: Ensuring provider participant exists...
Provider participant created or already exists
Step 3: Publishing pact...
PUT: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292/pacts/provider/VAIS-Producer/consumer/SF-Consumer/version/1.0.123
Pact published successfully!
Step 4: Tagging consumer version with branch 'main'...
PUT: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292/pacticipants/SF-Consumer/versions/1.0.123/tags/main
Consumer version tagged successfully with 'main'

SUCCESS: Pact published to broker!
View it at: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
```

## Provider Verification

### Manual Verification

Use the `verify-provider.ps1` script to run provider verification tests:

```powershell
# Basic usage with defaults
.\verify-provider.ps1 -ProviderVersion "1.0.0"

# Specify branch for tagging
.\verify-provider.ps1 -ProviderVersion "1.0.456" -Branch "develop"

# With custom broker URL
.\verify-provider.ps1 -ProviderVersion "1.0.456" -BrokerUrl "http://your-broker:9292"
```

### What the Script Does

The verification script:

1. **Sets Environment Variables**: Configures PACT_BROKER_BASE_URL, PROVIDER_VERSION, BRANCH_NAME
2. **Runs Tests**: Executes `dotnet test` in the Producer directory
3. **Reports Results**: Shows pass/fail status

### Direct Test Execution

You can also run tests directly:

```powershell
cd Producer

# Set required environment variables
$env:PACT_BROKER_BASE_URL = "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292"
$env:PROVIDER_VERSION = "1.0.456"
$env:BRANCH_NAME = "main"

# Run tests
dotnet test --logger "console;verbosity=detailed"
```

### Expected Output

```
Running Provider Verification Tests
====================================
Provider Version: 1.0.456
Broker URL: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
Branch: main

Running tests in Producer project...
Using pact file: C:\...\Consumer\pacts\SF-Consumer-VAIS-Producer.json
Provider version: 1.0.456
Pact Broker URL: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
Provider API started at: http://localhost:9001

Verifying a pact between SF-Consumer and VAIS-Producer
  [OK] A POST request to BulkUsers with invalid token
  [OK] A POST request to sync users via BulkUsers API
  [OK] A POST request to BulkUsers with invalid Windows user

Pact verification succeeded!
Publishing verification results to broker: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
...
Verification results published successfully: OK

SUCCESS: Provider verification passed!
Verification results have been published to the broker.
```

## Verification Results Publishing

### Automatic Publishing

The generated `ProducerPactTests.cs` includes automatic verification result publishing. This happens automatically when tests pass.

### How It Works

The test code performs these steps after successful verification:

1. **Fetches Pact Metadata**: Gets the pact version SHA from the broker
   ```
   GET /pacts/provider/VAIS-Producer/consumer/SF-Consumer/latest
   ```

2. **Publishes Verification Results**: POSTs the verification status
   ```
   POST /pacts/provider/VAIS-Producer/consumer/SF-Consumer/pact-version/{SHA}/verification-results
   ```
   
   Payload:
   ```json
   {
     "success": true,
     "providerApplicationVersion": "1.0.456",
     "verifiedBy": {
       "implementation": "PactNet",
       "version": "4.5.0"
     }
   }
   ```

3. **Tags Provider Version**: Tags the provider version with branch name
   ```
   PUT /pacticipants/VAIS-Producer/versions/1.0.456/tags/main
   ```

### Viewing Results in Broker UI

After successful verification and publishing:

1. Navigate to your Pact Broker URL in a web browser
2. You should see a **green checkmark** next to the verified pact
3. Click on the pact to see:
   - Contract details
   - Verification history
   - Consumer and provider versions
   - Verification results with timestamps

### Why Results Publishing Matters

- **Visibility**: Shows which provider versions have been verified against which consumer pacts
- **Deployment Safety**: Enables can-i-deploy checks to prevent breaking deployments
- **Audit Trail**: Maintains history of verifications over time
- **CI Integration**: Triggers downstream builds and deployments

## CI/CD Integration

### GitHub Actions Example

Create `.github/workflows/pact-ci.yml`:

```yaml
name: Pact Contract Tests

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  PACT_BROKER_BASE_URL: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292
  # PACT_BROKER_TOKEN: ${{ secrets.PACT_BROKER_TOKEN }}  # Uncomment if auth required

jobs:
  consumer-tests:
    name: Consumer Tests
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Consumer Tests
        working-directory: Consumer
        run: |
          dotnet restore
          dotnet test --logger "console;verbosity=detailed"
      
      - name: Upload Pact Files
        uses: actions/upload-artifact@v3
        with:
          name: pacts
          path: Consumer/pacts/*.json
      
      - name: Publish Pact to Broker
        env:
          BUILD_NUMBER: ${{ github.sha }}
          BRANCH_NAME: ${{ github.ref_name }}
        run: |
          .\publish-pact.ps1 -ConsumerVersion "$env:BUILD_NUMBER"

  provider-tests:
    name: Provider Verification
    runs-on: windows-latest
    needs: consumer-tests
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Download Pact Files
        uses: actions/download-artifact@v3
        with:
          name: pacts
          path: Consumer/pacts
      
      - name: Run Provider Verification
        env:
          BUILD_NUMBER: ${{ github.sha }}
          BRANCH_NAME: ${{ github.ref_name }}
        run: |
          .\verify-provider.ps1 -ProviderVersion "$env:BUILD_NUMBER"

  can-i-deploy:
    name: Can I Deploy Check
    runs-on: windows-latest
    needs: [consumer-tests, provider-tests]
    steps:
      - name: Install Pact CLI
        run: |
          choco install pact -y
      
      - name: Check Can I Deploy
        env:
          BUILD_NUMBER: ${{ github.sha }}
        run: |
          pact-broker can-i-deploy `
            --pacticipant SF-Consumer `
            --version "$env:BUILD_NUMBER" `
            --to-environment production `
            --broker-base-url "$env:PACT_BROKER_BASE_URL"
```

### Azure Pipelines Example

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'windows-latest'

variables:
  PACT_BROKER_BASE_URL: 'http://puvsfpactserver.tiger01-dev.ba.lab.local:9292'
  BUILD_VERSION: '$(Build.BuildNumber)'

stages:
  - stage: ConsumerTests
    jobs:
      - job: RunConsumerTests
        steps:
          - task: UseDotNet@2
            inputs:
              version: '8.0.x'
          
          - pwsh: |
              cd Consumer
              dotnet restore
              dotnet test
            displayName: 'Run Consumer Tests'
          
          - pwsh: |
              $env:BUILD_NUMBER = "$(Build.BuildNumber)"
              $env:BRANCH_NAME = "$(Build.SourceBranchName)"
              .\publish-pact.ps1 -ConsumerVersion "$env:BUILD_NUMBER"
            displayName: 'Publish Pact to Broker'
          
          - publish: Consumer/pacts
            artifact: pacts

  - stage: ProviderTests
    dependsOn: ConsumerTests
    jobs:
      - job: RunProviderTests
        steps:
          - task: UseDotNet@2
            inputs:
              version: '8.0.x'
          
          - download: current
            artifact: pacts
            displayName: 'Download Pact Files'
          
          - pwsh: |
              Copy-Item "$(Pipeline.Workspace)/pacts/*" -Destination "Consumer/pacts/" -Force
            displayName: 'Copy Pacts to Consumer Directory'
          
          - pwsh: |
              $env:BUILD_NUMBER = "$(Build.BuildNumber)"
              $env:BRANCH_NAME = "$(Build.SourceBranchName)"
              .\verify-provider.ps1 -ProviderVersion "$env:BUILD_NUMBER"
            displayName: 'Run Provider Verification'
```

## Troubleshooting

### Issue: Cannot Connect to Broker

**Symptoms:**
- Script fails with "Failed to publish pact" or connection timeout
- Error: "Unable to connect to the remote server"

**Solutions:**
1. Verify broker URL is correct and accessible:
   ```powershell
   Invoke-WebRequest -Uri "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292" -Method Get
   ```
2. Check network connectivity and firewall rules
3. Verify broker is running: Check broker logs or status page
4. Try accessing broker UI in a web browser

### Issue: Authentication Failures

**Symptoms:**
- HTTP 401 Unauthorized errors
- "Authentication failed" messages

**Solutions:**
1. Verify credentials are correct:
   ```powershell
   # Test basic auth
   $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("user:pass"))
   Invoke-WebRequest -Uri "$brokerUrl/pacticipants" -Headers @{Authorization="Basic $cred"}
   ```
2. Check if broker requires authentication (some brokers are open)
3. Verify token/username/password environment variables are set correctly
4. Check broker configuration for authentication settings

### Issue: Pact File Not Found

**Symptoms:**
- "Pact file not found" error in scripts
- Provider verification fails to locate pact

**Solutions:**
1. Ensure consumer tests ran successfully first:
   ```powershell
   cd Consumer
   dotnet test
   dir pacts  # Should show SF-Consumer-VAIS-Producer.json
   ```
2. Check pact file path in script parameters
3. Verify working directory when running scripts
4. For CI: Ensure pact artifact was uploaded and downloaded correctly

### Issue: Verification Results Not Appearing

**Symptoms:**
- Tests pass but no green checkmark in broker UI
- Verification results missing from broker

**Solutions:**
1. Check test output for publishing errors:
   ```
   Publishing verification results to broker: ...
   Verification results published successfully: OK
   ```
2. Verify PROVIDER_VERSION environment variable is set
3. Check broker logs for incoming verification result POSTs
4. Manually publish verification using provider version:
   ```powershell
   $env:PROVIDER_VERSION = "1.0.456"
   .\verify-provider.ps1 -ProviderVersion "$env:PROVIDER_VERSION"
   ```
5. Verify pact version SHA is correct (test code fetches this automatically)

### Issue: Version Tags Not Applied

**Symptoms:**
- Versions exist but not tagged with branch name
- Can-i-deploy fails due to missing tags

**Solutions:**
1. Check BRANCH_NAME environment variable is set
2. Manually tag versions:
   ```powershell
   # Tag consumer version
   Invoke-RestMethod -Method Put `
     -Uri "$brokerUrl/pacticipants/SF-Consumer/versions/1.0.123/tags/main" `
     -Body "{}" -ContentType "application/json"
   
   # Tag provider version
   Invoke-RestMethod -Method Put `
     -Uri "$brokerUrl/pacticipants/VAIS-Producer/versions/1.0.456/tags/main" `
     -Body "{}" -ContentType "application/json"
   ```
3. Check script output for tagging success/failure messages

### Issue: Duplicate or Conflicting Pacts

**Symptoms:**
- Broker shows multiple versions of same pact
- Verification uses wrong pact version

**Solutions:**
1. Use consistent version numbers (prefer git SHA or build numbers)
2. Don't manually edit pact files - regenerate from tests
3. Use can-i-deploy to check compatibility before deploying
4. Consider using git SHA as version identifier:
   ```powershell
   $version = git rev-parse --short HEAD
   .\publish-pact.ps1 -ConsumerVersion $version
   ```

## Additional Resources

- **Pact Broker Documentation**: https://docs.pact.io/pact_broker
- **Pact Specification**: https://github.com/pact-foundation/pact-specification
- **PactNet GitHub**: https://github.com/pact-foundation/pact-net
- **Pact CLI Tools**: https://docs.pact.io/implementation_guides/cli

## Support

For issues specific to this generated project:
- Review the [README.md](README.md) for project overview
- Check the [PACT-Agent-Blueprint.md](PACT-Agent-Blueprint.md) for implementation details
- Examine test output for detailed error messages

For general Pact questions:
- Pact Slack: https://slack.pact.io
- Pact GitHub Discussions: https://github.com/pact-foundation/pact-net/discussions
