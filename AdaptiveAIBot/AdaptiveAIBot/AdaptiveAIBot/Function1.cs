using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdaptiveAIBot
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _deepSeekApiKey;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();

            // API Key из environment variables
            _deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("DEEPSEEK_API_KEY not found");
        }

        [Function("TelegramWebhook")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Telegram webhook received");

            try
            {
                // Читаем тело запроса
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Request body: {requestBody}");

                // Временно - простое тестирование без парсинга Telegram
                var userMessage = "Привет! Как дела?"; // Хардкод для теста

                _logger.LogInformation($"Test message: {userMessage}");

                // Вызываем DeepSeek API
                var aiResponse = await CallDeepSeekApi(userMessage);

                _logger.LogInformation($"AI Response: {aiResponse}");

                // Возвращаем ответ AI (вместо отправки в Telegram)
                return new OkObjectResult(new
                {
                    userMessage = userMessage,
                    aiResponse = aiResponse
                });

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook");
                return new StatusCodeResult(500);
            }
        }

        private async Task<string> CallDeepSeekApi(string userMessage)
        {
            _logger.LogInformation($"Calling DeepSeek API with message: {userMessage}");
            _logger.LogInformation($"API Key present: {!string.IsNullOrEmpty(_deepSeekApiKey)}");

            var request = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                },
                max_tokens = 1000,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_deepSeekApiKey}");

            _logger.LogInformation("Sending request to DeepSeek API...");
            var response = await _httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);
            _logger.LogInformation($"DeepSeek API response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"DeepSeek API error: {response.StatusCode} - {errorContent}");
                return "Извините, произошла ошибка при обработке вашего сообщения.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"DeepSeek raw response: {responseJson}");

            var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);

            return deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "Не удалось получить ответ.";
        }

        private async Task SendTelegramMessage(long chatId, string message)
        {
            var telegramToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
            if (string.IsNullOrEmpty(telegramToken))
            {
                _logger.LogError("TELEGRAM_BOT_TOKEN not found");
                return;
            }

            var telegramMessage = new
            {
                chat_id = chatId,
                text = message
            };

            var json = JsonSerializer.Serialize(telegramMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var telegramUrl = $"https://api.telegram.org/bot{telegramToken}/sendMessage";
            var response = await _httpClient.PostAsync(telegramUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Telegram API error: {response.StatusCode} - {errorContent}");
            }
        }
    }

    // Data models для JSON десериализации
    public class TelegramUpdate
    {
        public int UpdateId { get; set; }
        public TelegramMessage? Message { get; set; }
    }

    public class TelegramMessage
    {
        public int MessageId { get; set; }
        public TelegramUser? From { get; set; }
        public TelegramChat Chat { get; set; } = null!;
        public string? Text { get; set; }
    }

    public class TelegramUser
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? Username { get; set; }
    }

    public class TelegramChat
    {
        public long Id { get; set; }
        public string? Type { get; set; }
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
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}