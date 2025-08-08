using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

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

                // Парсим Telegram Update
                var telegramUpdate = JsonSerializer.Deserialize<TelegramUpdate>(requestBody);

                if (telegramUpdate?.Message?.Text == null)
                {
                    return new OkResult(); // Ignore non-text messages
                }

                // Извлекаем данные сообщения
                var chatId = telegramUpdate.Message.Chat.Id;
                var userMessage = telegramUpdate.Message.Text;

                _logger.LogInformation($"Chat ID: {chatId}, Message: {userMessage}");

                // Вызываем DeepSeek API
                var aiResponse = await CallDeepSeekApi(userMessage);

                // Отправляем ответ обратно в Telegram
                await SendTelegramMessage(chatId, aiResponse);

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

            var response = await _httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"DeepSeek API error: {response.StatusCode} - {errorContent}");
                return "Извините, произошла ошибка при обработке вашего сообщения.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
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
        public DeepSeekChoice[]? Choices { get; set; }
    }

    public class DeepSeekChoice
    {
        public DeepSeekMessage? Message { get; set; }
    }

    public class DeepSeekMessage
    {
        public string? Content { get; set; }
    }
}