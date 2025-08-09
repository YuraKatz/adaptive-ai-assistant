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
            _logger.LogInformation($"Calling DeepSeek API with message: {userMessage}");
            _logger.LogInformation($"API Key present: {!string.IsNullOrEmpty(_apiKey)}");

            var request = new DeepSeekRequest
            {
                Model = "deepseek-chat",
                Messages = new[]
                {
                    new DeepSeekMessage { Role = "user", Content = userMessage }
                },
                MaxTokens = 1000,
                Temperature = 0.7
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

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
    }
}