using Application;
using Application.Interfaces;
using Application.Models;
using Application.Services;
using Infrastructure.BlobStorage;
using Infrastructure.Data;
using Infrastructure.Queue;
using Infrastructure.XApi;
using Microsoft.EntityFrameworkCore;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAsyncContext<CorrelationContext>, AsyncContext<CorrelationContext>>();
        services.AddSingleton<IAsyncContext<IdentityContext>, AsyncContext<IdentityContext>>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString($"Default")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<FolderSettings>(configuration.GetSection($"Folders"));
        services.Configure<JwtSettings>(configuration.GetSection($"Jwt"));

        services.AddScoped<ITweetSubmissionService, TweetSubmissionService>();
        services.AddScoped<ITweetQueryService, TweetQueryService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IXUserProfileService, XUserProfileService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddHttpClient<IXApiClient, XApiClient>();

        services.AddSingleton<IScrapeQueueService, ScrapeQueueService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        return services;
    }
}
