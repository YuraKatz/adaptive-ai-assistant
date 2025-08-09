using System.Text.Json.Serialization;

namespace AdaptiveAIBot.Models
{
    public class ConversationContext
    {
        public long UserId { get; set; }
        public List<ConversationMessage> Messages { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int MessageCount { get; set; }
        public bool IsCompressed { get; set; }
        public string? CompressedSummary { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = string.Empty; // "user", "assistant", "system"
        public string? Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsCompressed { get; set; }
        public double? ImportanceScore { get; set; }
        public List<string> Topics { get; set; } = new();
    }

    public class MessageAnalysis
    {
        public bool ContainsImportantInfo { get; set; }
        public List<string> ExtractedFacts { get; set; } = new();
        public List<string> Topics { get; set; } = new();
        public double ImportanceScore { get; set; }
        public string? SuggestedKnowledgeUpdate { get; set; }
    }

    public class KnowledgeUpdateSuggestion
    {
        public string TargetFile { get; set; } = string.Empty;
        public string UpdateType { get; set; } = string.Empty; // "add_section", "update_field", "create_entry"
        public object? Data { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}