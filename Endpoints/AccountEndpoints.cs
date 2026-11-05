using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Accounts;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class AccountEndpoints
{
    public static readonly EventId DispatchFailed = new(2501, "AccountConfirmationDispatchFailed");

    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPasswordResetEndpoints();
        app.MapPost("/accounts/register", RegisterAsync);
        app.MapPost("/accounts/confirm-email", ConfirmAsync);
    }

    public static async Task<IResult> RegisterAsync(RegisterAccountRequest request, UserManager<ApplicationUser> manager,
        IAccountConfirmationSender sender, LocalConfirmationSettings settings, ILoggerFactory logs, CancellationToken ct)
    {
        var email = request.Email?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) errors["email"] = ["E-posta zorunludur ve en fazla 256 karakter olabilir."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128) errors["password"] = ["Parola zorunludur ve en fazla 128 karakter olabilir."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        // No account is written when no dispatch implementation is enabled.
        if (!settings.Enabled) return Results.Problem(statusCode: 503, title: "Hesap oluşturma şu anda yapılandırılmamış.");
        ct.ThrowIfCancellationRequested();
        var user = new ApplicationUser { UserName = email, Email = email };
        IdentityResult created;
        try { created = await manager.CreateAsync(user, request.Password!); }
        catch (DbUpdateException ex) when (RegistrationWriteErrors.Classify(ex) == "accountName") { return CannotCreate(); }
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail")) return CannotCreate();
            foreach (var error in created.Errors)
            {
                var field = error.Code switch
                {
                    "InvalidEmail" or "InvalidUserName" => "email",
                    "PasswordTooShort" or "PasswordRequiresUniqueChars" or "PasswordRequiresDigit" or
                    "PasswordRequiresLower" or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric" => "password",
                    _ => throw new InvalidOperationException("Beklenmeyen Identity hesap oluşturma sonucu.")
                };
                errors[field] = [field == "email" ? "Geçerli bir e-posta girin." : "Parola en az 12 karakter olmalıdır."];
            }
            return Results.ValidationProblem(errors);
        }
        // CreateAsync has committed. Dispatch cannot roll that commit back.
        var dispatch = "queued-locally";
        try
        {
            ct.ThrowIfCancellationRequested();
            var raw = await manager.GenerateEmailConfirmationTokenAsync(user);
            var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
            var link = new Uri(QueryHelpers.AddQueryString(settings.PageUrl!.AbsoluteUri, new Dictionary<string, string?> { ["userId"] = user.Id, ["token"] = token }));
            await sender.SendAsync(email!, link, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { dispatch = "cancelled"; }
        catch (Exception ex)
        {
            dispatch = "failed";
            logs.CreateLogger("AccountConfirmation").LogWarning(DispatchFailed, "Local confirmation dispatch failed: {ExceptionType}", ex.GetType().Name);
        }
        return Results.Json(new RegisteredAccountResponse(user.Id, true, dispatch), statusCode: 201);
    }

    public static async Task<IResult> ConfirmAsync(ConfirmEmailRequest request, UserManager<ApplicationUser> manager, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450) errors["userId"] = ["Hesap kimliği zorunludur ve en fazla 450 karakter olabilir."];
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 8192) errors["token"] = ["Doğrulama token'ı zorunludur ve en fazla 8192 karakter olabilir."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        string token;
        try
        {
            if (request.Token!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return CannotConfirm();
            token = new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(request.Token!));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { return CannotConfirm(); }
        ct.ThrowIfCancellationRequested();
        var user = await manager.FindByIdAsync(request.UserId!);
        if (user is null) return CannotConfirm();
        ct.ThrowIfCancellationRequested();
        var result = await manager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            if (result.Errors.All(e => e.Code == "InvalidToken")) return CannotConfirm();
            throw new InvalidOperationException("Beklenmeyen Identity e-posta doğrulama sonucu.");
        }
        return Results.Ok(new ConfirmedEmailResponse(true));
    }

    private static IResult CannotCreate() => Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Hesap oluşturulamıyor."] },
        extensions: new Dictionary<string, object?> { ["code"] = "account_creation_failed" });
    private static IResult CannotConfirm() => Results.Problem(statusCode: 400, title: "E-posta doğrulanamadı.",
        extensions: new Dictionary<string, object?> { ["code"] = "confirmation_failed" });
}
