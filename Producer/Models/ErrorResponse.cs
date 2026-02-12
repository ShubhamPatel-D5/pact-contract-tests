using System.Text.Json.Serialization;

namespace VAIS_Producer.Models
{
    public class ErrorResponse
    {
        [JsonPropertyName("error")]
        public required string Error { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }
    }
}
