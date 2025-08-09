using Microsoft.Extensions.Logging;
using AdaptiveAIBot.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AdaptiveAIBot.Services
{
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

            // Анализируем сообщение на важность
            var messageAnalysis = await AnalyzeMessageAsync(userMessage);

            // Добавляем пару сообщений: пользователь -> AI
            context.Messages.Add(new ConversationMessage
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.UtcNow,
                ImportanceScore = messageAnalysis.ImportanceScore,
                Topics = messageAnalysis.Topics
            });

            context.Messages.Add(new ConversationMessage
            {
                Role = "assistant",
                Content = aiResponse,
                Timestamp = DateTime.UtcNow
            });

            context.LastActivity = DateTime.UtcNow;
            context.MessageCount = context.Messages.Count;

            _logger.LogInformation($"Added message pair for user {userId}, total messages: {context.MessageCount}, importance: {messageAnalysis.ImportanceScore:F2}");

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
                context.IsCompressed = true;
                context.CompressedSummary = summary;

                _logger.LogInformation($"Compressed {messagesToCompress.Count} messages for user {userId}, kept {recentMessages.Count} recent messages");
            }

            return Task.CompletedTask;
        }

        public Task<MessageAnalysis> AnalyzeMessageAsync(string message)
        {
            var analysis = new MessageAnalysis();

            // Простая эвристика для определения важности
            var importanceKeywords = new[]
            {
                "проект", "задача", "решение", "встреча", "дедлайн",
                "клиент", "контракт", "важно", "срочно", "план",
                "цель", "результат", "статус", "проблема", "идея"
            };

            var topics = new List<string>();
            var extractedFacts = new List<string>();
            double importanceScore = 0.0;

            var messageLower = message.ToLower();

            // Ищем ключевые слова
            foreach (var keyword in importanceKeywords)
            {
                if (messageLower.Contains(keyword))
                {
                    importanceScore += 0.1;
                    topics.Add(keyword);
                }
            }

            // Ищем даты
            var datePattern = @"\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4}";
            if (Regex.IsMatch(message, datePattern))
            {
                importanceScore += 0.2;
                extractedFacts.Add("содержит дату");
            }

            // Ищем числа (возможно, метрики)
            var numberPattern = @"\d+%|\d+\s*(рублей|долларов|евро|часов|дней)";
            if (Regex.IsMatch(message, numberPattern))
            {
                importanceScore += 0.15;
                extractedFacts.Add("содержит числовые данные");
            }

            // Длинные сообщения обычно важнее
            if (message.Length > 100)
            {
                importanceScore += 0.1;
            }

            analysis.ImportanceScore = Math.Min(1.0, importanceScore);
            analysis.Topics = topics.Distinct().ToList();
            analysis.ExtractedFacts = extractedFacts;
            analysis.ContainsImportantInfo = importanceScore > 0.3;

            return Task.FromResult(analysis);
        }

        public async Task<List<KnowledgeUpdateSuggestion>> GetKnowledgeUpdateSuggestionsAsync(long userId)
        {
            var context = await GetConversationContextAsync(userId);
            var suggestions = new List<KnowledgeUpdateSuggestion>();

            // Анализируем последние сообщения на предмет важной информации
            var recentImportantMessages = context.Messages
                .Where(m => m.Role == "user" && m.ImportanceScore > 0.3)
                .TakeLast(5)
                .ToList();

            foreach (var message in recentImportantMessages)
            {
                if (message.Topics.Contains("проект"))
                {
                    suggestions.Add(new KnowledgeUpdateSuggestion
                    {
                        TargetFile = "projects.yaml",
                        UpdateType = "add_section",
                        Data = new { content = message.Content, timestamp = message.Timestamp },
                        Reason = "Обнаружена информация о проекте",
                        Confidence = message.ImportanceScore ?? 0.5
                    });
                }

                if (message.Topics.Contains("встреча"))
                {
                    suggestions.Add(new KnowledgeUpdateSuggestion
                    {
                        TargetFile = "meetings.yaml",
                        UpdateType = "add_section",
                        Data = new { content = message.Content, timestamp = message.Timestamp },
                        Reason = "Обнаружена информация о встрече",
                        Confidence = message.ImportanceScore ?? 0.5
                    });
                }
            }

            _logger.LogInformation($"Generated {suggestions.Count} knowledge update suggestions for user {userId}");
            return suggestions;
        }

        private string CreateConversationSummary(List<ConversationMessage> messages)
        {
            // Простая стратегия для MVP - просто основные темы
            var userMessages = messages.Where(m => m.Role == "user").Select(m => m.Content).ToList();
            var allTopics = messages.SelectMany(m => m.Topics).Distinct().ToList();

            var timespan = messages.Max(m => m.Timestamp) - messages.Min(m => m.Timestamp);

            return $"Conversation summary ({messages.Count} messages over {timespan.TotalMinutes:F0} minutes): " +
                   $"Topics discussed: {string.Join(", ", allTopics.Take(5))}. " +
                   $"Key user queries: {string.Join("; ", userMessages.Take(3).Select(m => m?.Substring(0, Math.Min(50, m.Length ?? 0))))}";
        }
    }
}