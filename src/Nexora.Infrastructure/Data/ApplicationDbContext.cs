using Nexora.Application.Common;
using Nexora.Domain.Common;
using Nexora.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUserService = null) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<ParentGuardian> ParentGuardians => Set<ParentGuardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<ClassSection> ClassSections => Set<ClassSection>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectClassAssignment> SubjectClassAssignments => Set<SubjectClassAssignment>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<AssessmentComponent> AssessmentComponents => Set<AssessmentComponent>();
    public DbSet<GradeScale> GradeScales => Set<GradeScale>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();
    public DbSet<FeeCategory> FeeCategories => Set<FeeCategory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts => Set<AnnouncementReadReceipt>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Multi-tenant Query Filters:
        // Automatically filter entities by SchoolId unless SuperAdmin or no tenant context (e.g. unauthenticated)
        modelBuilder.Entity<User>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Student>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Teacher>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<ParentGuardian>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AcademicYear>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Semester>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<GradeLevel>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<ClassSection>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Subject>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<SubjectClassAssignment>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<StudentEnrollment>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<TimetableEntry>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AttendanceRecord>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Exam>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AssessmentComponent>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<GradeScale>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<StudentGrade>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<FeeCategory>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Invoice>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Announcement>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => e.User == null || _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.User.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<InvoiceItem>().HasQueryFilter(e => e.Invoice == null || _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.Invoice.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AnnouncementReadReceipt>().HasQueryFilter(e => e.Announcement == null || _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.Announcement.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<StudentGuardian>().HasQueryFilter(e => e.Student == null || _currentUserService == null || _currentUserService.IsSuperAdmin || _currentUserService.SchoolId == null || e.Student.SchoolId == _currentUserService.SchoolId);

        // School configuration
        modelBuilder.Entity<School>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        });

        // User configuration
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.Username }).IsUnique();
            b.HasIndex(x => new { x.SchoolId, x.Email }).IsUnique();
            b.Property(x => x.Username).HasMaxLength(100).IsRequired();
            b.Property(x => x.Email).HasMaxLength(150).IsRequired();
            b.Property(x => x.PasswordHash).IsRequired();
            b.HasOne(x => x.School).WithMany(s => s.Users).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Student configuration
        modelBuilder.Entity<Student>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.AdmissionNumber }).IsUnique();
            b.Property(x => x.AdmissionNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.HasOne(x => x.School).WithMany(s => s.Students).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.User).WithOne(u => u.StudentProfile).HasForeignKey<Student>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CurrentClassSection).WithMany().HasForeignKey(x => x.CurrentClassSectionId).OnDelete(DeleteBehavior.SetNull);
        });

        // Teacher configuration
        modelBuilder.Entity<Teacher>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.EmployeeNumber }).IsUnique();
            b.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.HasOne(x => x.School).WithMany(s => s.Teachers).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.User).WithOne(u => u.TeacherProfile).HasForeignKey<Teacher>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        // ParentGuardian & StudentGuardian
        modelBuilder.Entity<ParentGuardian>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne(x => x.User).WithOne(u => u.ParentProfile).HasForeignKey<ParentGuardian>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StudentGuardian>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.StudentId, x.GuardianId }).IsUnique();
            b.HasOne(x => x.Student).WithMany(s => s.StudentGuardians).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Guardian).WithMany(g => g.StudentGuardians).HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Cascade);
        });

        // Academic structure
        modelBuilder.Entity<AcademicYear>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne(x => x.School).WithMany(s => s.AcademicYears).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassSection>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.Name }).IsUnique();
            b.HasOne(x => x.ClassTeacher).WithMany(t => t.SupervisedClasses).HasForeignKey(x => x.ClassTeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SubjectClassAssignment>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ClassSectionId, x.SubjectId, x.TeacherId }).IsUnique();
            b.HasOne(x => x.ClassSection).WithMany(c => c.SubjectAssignments).HasForeignKey(x => x.ClassSectionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Teacher).WithMany(t => t.SubjectAssignments).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentEnrollment>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
            b.HasOne(x => x.Student).WithMany(s => s.Enrollments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ClassSection).WithMany(c => c.Enrollments).HasForeignKey(x => x.ClassSectionId).OnDelete(DeleteBehavior.Restrict);
        });

        // Timetable & Attendance
        modelBuilder.Entity<TimetableEntry>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ClassSectionId, x.DayOfWeek, x.StartTime });
            b.HasIndex(x => new { x.TeacherId, x.DayOfWeek, x.StartTime });
        });

        modelBuilder.Entity<AttendanceRecord>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ClassSectionId, x.StudentId, x.Date }).IsUnique();
            b.HasOne(x => x.Student).WithMany(s => s.AttendanceRecords).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ClassSection).WithMany(c => c.AttendanceRecords).HasForeignKey(x => x.ClassSectionId).OnDelete(DeleteBehavior.Restrict);
        });

        // Exams, Assessments & Grades
        modelBuilder.Entity<AssessmentComponent>(b =>
        {
            b.Property(x => x.MaxScore).HasPrecision(5, 2);
            b.Property(x => x.WeightPercentage).HasPrecision(5, 2);
        });

        modelBuilder.Entity<GradeScale>(b =>
        {
            b.Property(x => x.MinPercentage).HasPrecision(5, 2);
            b.Property(x => x.MaxPercentage).HasPrecision(5, 2);
            b.Property(x => x.GradePoint).HasPrecision(3, 2);
        });

        modelBuilder.Entity<StudentGrade>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.AssessmentComponentId, x.StudentId }).IsUnique();
            b.Property(x => x.ScoreObtained).HasPrecision(5, 2);
            b.HasOne(x => x.Student).WithMany(s => s.Grades).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        // Fees & Invoices
        modelBuilder.Entity<FeeCategory>(b =>
        {
            b.Property(x => x.DefaultAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.InvoiceNumber }).IsUnique();
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.HasOne(x => x.Student).WithMany(s => s.Invoices).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(b =>
        {
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.HasOne(x => x.Invoice).WithMany(i => i.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.ReceiptNumber }).IsUnique();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Student).WithMany(s => s.Payments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        // Announcements & Notifications
        modelBuilder.Entity<AnnouncementReadReceipt>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.AnnouncementId, x.UserId }).IsUnique();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
