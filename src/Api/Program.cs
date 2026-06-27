using System.Text;
using Api.Extensions;
using Api.Middleware;
using Application.Interfaces;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc($"v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = $"Memory of X API",
        Version = $"v1",
    });

    options.AddSecurityDefinition($"Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = $"Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = $"bearer",
        BearerFormat = $"JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = $"Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddLoadShedding(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection[$"Issuer"],
#pragma warning disable CA5404 // Internal-only API with no audience claim in JWT spec
            ValidateAudience = false,
#pragma warning restore CA5404
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection[$"Secret"]!)),
            RoleClaimType = $"role",
            NameClaimType = $"sub",
        };
    });
builder.Services.AddAuthorization();

var otel = builder.Services.AddOpenTelemetry();
otel.WithTracing(t =>
{
    t.AddAspNetCoreInstrumentation();
    t.AddHttpClientInstrumentation();
    t.AddSqlClientInstrumentation();
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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy($"LocalDev", policy =>
            policy.WithOrigins($"https://localhost:5000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<HttpLoggingMiddleware>();
app.UseRateLimiter();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors($"LocalDev");
}

app.UseAuthentication();
app.UseMiddleware<ThrottlingMiddleware>();
app.UseAuthorization();
app.UseMiddleware<IdentityMiddleware>();
app.UseMiddleware<UnitOfWorkMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
