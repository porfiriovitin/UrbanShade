using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UrbanShade.Communication.Enums;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Communication.Responses;

namespace UrbanShade.API.Authentication;

public sealed class RequestAdmission : IDisposable
{
    private readonly ConcurrencyLimiter _concurrency;
    private readonly PartitionedRateLimiter<string> _addresses;
    private readonly PartitionedRateLimiter<string> _accounts;

    public RequestAdmission(IConfiguration configuration)
    {
        int Limit(string name, int fallback)
        {
            var value = configuration.GetValue<int?>("SecurityTraffic:" + name) ?? fallback;
            return value is >= 1 and <= 100000 ? value : throw new InvalidOperationException("Invalid security traffic limit.");
        }
        _concurrency = new(new ConcurrencyLimiterOptions { PermitLimit = Limit("ConcurrentRequests", 8), QueueLimit = 0 });
        var addressLimit = Limit("RequestsPerMinute", 300);
        var accountLimit = Limit("AttemptsPerAccountPerMinute", 10);
        _addresses = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = addressLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        _accounts = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = accountLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    }

    public RateLimitLease Enter() => _concurrency.AttemptAcquire();
    public RateLimitLease Address(string address) => _addresses.AttemptAcquire(address);
    public RateLimitLease Account(string email) => _accounts.AttemptAcquire(
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()))));

    public static PayloadResponse Rejection() => new() { Status = nameof(ResponseStatus.Error), Messages = ["Too many requests. Try again shortly."] };
    public void Dispose() { _accounts.Dispose(); _addresses.Dispose(); _concurrency.Dispose(); }
}

public sealed class AuthenticationThrottleFilter(RequestAdmission admission) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var email = context.ActionArguments.Values.Select(value => value switch
        {
            LoginRequest login => login.Email,
            RegisterRequest register => register.Email,
            _ => null
        }).FirstOrDefault(value => value is not null);
        if (email is not null && email.Length <= 254)
        {
            using var lease = admission.Account(email);
            if (!lease.IsAcquired)
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.Result = new ObjectResult(RequestAdmission.Rejection()) { StatusCode = (int)HttpStatusCode.TooManyRequests };
                return;
            }
        }
        await next();
    }
}
