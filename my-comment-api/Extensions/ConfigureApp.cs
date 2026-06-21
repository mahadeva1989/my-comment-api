using Microsoft.EntityFrameworkCore;
using my_comment_api.Data;
using my_comment_api.Hubs;
using my_comment_api.Middleware;

namespace my_comment_api.Extensions;

public static class ConfigureApp
{
    public static WebApplication Configure(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.UseMiddleware<ExceptionMiddleware>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Comments API v1");
            options.RoutePrefix = "swagger";
        });
        app.UseCors();
        app.UseRateLimiter();
        app.UseRouting();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<CommentHub>("/hubs/comments");

        // Map — branches pipeline for /health only
        app.Map("/health", health =>
        {
            // Use — runs before and after, logs the health check request
            health.Use(async (context, next) =>
            {
                Console.WriteLine($"[HealthCheck] Request from {context.Connection.RemoteIpAddress}");
                await next();
                Console.WriteLine($"[HealthCheck] Responded {context.Response.StatusCode}");
            });

            // Run — terminal, returns health status
            health.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"status":"healthy","app":"comments-api"}""");
            });
        });

        return app;
    }
}