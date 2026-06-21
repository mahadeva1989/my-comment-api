using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace my_comment_api.Filters;

// Side-by-side comparison only — not wired up, not used anywhere.
// Same behavior as AuditFilter.cs, written with plain ILogger calls
// instead of the [LoggerMessage] source generator.
public class AuditFilterPlain(ILogger<AuditFilterPlain> logger) : IActionFilter
{
    private readonly Stopwatch _stopwatch = new();

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User.Identity?.Name ?? "Anonymous";
        var action = context.ActionDescriptor.DisplayName ?? string.Empty;

        logger.LogInformation("User [{User}] is calling [{Action}]", user, action);
        _stopwatch.Restart();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        _stopwatch.Stop();

        var user = context.HttpContext.User.Identity?.Name ?? "Anonymous";
        var action = context.ActionDescriptor.DisplayName ?? string.Empty;
        var statusCode = context.HttpContext.Response.StatusCode;

        logger.LogInformation(
            "User [{User}] called [{Action}] — responded {StatusCode} in {Elapsed}ms",
            user, action, statusCode, _stopwatch.ElapsedMilliseconds);
    }
}
