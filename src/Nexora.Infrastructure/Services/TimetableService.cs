using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Nexora.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Services;

public class TimetableService : ITimetableService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TimetableService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetClassTimetableAsync(Guid classSectionId, CancellationToken ct = default)
    {
        var entries = await _context.TimetableEntries
            .Where(t => t.ClassSectionId == classSectionId)
            .Include(t => t.ClassSection)
            .Include(t => t.Subject)
            .Include(t => t.Teacher)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .Select(t => new TimetableEntryDto(
                t.Id,
                t.SchoolId,
                t.ClassSectionId,
                t.ClassSection != null ? t.ClassSection.Name : string.Empty,
                t.SubjectId,
                t.Subject != null ? t.Subject.Name : string.Empty,
                t.Subject != null ? t.Subject.Code : string.Empty,
                t.TeacherId,
                t.Teacher != null ? t.Teacher.FullName : string.Empty,
                t.DayOfWeek,
                t.StartTime,
                t.EndTime,
                t.RoomNumber
            ))
            .ToListAsync(ct);

        return ApiResponse<List<TimetableEntryDto>>.Ok(entries);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId, CancellationToken ct = default)
    {
        var entries = await _context.TimetableEntries
            .Where(t => t.TeacherId == teacherId)
            .Include(t => t.ClassSection)
            .Include(t => t.Subject)
            .Include(t => t.Teacher)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .Select(t => new TimetableEntryDto(
                t.Id,
                t.SchoolId,
                t.ClassSectionId,
                t.ClassSection != null ? t.ClassSection.Name : string.Empty,
                t.SubjectId,
                t.Subject != null ? t.Subject.Name : string.Empty,
                t.Subject != null ? t.Subject.Code : string.Empty,
                t.TeacherId,
                t.Teacher != null ? t.Teacher.FullName : string.Empty,
                t.DayOfWeek,
                t.StartTime,
                t.EndTime,
                t.RoomNumber
            ))
            .ToListAsync(ct);

        return ApiResponse<List<TimetableEntryDto>>.Ok(entries);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _context.Students.FindAsync(new object[] { studentId }, ct);
        if (student == null || !student.CurrentClassSectionId.HasValue)
            return ApiResponse<List<TimetableEntryDto>>.Ok(new List<TimetableEntryDto>());

        return await GetClassTimetableAsync(student.CurrentClassSectionId.Value, ct);
    }

    public async Task<TimetableConflictResult> CheckConflictsAsync(CreateTimetableEntryRequest request, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.TimetableEntries
            .Include(t => t.ClassSection)
            .Include(t => t.Teacher)
            .Where(t => t.DayOfWeek == request.DayOfWeek);

        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        // Time overlap condition: (StartA < EndB) && (EndA > StartB)
        var overlapping = await query.Where(t =>
            request.StartTime < t.EndTime && request.EndTime > t.StartTime).ToListAsync(ct);

        // 1. Check Class overlap
        var classConflict = overlapping.FirstOrDefault(t => t.ClassSectionId == request.ClassSectionId);
        if (classConflict != null)
        {
            return new TimetableConflictResult(true,
                $"Class '{classConflict.ClassSection?.Name}' already has a scheduled slot from {classConflict.StartTime:hh\\:mm} to {classConflict.EndTime:hh\\:mm}.");
        }

        // 2. Check Teacher overlap
        var teacherConflict = overlapping.FirstOrDefault(t => t.TeacherId == request.TeacherId);
        if (teacherConflict != null)
        {
            return new TimetableConflictResult(true,
                $"Teacher '{teacherConflict.Teacher?.FullName}' is already booked for class '{teacherConflict.ClassSection?.Name}' from {teacherConflict.StartTime:hh\\:mm} to {teacherConflict.EndTime:hh\\:mm}.");
        }

        // 3. Check Room overlap
        if (!string.IsNullOrWhiteSpace(request.RoomNumber))
        {
            var roomConflict = overlapping.FirstOrDefault(t =>
                string.Equals(t.RoomNumber, request.RoomNumber, StringComparison.OrdinalIgnoreCase));
            if (roomConflict != null)
            {
                return new TimetableConflictResult(true,
                    $"Room '{request.RoomNumber}' is already occupied by class '{roomConflict.ClassSection?.Name}' from {roomConflict.StartTime:hh\\:mm} to {roomConflict.EndTime:hh\\:mm}.");
            }
        }

        return new TimetableConflictResult(false, null);
    }

    public async Task<ApiResponse<TimetableEntryDto>> CreateEntryAsync(CreateTimetableEntryRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<TimetableEntryDto>.Fail("School context required.");

        var conflict = await CheckConflictsAsync(request, null, ct);
        if (conflict.HasConflict)
        {
            return ApiResponse<TimetableEntryDto>.Fail(conflict.ConflictReason ?? "Timetable scheduling conflict detected.");
        }

        var entry = new TimetableEntry
        {
            SchoolId = schoolId.Value,
            ClassSectionId = request.ClassSectionId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RoomNumber = request.RoomNumber
        };

        _context.TimetableEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        var classSection = await _context.ClassSections.FindAsync(new object[] { request.ClassSectionId }, ct);
        var subject = await _context.Subjects.FindAsync(new object[] { request.SubjectId }, ct);
        var teacher = await _context.Teachers.FindAsync(new object[] { request.TeacherId }, ct);

        return ApiResponse<TimetableEntryDto>.Ok(new TimetableEntryDto(
            entry.Id,
            entry.SchoolId,
            entry.ClassSectionId,
            classSection?.Name ?? string.Empty,
            entry.SubjectId,
            subject?.Name ?? string.Empty,
            subject?.Code ?? string.Empty,
            entry.TeacherId,
            teacher?.FullName ?? string.Empty,
            entry.DayOfWeek,
            entry.StartTime,
            entry.EndTime,
            entry.RoomNumber
        ), "Timetable slot added successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteEntryAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.TimetableEntries.FindAsync(new object[] { id }, ct);
        if (entry == null)
            return ApiResponse<bool>.Fail("Timetable entry not found.");

        _context.TimetableEntries.Remove(entry);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Timetable slot deleted.");
    }
}
