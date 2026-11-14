using UniversityRegistration.Application;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;
using UniversityRegistration.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json.Serialization;
using UniversityRegistration.Accounts;

var builder = WebApplication.CreateBuilder(args);
if (!long.TryParse(builder.Configuration["EnrollmentPolicy:MaxCreditsPerTerm"],
    System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var creditLimit) || creditLimit <= 0)
    throw new InvalidOperationException("EnrollmentPolicy:MaxCreditsPerTerm zorunludur ve pozitif bir Int64 olmalıdır.");
builder.Services.AddSingleton(new EnrollmentPolicy(creditLimit));
var connectionString = builder.Configuration.GetConnectionString("UniversityDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:UniversityDatabase bağlantısı yapılandırılmalıdır.");
builder.Services.AddDbContext<UniversityDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddAccountIdentity(builder.Configuration, builder.Environment);
builder.Services.AddAuthenticationAbuse(builder.Configuration);
builder.Services.AddUniversityJwt(builder.Configuration, builder.Environment);
// Token failures and request URLs must never become credential logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None);
// A local confirmation URL contains a bearer secret. Do not log request URLs in this mode.
if (builder.Configuration.GetValue<bool>("Oidc:Enabled") || builder.Configuration.GetValue<bool>("Accounts:LocalMailboxEnabled") || builder.Configuration.GetValue<bool>("Jwt:Enabled"))
    builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddScoped<EnrollmentOperations>();
builder.Services.AddScoped<IdempotentEnrollmentCreation>();
builder.Services.AddScoped<CourseCatalogReader>();
builder.Services.AddScoped<IEnrollmentApprovedObserver, ApprovalLogObserver>();
builder.Services.AddScoped<EnrollmentApprovedPublisher>();
builder.Services.AddScoped<TermRegistrationSummaryFacade>();
builder.Services.AddScoped<RegistrationReports>();
builder.Services.AddScoped<AcademicEligibility>();
builder.Services.AddScoped<IAcademicEligibilityEvaluator>(services =>
    new TimingAcademicEligibilityEvaluator(services.GetRequiredService<AcademicEligibility>(),
        services.GetRequiredService<ILogger<TimingAcademicEligibilityEvaluator>>()));
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
// SQL exceptions can include rejected user values; log only safe metadata centrally.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
var app = builder.Build();
if (args.FirstOrDefault() is "roles" or "students" or "external")
{
    try { Environment.ExitCode = args[0] == "external" ? await ExternalLoginMaintenance.RunAsync(app.Services, app.Environment, args.Skip(1).ToArray()) : args[0] == "roles" ? await RoleMaintenance.RunAsync(app.Services, app.Environment, args.Skip(1).ToArray()) : await StudentMaintenance.RunAsync(app.Services, app.Environment, args.Skip(1).ToArray()); }
    catch (Exception ex) { Console.Error.WriteLine("Maintenance failed: " + ex.GetType().Name); Environment.ExitCode = 1; }
    await app.DisposeAsync(); return;
}
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true,
    ExceptionHandler = async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        app.Logger.LogError("Unhandled {ExceptionType}; trace {TraceId}", error?.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "İstek işlenirken beklenmeyen bir hata oluştu.")
            .ExecuteAsync(context);
    }
});
app.UseStatusCodePages(async context => await Results.Problem(statusCode: context.HttpContext.Response.StatusCode)
    .ExecuteAsync(context.HttpContext));
app.UseUniversityWebClient();
app.UseRouting();
app.UseAuthentication();
app.UseAuthenticationAbuse();
app.UseAuthorization();

app.MapCourseEndpoints();
app.MapAcademicTermEndpoints();
app.MapCourseOfferingEndpoints();
app.MapStudentEndpoints();
app.MapTermRegistrationSummaryEndpoint();
app.MapRegistrationReportEndpoints();
app.MapEnrollmentEndpoints();
app.MapWaitlistEndpoints();
app.MapPriorityPreviewEndpoint();
app.MapAccountEndpoints();
app.MapAccountSessionEndpoints();
app.MapUniversityJwtEndpoints();

app.Run();
