using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Responses;
using UrbanShade.Infraestructure.Security;
using UrbanShade.Domain.Enums;

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
                ValidateIssuer = false,
                ValidateAudience = false,
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
        services.AddAuthorizationBuilder().AddPolicy("PlatformAdministration", policy => policy.RequireRole(nameof(UserRoles.PlatformAdministrator)));
    }
}
