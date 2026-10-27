using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Models;

namespace UniversityRegistration.Data;

public class UniversityDbContext(DbContextOptions<UniversityDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();

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

        var term = modelBuilder.Entity<AcademicTerm>();
        term.ToTable("AcademicTerms", table => table.HasCheckConstraint("CK_AcademicTerms_DateRange", "[EndsOn] >= [StartsOn]"));
        term.HasKey(x => x.Id).HasName("PK_AcademicTerms");
        term.Property(x => x.Id).ValueGeneratedNever();
        term.Property(x => x.Name).IsRequired().HasMaxLength(100).IsUnicode();
        term.Property(x => x.StartsOn).HasColumnType("date").IsRequired();
        term.Property(x => x.EndsOn).HasColumnType("date").IsRequired();

        var offering = modelBuilder.Entity<CourseOffering>();
        offering.ToTable("CourseOfferings", table => table.HasCheckConstraint("CK_CourseOfferings_Capacity_Positive", "[Capacity] > 0"));
        offering.HasKey(x => x.Id).HasName("PK_CourseOfferings");
        offering.Property(x => x.Id).ValueGeneratedNever();
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
    }
}
