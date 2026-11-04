using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityRegistration.Accounts;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public sealed record BearerAccountResponse(string AccountId);
public sealed record DevTokenResponse(string AccessToken, string TokenType, int ExpiresIn);

public static class UniversityJwtEndpoints
{
    public static void MapUniversityJwtEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<UniversityJwtSettings>();
        if (!settings.Enabled) return;
        _ = app.Services.GetRequiredService<UniversityJwtKeys>(); // Fail at startup, not at the first request.
        app.MapGet("/accounts/bearer-me", (ClaimsPrincipal principal) =>
            Results.Ok(new BearerAccountResponse(principal.FindFirst("sub")!.Value)))
            .RequireAuthorization(UniversityJwtSetup.Policy);
        if (!settings.DevTokenEnabled) return;
        app.MapPost("/accounts/dev-token", async (HttpContext context, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, UniversityJwtKeys keys) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı."); }
            var id = users.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(id)) return Results.Unauthorized();
            var user = await users.FindByIdAsync(id);
            if (user is null || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user)) return Results.Unauthorized();
            var now = DateTime.UtcNow;
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = settings.Issuer, Audience = settings.Audience, IssuedAt = now, NotBefore = now, Expires = now.AddMinutes(10),
                Claims = new Dictionary<string, object> { ["sub"] = user.Id, ["jti"] = Guid.NewGuid().ToString("N") },
                SigningCredentials = new SigningCredentials(keys.SigningKey!, SecurityAlgorithms.RsaSha256)
            };
            var token = new JsonWebTokenHandler().CreateToken(descriptor);
            return Results.Ok(new DevTokenResponse(token, "Bearer", 600));
        }).RequireAuthorization(UniversityJwtSetup.CookiePolicy);
    }
}
