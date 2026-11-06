using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using UniversityRegistration.Accounts;
using UniversityRegistration.Models;
namespace UniversityRegistration.Endpoints;
public static class ExternalLoginEndpoints
{
    public static void MapExternalLoginEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<UniversityOidcSettings>();
        app.MapGet("/accounts/external/options", (HttpContext c) =>
        { c.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { enabled = settings.Enabled, displayName = UniversityOidc.DisplayName }); });
        if (!settings.Enabled) return;
        app.MapPost("/accounts/external/login", async (HttpContext c, IAntiforgery csrf, SignInManager<ApplicationUser> signIn) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            try { await csrf.ValidateRequestAsync(c); }
            catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı."); }
            if ((await c.AuthenticateAsync(IdentityConstants.ApplicationScheme)).Succeeded) return Results.Problem(statusCode: 409, title: "Önce mevcut oturumdan çıkış yapın.");
            if (!c.Request.HasFormContentType) return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı.");
            var form = await c.Request.ReadFormAsync(c.RequestAborted); var path = form["returnUrl"].ToString();
            if (path is not ("/" or "/index.html")) return Results.Problem(statusCode: 400, title: "Dönüş adresi geçersiz.");
            await c.SignOutAsync(IdentityConstants.ExternalScheme);
            var properties = signIn.ConfigureExternalAuthenticationProperties(UniversityOidc.Scheme, "/accounts/external/callback"); properties.Items["local-return"] = path;
            try { await c.ChallengeAsync(UniversityOidc.Scheme, properties); return Results.Empty; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            { await c.SignOutAsync(IdentityConstants.ExternalScheme); return Results.Redirect("/?external=failed"); }
        }).RequireRateLimiting(AuthenticationAbuse.LoginPolicy);
        app.MapGet("/accounts/external/callback", async (HttpContext c, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            try
            {
                if ((await c.AuthenticateAsync(IdentityConstants.ApplicationScheme)).Succeeded) return Results.Redirect("/?external=failed");
                var info = await signIn.GetExternalLoginInfoAsync();
                if (info is null || info.LoginProvider != UniversityOidc.Scheme || info.Principal.FindFirst(UniversityOidc.IssuerClaim)?.Value != settings.Authority || string.IsNullOrWhiteSpace(info.ProviderKey)) return Results.Redirect("/?external=failed");
                var user = await users.FindByLoginAsync(settings.LoginProvider, info.ProviderKey);
                if (user is null || await users.GetTwoFactorEnabledAsync(user)) return Results.Redirect("/?external=failed");
                var result = await signIn.ExternalLoginSignInAsync(settings.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
                if (!result.Succeeded) return Results.Redirect("/?external=failed");
                string? path = null; info.AuthenticationProperties?.Items.TryGetValue("local-return", out path); return Results.LocalRedirect(path is "/" or "/index.html" ? path : "/");
            }
            finally { await c.SignOutAsync(IdentityConstants.ExternalScheme); }
        });
    }
}
