using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace my_comment_api.Filters;

public class ETagFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        // only apply to GET requests that returned 200
        if (context.HttpContext.Request.Method != HttpMethods.Get
            || executed.Result is not OkObjectResult ok)
            return;

        // generate ETag from response body hash
        var json = JsonSerializer.Serialize(ok.Value);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(json));
        var etag = $"\"{Convert.ToBase64String(hash)}\"";

        var requestEtag = context.HttpContext.Request.Headers.IfNoneMatch.ToString();

        if (requestEtag == etag)
        {
            // client already has latest data — skip body
            executed.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            return;
        }

        // attach ETag to response so client can use it next time
        context.HttpContext.Response.Headers.ETag = etag;
    }
}
