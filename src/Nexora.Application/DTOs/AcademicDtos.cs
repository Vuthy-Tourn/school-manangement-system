using Nexora.Domain.Enums;

namespace Nexora.Application.DTOs;

public record AcademicYearDto(
    Guid Id,
    Guid SchoolId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent
);

public record CreateAcademicYearRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent
);

public record SemesterDto(
    Guid Id,
    Guid SchoolId,
    Guid AcademicYearId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent
);

public record GradeLevelDto(
    Guid Id,
    Guid SchoolId,
    string Name,
    int LevelOrder
);

public record ClassSectionDto(
    Guid Id,
    Guid SchoolId,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid GradeLevelId,
    string GradeLevelName,
    string Name,
    string? RoomNumber,
    int Capacity,
    Guid? ClassTeacherId,
    string? ClassTeacherName,
    int EnrolledCount
);

public record CreateClassSectionRequest(
    Guid AcademicYearId,
    Guid GradeLevelId,
    string Name,
    string? RoomNumber,
    int Capacity,
    Guid? ClassTeacherId
);

public record SubjectDto(
    Guid Id,
    Guid SchoolId,
    string Name,
    string Code,
    string? Description,
    int CreditHours
);

public record CreateSubjectRequest(
    string Name,
    string Code,
    string? Description,
    int CreditHours
);

public record AssignTeacherSubjectRequest(
    Guid ClassSectionId,
    Guid SubjectId,
    Guid TeacherId
);

public record StudentEnrollmentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    Guid ClassSectionId,
    string ClassName,
    Guid AcademicYearId,
    string AcademicYearName,
    int RollNumber,
    EnrollmentStatus Status,
    DateTime EnrollmentDate
);

public record EnrollStudentRequest(
    Guid StudentId,
    Guid ClassSectionId,
    Guid AcademicYearId,
    int RollNumber
);

public record PromoteStudentRequest(
    Guid StudentId,
    Guid TargetClassSectionId,
    Guid TargetAcademicYearId,
    int RollNumber
);
