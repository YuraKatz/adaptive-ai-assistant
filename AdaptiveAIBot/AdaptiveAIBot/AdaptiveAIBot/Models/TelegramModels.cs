using System.Text.Json.Serialization;

namespace AdaptiveAIBot.Models
{
    public class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public int UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }
    }

    public class TelegramMessage
    {
        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [JsonPropertyName("from")]
        public TelegramUser? From { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChat Chat { get; set; } = null!;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class TelegramUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public class TelegramChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}