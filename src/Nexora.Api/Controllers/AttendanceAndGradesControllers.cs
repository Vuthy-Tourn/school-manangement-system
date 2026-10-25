using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexora.Api.Controllers;

[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet("class/{classId:guid}")]
    public async Task<ActionResult<ApiResponse<List<AttendanceRecordDto>>>> GetClassAttendance(Guid classId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        return HandleResult(await _attendanceService.GetClassAttendanceAsync(classId, date, ct));
    }

    [HttpPost("batch")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<bool>>> RecordBatchAttendance([FromBody] BatchAttendanceRequest request, CancellationToken ct)
    {
        return HandleResult(await _attendanceService.RecordBatchAttendanceAsync(request, ct));
    }

    [HttpPut("{id:guid}/correct")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<bool>>> CorrectAttendance(Guid id, [FromBody] CorrectAttendanceRequest request, CancellationToken ct)
    {
        return HandleResult(await _attendanceService.CorrectAttendanceAsync(id, request, ct));
    }

    [HttpGet("class/{classId:guid}/summary")]
    public async Task<ActionResult<ApiResponse<AttendanceSummaryDto>>> GetClassAttendanceSummary(Guid classId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        return HandleResult(await _attendanceService.GetClassAttendanceSummaryAsync(classId, fromDate, toDate, ct));
    }

    [HttpGet("student/{studentId:guid}/stats")]
    public async Task<ActionResult<ApiResponse<StudentAttendanceStatsDto>>> GetStudentAttendanceStats(Guid studentId, CancellationToken ct)
    {
        return HandleResult(await _attendanceService.GetStudentAttendanceStatsAsync(studentId, ct));
    }
}

[Authorize]
public class GradesController : BaseApiController
{
    private readonly IGradeService _gradeService;

    public GradesController(IGradeService gradeService)
    {
        _gradeService = gradeService;
    }

    [HttpGet("exams")]
    public async Task<ActionResult<ApiResponse<List<ExamDto>>>> GetExams([FromQuery] Guid? academicYearId, CancellationToken ct)
    {
        return HandleResult(await _gradeService.GetExamsAsync(academicYearId, ct));
    }

    [HttpPost("exams")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<ExamDto>>> CreateExam([FromBody] CreateExamRequest request, CancellationToken ct)
    {
        return HandleResult(await _gradeService.CreateExamAsync(request, ct));
    }

    [HttpPut("exams/{id:guid}/publish")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishExam(Guid id, [FromQuery] bool publish, CancellationToken ct)
    {
        return HandleResult(await _gradeService.PublishExamResultsAsync(id, publish, ct));
    }

    [HttpGet("components")]
    public async Task<ActionResult<ApiResponse<List<AssessmentComponentDto>>>> GetComponents([FromQuery] Guid examId, [FromQuery] Guid classSectionId, CancellationToken ct)
    {
        return HandleResult(await _gradeService.GetAssessmentComponentsAsync(examId, classSectionId, ct));
    }

    [HttpPost("components")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<AssessmentComponentDto>>> CreateComponent([FromBody] CreateAssessmentComponentRequest request, CancellationToken ct)
    {
        return HandleResult(await _gradeService.CreateAssessmentComponentAsync(request, ct));
    }

    [HttpGet("components/{componentId:guid}/grades")]
    public async Task<ActionResult<ApiResponse<List<StudentGradeDto>>>> GetComponentGrades(Guid componentId, CancellationToken ct)
    {
        return HandleResult(await _gradeService.GetComponentGradesAsync(componentId, ct));
    }

    [HttpPost("batch")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<bool>>> RecordBatchGrades([FromBody] BatchGradeEntryRequest request, CancellationToken ct)
    {
        return HandleResult(await _gradeService.RecordBatchGradesAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateGrade(Guid id, [FromBody] UpdateSingleGradeRequest request, CancellationToken ct)
    {
        return HandleResult(await _gradeService.UpdateSingleGradeAsync(id, request, ct));
    }

    [HttpGet("report-card/{studentId:guid}/{examId:guid}")]
    public async Task<ActionResult<ApiResponse<ReportCardDto>>> GetReportCard(Guid studentId, Guid examId, CancellationToken ct)
    {
        return HandleResult(await _gradeService.GenerateStudentReportCardAsync(studentId, examId, ct));
    }
}
