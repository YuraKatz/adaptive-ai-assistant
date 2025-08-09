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

        [JsonPropertyName("usage")]
        public DeepSeekUsage? Usage { get; set; }
    }

    public class DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public class DeepSeekMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class DeepSeekUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}