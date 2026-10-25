namespace EduManage.Application.DTOs;

public record AdminDashboardStatsDto(
    int TotalStudents,
    int TotalTeachers,
    int TotalClasses,
    decimal TodayAttendancePercentage,
    decimal TotalFeesCollected,
    decimal TotalFeesOutstanding,
    List<AnnouncementDto> RecentAnnouncements,
    List<AuditLogDto> RecentAudits
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
