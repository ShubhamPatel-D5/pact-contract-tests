using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet;
using PactNet.Infrastructure.Outputters;
using SF_Consumer.Models;
using Xunit;
using Xunit.Abstractions;

namespace SF_Consumer.Tests
{
    public class ConsumerPactTests
    {
        private readonly IPactBuilderV3 _pactBuilder;
        private readonly ITestOutputHelper _output;

        public ConsumerPactTests(ITestOutputHelper output)
        {
            _output = output;

            var pactConfig = new PactConfig
            {
                PactDir = "../../../pacts",
                Outputters = new[]
                {
                    new CustomOutputter(output)
                },
                DefaultJsonSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }
            };

            _pactBuilder = Pact.V3("SF-Consumer", "VAIS-Producer", pactConfig).WithHttpInteractions();
        }

        [Fact]
        public async Task PostBulkUsers_WithInvalidToken_Returns401()
        {
            // Arrange
            _pactBuilder
                .UponReceiving("A POST request to BulkUsers with invalid token")
                .Given("Invalid authentication token provided")
                .WithRequest(HttpMethod.Post, "/BulkUsers")
                .WithHeader("Authorization", "Bearer invalid-token")
                .WithHeader("Content-Type", "application/json")
                .WithJsonBody(new List<User>())
                .WillRespond()
                .WithStatus(HttpStatusCode.Unauthorized);

            // Act & Assert
            await _pactBuilder.VerifyAsync(async ctx =>
            {
                var client = new HttpClient { BaseAddress = ctx.MockServerUri };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new List<User>();
                var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/BulkUsers", content);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            });
        }

        [Fact]
        public async Task PostBulkUsers_WithValidUser_Returns200WithSubject()
        {
            // Arrange
            var requestUser = new User
            {
                DisplayName = "TestUser",
                IdentityProviders = new List<IdentityProvider>
                {
                    new IdentityProvider
                    {
                        Provider = "windows",
                        ProviderId = "vms\\administrator"
                    }
                },
                IsAccountDisabled = false,
                Subject = null
            };

            var responseUser = new User
            {
                DisplayName = "TestUser",
                IdentityProviders = new List<IdentityProvider>
                {
                    new IdentityProvider
                    {
                        Provider = "windows",
                        ProviderId = "vms\\administrator"
                    }
                },
                IsAccountDisabled = false,
                Subject = "user-subject-id-123"
            };

            _pactBuilder
                .UponReceiving("A POST request to sync users via BulkUsers API")
                .Given("Valid Windows users exist in VAIS")
                .WithRequest(HttpMethod.Post, "/BulkUsers")
                .WithHeader("Authorization", "Bearer valid-token-from-SF")
                .WithHeader("Content-Type", "application/json")
                .WithJsonBody(new List<User> { requestUser })
                .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithJsonBody(new List<User> { responseUser });

            // Act & Assert
            await _pactBuilder.VerifyAsync(async ctx =>
            {
                var client = new HttpClient { BaseAddress = ctx.MockServerUri };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token-from-SF");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new List<User> { requestUser };
                var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/BulkUsers", content);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var responseContent = await response.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<User>>(responseContent);

                Assert.NotNull(users);
                Assert.Single(users);
                Assert.Equal("TestUser", users[0].DisplayName);
                Assert.Equal("user-subject-id-123", users[0].Subject);
            });
        }

        [Fact]
        public async Task PostBulkUsers_WithInvalidWindowsUser_Returns400WithError()
        {
            // Arrange
            var requestUser = new User
            {
                DisplayName = "InvalidUser",
                IdentityProviders = new List<IdentityProvider>
                {
                    new IdentityProvider
                    {
                        Provider = "windows",
                        ProviderId = "invalid-user"
                    }
                },
                IsAccountDisabled = false,
                Subject = null
            };

            var errorResponse = new ErrorResponse
            {
                Error = "InvalidWindowsUserName",
                Message = "Invalid UserName. User 'invalid-user' does not exist in Windows."
            };

            _pactBuilder
                .UponReceiving("A POST request to BulkUsers with invalid Windows user")
                .Given("Windows user does not exist in domain")
                .WithRequest(HttpMethod.Post, "/BulkUsers")
                .WithHeader("Authorization", "Bearer valid-token-from-SF")
                .WithHeader("Content-Type", "application/json")
                .WithJsonBody(new List<User> { requestUser })
                .WillRespond()
                .WithStatus(HttpStatusCode.BadRequest)
                .WithHeader("Content-Type", "application/json")
                .WithJsonBody(errorResponse);

            // Act & Assert
            await _pactBuilder.VerifyAsync(async ctx =>
            {
                var client = new HttpClient { BaseAddress = ctx.MockServerUri };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token-from-SF");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new List<User> { requestUser };
                var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/BulkUsers", content);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                var responseContent = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<ErrorResponse>(responseContent);

                Assert.NotNull(error);
                Assert.Equal("InvalidWindowsUserName", error.Error);
                Assert.Contains("invalid-user", error.Message);
            });
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
