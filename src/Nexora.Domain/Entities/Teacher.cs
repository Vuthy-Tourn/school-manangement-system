using Nexora.Domain.Common;

namespace Nexora.Domain.Entities;

public class Teacher : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime HireDate { get; set; }
    public string? Qualification { get; set; }
    public string? Specialization { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<ClassSection> SupervisedClasses { get; set; } = new List<ClassSection>();
    public ICollection<SubjectClassAssignment> SubjectAssignments { get; set; } = new List<SubjectClassAssignment>();
    public ICollection<TimetableEntry> TimetableEntries { get; set; } = new List<TimetableEntry>();
}
