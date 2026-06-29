using Application;

namespace Api.Middleware;

public class UnitOfWorkMiddleware
{
    private readonly RequestDelegate _next;

    public UnitOfWorkMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        await _next(context);

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(context.RequestAborted);
        }
    }
}
