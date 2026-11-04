using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record AccountIdentityResponse(string Id, string? Email);
public sealed record AntiforgeryResponse(string RequestToken);

public static class AccountSessionEndpoints
{
    public static void MapAccountSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/accounts/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new AntiforgeryResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));
        });
        app.MapPost("/accounts/login", async (LoginRequest request, HttpContext context,
            IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await ValidCsrf(context, antiforgery)) return CsrfFailure();
            var email = request.Email?.Trim();
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
                errors["email"] = ["En fazla 256 karakterlik geçerli bir e-posta gereklidir."];
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
                errors["password"] = ["Parola zorunludur ve en fazla 128 karakter olabilir."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await signIn.PasswordSignInAsync(email!, request.Password!, isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok() : Results.Problem(statusCode: 401, title: "Giriş yapılamadı.");
        });
        app.MapGet("/accounts/me", async (HttpContext context, UserManager<ApplicationUser> users) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var id = users.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(id)) return Results.Unauthorized();
            var user = await users.FindByIdAsync(id);
            return user is null ? Results.Unauthorized() : Results.Ok(new AccountIdentityResponse(user.Id, user.Email));
        }).RequireAuthorization();
        app.MapPost("/accounts/logout", async (HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await ValidCsrf(context, antiforgery)) return CsrfFailure();
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static async Task<bool> ValidCsrf(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult CsrfFailure() => Results.Problem(statusCode: 400, title: "İstek doğrulanamadı.",
        extensions: new Dictionary<string, object?> { ["code"] = "antiforgery_failed" });
}
