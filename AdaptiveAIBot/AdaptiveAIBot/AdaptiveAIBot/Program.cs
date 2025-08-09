using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdaptiveAIBot.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Регистрируем HTTP Client
builder.Services.AddHttpClient();

// Регистрируем наши сервисы
builder.Services.AddScoped<IDeepSeekService, DeepSeekService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// Application Insights (пока закомментировано для экономии)
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();