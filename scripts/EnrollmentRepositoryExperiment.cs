using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Learning experiment only; excluded from the web assembly. The caller owns the context.
// Find returns a tracked entity or null, and propagates DB errors/cancellation.
// Add only stages Added state. Neither method saves, commits or disposes anything.
internal sealed class DemoEnrollmentRepository(UniversityDbContext db)
{
    public Task<Enrollment?> FindAsync(int enrollmentId, CancellationToken cancellationToken) =>
        db.Enrollments.SingleOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

    public void Add(Enrollment enrollment) => db.Enrollments.Add(enrollment);
}

// Second deliberately small participant in the shared-context experiment, not a production service.
internal sealed class DemoStudentWriter(UniversityDbContext db)
{
    public void Add(Student student) => db.Students.Add(student);
}
