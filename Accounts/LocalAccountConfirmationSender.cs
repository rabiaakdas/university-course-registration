using System.Text.Json;

namespace UniversityRegistration.Accounts;

public interface IAccountConfirmationSender
{
    Task SendAsync(string recipient, Uri confirmationLink, CancellationToken cancellationToken);
}

public sealed class LocalAccountConfirmationSender(LocalConfirmationSettings settings) : IAccountConfirmationSender
{
    public async Task SendAsync(string recipient, Uri confirmationLink, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) throw new InvalidOperationException("Yerel mesaj kutusu etkin değil.");
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(settings.Directory);
        var path = Path.Combine(settings.Directory, Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
                await JsonSerializer.SerializeAsync(file, new { recipient, subject = "E-posta doğrulama (yerel geliştirme)", link = confirmationLink.AbsoluteUri }, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(path, Path.ChangeExtension(path, ".json"));
        }
        finally
        {
            // Best-effort partial-file cleanup must not mask the original dispatch error.
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
