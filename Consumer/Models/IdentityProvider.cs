using Newtonsoft.Json;

namespace SF_Consumer.Models
{
    public class IdentityProvider
    {
        [JsonProperty("provider")]
        public required string Provider { get; set; }

        [JsonProperty("providerId")]
        public required string ProviderId { get; set; }
    }
}
