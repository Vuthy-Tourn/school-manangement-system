using Nexora.Domain.Common;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities;

public class AcademicYear : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "2026-2027"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();
    public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}

public class Semester : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Semester 1"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}

public class GradeLevel : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Grade 10", "Grade 11"
    public int LevelOrder { get; set; }

    public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
}

public class ClassSection : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public Guid GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "10-A"
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; } = 40;

    public Guid? ClassTeacherId { get; set; }
    public Teacher? ClassTeacher { get; set; }

    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
    public ICollection<SubjectClassAssignment> SubjectAssignments { get; set; } = new List<SubjectClassAssignment>();
    public ICollection<TimetableEntry> TimetableEntries { get; set; } = new List<TimetableEntry>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

public class Subject : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Mathematics"
    public string Code { get; set; } = string.Empty; // e.g. "MATH-10"
    public string? Description { get; set; }
    public int CreditHours { get; set; } = 3;

    public ICollection<SubjectClassAssignment> ClassAssignments { get; set; } = new List<SubjectClassAssignment>();
}

public class SubjectClassAssignment : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid ClassSectionId { get; set; }
    public ClassSection? ClassSection { get; set; }

    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}

public class StudentEnrollment : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public Guid ClassSectionId { get; set; }
    public ClassSection? ClassSection { get; set; }

    public Guid AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public DateTime EnrollmentDate { get; set; }
    public int RollNumber { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;
    public DateTime? PromotedAtUtc { get; set; }
}
