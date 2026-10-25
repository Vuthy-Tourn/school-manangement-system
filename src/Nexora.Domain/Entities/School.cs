using Nexora.Domain.Common;

namespace Nexora.Domain.Entities;

public class School : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Phnom_Penh";
    public string CurrencyCode { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    public bool IsActive { get; set; } = true;
    public string? SubscriptionPlan { get; set; } = "Standard";

    // Navigation collections
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<AcademicYear> AcademicYears { get; set; } = new List<AcademicYear>();
    public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}
