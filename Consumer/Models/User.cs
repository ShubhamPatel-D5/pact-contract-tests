using System.Collections.Generic;
using Newtonsoft.Json;

namespace SF_Consumer.Models
{
    public class User
    {
        [JsonProperty("displayName")]
        public required string DisplayName { get; set; }

        [JsonProperty("identityProviders")]
        public required List<IdentityProvider> IdentityProviders { get; set; }

        [JsonProperty("isAccountDisabled")]
        public required bool IsAccountDisabled { get; set; }

        [JsonProperty("subject")]
        public string? Subject { get; set; }
    }
}
