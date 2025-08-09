using AdaptiveAIBot.Models;

namespace AdaptiveAIBot.Services
{
    public interface IConversationService
    {
        Task<ConversationContext> GetConversationContextAsync(long userId);
        Task AddMessageAsync(long userId, string userMessage, string aiResponse);
        Task<bool> ShouldCompressContextAsync(long userId);
        Task CompressConversationAsync(long userId);
        Task<MessageAnalysis> AnalyzeMessageAsync(string message);
        Task<List<KnowledgeUpdateSuggestion>> GetKnowledgeUpdateSuggestionsAsync(long userId);
    }
}