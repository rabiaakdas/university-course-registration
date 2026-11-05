using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;

namespace UniversityRegistration.Accounts;

public enum StudentAccessKind { Read, Manage, Decide }
public sealed record StudentAccessResource(int StudentId, string? UserId, string? AdvisorUserId);
public sealed record StudentAccessRequirement(StudentAccessKind Kind) : IAuthorizationRequirement;
public sealed class StudentAccessHandler(IOptions<IdentityOptions> options)
    : AuthorizationHandler<StudentAccessRequirement, StudentAccessResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, StudentAccessRequirement requirement, StudentAccessResource resource)
    {
        var ids = context.User.FindAll(options.Value.ClaimsIdentity.UserIdClaimType).ToArray();
        if (context.User.Identity?.IsAuthenticated != true || ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0].Value)) return Task.CompletedTask;
        var owner = context.User.IsInRole(UniversityRoles.Student) && resource.UserId == ids[0].Value;
        var advisor = context.User.IsInRole(UniversityRoles.Advisor) && resource.AdvisorUserId == ids[0].Value;
        if (requirement.Kind switch { StudentAccessKind.Manage => owner, StudentAccessKind.Decide => advisor, _ => owner || advisor }) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class StudentAccess
{
    public const string ReadPolicy = "StudentResourceRead", ManagePolicy = "StudentResourceManage", DecidePolicy = "StudentResourceDecision";
    public static void AddStudentAccess(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, StudentAccessHandler>();
        services.AddAuthorization(o =>
        {
            o.AddPolicy(ReadPolicy, p => p.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(UniversityRoles.Student, UniversityRoles.Advisor));
            o.AddPolicy(ManagePolicy, p => p.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(UniversityRoles.Student));
            o.AddPolicy(DecidePolicy, p => p.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(UniversityRoles.Advisor));
        });
    }
    // The filter is an HTTP boundary: resolve scoped query/authorization services per request, never in domain code.
    public static RouteHandlerBuilder WithStudentAccess(this RouteHandlerBuilder route, StudentAccessKind kind, bool enrollment = false, bool body = false, string routeKey = "id")
    {
        route.RequireAuthorization(kind switch { StudentAccessKind.Manage => ManagePolicy, StudentAccessKind.Decide => DecidePolicy, _ => ReadPolicy });
        route.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var id = body ? context.Arguments.OfType<CreateEnrollmentRequest>().Single().StudentId
                : int.TryParse(http.Request.RouteValues[routeKey]?.ToString(), out var parsed) ? parsed : 0;
            var db = http.RequestServices.GetRequiredService<UniversityDbContext>();
            var students = db.Students.Where(s => enrollment ? db.Enrollments.Any(e => e.Id == id && e.StudentId == s.Id) : s.Id == id);
            var resource = await students.Select(s => new StudentAccessResource(s.Id, s.UserId, s.AdvisorUserId)).SingleOrDefaultAsync(http.RequestAborted);
            var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
            if (resource is null || !(await authorization.AuthorizeAsync(http.User, resource, new StudentAccessRequirement(kind))).Succeeded)
                return Results.Problem(statusCode: 404, title: "Kaynak bulunamadı.");
            return await next(context);
        });
        return route;
    }
}
