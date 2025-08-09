using Microsoft.Extensions.Logging;
using AdaptiveAIBot.Models;
using System.Collections.Concurrent;

namespace AdaptiveAIBot.Services
{
    public interface IConversationService
    {
        Task<ConversationContext> GetConversationContextAsync(long userId);
        Task AddMessageAsync(long userId, string userMessage, string aiResponse);
        Task<bool> ShouldCompressContextAsync(long userId);
        Task CompressConversationAsync(long userId);
    }

    public class ConversationService : IConversationService
    {
        private readonly ILogger<ConversationService> _logger;

        // In-memory storage для MVP (в production будет Redis/CosmosDB)
        private static readonly ConcurrentDictionary<long, ConversationContext> _conversations = new();

        // Настройки сжатия контекста
        private const int MAX_MESSAGES_BEFORE_COMPRESSION = 20;
        private const int KEEP_RECENT_MESSAGES = 10;

        public ConversationService(ILogger<ConversationService> logger)
        {
            _logger = logger;
        }

        public Task<ConversationContext> GetConversationContextAsync(long userId)
        {
            var context = _conversations.GetOrAdd(userId, id => new ConversationContext
            {
                UserId = id,
                Messages = new List<ConversationMessage>(),
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            });

            context.LastActivity = DateTime.UtcNow;

            _logger.LogInformation($"Retrieved conversation context for user {userId}, messages count: {context.Messages.Count}");
            return Task.FromResult(context);
        }

        public async Task AddMessageAsync(long userId, string userMessage, string aiResponse)
        {
            var context = await GetConversationContextAsync(userId);

            // Добавляем пару сообщений: пользователь -> AI
            context.Messages.Add(new ConversationMessage
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            });

            context.Messages.Add(new ConversationMessage
            {
                Role = "assistant",
                Content = aiResponse,
                Timestamp = DateTime.UtcNow
            });

            context.LastActivity = DateTime.UtcNow;
            context.MessageCount = context.Messages.Count;

            _logger.LogInformation($"Added message pair for user {userId}, total messages: {context.MessageCount}");

            // Проверяем нужно ли сжимать контекст
            if (await ShouldCompressContextAsync(userId))
            {
                await CompressConversationAsync(userId);
            }
        }

        public Task<bool> ShouldCompressContextAsync(long userId)
        {
            if (!_conversations.TryGetValue(userId, out var context))
                return Task.FromResult(false);

            var shouldCompress = context.Messages.Count >= MAX_MESSAGES_BEFORE_COMPRESSION;

            if (shouldCompress)
            {
                _logger.LogInformation($"Context compression needed for user {userId}, messages: {context.Messages.Count}");
            }

            return Task.FromResult(shouldCompress);
        }

        public Task CompressConversationAsync(long userId)
        {
            if (!_conversations.TryGetValue(userId, out var context))
                return Task.CompletedTask;

            _logger.LogInformation($"Starting context compression for user {userId}");

            // Простая стратегия сжатия для MVP:
            // 1. Сохраняем последние N сообщений
            // 2. Старые сообщения сжимаем в summary

            var messagesToCompress = context.Messages
                .Take(context.Messages.Count - KEEP_RECENT_MESSAGES)
                .ToList();

            var recentMessages = context.Messages
                .Skip(context.Messages.Count - KEEP_RECENT_MESSAGES)
                .ToList();

            if (messagesToCompress.Any())
            {
                // Создаем compressed summary
                var summary = CreateConversationSummary(messagesToCompress);

                // Обновляем контекст
                context.Messages = new List<ConversationMessage>
                {
                    new ConversationMessage
                    {
                        Role = "system",
                        Content = $"[COMPRESSED HISTORY]: {summary}",
                        Timestamp = DateTime.UtcNow,
                        IsCompressed = true
                    }
                };

                context.Messages.AddRange(recentMessages);

                _logger.LogInformation($"Compressed {messagesToCompress.Count} messages for user {userId}, kept {recentMessages.Count} recent messages");
            }

            return Task.CompletedTask;
        }

        private string CreateConversationSummary(List<ConversationMessage> messages)
        {
            // Простая стратегия для MVP - просто основные темы
            var userMessages = messages.Where(m => m.Role == "user").Select(m => m.Content).ToList();
            var aiMessages = messages.Where(m => m.Role == "assistant").Select(m => m.Content).ToList();

            var topics = ExtractTopics(userMessages);
            var timespan = messages.Max(m => m.Timestamp) - messages.Min(m => m.Timestamp);

            return $"Conversation summary ({messages.Count} messages over {timespan.TotalMinutes:F0} minutes): " +
                   $"Topics discussed: {string.Join(", ", topics)}. " +
                   $"User asked about: {string.Join(", ", userMessages.Take(3))}";
        }

        private List<string> ExtractTopics(List<string> messages)
        {
            // Очень простое извлечение тем - ключевые слова
            var keywords = new HashSet<string>();

            foreach (var message in messages)
            {
                var words = message.ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 3)
                    .Take(2);

                foreach (var word in words)
                {
                    keywords.Add(word);
                }
            }

            return keywords.Take(5).ToList();
        }
    }
}