using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VAIS_Producer.Models
{
    public class User
    {
        [JsonPropertyName("displayName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("identityProviders")]
        public required List<IdentityProvider> IdentityProviders { get; set; }

        [JsonPropertyName("isAccountDisabled")]
        public required bool IsAccountDisabled { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }
    }
}
