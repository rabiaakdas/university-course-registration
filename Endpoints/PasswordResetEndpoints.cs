using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using UniversityRegistration.Accounts;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string? UserId, string? Token, string? NewPassword);

public static class PasswordResetEndpoints
{
    public static readonly EventId DispatchFailed = new(3001, "PasswordResetDispatchFailed");
    public static void MapPasswordResetEndpoints(this WebApplication app)
    {
        app.MapPost("/accounts/forgot-password", ForgotAsync).AllowAnonymous().RequireRateLimiting(AuthenticationAbuse.ForgotPolicy);
        app.MapPost("/accounts/reset-password", ResetAsync).AllowAnonymous();
    }
    private static IResult Accepted() => Results.Json(new { message = "Bilgiler uygunsa parola sıfırlama talimatı gönderilecektir." }, statusCode: 202);
    private static IResult Failed() => Results.Problem(statusCode: 400, title: "Parola sıfırlanamadı.", extensions: new Dictionary<string, object?> { ["code"] = "password_reset_failed" });
    public static async Task<IResult> ForgotAsync(ForgotPasswordRequest request, HttpContext context, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, IAccountConfirmationSender sender, LocalConfirmationSettings mailbox,
        PasswordResetSettings settings, ILoggerFactory logs, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Geçerli bir e-posta girin (en fazla 256 karakter)."] });
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed || !await signIn.CanSignInAsync(user)) return Accepted();
        if (!mailbox.Enabled || settings.PageUrl is null)
        {
            logs.CreateLogger("PasswordReset").LogWarning(DispatchFailed, "Password reset dispatch is not configured.");
            return Accepted();
        }
        var raw = await users.GeneratePasswordResetTokenAsync(user);
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
        var link = new Uri(QueryHelpers.AddQueryString(settings.PageUrl.AbsoluteUri, new Dictionary<string, string?> { ["userId"] = user.Id, ["token"] = token }));
        try { await sender.SendAsync(user.Email!, link, ct, "Parola sıfırlama (yerel geliştirme)"); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { logs.CreateLogger("PasswordReset").LogWarning(DispatchFailed, "Local password reset dispatch failed: {ExceptionType}", ex.GetType().Name); }
        return Accepted();
    }
    public static async Task<IResult> ResetAsync(ResetPasswordRequest request, HttpContext context, PasswordResetOperations operation, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450) errors["userId"] = ["Hesap kimliği zorunludur (en fazla 450 karakter)."];
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 8192) errors["token"] = ["Token zorunludur (en fazla 8192 karakter)."];
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > 128) errors["newPassword"] = ["Yeni parola zorunludur (en fazla 128 karakter)."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        string token;
        try
        {
            if (request.Token!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return Failed();
            token = new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(request.Token!));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { return Failed(); }
        var result = await operation.ResetAsync(request.UserId!, token, request.NewPassword!, ct);
        if (result.Succeeded) return Results.NoContent();
        if (result.Errors.All(x => x.Code.StartsWith("Password", StringComparison.Ordinal)))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Parola mevcut parola politikasını sağlamıyor (en az 12 karakter)."] });
        if (result.Errors.All(x => x.Code is "InvalidToken" or "ConcurrencyFailure")) return Failed();
        throw new InvalidOperationException("Beklenmeyen Identity parola sıfırlama sonucu.");
    }
}
