using UniversityRegistration.Application;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
if (!long.TryParse(builder.Configuration["EnrollmentPolicy:MaxCreditsPerTerm"],
    System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var creditLimit) || creditLimit <= 0)
    throw new InvalidOperationException("EnrollmentPolicy:MaxCreditsPerTerm zorunludur ve pozitif bir Int64 olmalıdır.");
builder.Services.AddSingleton(new EnrollmentPolicy(creditLimit));
var connectionString = builder.Configuration.GetConnectionString("UniversityDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:UniversityDatabase bağlantısı yapılandırılmalıdır.");
builder.Services.AddDbContext<UniversityDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<EnrollmentOperations>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
// SQL exceptions can include rejected user values; log only safe metadata centrally.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
var app = builder.Build();
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

app.MapCourseEndpoints();
app.MapAcademicTermEndpoints();
app.MapCourseOfferingEndpoints();
app.MapStudentEndpoints();
app.MapEnrollmentEndpoints();
app.MapPriorityPreviewEndpoint();

app.Run();
