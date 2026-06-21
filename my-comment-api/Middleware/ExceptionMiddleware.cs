using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace my_comment_api.Middleware;

public class ExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
            
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhadles exception occured. Path:{Path}", context.Request.Path);
            await HandledExceptionsAsync(context, ex);

        }

    }

    private static Task HandledExceptionsAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            InvalidOperationException => (HttpStatusCode.BadRequest, ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occured"),
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = JsonSerializer.Serialize(new
        {
            statusCode = context.Response.StatusCode,
            message

        });

        return context.Response.WriteAsync(response);

    }

}

