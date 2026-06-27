using Application;
using Application.Interfaces;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Infrastructure.BlobStorage;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Queue;
using Ingestion;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

var credentialProvider = new TokenCredentialProvider();

var kvUri = builder.Configuration[$"KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(new Uri(kvUri), credentialProvider.Credential);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not connect to Key Vault at {kvUri}. {ex.Message}");
    }
}

builder.Configuration.AddEnvironmentVariables();

var otel = builder.Services.AddOpenTelemetry();
otel.WithTracing(t =>
{
    if (builder.Environment.IsDevelopment())
    {
        t.AddConsoleExporter();
    }
});
otel.WithMetrics(m =>
{
    m.AddMeter($"MemoryOfX");
    if (builder.Environment.IsDevelopment())
    {
        m.AddConsoleExporter();
    }
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(o =>
            o.ConnectionString = builder.Configuration[$"ApplicationInsights:ConnectionString"]);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString($"Default")));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddIngestion();
builder.Services.AddSingleton<Worker.Messaging.IMessageParser, Worker.Messaging.MessageParser>();
builder.Services.AddSingleton<Worker.Messaging.IMessageDispatcher, Worker.Messaging.MessageDispatcher>();
builder.Services.AddScoped<Worker.Messaging.IMessageHandler, Worker.Handlers.ScrapeTweetHandler>();
builder.Services.AddHostedService<ScrapeWorker>();

var host = builder.Build();
host.Run();
