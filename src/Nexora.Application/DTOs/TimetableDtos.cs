namespace Nexora.Application.DTOs;

public record TimetableEntryDto(
    Guid Id,
    Guid SchoolId,
    Guid ClassSectionId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    string SubjectCode,
    Guid TeacherId,
    string TeacherName,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber
);

public record CreateTimetableEntryRequest(
    Guid ClassSectionId,
    Guid SubjectId,
    Guid TeacherId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber
);

public record TimetableConflictResult(
    bool HasConflict,
    string? ConflictReason
);
