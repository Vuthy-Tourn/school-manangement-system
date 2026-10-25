using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Nexora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexora.Api.Controllers;

[Authorize]
public class FeesController : BaseApiController
{
    private readonly IFeeService _feeService;

    public FeesController(IFeeService feeService)
    {
        _feeService = feeService;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<FeeCategoryDto>>>> GetCategories(CancellationToken ct)
    {
        return HandleResult(await _feeService.GetCategoriesAsync(ct));
    }

    [HttpPost("categories")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<FeeCategoryDto>>> CreateCategory([FromBody] CreateFeeCategoryRequest request, CancellationToken ct)
    {
        return HandleResult(await _feeService.CreateCategoryAsync(request, ct));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<List<InvoiceDto>>>> GetInvoices([FromQuery] Guid? studentId, [FromQuery] InvoiceStatus? status, CancellationToken ct)
    {
        return HandleResult(await _feeService.GetInvoicesAsync(studentId, status, ct));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(Guid id, CancellationToken ct)
    {
        return HandleResult(await _feeService.GetInvoiceByIdAsync(id, ct));
    }

    [HttpPost("invoices")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        return HandleResult(await _feeService.CreateInvoiceAsync(request, ct));
    }

    [HttpPost("payments")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> RecordPayment([FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        return HandleResult(await _feeService.RecordPaymentAsync(request, ct));
    }

    [HttpGet("summary")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<FinancialSummaryDto>>> GetSummary(CancellationToken ct)
    {
        return HandleResult(await _feeService.GetFinancialSummaryAsync(ct));
    }
}

[Authorize]
public class AnnouncementsController : BaseApiController
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AnnouncementDto>>>> GetAnnouncements(CancellationToken ct)
    {
        return HandleResult(await _announcementService.GetAnnouncementsAsync(ct));
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<ActionResult<ApiResponse<AnnouncementDto>>> CreateAnnouncement([FromBody] CreateAnnouncementRequest request, CancellationToken ct)
    {
        return HandleResult(await _announcementService.CreateAnnouncementAsync(request, ct));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead(Guid id, CancellationToken ct)
    {
        return HandleResult(await _announcementService.MarkAsReadAsync(id, ct));
    }
}

[Authorize]
public class DashboardsController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("admin")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<AdminDashboardStatsDto>>> GetAdminDashboard(CancellationToken ct)
    {
        return HandleResult(await _dashboardService.GetAdminDashboardAsync(ct));
    }

    [HttpGet("teacher")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<TeacherDashboardStatsDto>>> GetTeacherDashboard(CancellationToken ct)
    {
        return HandleResult(await _dashboardService.GetTeacherDashboardAsync(ct));
    }

    [HttpGet("student")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<ApiResponse<StudentDashboardStatsDto>>> GetStudentDashboard(CancellationToken ct)
    {
        return HandleResult(await _dashboardService.GetStudentDashboardAsync(ct));
    }

    [HttpGet("parent")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<ApiResponse<ParentDashboardStatsDto>>> GetParentDashboard([FromQuery] Guid? childId, CancellationToken ct)
    {
        return HandleResult(await _dashboardService.GetParentDashboardAsync(childId, ct));
    }
}

[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("students/csv")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<IActionResult> ExportStudentsCsv([FromQuery] Guid? classSectionId, CancellationToken ct)
    {
        var bytes = await _reportService.ExportStudentsToCsvAsync(classSectionId, ct);
        return File(bytes, "text/csv", $"students_export_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("attendance/csv")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin,Teacher")]
    public async Task<IActionResult> ExportAttendanceCsv([FromQuery] Guid classSectionId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var bytes = await _reportService.ExportAttendanceToCsvAsync(classSectionId, date, ct);
        return File(bytes, "text/csv", $"attendance_{classSectionId}_{date:yyyyMMdd}.csv");
    }

    [HttpGet("invoices/csv")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<IActionResult> ExportInvoicesCsv(CancellationToken ct)
    {
        var bytes = await _reportService.ExportInvoicesToCsvAsync(ct);
        return File(bytes, "text/csv", $"invoices_export_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

[Authorize]
public class AuditLogsController : BaseApiController
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> GetLogs([FromQuery] int count = 50, CancellationToken ct = default)
    {
        return HandleResult(await _auditLogService.GetLogsAsync(count, ct));
    }
}
