using EduManage.Domain.Enums;

namespace EduManage.Application.DTOs;

public record AttendanceRecordDto(
    Guid Id,
    Guid SchoolId,
    Guid ClassSectionId,
    string ClassName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    DateOnly Date,
    AttendanceStatus Status,
    string? Remarks,
    DateTime RecordedAtUtc,
    string? RecordedByName,
    DateTime? CorrectedAtUtc,
    string? CorrectedByName,
    string? CorrectionReason
);

public record BatchAttendanceRequest(
    Guid ClassSectionId,
    DateOnly Date,
    List<StudentAttendanceItem> Records
);

public record StudentAttendanceItem(
    Guid StudentId,
    AttendanceStatus Status,
    string? Remarks
);

public record CorrectAttendanceRequest(
    AttendanceStatus Status,
    string Reason,
    string? Remarks
);

public record AttendanceSummaryDto(
    int TotalStudents,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    decimal AttendancePercentage
);

public record StudentAttendanceStatsDto(
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int ExcusedDays,
    decimal AttendancePercentage
);
