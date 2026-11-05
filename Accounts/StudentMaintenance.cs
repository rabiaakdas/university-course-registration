using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public static class StudentMaintenance
{
    public static async Task<int> RunAsync(IServiceProvider services, IHostEnvironment environment, string[] args)
    {
        if (!environment.IsDevelopment()) { Console.Error.WriteLine("Student maintenance requires Development."); return 1; }
        if (args.Length != 3 || args[0] is not ("link" or "advisor") || !int.TryParse(args[1], out var id) || id <= 0)
        { Console.Error.WriteLine("Use students link/advisor <studentId> <accountId>; advisor accepts none to clear."); return 1; }
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var student = await db.Students.SingleOrDefaultAsync(s => s.Id == id);
        if (student is null) { Console.Error.WriteLine("Student not found."); return 1; }
        if (args[0] == "advisor" && args[2] == "none") student.AssignAdvisor(null);
        else
        {
            var user = await users.FindByIdAsync(args[2]);
            if (user is null) { Console.Error.WriteLine("Account not found."); return 1; }
            if (args[0] == "advisor")
            {
                if (!await users.IsInRoleAsync(user, UniversityRoles.Advisor)) { Console.Error.WriteLine("Advisor role required."); return 1; }
                student.AssignAdvisor(user.Id);
            }
            else if (student.UserId != user.Id)
            {
                if (await db.Students.AnyAsync(s => s.UserId == user.Id) || !student.TryLinkAccount(user.Id)) { Console.Error.WriteLine("Account link rejected."); return 1; }
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine("Student relationship saved (unchanged on identical assignment)."); return 0;
    }
}
