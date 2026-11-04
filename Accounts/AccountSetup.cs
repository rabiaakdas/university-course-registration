using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public sealed record LocalConfirmationSettings(bool Enabled, Uri? PageUrl, string Directory);

public static class AccountSetup
{
    public static void AddAccountIdentity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool>("Accounts:LocalMailboxEnabled");
        Uri? page = null;
        if (enabled)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Yerel mesaj kutusu yalnız Development ortamında etkinleştirilebilir.");
            if (!Uri.TryCreate(configuration["Accounts:ConfirmationPageUrl"], UriKind.Absolute, out page)
                || !(page.Scheme == "https" || page.Scheme == "http" && page.IsLoopback)
                || !string.IsNullOrEmpty(page.UserInfo) || !string.IsNullOrEmpty(page.Query) || !string.IsNullOrEmpty(page.Fragment))
                throw new InvalidOperationException("Accounts:ConfirmationPageUrl HTTPS veya yerel HTTP adresi olmalıdır; query/fragment içeremez.");
        }
        var localRoot = Path.Combine(environment.ContentRootPath, ".local", "accounts");
        services.AddSingleton(new LocalConfirmationSettings(enabled, page, Path.Combine(localRoot, "messages")));
        services.AddSingleton<IAccountConfirmationSender, LocalAccountConfirmationSender>();
        var protection = services.AddDataProtection().SetApplicationName("UniversityRegistration.Accounts.v1")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(localRoot, "keys")));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            // Email is validated by Identity's email validator, not an ASCII username whitelist.
            options.User.AllowedUserNameCharacters = "";
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<UniversityDbContext>().AddSignInManager()
          .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(TokenOptions.DefaultProvider);
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-University.Account";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            options.SlidingExpiration = false;
            // This API has no login/access-denied HTML pages.
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-University.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));
    }
}
