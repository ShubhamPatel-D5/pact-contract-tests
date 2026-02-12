using Newtonsoft.Json;

namespace SF_Consumer.Models
{
    public class ErrorResponse
    {
        [JsonProperty("error")]
        public required string Error { get; set; }

        [JsonProperty("message")]
        public required string Message { get; set; }
    }
}
