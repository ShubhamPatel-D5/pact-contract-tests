using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using Xunit;
using Xunit.Abstractions;

namespace VAIS_Producer.Tests
{
    public class ProducerPactTests : IDisposable
    {
        private readonly IHost _host;
        private readonly string _providerUri = "http://localhost:9001";
        private readonly string _pactPath;
        private readonly ITestOutputHelper _output;
        private readonly string _pactBrokerUrl = Environment.GetEnvironmentVariable("PACT_BROKER_BASE_URL")
            ?? "http://puvsfpactserver.tiger01-dev.ba.lab.local:9292";
        private readonly string _providerVersion = Environment.GetEnvironmentVariable("PROVIDER_VERSION")
            ?? $"1.0.{DateTime.UtcNow:yyyyMMddHHmmss}";

        public ProducerPactTests(ITestOutputHelper output)
        {
            _output = output;

            // Dynamic pact file finder - searches upward from test directory
            _pactPath = FindPactFile("SF-Consumer-VAIS-Producer.json")
                        ?? "../../../../Consumer/pacts/SF-Consumer-VAIS-Producer.json"; // Fallback

            _output.WriteLine($"Using pact file: {Path.GetFullPath(_pactPath)}");
            _output.WriteLine($"Provider version: {_providerVersion}");
            _output.WriteLine($"Pact Broker URL: {_pactBrokerUrl}");

            // Start the test host
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<TestStartup>();
                    webBuilder.UseUrls(_providerUri);
                })
                .Build();

            _host.Start();
            _output.WriteLine($"Provider API started at: {_providerUri}");
        }

        /// <summary>
        /// Finds pact file by searching upward from current directory.
        /// Looks for Consumer/pacts folder structure starting from test execution directory.
        /// This makes tests robust against directory structure changes.
        /// </summary>
        private string? FindPactFile(string pactFileName)
        {
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

            // Search upward max 10 levels
            for (int i = 0; i < 10 && currentDir != null; i++)
            {
                // Look for Consumer/pacts directory
                var consumerPactsPath = Path.Combine(currentDir.FullName, "Consumer", "pacts", pactFileName);
                if (File.Exists(consumerPactsPath))
                {
                    return consumerPactsPath;
                }

                currentDir = currentDir.Parent;
            }

            return null;
        }

        [Fact]
        public void EnsureProviderApiHonoursPactWithConsumer_LocalFile()
        {
            var config = new PactVerifierConfig
            {
                Outputters = new[] { new CustomOutputter(_output) }
            };

            bool verificationSuccess = false;
            try
            {
                new PactVerifier(config)
                    .ServiceProvider("VAIS-Producer", new Uri(_providerUri))
                    .WithFileSource(new FileInfo(_pactPath))
                    .WithProviderStateUrl(new Uri($"{_providerUri}/provider-states"))
                    .Verify();

                verificationSuccess = true;
                _output.WriteLine("Pact verification succeeded!");
            }
            catch
            {
                _output.WriteLine("Pact verification failed!");
                throw;
            }
            finally
            {
                if (verificationSuccess)
                {
                    PublishVerificationResults(success: true);
                }
            }
        }

        /* Uncomment to verify against Pact Broker instead of local file:
        [Fact]
        public void EnsureProviderApiHonoursPactWithConsumer_Broker()
        {
            var config = new PactVerifierConfig
            {
                Outputters = new[] { new CustomOutputter(_output) }
            };

            bool verificationSuccess = false;
            try
            {
                var verifier = new PactVerifier(config)
                    .ServiceProvider("VAIS-Producer", new Uri(_providerUri))
                    .WithPactBrokerSource(new Uri(_pactBrokerUrl), options =>
                    {
                        // Verify latest pacts tagged with 'main' branch
                        options.ConsumerVersionSelectors(new PactNet.Verifier.ConsumerVersionSelector
                        {
                            Branch = "main",
                            Latest = true
                        });

                        // Add authentication if needed:
                        // options.TokenAuthentication("your-bearer-token");
                        // OR
                        // options.BasicAuthentication("username", "password");

                        options.PublishResults(_providerVersion);
                    })
                    .WithProviderStateUrl(new Uri($"{_providerUri}/provider-states"));

                verifier.Verify();

                verificationSuccess = true;
                _output.WriteLine("Pact verification succeeded!");
            }
            catch
            {
                _output.WriteLine("Pact verification failed!");
                throw;
            }
        }
        */

        private void PublishVerificationResults(bool success)
        {
            try
            {
                _output.WriteLine($"Publishing verification results to broker: {_pactBrokerUrl}");

                using var httpClient = new HttpClient();

                // Step 1: Fetch pact metadata from broker to get SHA
                var pactMetadataUrl = $"{_pactBrokerUrl}/pacts/provider/VAIS-Producer/consumer/SF-Consumer/latest";
                _output.WriteLine($"Fetching pact metadata from: {pactMetadataUrl}");

                var metadataResponse = httpClient.GetAsync(pactMetadataUrl).Result;
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    _output.WriteLine($"Failed to fetch pact metadata: {metadataResponse.StatusCode}");
                    _output.WriteLine("Skipping verification result publishing.");
                    return;
                }

                var metadataJson = metadataResponse.Content.ReadAsStringAsync().Result;
                _output.WriteLine($"Pact metadata response: {metadataJson}");

                using var metadataDoc = JsonDocument.Parse(metadataJson);
                var pactVersionElement = metadataDoc.RootElement.GetProperty("_links").GetProperty("pb:pact-version");
                var pactVersionSha = pactVersionElement.GetProperty("name").GetString();

                if (string.IsNullOrEmpty(pactVersionSha))
                {
                    _output.WriteLine("Could not extract pact version SHA from metadata.");
                    return;
                }

                _output.WriteLine($"Pact version SHA: {pactVersionSha}");

                // Step 2: Publish verification results
                var verificationUrl = $"{_pactBrokerUrl}/pacts/provider/VAIS-Producer/consumer/SF-Consumer/pact-version/{pactVersionSha}/verification-results";
                _output.WriteLine($"Publishing verification results to: {verificationUrl}");

                var verificationPayload = new
                {
                    success = success,
                    providerApplicationVersion = _providerVersion,
                    verifiedBy = new
                    {
                        implementation = "PactNet",
                        version = "4.5.0"
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(verificationPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var verificationResponse = httpClient.PostAsync(verificationUrl, content).Result;
                if (verificationResponse.IsSuccessStatusCode)
                {
                    _output.WriteLine($"Verification results published successfully: {verificationResponse.StatusCode}");
                }
                else
                {
                    _output.WriteLine($"Failed to publish verification results: {verificationResponse.StatusCode}");
                    var errorBody = verificationResponse.Content.ReadAsStringAsync().Result;
                    _output.WriteLine($"Error response: {errorBody}");
                }

                // Step 3: Tag provider version
                var branchName = Environment.GetEnvironmentVariable("BRANCH_NAME") ?? "main";
                var tagUrl = $"{_pactBrokerUrl}/pacticipants/VAIS-Producer/versions/{_providerVersion}/tags/{branchName}";
                _output.WriteLine($"Tagging provider version: {tagUrl}");

                var tagResponse = httpClient.PutAsync(tagUrl, new StringContent("{}", Encoding.UTF8, "application/json")).Result;
                if (tagResponse.IsSuccessStatusCode)
                {
                    _output.WriteLine($"Provider version tagged successfully with '{branchName}'");
                }
                else
                {
                    _output.WriteLine($"Failed to tag provider version: {tagResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error publishing verification results: {ex.Message}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't fail the test if publishing fails
            }
        }

        public void Dispose()
        {
            _output.WriteLine("Stopping provider API host...");
            _host?.StopAsync().Wait();
            _host?.Dispose();
            _output.WriteLine("Provider API host stopped.");
        }

        // Helper class for xUnit output (PactNet 4.5.0 uses IOutput interface)
        private class CustomOutputter : IOutput
        {
            private readonly ITestOutputHelper _output;

            public CustomOutputter(ITestOutputHelper output)
            {
                _output = output;
            }

            public void WriteLine(string line)
            {
                _output.WriteLine(line);
            }
        }
    }
}
