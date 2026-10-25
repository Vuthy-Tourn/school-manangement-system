using Nexora.Domain.Common;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities;

public class Student : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string AdmissionNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "Other";
    public string? BloodGroup { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfilePhotoUrl { get; set; }

    // Emergency Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }

    // Active Class Section
    public Guid? CurrentClassSectionId { get; set; }
    public ClassSection? CurrentClassSection { get; set; }

    // Navigation
    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<StudentGrade> Grades { get; set; } = new List<StudentGrade>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class ParentGuardian : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Address { get; set; }

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}

public class StudentGuardian : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public Guid GuardianId { get; set; }
    public ParentGuardian? Guardian { get; set; }

    public string Relationship { get; set; } = "Parent";
    public bool IsPrimaryContact { get; set; } = true;
}
