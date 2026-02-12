# Pact Contract Testing - SF-Consumer & VAIS-Producer

This project contains Consumer and Producer contract tests generated from a Pact JSON file using PactNet 4.5.0.

## Project Structure

```
generated-pact/
├── Consumer/                 # Consumer test project
│   ├── Consumer.csproj
│   ├── ConsumerPactTests.cs
│   ├── Models/
│   └── pacts/               # Generated pact files go here
├── Producer/                 # Provider API and verification tests
│   ├── Producer.csproj
│   ├── ProducerPactTests.cs
│   ├── TestStartup.cs
│   ├── Controllers/
│   └── Models/
├── publish-pact.ps1         # Publish pact to broker
├── verify-provider.ps1      # Run provider verification
├── pact-config.json         # Broker configuration
└── README.md               # This file
```

## Quick Start

### Prerequisites

- .NET 8 SDK
- PowerShell (for publishing scripts)

### Step 1: Run Consumer Tests

Consumer tests generate the pact file from test interactions:

```powershell
cd Consumer
dotnet restore
dotnet test
```

This will create `pacts/SF-Consumer-VAIS-Producer.json` in the Consumer directory.

### Step 2: Publish Pact to Broker (Optional)

Publish the generated pact to the Pact Broker:

```powershell
cd ..
.\publish-pact.ps1 -ConsumerVersion "1.0.0"
```

The script will:
- Read the pact file from Consumer/pacts/
- Create consumer and provider participants if needed
- Publish the pact to the broker
- Tag the consumer version with the branch name

### Step 3: Run Provider Verification Tests

Provider tests verify that the API honors the contract:

```powershell
cd Producer
dotnet restore
dotnet test
```

Or use the verification script to automatically publish results:

```powershell
cd ..
.\verify-provider.ps1 -ProviderVersion "1.0.0"
```

The verification tests will:
- Start a test API server on http://localhost:9001
- Verify the pact contract against the running API
- Automatically publish verification results to the broker
- Tag the provider version

## Contract Details

**Consumer:** SF-Consumer  
**Provider:** VAIS-Producer  
**Endpoint:** POST /BulkUsers  

### Interactions

1. **Invalid Token** - Returns 401 when invalid authentication token is provided
2. **Valid User Sync** - Returns 200 with user subject when valid Windows users are synced
3. **Invalid Windows User** - Returns 400 with error when Windows user doesn't exist

## Environment Variables

Configure these environment variables for broker integration:

- `PACT_BROKER_BASE_URL` - Broker URL (default: http://puvsfpactserver.tiger01-dev.ba.lab.local:9292)
- `PACT_BROKER_TOKEN` - Bearer token for authentication (optional)
- `PACT_BROKER_USERNAME` - Username for basic auth (optional)
- `PACT_BROKER_PASSWORD` - Password for basic auth (optional)
- `BUILD_NUMBER` or `GITHUB_SHA` - Version identifier for consumer/provider
- `BRANCH_NAME` - Branch name for tagging (default: main)
- `PROVIDER_VERSION` - Provider version (defaults to timestamp if not set)

## CI/CD Integration

See [.github/workflows/pact-ci.yml](.github/workflows/pact-ci.yml) for a GitHub Actions workflow template.

Typical CI workflow:

1. **Consumer Job**: Run consumer tests → generate pact → publish to broker
2. **Provider Job**: Run provider tests → verify pact → publish verification results
3. **Deploy Job**: Check can-i-deploy → deploy if safe

## Pact Broker Guide

For detailed instructions on publishing, verification, and troubleshooting, see:
- [PACT-BROKER-GUIDE.md](PACT-BROKER-GUIDE.md) - Comprehensive broker usage guide
- [pact-config.json](pact-config.json) - Broker configuration reference

## Package Versions

This project uses these exact package versions (as specified in the blueprint):

- **PactNet**: 4.5.0 (NOT 4.6.1 or 5.0.0)
- **xUnit**: 2.6.6
- **Microsoft.NET.Test.Sdk**: 17.9.0
- **Newtonsoft.Json**: 13.0.3 (Consumer only)
- **Microsoft.AspNetCore.Mvc.Testing**: 8.0.1 (Producer only)

## Key Implementation Details

### Consumer Tests
- **NO IDisposable**: Consumer test class does NOT implement IDisposable (PactNet 4.5.0 requirement)
- **Mock Server**: Uses `.WithHttpInteractions()` without port parameter - port is auto-selected
- **JSON Serialization**: Uses Newtonsoft.Json with CamelCasePropertyNamesContractResolver

### Producer Tests
- **WITH IDisposable**: Producer test class DOES implement IDisposable to clean up test host
- **Dynamic Pact File Finder**: Searches upward from test directory to find pact file
- **Automatic Verification Publishing**: Publishes verification results to broker after successful verification
- **Test Host**: Starts API on http://localhost:9001 during tests

## Troubleshooting

### Pact File Not Found
- Consumer tests should generate the pact file in `Consumer/pacts/`
- Producer tests use dynamic file finder to locate the pact file
- If not found, check that consumer tests ran successfully first

### Verification Fails
- Ensure the provider API is running (test host starts automatically)
- Check provider state setup in ProviderStatesController
- Verify Authorization headers match expected values in BulkUsersController

### Broker Publishing Fails
- Check network connectivity to broker URL
- Verify authentication credentials if required
- Check broker logs for detailed error messages

## Additional Resources

- [PactNet Documentation](https://github.com/pact-foundation/pact-net)
- [Pact Specification](https://github.com/pact-foundation/pact-specification)
- [Pact Broker Documentation](https://docs.pact.io/pact_broker)

## Generated By

This project was generated using the Pact-Agent Blueprint for .NET 8.
Generated on: 2026-02-12

For questions or issues, refer to the PACT-Agent-Blueprint.md documentation.
