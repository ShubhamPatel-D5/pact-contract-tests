using System.Text.Json.Serialization;

namespace VAIS_Producer.Models
{
    public class ProviderStateRequest
    {
        [JsonPropertyName("consumer")]
        public string? Consumer { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("params")]
        public object? Params { get; set; }
    }
}
