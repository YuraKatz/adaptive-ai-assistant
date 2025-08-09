using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AdaptiveAIBot.Models;
using AdaptiveAIBot.Services;

namespace AdaptiveAIBot.Functions
{
    public class TelegramWebhookFunction
    {
        private readonly ILogger<TelegramWebhookFunction> _logger;
        private readonly IDeepSeekService _deepSeekService;
        private readonly ITelegramService _telegramService;
        private readonly IConversationService _conversationService;

        public TelegramWebhookFunction(
            ILogger<TelegramWebhookFunction> logger,
            IDeepSeekService deepSeekService,
            ITelegramService telegramService,
            IConversationService conversationService)
        {
            _logger = logger;
            _deepSeekService = deepSeekService;
            _telegramService = telegramService;
            _conversationService = conversationService;
        }

        [Function("TelegramWebhook")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Telegram webhook received");

            try
            {
                // Читаем и парсим Telegram webhook
                var update = await ParseTelegramUpdate(req);
                if (update?.Message?.Text == null)
                {
                    _logger.LogInformation("No text message found in update");
                    return new OkResult();
                }

                var message = update.Message;
                var userId = message.From?.Id ?? message.Chat.Id;
                var userMessage = message.Text;

                _logger.LogInformation($"Processing message from user {userId}: {userMessage}");

                // Получаем контекст разговора
                var conversationContext = await _conversationService.GetConversationContextAsync(userId);

                // Обрабатываем сообщение через AI
                var aiResponse = await _deepSeekService.ProcessMessageAsync(userMessage, conversationContext);

                // Сохраняем сообщения в контекст
                await _conversationService.AddMessageAsync(userId, userMessage, aiResponse);

                // Отправляем ответ пользователю
                await _telegramService.SendMessageAsync(message.Chat.Id, aiResponse);

                _logger.LogInformation($"Successfully processed message for user {userId}");
                return new OkResult();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Telegram update JSON");
                return new BadRequestObjectResult("Invalid JSON format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook");
                return new StatusCodeResult(500);
            }
        }

        private async Task<TelegramUpdate?> ParseTelegramUpdate(HttpRequest req)
        {
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received Telegram update: {requestBody}");

                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    _logger.LogWarning("Empty request body received");
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var update = JsonSerializer.Deserialize<TelegramUpdate>(requestBody, options);

                if (update == null)
                {
                    _logger.LogWarning("Failed to deserialize Telegram update");
                    return null;
                }

                _logger.LogInformation($"Parsed update ID: {update.UpdateId}, Message: {update.Message?.Text ?? "null"}");
                return update;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Telegram update");
                throw;
            }
        }
    }
}