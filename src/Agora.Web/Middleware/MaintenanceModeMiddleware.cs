using Agora.Web.Services;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace Agora.Web.Middleware;

/// <summary>Stops business mutations before MVC actions or services execute while retaining sign-in/out access.</summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next, ILogger<MaintenanceModeMiddleware> logger)
{
    private static readonly PathString[] AllowedWrites = ["/Account/Login", "/Account/Logout"];

    public async Task InvokeAsync(HttpContext context, IMaintenanceStateService maintenance)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method) ||
            IsSignalRTransportEndpoint(context.Request.Path) || AllowedWrites.Any(path => context.Request.Path.Equals(path)))
        {
            await next(context);
            return;
        }

        var state = await maintenance.GetAsync(context.RequestAborted);
        if (!state.Enabled)
        {
            await next(context);
            return;
        }

        logger.LogInformation("Blocked {Method} {Path} because Argus maintenance mode is active", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        if (context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true) ||
            context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true ||
            context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            await context.Response.WriteAsJsonAsync(new { error = "maintenance", message = state.DisplayMessage },
                new JsonSerializerOptions { Encoder = JavaScriptEncoder.Default }, context.RequestAborted);
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync($"<!doctype html><html><body><h1>Temporarily unavailable</h1><p>{System.Net.WebUtility.HtmlEncode(state.DisplayMessage)}</p></body></html>", context.RequestAborted);
    }

    private static bool IsSignalRTransportEndpoint(PathString path) =>
        path.Equals("/hubs/notifications") || path.Equals("/hubs/notifications/negotiate") ||
        path.Equals("/hubs/maintenance") || path.Equals("/hubs/maintenance/negotiate");
}
