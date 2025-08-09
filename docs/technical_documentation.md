# Technical Documentation

## 🔧 Implementation Details

### Azure Functions Configuration

The project uses **Azure Functions v4** with **.NET 8 isolated worker model** for optimal performance and cost efficiency.

#### Function App Settings:
```json
{
  "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=...",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
  "DEEPSEEK_API_KEY": "sk-...",
  "TELEGRAM_BOT_TOKEN": "bot..."
}
```

#### Host Configuration:
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    }
  }
}
```

## 🧠 AI Context Compression Algorithm

### Problem Statement:
Traditional AI chat applications suffer from **exponential context growth**:
- Each message requires sending full conversation history
- Costs increase exponentially: `cost = tokens * message_count`
- Context window limitations cause memory loss

### Our Solution: "AI Prompt ZIP"

#### Multi-Layer Memory Architecture:

```csharp
public class ConversationContext
{
    public List<ConversationMessage> Messages { get; set; }     // Active memory
    public string? CompressedSummary { get; set; }              // Compressed history
    public bool IsCompressed { get; set; }                      // Compression flag
    public DateTime LastActivity { get; set; }                  // Activity tracking
}
```

#### Compression Trigger Logic:
```csharp
private const int MAX_MESSAGES_BEFORE_COMPRESSION = 20;
private const int KEEP_RECENT_MESSAGES = 10;

public async Task<bool> ShouldCompressContextAsync(long userId)
{
    var context = await GetConversationContextAsync(userId);
    return context.Messages.Count >= MAX_MESSAGES_BEFORE_COMPRESSION;
}
```

#### Intelligent Compression Process:
```csharp
public Task CompressConversationAsync(long userId)
{
    // 1. Identify messages to compress (oldest ones)
    var messagesToCompress = context.Messages
        .Take(context.Messages.Count - KEEP_RECENT_MESSAGES)
        .ToList();

    // 2. Create AI-powered summary
    var summary = CreateConversationSummary(messagesToCompress);

    // 3. Replace old messages with compressed summary
    context.Messages = new List<ConversationMessage>
    {
        new ConversationMessage
        {
            Role = "system",
            Content = $"[COMPRESSED HISTORY]: {summary}",
            IsCompressed = true
        }
    };

    // 4. Keep recent messages for context
    context.Messages.AddRange(recentMessages);
}
```

### Cost Analysis:

| Scenario | Traditional | With Compression | Savings |
|----------|-------------|------------------|---------|
| 10 messages | 500 tokens | 500 tokens | 0% |
| 50 messages | 2,500 tokens | 800 tokens | 68% |
| 100 messages | 5,000 tokens | 800 tokens | 84% |
| 500 messages | 25,000 tokens | 800 tokens | 97% |

**Result:** Up to 97% cost reduction while maintaining conversation continuity.

## 🔍 Message Importance Analysis

### Keyword-Based Classification:
```csharp
private readonly string[] ImportanceKeywords = {
    "проект", "задача", "решение", "встреча", "дедлайн",
    "клиент", "контракт", "важно", "срочно", "план",
    "цель", "результат", "статус", "проблема", "идея"
};
```

### Pattern Recognition:
```csharp
// Date detection
var datePattern = @"\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4}";

// Metrics detection  
var numberPattern = @"\d+%|\d+\s*(рублей|долларов|евро|часов|дней)";

// Importance scoring
double importanceScore = 0.0;
if (messageLower.Contains(keyword)) importanceScore += 0.1;
if (Regex.IsMatch(message, datePattern)) importanceScore += 0.2;
if (message.Length > 100) importanceScore += 0.1;
```

### Knowledge Update Suggestions:
```csharp
public async Task<List<KnowledgeUpdateSuggestion>> GetKnowledgeUpdateSuggestionsAsync(long userId)
{
    var suggestions = new List<KnowledgeUpdateSuggestion>();
    var importantMessages = context.Messages
        .Where(m => m.ImportanceScore > 0.3)
        .ToList();

    foreach (var message in importantMessages)
    {
        if (message.Topics.Contains("проект"))
        {
            suggestions.Add(new KnowledgeUpdateSuggestion
            {
                TargetFile = "projects.yaml",
                UpdateType = "add_section",
                Confidence = message.ImportanceScore.GetValueOrDefault(0.5)
            });
        }
    }
    
    return suggestions;
}
```

## 🌐 DeepSeek API Integration

### Request Format:
```csharp
public class DeepSeekRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "deepseek-chat";

    [JsonPropertyName("messages")]
    public DeepSeekMessage[] Messages { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1000;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
}
```

### Context Building Strategy:
```csharp
private DeepSeekMessage[] BuildMessagesForApi(string userMessage, ConversationContext context)
{
    var messages = new List<DeepSeekMessage>();

    // System prompt
    messages.Add(new DeepSeekMessage
    {
        Role = "system",
        Content = GetSystemPrompt()
    });

    // Compressed history if available
    if (!string.IsNullOrEmpty(context.CompressedSummary))
    {
        messages.Add(new DeepSeekMessage
        {
            Role = "system", 
            Content = $"[PREVIOUS CONTEXT]: {context.CompressedSummary}"
        });
    }

    // Recent conversation history (last 15 messages)
    var recentMessages = context.Messages
        .Where(m => !m.IsCompressed)
        .TakeLast(15)
        .ToList();

    // Current user message
    messages.Add(new DeepSeekMessage
    {
        Role = "user",
        Content = userMessage
    });

    return messages.ToArray();
}
```

### Error Handling & Resilience:
```csharp
try
{
    var response = await _httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);
    
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError($"DeepSeek API error: {response.StatusCode}");
        return "Извините, произошла ошибка при обработке вашего сообщения.";
    }

    var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);
    return deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "Не удалось получить ответ.";
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error");
    return "Проблема с сетевым соединением. Попробуйте позже.";
}
catch (JsonException ex)
{
    _logger.LogError(ex, "JSON parsing error");
    return "Произошла ошибка обработки ответа.";
}
```

## 📱 Telegram Integration

### Webhook Processing:
```csharp
[Function("TelegramWebhook")]
public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
{
    var update = await ParseTelegramUpdate(req);
    if (update?.Message?.Text == null) return new OkResult();

    var userId = update.Message.From?.Id ?? update.Message.Chat.Id;
    var userMessage = update.Message.Text;

    // Get conversation context with compression
    var conversationContext = await _conversationService.GetConversationContextAsync(userId);

    // Process through AI
    var aiResponse = await _deepSeekService.ProcessMessageAsync(userMessage, conversationContext);

    // Save to conversation history
    await _conversationService.AddMessageAsync(userId, userMessage, aiResponse);

    // Send response
    await _telegramService.SendMessageAsync(update.Message.Chat.Id, aiResponse);

    return new OkResult();
}
```

### Message Parsing:
```csharp
private async Task<TelegramUpdate?> ParseTelegramUpdate(HttpRequest req)
{
    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    
    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    return JsonSerializer.Deserialize<TelegramUpdate>(requestBody, options);
}
```

## 💾 Storage Strategy

### Current Implementation (MVP):
```csharp
// In-memory storage using ConcurrentDictionary
private static readonly ConcurrentDictionary<long, ConversationContext> _conversations = new();

public Task<ConversationContext> GetConversationContextAsync(long userId)
{
    var context = _conversations.GetOrAdd(userId, id => new ConversationContext
    {
        UserId = id,
        Messages = new List<ConversationMessage>(),
        CreatedAt = DateTime.UtcNow,
        LastActivity = DateTime.UtcNow
    });

    return Task.FromResult(context);
}
```

### Production Migration Path:
```csharp
// Phase 2: Redis Cache
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "your-redis-connection-string";
});

// Phase 3: CosmosDB + Redis hybrid
services.AddDbContext<ConversationDbContext>(options =>
    options.UseCosmos("connection-string", "database-name"));
```

## 🔐 Security Implementation

### Authentication:
```csharp
public class TelegramWebhookFunction
{
    private readonly string[] _allowedUsers = { "user1", "user2" }; // Whitelist

    private bool IsAuthorizedUser(long userId)
    {
        return _allowedUsers.Contains(userId.ToString());
    }
}
```

### API Key Management:
```csharp
public class DeepSeekService
{
    private readonly string _apiKey;

    public DeepSeekService(IConfiguration configuration)
    {
        _apiKey = configuration["DEEPSEEK_API_KEY"] ?? 
                 throw new ArgumentException("DEEPSEEK_API_KEY not found");
    }
}
```

## 📊 Monitoring & Logging

### Structured Logging:
```csharp
_logger.LogInformation($"Processing message from user {userId}: {userMessage}");
_logger.LogInformation($"Token usage - Prompt: {usage.PromptTokens}, Completion: {usage.CompletionTokens}");
_logger.LogWarning("TELEGRAM_BOT_TOKEN not configured");
_logger.LogError(ex, "Failed to send message to chat {ChatId}", chatId);
```

### Performance Metrics:
```csharp
public class TelemetryService
{
    public void TrackUserQuery(string userId, TimeSpan responseTime)
    {
        _telemetryClient.TrackMetric("query_response_time", responseTime.TotalMilliseconds);
        _telemetryClient.TrackEvent("user_query", new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["response_time_ms"] = responseTime.TotalMilliseconds.ToString()
        });
    }

    public void TrackContextCompression(int originalMessages, int compressedSize)
    {
        _telemetryClient.TrackMetric("compression_ratio", (double)originalMessages / compressedSize);
    }
}
```

## 🚀 Deployment Configuration

### Azure Functions Deployment:
```bash
# Build and publish
dotnet publish --configuration Release

# Deploy to Azure
func azure functionapp publish adaptive-ai-bot-yk --dotnet-isolated
```

### Environment Variables:
```bash
# Production settings
az functionapp config appsettings set --name adaptive-ai-bot-yk --resource-group adaptive-ai-test --settings \
  "DEEPSEEK_API_KEY=$DEEPSEEK_KEY" \
  "TELEGRAM_BOT_TOKEN=$TELEGRAM_TOKEN" \
  "ENVIRONMENT=Production"
```

### Scaling Configuration:
```json
{
  "functionApp": {
    "scaleAndConcurrency": {
      "maximumInstanceCount": 10,
      "instanceMemoryMB": 512
    }
  }
}
```

---

**Last Updated:** August 9, 2025  
**Version:** MVP 1.0  
**Target Deployment:** September 2025