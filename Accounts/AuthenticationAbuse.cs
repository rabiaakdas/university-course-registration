using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

// Holds no scoped service and is never attached to a DbContext.
public sealed class DummyPasswordVerification
{
    private readonly ApplicationUser user = new();
    private readonly string hash;
    public DummyPasswordVerification(IPasswordHasher<ApplicationUser> hasher)
    {
        hash = hasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    }
    public void Verify(IPasswordHasher<ApplicationUser> hasher, string password)
        => _ = hasher.VerifyHashedPassword(user, hash, password);
}

public static class AuthenticationAbuse
{
    public const string LoginPolicy = "AccountLoginIp", ForgotPolicy = "ForgotPasswordIp";
    public static readonly EventId RateRejected = new(3301, "AuthenticationRateLimited");
    public static readonly EventId LoginRejected = new(3302, "AuthenticationDenied");

    public static string PartitionKey(IPAddress? address)
        => address is null ? "unknown-peer" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

    public static void AddAuthenticationAbuse(this IServiceCollection services, IConfiguration configuration)
    {
        int Read(string key, int max)
        {
            if (!int.TryParse(configuration[key], NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value > max)
                throw new InvalidOperationException($"{key} zorunludur; 1..{max} aralığında tamsayı olmalıdır.");
            return value;
        }
        var loginPermits = Read("AuthenticationLimits:Login:PermitLimit", 10000);
        var loginSeconds = Read("AuthenticationLimits:Login:WindowSeconds", 86400);
        var forgotPermits = Read("AuthenticationLimits:ForgotPassword:PermitLimit", 10000);
        var forgotSeconds = Read("AuthenticationLimits:ForgotPassword:WindowSeconds", 86400);
        services.AddSingleton(sp =>
        {
            using var scope = sp.CreateScope();
            return new DummyPasswordVerification(scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>());
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginPolicy, context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromSeconds(loginSeconds), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy(ForgotPolicy, context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = forgotPermits, Window = TimeSpan.FromSeconds(forgotSeconds), QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = async (rejected, ct) =>
            {
                var context = rejected.HttpContext;
                context.Response.Headers.CacheControl = "no-store";
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay))
                    context.Response.Headers.RetryAfter = Math.Max(0, Math.Ceiling(delay.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AuthenticationAbuse")
                    .LogDebug(RateRejected, "Authentication rate limit; trace {TraceId}", context.TraceIdentifier);
                await Results.Problem(statusCode: 429, title: "Çok fazla istek. Daha sonra yeniden deneyin.",
                    extensions: new Dictionary<string, object?> { ["code"] = "authentication_rate_limited" }).ExecuteAsync(context);
            };
        });
    }

    // Invoke after routing and authentication. Prepare the dummy hash once before requests arrive.
    public static void UseAuthenticationAbuse(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<DummyPasswordVerification>();
        app.UseRateLimiter();
    }
}
