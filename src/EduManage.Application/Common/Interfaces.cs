using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Application.Common;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? SchoolId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface IJwtTokenService
{
    (string Token, string JwtId, DateTime ExpiresAtUtc) GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

public interface IApplicationDbContext
{
    DbSet<School> Schools { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Student> Students { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<ParentGuardian> ParentGuardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<AcademicYear> AcademicYears { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<GradeLevel> GradeLevels { get; }
    DbSet<ClassSection> ClassSections { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<SubjectClassAssignment> SubjectClassAssignments { get; }
    DbSet<StudentEnrollment> StudentEnrollments { get; }
    DbSet<TimetableEntry> TimetableEntries { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<Exam> Exams { get; }
    DbSet<AssessmentComponent> AssessmentComponents { get; }
    DbSet<GradeScale> GradeScales { get; }
    DbSet<StudentGrade> StudentGrades { get; }
    DbSet<FeeCategory> FeeCategories { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
