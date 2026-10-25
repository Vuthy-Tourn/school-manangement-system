using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Infrastructure.Services;

public class TeacherService : ITeacherService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public TeacherService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<TeacherDto>>> GetTeachersAsync(CancellationToken ct = default)
    {
        var teachers = await _context.Teachers
            .Include(t => t.SubjectAssignments)
                .ThenInclude(sa => sa.ClassSection)
            .Include(t => t.SubjectAssignments)
                .ThenInclude(sa => sa.Subject)
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .Select(t => new TeacherDto(
                t.Id,
                t.SchoolId,
                t.EmployeeNumber,
                t.FirstName,
                t.LastName,
                t.FullName,
                t.Email,
                t.PhoneNumber,
                t.HireDate,
                t.Qualification,
                t.Specialization,
                t.IsActive,
                t.SubjectAssignments.Select(sa => new TeacherSubjectAssignmentDto(
                    sa.Id,
                    sa.ClassSectionId,
                    sa.ClassSection != null ? sa.ClassSection.Name : string.Empty,
                    sa.SubjectId,
                    sa.Subject != null ? sa.Subject.Name : string.Empty,
                    sa.Subject != null ? sa.Subject.Code : string.Empty
                )).ToList()
            ))
            .ToListAsync(ct);

        return ApiResponse<List<TeacherDto>>.Ok(teachers);
    }

    public async Task<ApiResponse<TeacherDto>> GetTeacherByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _context.Teachers
            .Include(t => t.SubjectAssignments)
                .ThenInclude(sa => sa.ClassSection)
            .Include(t => t.SubjectAssignments)
                .ThenInclude(sa => sa.Subject)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (t == null)
            return ApiResponse<TeacherDto>.Fail("Teacher not found.");

        var dto = new TeacherDto(
            t.Id,
            t.SchoolId,
            t.EmployeeNumber,
            t.FirstName,
            t.LastName,
            t.FullName,
            t.Email,
            t.PhoneNumber,
            t.HireDate,
            t.Qualification,
            t.Specialization,
            t.IsActive,
            t.SubjectAssignments.Select(sa => new TeacherSubjectAssignmentDto(
                sa.Id,
                sa.ClassSectionId,
                sa.ClassSection != null ? sa.ClassSection.Name : string.Empty,
                sa.SubjectId,
                sa.Subject != null ? sa.Subject.Name : string.Empty,
                sa.Subject != null ? sa.Subject.Code : string.Empty
            )).ToList()
        );

        return ApiResponse<TeacherDto>.Ok(dto);
    }

    public async Task<ApiResponse<TeacherDto>> CreateTeacherAsync(CreateTeacherRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<TeacherDto>.Fail("School context required.");

        var exists = await _context.Teachers.AnyAsync(t => t.EmployeeNumber == request.EmployeeNumber, ct);
        if (exists)
            return ApiResponse<TeacherDto>.Fail($"Employee number '{request.EmployeeNumber}' already exists.");

        Guid? createdUserId = null;
        if (!string.IsNullOrWhiteSpace(request.Username) && !string.IsNullOrWhiteSpace(request.Password))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Username == request.Username, ct);
            if (userExists)
                return ApiResponse<TeacherDto>.Fail($"Username '{request.Username}' is already taken.");

            var newUser = new User
            {
                SchoolId = schoolId.Value,
                Username = request.Username,
                Email = request.Email,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Role = UserRole.Teacher,
                IsActive = true
            };
            _context.Users.Add(newUser);
            createdUserId = newUser.Id;
        }

        var teacher = new Teacher
        {
            SchoolId = schoolId.Value,
            UserId = createdUserId,
            EmployeeNumber = request.EmployeeNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            HireDate = DateTime.SpecifyKind(request.HireDate, DateTimeKind.Utc),
            Qualification = request.Qualification,
            Specialization = request.Specialization,
            IsActive = true
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.Create, nameof(Teacher), teacher.Id.ToString(), $"Created teacher {teacher.FullName} ({teacher.EmployeeNumber})", ct: ct);

        return await GetTeacherByIdAsync(teacher.Id, ct);
    }

    public async Task<ApiResponse<TeacherDto>> UpdateTeacherAsync(Guid id, UpdateTeacherRequest request, CancellationToken ct = default)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return ApiResponse<TeacherDto>.Fail("Teacher not found.");

        teacher.FirstName = request.FirstName;
        teacher.LastName = request.LastName;
        teacher.Email = request.Email;
        teacher.PhoneNumber = request.PhoneNumber;
        teacher.Qualification = request.Qualification;
        teacher.Specialization = request.Specialization;
        teacher.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Update, nameof(Teacher), teacher.Id.ToString(), $"Updated teacher {teacher.FullName}", ct: ct);

        return await GetTeacherByIdAsync(teacher.Id, ct);
    }

    public async Task<ApiResponse<bool>> DeactivateTeacherAsync(Guid id, CancellationToken ct = default)
    {
        var teacher = await _context.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return ApiResponse<bool>.Fail("Teacher not found.");

        teacher.IsActive = false;
        if (teacher.User != null)
        {
            teacher.User.IsActive = false;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Update, nameof(Teacher), teacher.Id.ToString(), $"Deactivated teacher {teacher.FullName}", ct: ct);

        return ApiResponse<bool>.Ok(true, "Teacher account deactivated.");
    }
}
