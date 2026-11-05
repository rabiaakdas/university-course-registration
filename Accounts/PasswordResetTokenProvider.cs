using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public sealed class PasswordResetProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetProviderOptions() { Name = "University.PasswordReset.v1"; TokenLifespan = TimeSpan.FromMinutes(15); }
}
public sealed class PasswordResetTokenProvider(IDataProtectionProvider protection, IOptions<PasswordResetProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger)
{
    public const string ProviderName = "UniversityPasswordReset";
}
public sealed record PasswordResetSettings(Uri? PageUrl);
