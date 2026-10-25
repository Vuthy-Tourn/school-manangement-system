namespace Nexora.Application.DTOs;

public record ExamDto(
    Guid Id,
    Guid SchoolId,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid SemesterId,
    string SemesterName,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsPublished,
    DateTime? PublishedAtUtc
);

public record CreateExamRequest(
    Guid AcademicYearId,
    Guid SemesterId,
    string Name,
    DateTime StartDate,
    DateTime EndDate
);

public record AssessmentComponentDto(
    Guid Id,
    Guid ExamId,
    string ExamName,
    Guid SubjectId,
    string SubjectName,
    Guid ClassSectionId,
    string ClassName,
    string Name,
    decimal MaxScore,
    decimal WeightPercentage
)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(SubjectName) ? Name : $"{SubjectName} ({Name})";
}

public record CreateAssessmentComponentRequest(
    Guid ExamId,
    Guid SubjectId,
    Guid ClassSectionId,
    string Name,
    decimal MaxScore,
    decimal WeightPercentage
);

public record StudentGradeDto(
    Guid Id,
    Guid AssessmentComponentId,
    string ComponentName,
    Guid SubjectId,
    string SubjectName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    decimal ScoreObtained,
    decimal MaxScore,
    decimal Percentage,
    string GradeLetter,
    decimal GradePoint,
    string? Remarks
);

public record BatchGradeEntryRequest(
    Guid AssessmentComponentId,
    List<StudentGradeItem> Grades
);

public record StudentGradeItem(
    Guid StudentId,
    decimal ScoreObtained,
    string? Remarks
);

public record UpdateSingleGradeRequest(
    decimal ScoreObtained,
    string ChangeReason,
    string? Remarks
);

public record ReportCardDto(
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string ClassName,
    string AcademicYearName,
    string SemesterName,
    List<SubjectReportItem> Subjects,
    decimal TotalScoreObtained,
    decimal TotalMaxScore,
    decimal OverallPercentage,
    decimal CumulativeGpa,
    string OverallGradeLetter,
    int RankInClass
);

public record SubjectReportItem(
    Guid SubjectId,
    string SubjectName,
    string SubjectCode,
    int CreditHours,
    decimal TotalScore,
    decimal MaxScore,
    decimal Percentage,
    string GradeLetter,
    decimal GradePoint
);

public record GradeScaleDto(
    Guid Id,
    string GradeLetter,
    decimal MinPercentage,
    decimal MaxPercentage,
    decimal GradePoint,
    string? Description
);
