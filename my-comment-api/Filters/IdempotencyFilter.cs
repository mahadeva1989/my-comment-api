using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace my_comment_api.Filters;

public class IdempotencyFilter(IMemoryCache cache) : IAsyncActionFilter
{
    private const string HeaderKey = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[HeaderKey].ToString();

        // only apply to non-idempotent methods
        var method = context.HttpContext.Request.Method;
        if (string.IsNullOrEmpty(key) || (method != HttpMethods.Post && method != HttpMethods.Patch))
        {
            await next();
            return;
        }

        // key already processed — return cached response
        if (cache.TryGetValue(key, out object? cachedResult))
        {
            context.Result = new OkObjectResult(cachedResult);
            return;
        }

        // execute the action
        var executed = await next();

        // cache the result for 24 hours
        if (executed.Result is OkObjectResult ok || executed.Result is CreatedAtActionResult)
        {
            var value = executed.Result is OkObjectResult okResult
                ? okResult.Value
                : ((CreatedAtActionResult)executed.Result!).Value;

            cache.Set(key, value, TimeSpan.FromHours(24));
        }
    }
}
