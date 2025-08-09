# API Documentation

## 🔗 Endpoints

### Telegram Webhook

**Endpoint:** `POST /api/TelegramWebhook`  
**Description:** Receives and processes Telegram webhook messages  
**Authorization:** Function-level key required

#### Request:
```json
{
  "update_id": 123456789,
  "message": {
    "message_id": 12345,
    "from": {
      "id": 987654321,
      "first_name": "Yuri",
      "username": "yurikatz"
    },
    "chat": {
      "id": 987654321,
      "type": "private"
    },
    "text": "Привет! Как дела с проектом?"
  }
}
```

#### Response:
```json
{
  "status": "success",
  "processed": true
}
```

### Internal Service APIs

## 🧠 ConversationService

### GetConversationContextAsync
```csharp
Task<ConversationContext> GetConversationContextAsync(long userId)
```

**Purpose:** Retrieve or create conversation context for user  
**Parameters:**
- `userId` (long): Telegram user ID

**Returns:**
```csharp
public class ConversationContext
{
    public long UserId { get; set; }
    public List<ConversationMessage> Messages { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public int MessageCount { get; set; }
    public bool IsCompressed { get; set; }
    public string? CompressedSummary { get; set; }
}
```

### AddMessageAsync
```csharp
Task AddMessageAsync(long userId, string userMessage, string aiResponse)
```

**Purpose:** Add message pair to conversation history with importance analysis  
**Parameters:**
- `userId` (long): User identifier
- `userMessage` (string): User's input message
- `aiResponse` (string): AI-generated response

**Side Effects:**
- Analyzes message importance
- Triggers context compression if needed
- Updates conversation statistics

### ShouldCompressContextAsync
```csharp
Task<bool> ShouldCompressContextAsync(long userId)
```

**Purpose:** Determine if conversation context needs compression  
**Logic:** Returns `true` if message count >= 20

### CompressConversationAsync
```csharp
Task CompressConversationAsync(long userId)
```

**Purpose:** Compress old messages into intelligent summary  
**Algorithm:**
1. Keep last 10 messages
2. Compress older messages into summary
3. Replace old messages with compressed version
4. Preserve conversation continuity

### AnalyzeMessageAsync
```csharp
Task<MessageAnalysis> AnalyzeMessageAsync(string message)
```

**Purpose:** Analyze message importance and extract topics  
**Returns:**
```csharp
public class MessageAnalysis
{
    public bool ContainsImportantInfo { get; set; }
    public List<string> ExtractedFacts { get; set; }
    public List<string> Topics { get; set; }
    public double ImportanceScore { get; set; }
    public string? SuggestedKnowledgeUpdate { get; set; }
}
```

**Scoring Criteria:**
- Keywords (проект, задача, решение, etc.): +0.1 each
- Date patterns: +0.2
- Numeric data: +0.15
- Message length >100 chars: +0.1
- **Max Score:** 1.0

### GetKnowledgeUpdateSuggestionsAsync
```csharp
Task<List<KnowledgeUpdateSuggestion>> GetKnowledgeUpdateSuggestionsAsync(long userId)
```

**Purpose:** Generate suggestions for knowledge base updates  
**Returns:**
```csharp
public class KnowledgeUpdateSuggestion
{
    public string TargetFile { get; set; }      // "projects.yaml", "meetings.yaml"
    public string UpdateType { get; set; }      // "add_section", "update_field"
    public object? Data { get; set; }           // Structured data to save
    public string Reason { get; set; }          // Human-readable explanation
    public double Confidence { get; set; }      // 0.0-1.0 confidence score
}
```

## 🤖 DeepSeekService

### ProcessMessageAsync (Simple)
```csharp
Task<string> ProcessMessageAsync(string userMessage)
```

**Purpose:** Process message without conversation context  
**Use Case:** One-off queries

### ProcessMessageAsync (With Context)
```csharp
Task<string> ProcessMessageAsync(string userMessage, ConversationContext context)
```

**Purpose:** Process message with full conversation context  
**Parameters:**
- `userMessage` (string): Current user input
- `context` (ConversationContext): Full conversation history

**API Call Structure:**
```json
{
  "model": "deepseek-chat",
  "messages": [
    {
      "role": "system",
      "content": "You are a smart AI assistant for personal knowledge management..."
    },
    {
      "role": "system", 
      "content": "[PREVIOUS CONTEXT]: Compressed conversation summary..."
    },
    {
      "role": "user",
      "content": "Previous user message"
    },
    {
      "role": "assistant",
      "content": "Previous AI response"
    },
    {
      "role": "user",
      "content": "Current user message"
    }
  ],
  "max_tokens": 1000,
  "temperature": 0.7
}
```

**Response Processing:**
```csharp
var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);
var aiResponse = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content;

// Log token usage for cost tracking
if (deepSeekResponse?.Usage != null)
{
    var usage = deepSeekResponse.Usage;
    _logger.LogInformation($"Token usage - Prompt: {usage.PromptTokens}, Completion: {usage.CompletionTokens}");
}
```

## 📱 TelegramService

### SendMessageAsync
```csharp
Task SendMessageAsync(long chatId, string message)
```

**Purpose:** Send message to Telegram chat  
**Parameters:**
- `chatId` (long): Telegram chat identifier
- `message` (string): Message text (supports HTML formatting)

**API Call:**
```json
{
  "chat_id": 987654321,
  "text": "Your message here",
  "parse_mode": "HTML"
}
```

**Error Handling:**
- Network failures: Retry logic
- API errors: Detailed logging
- Invalid chat_id: Graceful degradation

## 📊 Data Models

### ConversationMessage
```csharp
public class ConversationMessage
{
    public string Role { get; set; }              // "user", "assistant", "system"
    public string? Content { get; set; }          // Message content
    public DateTime Timestamp { get; set; }       // When message was created
    public bool IsCompressed { get; set; }        // Part of compressed history
    public double? ImportanceScore { get; set; }  // 0.0-1.0 importance rating
    public List<string> Topics { get; set; }      // Extracted topics/keywords
}
```

### DeepSeek API Models
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

public class DeepSeekResponse
{
    [JsonPropertyName("choices")]
    public DeepSeekChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

public class DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
```

### Telegram Models
```csharp
public class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public int UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
```

## ⚡ Performance Characteristics

### Context Compression Impact:
| Messages | Without Compression | With Compression | Token Reduction |
|----------|-------------------|------------------|-----------------|
| 20 | 1,000 tokens | 1,000 tokens | 0% |
| 50 | 2,500 tokens | 800 tokens | 68% |
| 100 | 5,000 tokens | 800 tokens | 84% |
| 500 | 25,000 tokens | 800 tokens | 97% |

### Response Times:
- **Local processing:** <100ms
- **DeepSeek API call:** 500-2000ms
- **Telegram message send:** 100-500ms
- **Total end-to-end:** 1-3 seconds

### Cost Analysis (DeepSeek):
- **Input tokens:** $0.27 per 1M tokens
- **Output tokens:** $1.10 per 1M tokens
- **Average message:** ~50 tokens input, ~100 tokens output
- **Cost per message:** ~$0.000125 (without compression)
- **With compression:** ~90% cost reduction after 50+ messages

## 🔧 Configuration

### Required Environment Variables:
```bash
DEEPSEEK_API_KEY=sk-xxxxxxxxxxxxxxxx
TELEGRAM_BOT_TOKEN=bot123456789:XXXXXXXXXXXXXXX
AzureWebJobsStorage=DefaultEndpointsProtocol=https;...
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
```

### Optional Settings:
```bash
ENVIRONMENT=Development|Production
LOG_LEVEL=Information|Debug|Warning|Error
MAX_CONTEXT_MESSAGES=20
COMPRESSION_THRESHOLD=15
```

## 🛡️ Error Handling

### Common Error Responses:

#### DeepSeek API Errors:
```json
{
  "error": "API quota exceeded",
  "status": 429,
  "response": "Превышена квота API. Попробуйте позже."
}
```

#### Telegram API Errors:
```json
{
  "error": "Chat not found", 
  "status": 400,
  "response": "Не удалось отправить сообщение."
}
```

#### Internal Errors:
```json
{
  "error": "Context compression failed",
  "status": 500,
  "response": "Произошла техническая ошибка."
}
```

---

**API Version:** 1.0  
**Last Updated:** August 9, 2025  
**Base URL:** `https://adaptive-ai-bot-yk.azurewebsites.net/api/`