using System.Text.Json.Serialization;

namespace VAIS_Producer.Models
{
    public class IdentityProvider
    {
        [JsonPropertyName("provider")]
        public required string Provider { get; set; }

        [JsonPropertyName("providerId")]
        public required string ProviderId { get; set; }
    }
}
