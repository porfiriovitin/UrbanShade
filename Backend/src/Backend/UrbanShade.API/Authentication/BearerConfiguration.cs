using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.API.Authentication;

internal static class BearerConfiguration
{
    public static void AddBearerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        /// :: Get the JWT settings from the configuration file.
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new();

        /// :: Add the JWT Bearer authentication scheme to the service collection and configure it with the settings;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;

            /// :: Set parameters for validating the JWT tokens.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                ValidateLifetime = true, 
                RequireExpirationTime = true, 
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero, 
                NameClaimType = "sub", 
                RoleClaimType = "role"
            };

            /// :: Set up challenges and forbidden responses handler.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ||
                        !int.TryParse(principal.FindFirst("security_version")?.Value, out var version))
                    {
                        context.Fail("Invalid token.");
                        return;
                    }

                    /// :: Check live account state so password changes invalidate existing tokens.
                    var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                    var user = await users.FindById(id, context.HttpContext.RequestAborted);
                    var firstAccess = principal.FindFirst("purpose")?.Value == "first_access";
                    if (user is null || !user.Active || user.SecurityVersion != version ||
                        (firstAccess ? !user.MustChangePassword || !user.TemporaryPasswordExpiresAt.HasValue || user.TemporaryPasswordExpiresAt <= DateTime.UtcNow
                            : user.MustChangePassword || principal.FindFirst("purpose")?.Value != "access" || principal.FindFirst("role")?.Value != user.Role.ToString()))
                    {
                        context.Fail("Account or session is unavailable.");
                        return;
                    }
                    if (!firstAccess)
                    {
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<IRefreshTokenRepository>();
                        if (!Guid.TryParse(principal.FindFirst("sid")?.Value, out var sessionId) ||
                            !await sessions.IsSessionActive(sessionId, id, version, DateTime.UtcNow, context.HttpContext.RequestAborted))
                            context.Fail("Account or session is unavailable.");
                    }
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Bearer";

                    /// :: Custom response for unauthorized access.
                    await context.Response.WriteAsJsonAsync(new PayloadResponse
                    {
                        Status = nameof(ResponseStatus.Unauthorized), 
                        Messages = ["A valid bearer token is required."]
                    });
                },
                OnForbidden = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;

                    /// :: Custom response for forbidden access.
                    return context.Response.WriteAsJsonAsync(new PayloadResponse
                    {
                        Status = nameof(ResponseStatus.Error), 
                        Messages = ["Access denied."]
                    });
                }
            };
        });
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim("purpose", "access").Build())
            .AddPolicy("PlatformAdministration", policy => policy.RequireAuthenticatedUser().RequireClaim("purpose", "access").RequireRole(nameof(UserRoles.PlatformAdministrator)))
            .AddPolicy("InstitutionalDashboard", policy => policy.RequireAuthenticatedUser().RequireClaim("purpose", "access").RequireRole(nameof(UserRoles.PlatformAdministrator), nameof(UserRoles.Manager)))
            .AddPolicy("FirstAccess", policy => policy.RequireAuthenticatedUser().RequireClaim("purpose", "first_access"));
    }
}
