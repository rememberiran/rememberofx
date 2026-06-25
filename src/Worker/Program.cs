using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

var credential = new DefaultAzureCredential();

var kvUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(new Uri(kvUri), credential);
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
        t.AddConsoleExporter();
});
otel.WithMetrics(m =>
{
    m.AddMeter("MemoryOfX");
    if (builder.Environment.IsDevelopment())
        m.AddConsoleExporter();
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(o =>
            o.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]);
}

builder.Services.AddHostedService<ScrapeWorker>();

var host = builder.Build();
host.Run();
