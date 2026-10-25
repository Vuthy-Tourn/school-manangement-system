using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;

namespace Nexora.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress, CancellationToken ct = default);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeTokenAsync(string token, string ipAddress, CancellationToken ct = default);
    Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken ct = default);
}

public interface IStudentService
{
    Task<ApiResponse<PaginatedResult<StudentDto>>> GetStudentsAsync(StudentFilter filter, CancellationToken ct = default);
    Task<ApiResponse<StudentDto>> GetStudentByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<StudentDto>> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default);
    Task<ApiResponse<StudentDto>> UpdateStudentAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeactivateStudentAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> LinkParentGuardianAsync(LinkStudentRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ParentGuardianDto>>> GetStudentGuardiansAsync(Guid studentId, CancellationToken ct = default);
}

public interface ITeacherService
{
    Task<ApiResponse<List<TeacherDto>>> GetTeachersAsync(CancellationToken ct = default);
    Task<ApiResponse<TeacherDto>> GetTeacherByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<TeacherDto>> CreateTeacherAsync(CreateTeacherRequest request, CancellationToken ct = default);
    Task<ApiResponse<TeacherDto>> UpdateTeacherAsync(Guid id, UpdateTeacherRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeactivateTeacherAsync(Guid id, CancellationToken ct = default);
}

public interface IAcademicService
{
    Task<ApiResponse<List<AcademicYearDto>>> GetAcademicYearsAsync(CancellationToken ct = default);
    Task<ApiResponse<AcademicYearDto>> CreateAcademicYearAsync(CreateAcademicYearRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<SemesterDto>>> GetSemestersAsync(Guid academicYearId, CancellationToken ct = default);
    Task<ApiResponse<List<GradeLevelDto>>> GetGradeLevelsAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ClassSectionDto>>> GetClassSectionsAsync(Guid? academicYearId = null, CancellationToken ct = default);
    Task<ApiResponse<ClassSectionDto>> CreateClassSectionAsync(CreateClassSectionRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<SubjectDto>>> GetSubjectsAsync(CancellationToken ct = default);
    Task<ApiResponse<SubjectDto>> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> AssignTeacherToSubjectAsync(AssignTeacherSubjectRequest request, CancellationToken ct = default);
    Task<ApiResponse<StudentEnrollmentDto>> EnrollStudentAsync(EnrollStudentRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> PromoteStudentAsync(PromoteStudentRequest request, CancellationToken ct = default);
}

public interface ITimetableService
{
    Task<ApiResponse<List<TimetableEntryDto>>> GetClassTimetableAsync(Guid classSectionId, CancellationToken ct = default);
    Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId, CancellationToken ct = default);
    Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId, CancellationToken ct = default);
    Task<ApiResponse<TimetableEntryDto>> CreateEntryAsync(CreateTimetableEntryRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteEntryAsync(Guid id, CancellationToken ct = default);
    Task<TimetableConflictResult> CheckConflictsAsync(CreateTimetableEntryRequest request, Guid? excludeId = null, CancellationToken ct = default);
}

public interface IAttendanceService
{
    Task<ApiResponse<List<AttendanceRecordDto>>> GetClassAttendanceAsync(Guid classSectionId, DateOnly date, CancellationToken ct = default);
    Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request, CancellationToken ct = default);
    Task<ApiResponse<AttendanceSummaryDto>> GetClassAttendanceSummaryAsync(Guid classSectionId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);
    Task<ApiResponse<StudentAttendanceStatsDto>> GetStudentAttendanceStatsAsync(Guid studentId, CancellationToken ct = default);
}

public interface IGradeService
{
    Task<ApiResponse<List<ExamDto>>> GetExamsAsync(Guid? academicYearId = null, CancellationToken ct = default);
    Task<ApiResponse<ExamDto>> CreateExamAsync(CreateExamRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> PublishExamResultsAsync(Guid examId, bool publish, CancellationToken ct = default);
    Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classSectionId, CancellationToken ct = default);
    Task<ApiResponse<AssessmentComponentDto>> CreateAssessmentComponentAsync(CreateAssessmentComponentRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid assessmentComponentId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateSingleGradeAsync(Guid gradeId, UpdateSingleGradeRequest request, CancellationToken ct = default);
    Task<ApiResponse<ReportCardDto>> GenerateStudentReportCardAsync(Guid studentId, Guid examId, CancellationToken ct = default);
}

public interface IFeeService
{
    Task<ApiResponse<List<FeeCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ApiResponse<FeeCategoryDto>> CreateCategoryAsync(CreateFeeCategoryRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null, CancellationToken ct = default);
    Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default);
    Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default);
    Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync(CancellationToken ct = default);
}

public interface IAnnouncementService
{
    Task<ApiResponse<List<AnnouncementDto>>> GetAnnouncementsAsync(CancellationToken ct = default);
    Task<ApiResponse<AnnouncementDto>> GetAnnouncementByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<AnnouncementDto>> CreateAnnouncementAsync(CreateAnnouncementRequest request, CancellationToken ct = default);
    Task<ApiResponse<AnnouncementDto>> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAnnouncementAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid announcementId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<ApiResponse<AdminDashboardStatsDto>> GetAdminDashboardAsync(CancellationToken ct = default);
    Task<ApiResponse<TeacherDashboardStatsDto>> GetTeacherDashboardAsync(CancellationToken ct = default);
    Task<ApiResponse<StudentDashboardStatsDto>> GetStudentDashboardAsync(CancellationToken ct = default);
    Task<ApiResponse<ParentDashboardStatsDto>> GetParentDashboardAsync(Guid? childId = null, CancellationToken ct = default);
}

public interface IReportService
{
    Task<byte[]> ExportStudentsToCsvAsync(Guid? classSectionId = null, CancellationToken ct = default);
    Task<byte[]> ExportAttendanceToCsvAsync(Guid classSectionId, DateOnly date, CancellationToken ct = default);
    Task<byte[]> ExportInvoicesToCsvAsync(CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task LogAsync(AuditAction action, string entityName, string? entityId, string? details, string? oldValues = null, string? newValues = null, CancellationToken ct = default);
    Task<ApiResponse<List<AuditLogDto>>> GetLogsAsync(int count = 50, CancellationToken ct = default);
}
