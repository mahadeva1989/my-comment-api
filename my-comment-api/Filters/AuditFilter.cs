using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace my_comment_api.Filters;

public partial class AuditFilter(ILogger<AuditFilter> logger) : IActionFilter
{
    private readonly Stopwatch _stopwatch = new();

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User.Identity?.Name ?? "Anonymous";
        var action = context.ActionDescriptor.DisplayName ?? string.Empty;

        LogActionExecuting(logger, user, action);
        _stopwatch.Restart();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        _stopwatch.Stop();

        var user = context.HttpContext.User.Identity?.Name ?? "Anonymous";
        var action = context.ActionDescriptor.DisplayName ?? string.Empty;
        var statusCode = context.HttpContext.Response.StatusCode;

        LogActionExecuted(logger, user, action, statusCode, _stopwatch.ElapsedMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User [{User}] is calling [{Action}]")]
    private static partial void LogActionExecuting(ILogger logger, string user, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "User [{User}] called [{Action}] — responded {StatusCode} in {Elapsed}ms")]
    private static partial void LogActionExecuted(ILogger logger, string user, string action, int statusCode, long elapsed);
}
