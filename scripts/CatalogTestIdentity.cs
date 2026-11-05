using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Real Identity login fixture for the existing out-of-process catalog regression host.
internal sealed class CatalogTestIdentity : IAsyncDisposable
{
    readonly Func<UniversityDbContext> factory;
    readonly string allowedRoot;
    readonly string directory, password = Guid.NewGuid().ToString("N") + "Az9!";
    readonly X509Certificate2 certificate;
    readonly List<string> roleIds = new();
    string? userId;
    readonly string email = "catalog31-" + Guid.NewGuid().ToString("N") + "@example.test";
    public HttpClient Client { get; }
    public CatalogTestIdentity(string root, int port, Func<UniversityDbContext> factory)
    {
        this.factory = factory;
        allowedRoot = Path.GetFullPath(Path.Combine(root, "obj", "catalog-identity")) + Path.DirectorySeparatorChar;
        directory = Path.Combine(allowedRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); san.AddDnsName("localhost"); request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(2));
        certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        File.WriteAllBytes(Path.Combine(directory,"server.pfx"), certificate.Export(X509ContentType.Pfx, password));
        Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new(),
            ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.RawData.AsSpan().SequenceEqual(certificate.RawData) })
            { BaseAddress = new Uri($"https://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
    }
    public void Configure(ProcessStartInfo info)
    {
        info.ArgumentList.Add("--contentRoot"); info.ArgumentList.Add(directory);
        info.Environment["Kestrel__Certificates__Default__Path"] = Path.Combine(directory,"server.pfx");
        info.Environment["Kestrel__Certificates__Default__Password"] = password;
        info.Environment["EnrollmentPolicy__MaxCreditsPerTerm"] = "30";
        info.Environment["Accounts__LocalMailboxEnabled"] = "false";
        info.Environment["Jwt__Enabled"] = "false"; info.Environment["Jwt__DevTokenEnabled"] = "false";
    }
    public async Task LoginAsync()
    {
        if (userId is null)
        {
            await using var db = factory();
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = directory });
            b.Logging.ClearProviders(); b.Configuration["Accounts:LocalMailboxEnabled"] = "false";
            b.Services.AddDbContext<UniversityDbContext>(o=>o.UseSqlServer(db.Database.GetConnectionString()));
            b.Services.AddAccountIdentity(b.Configuration,b.Environment);
            await using var app = b.Build(); await using var scope = app.Services.CreateAsyncScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(UniversityRoles.StudentAffairs))
            { var role = new IdentityRole(UniversityRoles.StudentAffairs); Ensure(await roles.CreateAsync(role)); roleIds.Add(role.Id); }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName=email, Email=email, EmailConfirmed=true };
            Ensure(await users.CreateAsync(user,password)); userId=user.Id; Ensure(await users.AddToRoleAsync(user,UniversityRoles.StudentAffairs));
        }
        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var csrf = await Token();
        using var request = new HttpRequestMessage(HttpMethod.Post,"/accounts/login") { Content=JsonContent.Create(new {email,password}) };
        request.Headers.Add("X-CSRF-TOKEN",csrf);
        using var response=await Client.SendAsync(request);
        if ((int)response.StatusCode!=200) throw new BehaviorFailure("Catalog fixture Identity login expected200 actual"+(int)response.StatusCode);
        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Token());
    }
    async Task<string> Token() { using var r=await Client.GetAsync("/accounts/antiforgery");r.EnsureSuccessStatusCode();return JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>(); }
    static void Ensure(IdentityResult r) { if(!r.Succeeded) throw new BehaviorFailure("Catalog Identity fixture failed"); }
    public async Task CleanupAsync()
    {
        if(userId is null) return;
        await using var db=factory();
        await db.UserRoles.Where(x=>x.UserId==userId).ExecuteDeleteAsync();
        await db.Users.Where(x=>x.Id==userId).ExecuteDeleteAsync();
        await db.Roles.Where(x=>roleIds.Contains(x.Id)).ExecuteDeleteAsync(); userId=null;
    }
    public async ValueTask DisposeAsync()
    { try { await CleanupAsync(); } finally { Client.Dispose(); certificate.Dispose();
        if (!Path.GetFullPath(directory).StartsWith(allowedRoot,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe fixture cleanup");
        if (Directory.Exists(directory)) Directory.Delete(directory,true); } }
}
