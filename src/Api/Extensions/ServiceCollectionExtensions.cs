using Api.Mappers;
using Application;
using Application.Interfaces;
using Application.Models;
using Application.Services;
using Infrastructure.BlobStorage;
using Infrastructure.Data;
using Infrastructure.Identity;
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

        services.AddSingleton<QueryLoggingInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseSqlServer(configuration.GetConnectionString($"Default"))
                   .AddInterceptors(sp.GetRequiredService<QueryLoggingInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<FolderSettings>(configuration.GetSection($"Folders"));
        services.Configure<JwtSettings>(configuration.GetSection($"Jwt"));

        services.AddScoped<ITweetSubmissionService, TweetSubmissionService>();
        services.AddScoped<ITweetQueryService, TweetQueryService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IXUserProfileService, XUserProfileService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRemovalRequestService, RemovalRequestService>();
        services.AddScoped<IViolationReportService, ViolationReportService>();
        services.AddScoped<IPendingService, PendingService>();

        services.AddHttpClient<IXApiClient, XApiClient>();

        services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();
        services.AddSingleton<IQueueService, QueueService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        services.AddSingleton<TweetDtoMapper>();

        return services;
    }
}
