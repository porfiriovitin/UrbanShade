using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;

namespace UrbanShade.API.Authentication;

internal static class RateLimitingConfiguration
{
    public static void AddRequestRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options => ConfigureRateLimiting(options, configuration));
    }

    private static void ConfigureRateLimiting(RateLimiterOptions options, IConfiguration configuration)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        /// :: Limit login attempts by address.
        options.AddPolicy("Authentication", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => CreateWindow(configuration.GetValue<int?>("SecurityTraffic:AuthenticationRequestsPerMinute") ?? 20)));

        /// :: Limit other requests by user or address.
        AddUserPolicy(options, "PublicReads", 60);
        AddUserPolicy(options, "InstitutionalDashboard", 60);
        AddUserPolicy(options, "Routing", 10);
        AddUserPolicy(options, "RegionThermalAdministration", 10);
        AddUserPolicy(options, "GeoAdministration", 10);
        AddUserPolicy(options, "WeatherAdministration", 10);

        options.OnRejected = WriteRateLimitResponse;
    }

    private static void AddUserPolicy(RateLimiterOptions options, string name, int permitLimit)
    {
        options.AddPolicy(name, context =>
        {
            var key = context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => CreateWindow(permitLimit));
        });
    }

    private static FixedWindowRateLimiterOptions CreateWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };

    private static async ValueTask WriteRateLimitResponse(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var message = context.HttpContext.Request.Path.StartsWithSegments("/api/routes") ? "Too many routing requests. Try again shortly." : "Too many administrative requests. Try again shortly.";

        await context.HttpContext.Response.WriteAsJsonAsync(new PayloadResponse
        {
            Status = nameof(ResponseStatus.Error),
            Messages = [message]
        }, cancellationToken);
    }
}
