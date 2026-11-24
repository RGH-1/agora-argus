using System.Diagnostics;
using Argus.Contracts;
using Argus.Contracts.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Argus.Client;

/// <summary>Times every request, counts errors, and reports unhandled exceptions to Argus.</summary>
public sealed class ArgusMetricsMiddleware(RequestDelegate next, RequestMetrics metrics, ArgusSender sender)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await next(context);
            failed = context.Response.StatusCode >= 500;
        }
        catch (Exception ex)
        {
            failed = true;
            await sender.SendAsync(ArgusRoutingKeys.ErrorReported, new ErrorReported
            {
                Message = ex.Message,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                Path = context.Request.Path,
            });
            throw; // the app's own error handling still decides what the user sees
        }
        finally
        {
            metrics.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, failed);
        }
    }
}

public static class ArgusApplicationBuilderExtensions
{
    /// <summary>
    /// Call it after UseExceptionHandler, so exceptions pass through it before the error page is made:
    /// <c>app.UseExceptionHandler(...); app.UseArgusMetrics();</c>
    /// </summary>
    public static IApplicationBuilder UseArgusMetrics(this IApplicationBuilder app) =>
        app.UseMiddleware<ArgusMetricsMiddleware>();
}