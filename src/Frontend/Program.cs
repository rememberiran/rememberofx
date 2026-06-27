using Azure.Monitor.OpenTelemetry.AspNetCore;
using Frontend.Components;
using Frontend.Services;
using Infrastructure.Identity;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration[$"BackendApi:BaseUrl"] ?? $"https://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<LoggingDelegatingHandler>();

builder.Services.AddTransient<LoggingDelegatingHandler>();

var otel = builder.Services.AddOpenTelemetry();
otel.WithTracing(t =>
{
    t.AddAspNetCoreInstrumentation();
    t.AddHttpClientInstrumentation();
    if (builder.Environment.IsDevelopment())
    {
        t.AddConsoleExporter();
    }
});
otel.WithMetrics(m =>
{
    m.AddAspNetCoreInstrumentation();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler($"/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
