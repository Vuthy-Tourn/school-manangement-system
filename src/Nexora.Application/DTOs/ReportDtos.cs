namespace Nexora.Application.DTOs;

public record AttendanceTrendItemDto(string Day, decimal Percentage);

public record GradeDistributionDto(
    decimal GradeAPercentage,
    int GradeACount,
    decimal GradeBPercentage,
    int GradeBCount,
    decimal GradeCPercentage,
    int GradeCCount,
    decimal GradeDFPercentage,
    int GradeDFCount,
    int TotalEvaluated
);

public record AdminDashboardStatsDto(
    int TotalStudents,
    int TotalTeachers,
    int TotalClasses,
    decimal TodayAttendancePercentage,
    decimal TotalFeesCollected,
    decimal TotalFeesOutstanding,
    List<AnnouncementDto> RecentAnnouncements,
    List<AuditLogDto> RecentAudits,
    List<AttendanceTrendItemDto>? WeeklyAttendanceTrend = null,
    GradeDistributionDto? GradeDistribution = null
);

public record TeacherDashboardStatsDto(
    int AssignedClassesCount,
    int AssignedSubjectsCount,
    int TotalStudentsTaught,
    List<TimetableEntryDto> TodaySchedule,
    List<ClassSectionDto> ClassesList
);

public record StudentDashboardStatsDto(
    StudentDto Student,
    decimal OverallAttendancePercentage,
    decimal CurrentGpa,
    decimal PendingFees,
    List<TimetableEntryDto> TodaySchedule,
    List<AnnouncementDto> Announcements
);

public record ParentDashboardStatsDto(
    List<LinkedStudentDto> Children,
    Guid SelectedStudentId,
    StudentDto StudentDetails,
    decimal AttendancePercentage,
    decimal PendingFees,
    List<StudentGradeDto> RecentGrades
);
