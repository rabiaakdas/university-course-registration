using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace UniversityRegistration.Accounts;

public sealed record UniversityJwtSettings(bool Enabled, bool DevTokenEnabled, string Issuer, string Audience);

public static class UniversityJwtSetup
{
    public const string Scheme = "UniversityBearer";
    public const string Policy = "UniversityBearerOnly";
    public const string CookiePolicy = "UniversityCookieOnly";
    public static void AddUniversityJwt(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var enabled = config.GetValue<bool>("Jwt:Enabled");
        var dev = config.GetValue<bool>("Jwt:DevTokenEnabled");
        if (dev && (!environment.IsDevelopment() || !enabled))
            throw new InvalidOperationException("Jwt:DevTokenEnabled yalnız Development ve Jwt:Enabled ile kullanılabilir.");
        var issuer = config["Jwt:Issuer"] ?? ""; var audience = config["Jwt:Audience"] ?? "";
        services.AddSingleton(new UniversityJwtSettings(enabled, dev, issuer, audience));
        services.AddAuthorization(options => options.AddPolicy(CookiePolicy, policy =>
            policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser()));
        if (!enabled) return;
        if (dev)
        {
            if (!int.TryParse(config["Jwt:AccessTokenSeconds"], out var access) || access is < 1 or > 3600
                || !int.TryParse(config["Jwt:RefreshSessionDays"], out var days) || days is < 1 or > 30)
                throw new InvalidOperationException("Jwt:AccessTokenSeconds 1..3600 ve Jwt:RefreshSessionDays 1..30 aralığında yapılandırılmalıdır.");
            services.AddSingleton(new TokenSessionPolicy(access, days));
            services.AddScoped<TokenSessions>();
        }
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("JWT için mutlak URI Issuer ve boş olmayan Audience gereklidir.");
        var publicPath = config["Jwt:PublicKeyPath"]; var privatePath = dev ? config["Jwt:PrivateKeyPath"] : null;
        if (string.IsNullOrWhiteSpace(publicPath) || !Path.IsPathFullyQualified(publicPath)
            || dev && (string.IsNullOrWhiteSpace(privatePath) || !Path.IsPathFullyQualified(privatePath)))
            throw new InvalidOperationException("JWT anahtar dosyalarının mutlak yolları yapılandırılmalıdır.");
        if (dev && Path.GetFullPath(privatePath!).StartsWith(Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Geliştirme özel anahtarı uygulama kaynak dizini dışında olmalıdır.");
        services.AddSingleton(_ => UniversityJwtKeys.Load(publicPath!, privatePath));
        // No defaults changed: application cookie remains the default authentication scheme.
        services.AddAuthentication().AddJwtBearer(Scheme, _ => { });
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<UniversityJwtKeys>((options, keys) =>
        {
            options.MapInboundClaims = false; options.IncludeErrorDetails = false; options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = keys.VerificationKey, RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = true, RequireAudience = true, ValidAudience = audience,
                IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub", IncludeTokenOnFailedValidation = false, LogTokenId = false
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // After cryptographic validation, reject arrays/duplicate JSON properties too.
                    var subjects = context.Principal!.FindAll("sub").ToArray();
                    using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(((JsonWebToken)context.SecurityToken).EncodedPayload));
                    var members = payload.RootElement.EnumerateObject().Where(p => p.Name == "sub").ToArray();
                    if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value)
                        || members.Length != 1 || members[0].Value.ValueKind != JsonValueKind.String)
                        context.Fail("Invalid subject.");
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()));
    }
}
