using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Models;

namespace UniversityRegistration.Data;

public class UniversityDbContext(DbContextOptions<UniversityDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<StudentCompletedCourse> StudentCompletedCourses => Set<StudentCompletedCourse>();
    public DbSet<CourseOfferingTimeSlot> CourseOfferingTimeSlots => Set<CourseOfferingTimeSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("CourseIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var course = modelBuilder.Entity<Course>();
        course.ToTable("Courses", table => table.HasCheckConstraint("CK_Courses_Credits_Positive", "[Credits] > 0"));
        course.HasKey(x => x.Id).HasName("PK_Courses");
        course.Property(x => x.Id).ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEXT VALUE FOR [dbo].[CourseIds]", "DF_Courses_Id");
        course.Property(x => x.Code).IsRequired().HasMaxLength(20).IsUnicode();
        course.Property(x => x.Name).IsRequired().HasMaxLength(200).IsUnicode();
        course.Property(x => x.Credits).IsRequired();
        course.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Courses_Code");

        modelBuilder.HasSequence<int>("AcademicTermIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var term = modelBuilder.Entity<AcademicTerm>();
        term.ToTable("AcademicTerms", table => table.HasCheckConstraint("CK_AcademicTerms_DateRange", "[EndsOn] >= [StartsOn]"));
        term.HasKey(x => x.Id).HasName("PK_AcademicTerms");
        term.Property(x => x.Id).ValueGeneratedOnAdd().HasDefaultValueSql("NEXT VALUE FOR [dbo].[AcademicTermIds]", "DF_AcademicTerms_Id");
        term.Property(x => x.Name).IsRequired().HasMaxLength(100).IsUnicode();
        term.Property(x => x.StartsOn).HasColumnType("date").IsRequired();
        term.Property(x => x.EndsOn).HasColumnType("date").IsRequired();

        modelBuilder.HasSequence<int>("CourseOfferingIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var offering = modelBuilder.Entity<CourseOffering>();
        offering.ToTable("CourseOfferings", table => table.HasCheckConstraint("CK_CourseOfferings_Capacity_Positive", "[Capacity] > 0"));
        offering.HasKey(x => x.Id).HasName("PK_CourseOfferings");
        offering.Property(x => x.Id).ValueGeneratedOnAdd().HasDefaultValueSql("NEXT VALUE FOR [dbo].[CourseOfferingIds]", "DF_CourseOfferings_Id");
        offering.Property(x => x.CourseId).IsRequired();
        offering.Property(x => x.AcademicTermId).IsRequired();
        offering.Property(x => x.SectionCode).IsRequired().HasMaxLength(10).IsUnicode();
        offering.Property(x => x.Capacity).IsRequired();
        offering.HasIndex(x => new { x.CourseId, x.AcademicTermId, x.SectionCode })
            .IsUnique().HasDatabaseName("UX_CourseOfferings_Course_Term_Section");
        offering.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CourseOfferings_Courses");
        offering.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => x.AcademicTermId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CourseOfferings_AcademicTerms");

        modelBuilder.HasSequence<int>("StudentIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var student = modelBuilder.Entity<Student>();
        student.ToTable("Students");
        student.HasKey(x => x.Id).HasName("PK_Students");
        student.Property(x => x.Id).ValueGeneratedOnAdd().HasDefaultValueSql("NEXT VALUE FOR [dbo].[StudentIds]", "DF_Students_Id");
        student.Property(x => x.StudentNumber).IsRequired().HasMaxLength(30).IsUnicode();
        student.Property(x => x.FullName).IsRequired().HasMaxLength(200).IsUnicode();
        student.HasIndex(x => x.StudentNumber).IsUnique().HasDatabaseName("UX_Students_StudentNumber");

        modelBuilder.HasSequence<int>("EnrollmentIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var enrollment = modelBuilder.Entity<Enrollment>();
        enrollment.ToTable("Enrollments", table =>
        {
            table.HasCheckConstraint("CK_Enrollments_Status", "[Status] IN (0, 1, 2, 3)");
            // Match String.Trim whitespace, including tabs/newlines and Unicode spaces.
            table.HasCheckConstraint("CK_Enrollments_RejectionReason",
                "([Status] = 3 AND [RejectionReason] IS NOT NULL AND LEN(TRIM(NCHAR(9)+NCHAR(10)+NCHAR(11)+NCHAR(12)+NCHAR(13)+NCHAR(32)+NCHAR(133)+NCHAR(160)+NCHAR(5760)+NCHAR(8192)+NCHAR(8193)+NCHAR(8194)+NCHAR(8195)+NCHAR(8196)+NCHAR(8197)+NCHAR(8198)+NCHAR(8199)+NCHAR(8200)+NCHAR(8201)+NCHAR(8202)+NCHAR(8232)+NCHAR(8233)+NCHAR(8239)+NCHAR(8287)+NCHAR(12288) FROM [RejectionReason])) > 0) OR ([Status] <> 3 AND [RejectionReason] IS NULL)");
        });
        enrollment.HasKey(x => x.Id).HasName("PK_Enrollments");
        enrollment.Property(x => x.Id).ValueGeneratedOnAdd().HasDefaultValueSql("NEXT VALUE FOR [dbo].[EnrollmentIds]", "DF_Enrollments_Id");
        enrollment.Property(x => x.StudentId).IsRequired();
        enrollment.Property(x => x.CourseOfferingId).IsRequired();
        enrollment.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        enrollment.Property(x => x.Status).HasConversion<int>().IsRequired()
            .HasDefaultValue(EnrollmentStatus.Draft, "DF_Enrollments_Status");
        enrollment.Property(x => x.RejectionReason).HasMaxLength(500).IsUnicode();
        enrollment.HasIndex(x => new { x.StudentId, x.CourseOfferingId }).IsUnique().HasDatabaseName("UX_Enrollments_Student_Offering");
        enrollment.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Enrollments_Students");
        enrollment.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.CourseOfferingId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Enrollments_CourseOfferings");
        var prerequisite = modelBuilder.Entity<CoursePrerequisite>();
        prerequisite.ToTable("CoursePrerequisites", table => table.HasCheckConstraint("CK_CoursePrerequisites_NotSelf", "[CourseId] <> [PrerequisiteCourseId]"));
        prerequisite.HasKey(x => new { x.CourseId, x.PrerequisiteCourseId }).HasName("PK_CoursePrerequisites");
        prerequisite.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CoursePrerequisites_Courses");
        prerequisite.HasOne<Course>().WithMany().HasForeignKey(x => x.PrerequisiteCourseId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CoursePrerequisites_PrerequisiteCourses");

        var completed = modelBuilder.Entity<StudentCompletedCourse>();
        completed.ToTable("StudentCompletedCourses");
        completed.HasKey(x => new { x.StudentId, x.CourseId }).HasName("PK_StudentCompletedCourses");
        completed.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StudentCompletedCourses_Students");
        completed.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_StudentCompletedCourses_Courses");

        modelBuilder.HasSequence<int>("CourseOfferingTimeSlotIds", "dbo").StartsAt(1).IncrementsBy(1).HasMin(1);
        var slot = modelBuilder.Entity<CourseOfferingTimeSlot>();
        slot.ToTable("CourseOfferingTimeSlots", table =>
        {
            table.HasCheckConstraint("CK_CourseOfferingTimeSlots_Day", "[Day] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_CourseOfferingTimeSlots_Range", "[StartsAt] < [EndsAt]");
        });
        slot.HasKey(x => x.Id).HasName("PK_CourseOfferingTimeSlots");
        slot.Property(x => x.Id).ValueGeneratedOnAdd().HasDefaultValueSql("NEXT VALUE FOR [dbo].[CourseOfferingTimeSlotIds]", "DF_CourseOfferingTimeSlots_Id");
        slot.Property(x => x.Day).HasConversion<int>().IsRequired();
        slot.Property(x => x.StartsAt).HasColumnType("time(7)").IsRequired();
        slot.Property(x => x.EndsAt).HasColumnType("time(7)").IsRequired();
        slot.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.CourseOfferingId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CourseOfferingTimeSlots_CourseOfferings");
        slot.HasIndex(x => new { x.CourseOfferingId, x.Day, x.StartsAt, x.EndsAt })
            .IsUnique().HasDatabaseName("UX_CourseOfferingTimeSlots_Offering_Day_Range");
    }
}
