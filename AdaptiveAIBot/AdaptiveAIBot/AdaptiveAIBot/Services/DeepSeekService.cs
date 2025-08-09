using AdaptiveAIBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

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
            // Простая версия без контекста для backward compatibility
            return await ProcessMessageAsync(userMessage, new ConversationContext());
        }

        public async Task<string> ProcessMessageAsync(string userMessage, ConversationContext context)
        {
            _logger.LogInformation($"Calling DeepSeek API with message: {userMessage}, context messages: {context.Messages.Count}");

            // Строим массив сообщений для API включая контекст
            var messages = BuildMessagesForApi(userMessage, context);

            var request = new DeepSeekRequest
            {
                Model = "deepseek-chat",
                Messages = messages,
                MaxTokens = 1000,
                Temperature = 0.7
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            _logger.LogInformation($"Sending request to DeepSeek API with {messages.Length} messages...");

            try
            {
                var response = await _httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);
                _logger.LogInformation($"DeepSeek API response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"DeepSeek API error: {response.StatusCode} - {errorContent}");
                    return "Извините, произошла ошибка при обработке вашего сообщения.";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"DeepSeek response received, length: {responseJson.Length}");

                var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);
                var aiResponse = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "Не удалось получить ответ.";

                _logger.LogInformation($"AI response generated, length: {aiResponse.Length}");
                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling DeepSeek API");
                return "Произошла техническая ошибка. Попробуйте позже.";
            }
        }

        private DeepSeekMessage[] BuildMessagesForApi(string userMessage, ConversationContext context)
        {
            var messages = new List<DeepSeekMessage>();

            // Добавляем system prompt
            messages.Add(new DeepSeekMessage
            {
                Role = "system",
                Content = GetSystemPrompt(context.UserId)
            });

            // Добавляем контекст разговора (если есть)
            foreach (var contextMessage in context.Messages.Take(20)) // Ограничиваем количество для экономии токенов
            {
                if (!string.IsNullOrWhiteSpace(contextMessage.Content))
                {
                    messages.Add(new DeepSeekMessage
                    {
                        Role = contextMessage.Role,
                        Content = contextMessage.Content
                    });
                }
            }

            // Добавляем текущее сообщение пользователя
            messages.Add(new DeepSeekMessage
            {
                Role = "user",
                Content = userMessage
            });

            _logger.LogInformation($"Built message array with {messages.Count} messages for API");
            return messages.ToArray();
        }

        private string GetSystemPrompt(long userId)
        {
            return @"Ты умный AI-ассистент для персонального управления знаниями. 

Твои возможности:
- Отвечаешь на вопросы пользователя, используя контекст предыдущих разговоров
- Помогаешь организовывать и находить информацию
- Предлагаешь сохранить важную информацию в базу знаний
- Общаешься естественно и дружелюбно на русском языке

Если в разговоре появляется важная информация (проекты, задачи, решения, контакты), предложи пользователю сохранить её.

Отвечай кратко и по существу, но дружелюбно.";
        }
    }
}