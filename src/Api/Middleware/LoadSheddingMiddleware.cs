using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Middleware;

public static class LoadSheddingMiddleware
{
    public static IServiceCollection AddLoadShedding(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("LoadShedding:PermitLimit", 200);
        var queueLimit = configuration.GetValue("LoadShedding:QueueLimit", 50);

        services.AddRateLimiter(options =>
        {
            options.AddConcurrencyLimiter("load-shed", o =>
            {
                o.PermitLimit = permitLimit;
                o.QueueLimit = queueLimit;
                o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            });

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 503;
                context.HttpContext.Response.Headers["Retry-After"] = "5";
                await context.HttpContext.Response.WriteAsync(
                    "Service temporarily unavailable. Please retry shortly.", ct);
            };
        });

        return services;
    }
}
