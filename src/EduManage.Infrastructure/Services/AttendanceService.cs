using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Infrastructure.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public AttendanceService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetClassAttendanceAsync(Guid classSectionId, DateOnly date, CancellationToken ct = default)
    {
        var records = await _context.AttendanceRecords
            .Where(a => a.ClassSectionId == classSectionId && a.Date == date)
            .Include(a => a.ClassSection)
            .Include(a => a.Student)
            .Include(a => a.RecordedByUser)
            .Include(a => a.CorrectedByUser)
            .OrderBy(a => a.Student!.LastName)
            .ThenBy(a => a.Student!.FirstName)
            .Select(a => new AttendanceRecordDto(
                a.Id,
                a.SchoolId,
                a.ClassSectionId,
                a.ClassSection != null ? a.ClassSection.Name : string.Empty,
                a.StudentId,
                a.Student != null ? a.Student.FullName : string.Empty,
                a.Student != null ? a.Student.AdmissionNumber : string.Empty,
                a.Date,
                a.Status,
                a.Remarks,
                a.RecordedAtUtc,
                a.RecordedByUser != null ? a.RecordedByUser.FullName : null,
                a.CorrectedAtUtc,
                a.CorrectedByUser != null ? a.CorrectedByUser.FullName : null,
                a.CorrectionReason
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records);
    }

    public async Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        var userId = _currentUserService.UserId ?? Guid.Empty;
        if (!schoolId.HasValue)
            return ApiResponse<bool>.Fail("School context required.");

        var existingRecords = await _context.AttendanceRecords
            .Where(a => a.ClassSectionId == request.ClassSectionId && a.Date == request.Date)
            .ToListAsync(ct);

        var existingMap = existingRecords.ToDictionary(r => r.StudentId);

        foreach (var item in request.Records)
        {
            if (existingMap.TryGetValue(item.StudentId, out var record))
            {
                // Update existing record if status changed
                if (record.Status != item.Status || record.Remarks != item.Remarks)
                {
                    record.Status = item.Status;
                    record.Remarks = item.Remarks;
                    record.CorrectedByUserId = userId;
                    record.CorrectedAtUtc = DateTime.UtcNow;
                    record.CorrectionReason = "Updated during attendance recording";
                }
            }
            else
            {
                // Create new attendance record
                var newRecord = new AttendanceRecord
                {
                    SchoolId = schoolId.Value,
                    ClassSectionId = request.ClassSectionId,
                    StudentId = item.StudentId,
                    Date = request.Date,
                    Status = item.Status,
                    Remarks = item.Remarks,
                    RecordedByUserId = userId,
                    RecordedAtUtc = DateTime.UtcNow
                };
                _context.AttendanceRecords.Add(newRecord);
            }
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Create, nameof(AttendanceRecord), request.ClassSectionId.ToString(), $"Recorded attendance for class on {request.Date}", ct: ct);

        return ApiResponse<bool>.Ok(true, "Attendance recorded successfully.");
    }

    public async Task<ApiResponse<bool>> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request, CancellationToken ct = default)
    {
        var record = await _context.AttendanceRecords.Include(a => a.Student).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (record == null)
            return ApiResponse<bool>.Fail("Attendance record not found.");

        var oldStatus = record.Status;
        record.Status = request.Status;
        record.Remarks = request.Remarks ?? record.Remarks;
        record.CorrectedByUserId = _currentUserService.UserId;
        record.CorrectedAtUtc = DateTime.UtcNow;
        record.CorrectionReason = request.Reason;

        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.AttendanceCorrection, nameof(AttendanceRecord), record.Id.ToString(),
            $"Corrected attendance for {record.Student?.FullName} from {oldStatus} to {request.Status}. Reason: {request.Reason}",
            oldValues: oldStatus.ToString(),
            newValues: request.Status.ToString(),
            ct: ct);

        return ApiResponse<bool>.Ok(true, "Attendance corrected successfully.");
    }

    public async Task<ApiResponse<AttendanceSummaryDto>> GetClassAttendanceSummaryAsync(Guid classSectionId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var query = _context.AttendanceRecords.Where(a => a.ClassSectionId == classSectionId);

        if (fromDate.HasValue) query = query.Where(a => a.Date >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.Date <= toDate.Value);

        var list = await query.ToListAsync(ct);

        int total = list.Count;
        int present = list.Count(a => a.Status == AttendanceStatus.Present);
        int absent = list.Count(a => a.Status == AttendanceStatus.Absent);
        int late = list.Count(a => a.Status == AttendanceStatus.Late);
        int excused = list.Count(a => a.Status == AttendanceStatus.Excused);

        decimal percentage = total > 0
            ? Math.Round(((present + (late * 0.5m) + excused) / (decimal)total) * 100m, 2)
            : 0m;

        return ApiResponse<AttendanceSummaryDto>.Ok(new AttendanceSummaryDto(total, present, absent, late, excused, percentage));
    }

    public async Task<ApiResponse<StudentAttendanceStatsDto>> GetStudentAttendanceStatsAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _context.Students.FindAsync(new object[] { studentId }, ct);
        if (student == null)
            return ApiResponse<StudentAttendanceStatsDto>.Fail("Student not found.");

        var list = await _context.AttendanceRecords
            .Where(a => a.StudentId == studentId)
            .ToListAsync(ct);

        int total = list.Count;
        int present = list.Count(a => a.Status == AttendanceStatus.Present);
        int absent = list.Count(a => a.Status == AttendanceStatus.Absent);
        int late = list.Count(a => a.Status == AttendanceStatus.Late);
        int excused = list.Count(a => a.Status == AttendanceStatus.Excused);

        decimal percentage = total > 0
            ? Math.Round(((present + (late * 0.5m) + excused) / (decimal)total) * 100m, 2)
            : 0m;

        return ApiResponse<StudentAttendanceStatsDto>.Ok(new StudentAttendanceStatsDto(
            student.Id,
            student.FullName,
            student.AdmissionNumber,
            total,
            present,
            absent,
            late,
            excused,
            percentage
        ));
    }
}
