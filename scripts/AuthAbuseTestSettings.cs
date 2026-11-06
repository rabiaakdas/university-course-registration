using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using UniversityRegistration.Accounts;

// Explicit high quotas only for isolated regression hosts. Real low quotas are exercised by auth-abuse.
internal static class AuthAbuseTestSettings
{
    public static void Configure(WebApplicationBuilder b, bool low = false)
    {
        b.Configuration["AuthenticationLimits:Login:PermitLimit"] = low ? "10" : "10000";
        b.Configuration["AuthenticationLimits:Login:WindowSeconds"] = low ? "5" : "60";
        b.Configuration["AuthenticationLimits:ForgotPassword:PermitLimit"] = low ? "3" : "10000";
        b.Configuration["AuthenticationLimits:ForgotPassword:WindowSeconds"] = low ? "5" : "60";
        b.Services.AddAuthenticationAbuse(b.Configuration);
    }
    public static void Configure(ProcessStartInfo process)
    {
        process.Environment["AuthenticationLimits__Login__PermitLimit"] = "10000";
        process.Environment["AuthenticationLimits__Login__WindowSeconds"] = "60";
        process.Environment["AuthenticationLimits__ForgotPassword__PermitLimit"] = "10000";
        process.Environment["AuthenticationLimits__ForgotPassword__WindowSeconds"] = "60";
    }
}
