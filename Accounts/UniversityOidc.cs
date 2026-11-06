using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UniversityRegistration.Models;
namespace UniversityRegistration.Accounts;

public sealed record UniversityOidcSettings(bool Enabled, string Authority)
{
    // Changing authority cannot reinterpret previously linked subjects.
    public string LoginProvider => UniversityOidc.Scheme + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Authority)));
}
public static class UniversityOidc
{
    public const string Scheme = "UniversityOidc", DisplayName = "Test kimlik sağlayıcısıyla giriş", IssuerClaim = "university:external-issuer";
    public static void AddUniversityOidc(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool>("Oidc:Enabled"); var authority = configuration["Oidc:Authority"] ?? "";
        if (enabled && (!environment.IsDevelopment() || !Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || authority.EndsWith('/')
            || string.IsNullOrWhiteSpace(configuration["Oidc:ClientId"]) || string.IsNullOrWhiteSpace(configuration["Oidc:ClientSecret"])))
            throw new InvalidOperationException("OIDC test girişi yalnız Development'ta HTTPS Authority (sonunda / olmadan), ClientId ve ClientSecret ile etkinleştirilir.");
        services.AddSingleton(new UniversityOidcSettings(enabled, authority)); if (!enabled) return;
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ExternalScheme, o =>
        {
            o.Cookie.Name = "__Host-University.External"; o.Cookie.Path = "/"; o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax; o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
        services.AddAuthentication().AddOpenIdConnect(Scheme, DisplayName, o =>
        {
            o.Authority = authority; o.ClientId = configuration["Oidc:ClientId"]; o.ClientSecret = configuration["Oidc:ClientSecret"];
            o.SignInScheme = IdentityConstants.ExternalScheme; o.CallbackPath = "/signin-oidc";
            o.ResponseType = OpenIdConnectResponseType.Code; o.ResponseMode = OpenIdConnectResponseMode.FormPost;
            o.UsePkce = true; o.RequireHttpsMetadata = true; o.SaveTokens = false; o.GetClaimsFromUserInfoEndpoint = false;
            o.MapInboundClaims = false; o.Scope.Clear(); o.Scope.Add("openid"); o.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
            o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always; o.CorrelationCookie.SameSite = SameSiteMode.None;
            o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always; o.NonceCookie.SameSite = SameSiteMode.None;
            o.TokenValidationParameters.ValidateIssuer = true;
            o.TokenValidationParameters.IssuerValidator = (issuer, _, _) => issuer == authority ? issuer : throw new SecurityTokenInvalidIssuerException("Unexpected issuer.");
            o.TokenValidationParameters.ValidateAudience = true; o.TokenValidationParameters.ValidAudience = o.ClientId;
            o.TokenValidationParameters.ValidateLifetime = true; o.TokenValidationParameters.RequireExpirationTime = true;
            o.TokenValidationParameters.RequireSignedTokens = true; o.TokenValidationParameters.ValidateIssuerSigningKey = true;
            o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256]; o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            o.Events.OnTokenValidated = c =>
            {
                var subjects = c.Principal!.FindAll("sub").ToArray();
                if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value)) { c.Fail("Invalid subject."); return Task.CompletedTask; }
                // Identity's key comes only from validated sub, never email or inbound mapping.
                c.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subjects[0].Value), new Claim(ClaimTypes.NameIdentifier, subjects[0].Value), new Claim(IssuerClaim, authority)], Scheme));
                return Task.CompletedTask;
            };
            o.Events.OnRemoteFailure = async c =>
            { c.Response.Headers.CacheControl = "no-store"; await c.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme); c.HandleResponse(); c.Response.Redirect("/?external=failed"); };
        });
    }
}
public static class ExternalLoginMaintenance
{
    public static async Task<int> RunAsync(IServiceProvider services, IHostEnvironment environment, string[] args)
    {
        var settings = services.GetRequiredService<UniversityOidcSettings>();
        if (!environment.IsDevelopment() || !settings.Enabled || args.Length != 3 || args[0] != "link" || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]) || args[2].Length > 128)
        { Console.Error.WriteLine("Use Development configured OIDC: external link <accountId> <verifiedSubject>."); return 1; }
        await using var scope = services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(args[1]); if (user is null) return 1;
        var linked = await users.FindByLoginAsync(settings.LoginProvider, args[2]);
        if (linked is not null) { Console.WriteLine(linked.Id == user.Id ? "External link already exists." : "External identity belongs to another account."); return linked.Id == user.Id ? 0 : 1; }
        var result = await users.AddLoginAsync(user, new UserLoginInfo(settings.LoginProvider, args[2], UniversityOidc.DisplayName));
        Console.WriteLine(result.Succeeded ? "External link saved." : "External link rejected."); return result.Succeeded ? 0 : 1;
    }
}
