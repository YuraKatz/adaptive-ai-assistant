using System.Text.Json.Serialization;

namespace AdaptiveAIBot.Models
{
    public class DeepSeekRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public DeepSeekMessage[] Messages { get; set; } = Array.Empty<DeepSeekMessage>();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    public class DeepSeekResponse
    {
        [JsonPropertyName("choices")]
        public DeepSeekChoice[]? Choices { get; set; }
    }

    public class DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekMessage? Message { get; set; }
    }

    public class DeepSeekMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}