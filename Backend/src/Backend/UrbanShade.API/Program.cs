using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UrbanShade.API.Filters;
using UrbanShade.API.Authentication;
using UrbanShade.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Enums;
using UrbanShade.Infraestructure;
using UrbanShade.Infraestructure.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<RequestAdmission>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("SecurityTraffic:AuthenticationRequestsPerMinute") ?? 20,
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("PublicReads", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("InstitutionalDashboard", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("Routing", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("RegionThermalAdministration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsJsonAsync(new PayloadResponse
        {
            Status = nameof(ResponseStatus.Error),
            Messages = [context.HttpContext.Request.Path.StartsWithSegments("/api/routes") ? "Too many routing requests. Try again shortly." : "Too many administrative requests. Try again shortly."]
        }, cancellationToken);
    options.AddPolicy("GeoAdministration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("WeatherAdministration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSwaggerGen(options =>
{
    /// :: Define the JWT Bearer authentication scheme for Swagger.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the access token."
    });

    /// :: Include the bearer token in requests sent through Swagger.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddMvc(options => { options.Filters.Add<ExceptionFilter>(); options.Filters.Add<AuthenticationThrottleFilter>(); });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddBearerAuthentication(builder.Configuration);

builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(new PayloadResponse
    {
        Status = nameof(ResponseStatus.Error),
        Messages = [.. context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)]
    }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Production uses a separate deployment identity for DDL and seed operations.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Tests"))
    await ExecuteMigrations();
else
    await UrbanShade.Infraestructure.DataAcess.RuntimeDatabaseSecurity.Validate(app.Services);

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new PayloadResponse
    {
        Status = nameof(ResponseStatus.Error), Messages = ["An unexpected error occurred."]
    }, context.RequestAborted);
}));

app.UseHttpsRedirection();

// Bound work before authentication, including the live account/session DB lookups.
app.Use(async (context, next) =>
{
    var admission = context.RequestServices.GetRequiredService<RequestAdmission>();
    using var address = admission.Address(context.Connection.RemoteIpAddress?.ToString() ?? "anonymous");
    using var concurrency = admission.Enter();
    if (!address.IsAcquired || !concurrency.IsAcquired)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "60";
        await context.Response.WriteAsJsonAsync(RequestAdmission.Rejection(), context.RequestAborted);
        return;
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

async Task ExecuteMigrations()
{

    await using var scope = app.Services.CreateAsyncScope();

    await DatabaseMigration.ExecuteMigrations(scope.ServiceProvider);

}

public partial class Program { }
