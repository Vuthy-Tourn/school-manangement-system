using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Infrastructure.Services;

public class AcademicService : IAcademicService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public AcademicService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<AcademicYearDto>>> GetAcademicYearsAsync(CancellationToken ct = default)
    {
        var years = await _context.AcademicYears
            .OrderByDescending(y => y.StartDate)
            .Select(y => new AcademicYearDto(
                y.Id,
                y.SchoolId,
                y.Name,
                y.StartDate,
                y.EndDate,
                y.IsCurrent
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AcademicYearDto>>.Ok(years);
    }

    public async Task<ApiResponse<AcademicYearDto>> CreateAcademicYearAsync(CreateAcademicYearRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<AcademicYearDto>.Fail("School context required.");

        if (request.IsCurrent)
        {
            var currentYears = await _context.AcademicYears.Where(y => y.IsCurrent).ToListAsync(ct);
            foreach (var y in currentYears) y.IsCurrent = false;
        }

        var year = new AcademicYear
        {
            SchoolId = schoolId.Value,
            Name = request.Name,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
            IsCurrent = request.IsCurrent
        };

        _context.AcademicYears.Add(year);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<AcademicYearDto>.Ok(new AcademicYearDto(
            year.Id,
            year.SchoolId,
            year.Name,
            year.StartDate,
            year.EndDate,
            year.IsCurrent
        ));
    }

    public async Task<ApiResponse<List<SemesterDto>>> GetSemestersAsync(Guid academicYearId, CancellationToken ct = default)
    {
        var semesters = await _context.Semesters
            .Where(s => s.AcademicYearId == academicYearId)
            .OrderBy(s => s.StartDate)
            .Select(s => new SemesterDto(
                s.Id,
                s.SchoolId,
                s.AcademicYearId,
                s.Name,
                s.StartDate,
                s.EndDate,
                s.IsCurrent
            ))
            .ToListAsync(ct);

        return ApiResponse<List<SemesterDto>>.Ok(semesters);
    }

    public async Task<ApiResponse<List<GradeLevelDto>>> GetGradeLevelsAsync(CancellationToken ct = default)
    {
        var levels = await _context.GradeLevels
            .OrderBy(g => g.LevelOrder)
            .Select(g => new GradeLevelDto(g.Id, g.SchoolId, g.Name, g.LevelOrder))
            .ToListAsync(ct);

        return ApiResponse<List<GradeLevelDto>>.Ok(levels);
    }

    public async Task<ApiResponse<List<ClassSectionDto>>> GetClassSectionsAsync(Guid? academicYearId = null, CancellationToken ct = default)
    {
        var query = _context.ClassSections
            .Include(c => c.AcademicYear)
            .Include(c => c.GradeLevel)
            .Include(c => c.ClassTeacher)
            .Include(c => c.Enrollments)
            .AsNoTracking()
            .AsQueryable();

        if (academicYearId.HasValue)
        {
            query = query.Where(c => c.AcademicYearId == academicYearId.Value);
        }

        var classes = await query
            .OrderBy(c => c.Name)
            .Select(c => new ClassSectionDto(
                c.Id,
                c.SchoolId,
                c.AcademicYearId,
                c.AcademicYear != null ? c.AcademicYear.Name : string.Empty,
                c.GradeLevelId,
                c.GradeLevel != null ? c.GradeLevel.Name : string.Empty,
                c.Name,
                c.RoomNumber,
                c.Capacity,
                c.ClassTeacherId,
                c.ClassTeacher != null ? c.ClassTeacher.FullName : null,
                c.Enrollments.Count(e => e.Status == EnrollmentStatus.Enrolled)
            ))
            .ToListAsync(ct);

        return ApiResponse<List<ClassSectionDto>>.Ok(classes);
    }

    public async Task<ApiResponse<ClassSectionDto>> CreateClassSectionAsync(CreateClassSectionRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<ClassSectionDto>.Fail("School context required.");

        var exists = await _context.ClassSections.AnyAsync(
            c => c.AcademicYearId == request.AcademicYearId && c.Name.ToLower() == request.Name.ToLower(), ct);
        if (exists)
            return ApiResponse<ClassSectionDto>.Fail($"Class section '{request.Name}' already exists for this academic year.");

        var section = new ClassSection
        {
            SchoolId = schoolId.Value,
            AcademicYearId = request.AcademicYearId,
            GradeLevelId = request.GradeLevelId,
            Name = request.Name,
            RoomNumber = request.RoomNumber,
            Capacity = request.Capacity,
            ClassTeacherId = request.ClassTeacherId
        };

        _context.ClassSections.Add(section);
        await _context.SaveChangesAsync(ct);

        return (await GetClassSectionsAsync(section.AcademicYearId, ct)).Data!
            .Find(c => c.Id == section.Id) is { } dto
            ? ApiResponse<ClassSectionDto>.Ok(dto)
            : ApiResponse<ClassSectionDto>.Fail("Class section created but could not retrieve.");
    }

    public async Task<ApiResponse<List<SubjectDto>>> GetSubjectsAsync(CancellationToken ct = default)
    {
        var subjects = await _context.Subjects
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.SchoolId, s.Name, s.Code, s.Description, s.CreditHours))
            .ToListAsync(ct);

        return ApiResponse<List<SubjectDto>>.Ok(subjects);
    }

    public async Task<ApiResponse<SubjectDto>> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<SubjectDto>.Fail("School context required.");

        var exists = await _context.Subjects.AnyAsync(s => s.Code.ToLower() == request.Code.ToLower(), ct);
        if (exists)
            return ApiResponse<SubjectDto>.Fail($"Subject code '{request.Code}' already exists.");

        var subject = new Subject
        {
            SchoolId = schoolId.Value,
            Name = request.Name,
            Code = request.Code.ToUpperInvariant(),
            Description = request.Description,
            CreditHours = request.CreditHours
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<SubjectDto>.Ok(new SubjectDto(subject.Id, subject.SchoolId, subject.Name, subject.Code, subject.Description, subject.CreditHours));
    }

    public async Task<ApiResponse<bool>> AssignTeacherToSubjectAsync(AssignTeacherSubjectRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<bool>.Fail("School context required.");

        var exists = await _context.SubjectClassAssignments.AnyAsync(
            sa => sa.ClassSectionId == request.ClassSectionId && sa.SubjectId == request.SubjectId && sa.TeacherId == request.TeacherId, ct);
        if (exists)
            return ApiResponse<bool>.Ok(true, "Teacher already assigned.");

        var assignment = new SubjectClassAssignment
        {
            SchoolId = schoolId.Value,
            ClassSectionId = request.ClassSectionId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId
        };

        _context.SubjectClassAssignments.Add(assignment);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Subject assignment saved successfully.");
    }

    public async Task<ApiResponse<StudentEnrollmentDto>> EnrollStudentAsync(EnrollStudentRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<StudentEnrollmentDto>.Fail("School context required.");

        var student = await _context.Students.FindAsync(new object[] { request.StudentId }, ct);
        if (student == null)
            return ApiResponse<StudentEnrollmentDto>.Fail("Student not found.");

        var existing = await _context.StudentEnrollments.FirstOrDefaultAsync(
            e => e.StudentId == request.StudentId && e.AcademicYearId == request.AcademicYearId, ct);
        if (existing != null)
        {
            existing.ClassSectionId = request.ClassSectionId;
            existing.RollNumber = request.RollNumber;
            existing.Status = EnrollmentStatus.Enrolled;
        }
        else
        {
            var enrollment = new StudentEnrollment
            {
                SchoolId = schoolId.Value,
                StudentId = request.StudentId,
                ClassSectionId = request.ClassSectionId,
                AcademicYearId = request.AcademicYearId,
                RollNumber = request.RollNumber,
                EnrollmentDate = DateTime.UtcNow,
                Status = EnrollmentStatus.Enrolled
            };
            _context.StudentEnrollments.Add(enrollment);
        }

        student.CurrentClassSectionId = request.ClassSectionId;
        await _context.SaveChangesAsync(ct);

        var classSection = await _context.ClassSections.FindAsync(new object[] { request.ClassSectionId }, ct);
        var year = await _context.AcademicYears.FindAsync(new object[] { request.AcademicYearId }, ct);

        return ApiResponse<StudentEnrollmentDto>.Ok(new StudentEnrollmentDto(
            Guid.NewGuid(),
            student.Id,
            student.FullName,
            student.AdmissionNumber,
            request.ClassSectionId,
            classSection?.Name ?? string.Empty,
            request.AcademicYearId,
            year?.Name ?? string.Empty,
            request.RollNumber,
            EnrollmentStatus.Enrolled,
            DateTime.UtcNow
        ), "Student enrolled successfully.");
    }

    public async Task<ApiResponse<bool>> PromoteStudentAsync(PromoteStudentRequest request, CancellationToken ct = default)
    {
        var student = await _context.Students.FindAsync(new object[] { request.StudentId }, ct);
        if (student == null)
            return ApiResponse<bool>.Fail("Student not found.");

        // Mark previous enrollment as promoted
        var prevEnrollment = await _context.StudentEnrollments
            .Where(e => e.StudentId == request.StudentId && e.Status == EnrollmentStatus.Enrolled)
            .OrderByDescending(e => e.EnrollmentDate)
            .FirstOrDefaultAsync(ct);

        if (prevEnrollment != null)
        {
            prevEnrollment.Status = EnrollmentStatus.Promoted;
            prevEnrollment.PromotedAtUtc = DateTime.UtcNow;
        }

        var newEnrollment = new StudentEnrollment
        {
            SchoolId = student.SchoolId,
            StudentId = student.Id,
            ClassSectionId = request.TargetClassSectionId,
            AcademicYearId = request.TargetAcademicYearId,
            RollNumber = request.RollNumber,
            EnrollmentDate = DateTime.UtcNow,
            Status = EnrollmentStatus.Enrolled
        };

        _context.StudentEnrollments.Add(newEnrollment);
        student.CurrentClassSectionId = request.TargetClassSectionId;

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Update, nameof(Student), student.Id.ToString(), $"Promoted student {student.FullName}", ct: ct);

        return ApiResponse<bool>.Ok(true, "Student promoted successfully.");
    }
}
