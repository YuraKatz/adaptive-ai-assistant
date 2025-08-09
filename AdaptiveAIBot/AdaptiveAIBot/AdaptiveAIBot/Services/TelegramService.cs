using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AdaptiveAIBot.Services
{
    public interface ITelegramService
    {
        Task SendMessageAsync(long chatId, string message);
    }

    public class TelegramService : ITelegramService
    {
        private readonly HttpClient _httpClient;
        private readonly string _botToken;
        private readonly ILogger<TelegramService> _logger;

        public TelegramService(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramService> logger)
        {
            _httpClient = httpClient;
            _botToken = configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty;
            _logger = logger;
        }

        public async Task SendMessageAsync(long chatId, string message)
        {
            if (string.IsNullOrEmpty(_botToken))
            {
                _logger.LogWarning("TELEGRAM_BOT_TOKEN not configured, skipping message send");
                return;
            }

            var telegramMessage = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML" // Поддержка HTML форматирования
            };

            var json = JsonSerializer.Serialize(telegramMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var telegramUrl = $"https://api.telegram.org/bot{_botToken}/sendMessage";

            try
            {
                _logger.LogInformation($"Sending message to chat {chatId}: {message.Substring(0, Math.Min(50, message.Length))}...");

                var response = await _httpClient.PostAsync(telegramUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Telegram API error: {response.StatusCode} - {errorContent}");
                }
                else
                {
                    _logger.LogInformation($"Message sent successfully to chat {chatId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send message to chat {chatId}");
                throw; // Re-throw для обработки в вызывающем коде
            }
        }
    }
}