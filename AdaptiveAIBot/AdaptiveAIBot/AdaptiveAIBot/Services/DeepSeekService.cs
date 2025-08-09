using AdaptiveAIBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdaptiveAIBot.Services
{
    public interface IDeepSeekService
    {
        Task<string> ProcessMessageAsync(string userMessage);
        Task<string> ProcessMessageAsync(string userMessage, ConversationContext context);
    }

    public class DeepSeekService : IDeepSeekService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<DeepSeekService> _logger;

        public DeepSeekService(HttpClient httpClient, IConfiguration configuration, ILogger<DeepSeekService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["DEEPSEEK_API_KEY"] ?? throw new ArgumentException("DEEPSEEK_API_KEY not found");
            _logger = logger;
        }

        public async Task<string> ProcessMessageAsync(string userMessage)
        {
            return await ProcessMessageAsync(userMessage, new ConversationContext());
        }

        public async Task<string> ProcessMessageAsync(string userMessage, ConversationContext context)
        {
            _logger.LogInformation($"Calling DeepSeek API with message: {userMessage}, context messages: {context.Messages.Count}");

            var messages = BuildMessagesForApi(userMessage, context);

            var request = new DeepSeekRequest
            {
                Model = "deepseek-chat",
                Messages = messages,
                MaxTokens = 1000,
                Temperature = 0.7
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            _logger.LogInformation($"Sending request to DeepSeek API with {messages.Length} messages...");

            try
            {
                var response = await _httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"DeepSeek API response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"DeepSeek API error: {response.StatusCode} - {responseJson}");
                    return "Извините, произошла ошибка при обработке вашего сообщения.";
                }

                var responseOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null
                };

                var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, responseOptions);
                var aiResponse = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "Не удалось получить ответ.";

                if (deepSeekResponse?.Usage != null)
                {
                    var usage = deepSeekResponse.Usage;
                    _logger.LogInformation($"Token usage - Prompt: {usage.PromptTokens}, Completion: {usage.CompletionTokens}, Total: {usage.TotalTokens}");
                }

                return aiResponse;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error");
                return "Произошла ошибка обработки ответа.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling DeepSeek API");
                return "Произошла техническая ошибка.";
            }
        }

        private DeepSeekMessage[] BuildMessagesForApi(string userMessage, ConversationContext context)
        {
            var messages = new List<DeepSeekMessage>();

            messages.Add(new DeepSeekMessage
            {
                Role = "system",
                Content = GetSystemPrompt()
            });

            if (!string.IsNullOrEmpty(context.CompressedSummary))
            {
                messages.Add(new DeepSeekMessage
                {
                    Role = "system",
                    Content = $"[PREVIOUS CONTEXT]: {context.CompressedSummary}"
                });
            }

            var recentMessages = context.Messages
                .Where(m => !m.IsCompressed && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(15)
                .ToList();

            foreach (var contextMessage in recentMessages)
            {
                messages.Add(new DeepSeekMessage
                {
                    Role = contextMessage.Role,
                    Content = contextMessage.Content
                });
            }

            messages.Add(new DeepSeekMessage
            {
                Role = "user",
                Content = userMessage
            });

            return messages.ToArray();
        }

        private string GetSystemPrompt()
        {
            return @"Ты умный AI-ассистент для персонального управления знаниями. 

Твои возможности:
- Отвечаешь на вопросы пользователя, используя контекст предыдущих разговоров
- Помогаешь организовывать и находить информацию
- Предлагаешь сохранить важную информацию в базу знаний
- Общаешься естественно и дружелюбно на русском языке

Стиль общения: прямой, без лишних восторгов, техническая глубина когда нужно.

Если в разговоре появляется важная информация (проекты, задачи, решения, контакты), предложи пользователю сохранить её.

Отвечай кратко и по существу, но дружелюбно.";
        }
    }
}