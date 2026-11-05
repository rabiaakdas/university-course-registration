using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using UniversityRegistration.Accounts;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public sealed record BearerAccountResponse(string AccountId);
public sealed record RefreshRequest(string? RefreshToken);

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
        app.MapGet("/accounts/sessions", async (HttpContext context, UserManager<ApplicationUser> users, TokenSessions sessions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var id = users.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(id)) return Results.Unauthorized();
            return Results.Ok(await sessions.ListAsync(id, ct));
        }).RequireAuthorization(UniversityJwtSetup.CookiePolicy);
        app.MapPost("/accounts/sessions/{sessionId:guid}/revoke", async (Guid sessionId, HttpContext context,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users, TokenSessions sessions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı."); }
            var id = users.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(id)) return Results.Unauthorized();
            return await sessions.RevokeAsync(sessionId, id, ct) ? Results.NoContent()
                : Results.Problem(statusCode: 404, title: "Oturum bulunamadı.");
        }).RequireAuthorization(UniversityJwtSetup.CookiePolicy);
        // This teaching issuer stays opt-in Development-only; production remains validation-only.
        app.MapPost("/accounts/refresh", async (RefreshRequest request, HttpContext context, TokenSessions sessions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps || !TokenSessions.IsValidFormat(request.RefreshToken))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["refreshToken"] = ["Geçerli biçimde refresh token ve HTTPS gereklidir."] });
            var result = await sessions.RefreshAsync(request.RefreshToken!, ct);
            return result is null ? Results.Problem(statusCode: 401, title: "Oturum yenilenemedi.") : Results.Ok(result);
        }).AllowAnonymous();
        app.MapPost("/accounts/dev-token", async (HttpContext context, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, TokenSessions sessions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps) return Results.Problem(statusCode: 400, title: "HTTPS gereklidir.");
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı."); }
            var id = users.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(id)) return Results.Unauthorized();
            var result = await sessions.CreateAsync(context.User, ct);
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        }).RequireAuthorization(UniversityJwtSetup.CookiePolicy);
    }
}
