using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public static class UniversityRoles
{
    public const string Student = "Student", Instructor = "Instructor", Advisor = "Advisor", StudentAffairs = "StudentAffairs";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Student, Instructor, Advisor, StudentAffairs });
    public const string ManageCatalog = "ManageAcademicCatalog";
    public const string JwtRoleClaim = "role";
}

public sealed class CatalogAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "İstek doğrulanamadı.", extensions: new Dictionary<string, object?> { ["code"] = "antiforgery_failed" }); }
        return await next(context);
    }
}

// Explicit maintenance operation; never called during ordinary startup.
public static class RoleMaintenance
{
    public static async Task<int> RunAsync(IServiceProvider services, IHostEnvironment environment, string[] args)
    {
        if (!environment.IsDevelopment()) { Console.Error.WriteLine("Role maintenance requires Development."); return 1; }
        if (!(args.Length == 1 && args[0] == "prepare") && !(args.Length == 3 && args[0] is "add" or "remove" && UniversityRoles.All.Contains(args[2])))
        { Console.Error.WriteLine("Use roles prepare | roles add/remove <accountId> <allowedRole>."); return 1; }
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (args[0] == "prepare")
        {
            foreach (var role in UniversityRoles.All)
                if (!await roles.RoleExistsAsync(role) && !(await roles.CreateAsync(new IdentityRole(role))).Succeeded) return 1;
        }
        else
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(args[1]);
            if (user is null || !await roles.RoleExistsAsync(args[2])) { Console.Error.WriteLine("Account or allowed role not found."); return 1; }
            var member = await users.IsInRoleAsync(user, args[2]);
            if (args[0] == "add" && !member && !(await users.AddToRoleAsync(user, args[2])).Succeeded) return 1;
            if (args[0] == "remove" && member && !(await users.RemoveFromRoleAsync(user, args[2])).Succeeded) return 1;
        }
        Console.WriteLine("Role maintenance completed; existing cookies/JWTs are not instantly updated.");
        return 0;
    }
}
