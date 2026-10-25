using Nexora.Domain.Common;

namespace Nexora.Domain.Entities;

public class Exam : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public Guid SemesterId { get; set; }
    public Semester? Semester { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Midterm Examination", "Final Examination"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public ICollection<AssessmentComponent> AssessmentComponents { get; set; } = new List<AssessmentComponent>();
}

public class AssessmentComponent : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public Guid ClassSectionId { get; set; }
    public ClassSection? ClassSection { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Written Exam", "Quiz 1", "Lab Report"
    public decimal MaxScore { get; set; } = 100m;
    public decimal WeightPercentage { get; set; } = 100m;

    public ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}

public class GradeScale : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string GradeLetter { get; set; } = string.Empty; // e.g. "A", "B", "C", "D", "F"
    public decimal MinPercentage { get; set; }
    public decimal MaxPercentage { get; set; }
    public decimal GradePoint { get; set; } // e.g. 4.0, 3.0, 2.0, 1.0, 0.0
    public string? Description { get; set; } // e.g. "Excellent", "Good", "Pass", "Fail"
}

public class StudentGrade : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid AssessmentComponentId { get; set; }
    public AssessmentComponent? AssessmentComponent { get; set; }

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public decimal ScoreObtained { get; set; }
    public string? Remarks { get; set; }

    public Guid GradedByUserId { get; set; }
    public User? GradedByUser { get; set; }
    public DateTime GradedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ModifiedByUserId { get; set; }
    public User? ModifiedByUser { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ChangeReason { get; set; }
}
