using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Services;

public class StudentService : IStudentService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public StudentService(
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

    public async Task<ApiResponse<PaginatedResult<StudentDto>>> GetStudentsAsync(StudentFilter filter, CancellationToken ct = default)
    {
        var query = _context.Students
            .Include(s => s.CurrentClassSection)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(term) ||
                s.LastName.ToLower().Contains(term) ||
                s.AdmissionNumber.ToLower().Contains(term) ||
                (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        if (filter.ClassSectionId.HasValue)
        {
            query = query.Where(s => s.CurrentClassSectionId == filter.ClassSectionId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == filter.Status.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new StudentDto(
                s.Id,
                s.SchoolId,
                s.AdmissionNumber,
                s.FirstName,
                s.LastName,
                s.FullName,
                s.DateOfBirth,
                s.Gender,
                s.BloodGroup,
                s.EnrollmentDate,
                s.Status,
                s.Email,
                s.PhoneNumber,
                s.Address,
                s.ProfilePhotoUrl,
                s.CurrentClassSectionId,
                s.CurrentClassSection != null ? s.CurrentClassSection.Name : null,
                s.EmergencyContactName,
                s.EmergencyContactPhone,
                s.EmergencyContactRelationship
            ))
            .ToListAsync(ct);

        var paginated = new PaginatedResult<StudentDto>(items, totalCount, filter.PageNumber, filter.PageSize);
        return ApiResponse<PaginatedResult<StudentDto>>.Ok(paginated);
    }

    public async Task<ApiResponse<StudentDto>> GetStudentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _context.Students
            .Include(s => s.CurrentClassSection)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (s == null)
            return ApiResponse<StudentDto>.Fail("Student not found.");

        var dto = new StudentDto(
            s.Id,
            s.SchoolId,
            s.AdmissionNumber,
            s.FirstName,
            s.LastName,
            s.FullName,
            s.DateOfBirth,
            s.Gender,
            s.BloodGroup,
            s.EnrollmentDate,
            s.Status,
            s.Email,
            s.PhoneNumber,
            s.Address,
            s.ProfilePhotoUrl,
            s.CurrentClassSectionId,
            s.CurrentClassSection != null ? s.CurrentClassSection.Name : null,
            s.EmergencyContactName,
            s.EmergencyContactPhone,
            s.EmergencyContactRelationship
        );

        return ApiResponse<StudentDto>.Ok(dto);
    }

    public async Task<ApiResponse<StudentDto>> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<StudentDto>.Fail("School context is required.");

        var existingAdmission = await _context.Students.AnyAsync(s => s.AdmissionNumber == request.AdmissionNumber, ct);
        if (existingAdmission)
            return ApiResponse<StudentDto>.Fail($"Admission number '{request.AdmissionNumber}' already exists.");

        Guid? createdUserId = null;
        if (!string.IsNullOrWhiteSpace(request.Username) && !string.IsNullOrWhiteSpace(request.Password))
        {
            var existingUsername = await _context.Users.AnyAsync(u => u.Username == request.Username, ct);
            if (existingUsername)
                return ApiResponse<StudentDto>.Fail($"Username '{request.Username}' is already taken.");

            var newUser = new User
            {
                SchoolId = schoolId.Value,
                Username = request.Username,
                Email = request.Email ?? $"{request.Username.ToLower()}@nexora.school",
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Role = UserRole.Student,
                IsActive = true
            };
            _context.Users.Add(newUser);
            createdUserId = newUser.Id;
        }

        var student = new Student
        {
            SchoolId = schoolId.Value,
            UserId = createdUserId,
            AdmissionNumber = request.AdmissionNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = DateTime.SpecifyKind(request.DateOfBirth, DateTimeKind.Utc),
            Gender = request.Gender,
            BloodGroup = request.BloodGroup,
            EnrollmentDate = DateTime.SpecifyKind(request.EnrollmentDate, DateTimeKind.Utc),
            Status = StudentStatus.Active,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            ProfilePhotoUrl = request.ProfilePhotoUrl,
            CurrentClassSectionId = request.ClassSectionId,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
            EmergencyContactRelationship = request.EmergencyContactRelationship
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.Create, nameof(Student), student.Id.ToString(), $"Created student {student.FullName} ({student.AdmissionNumber})", ct: ct);

        return await GetStudentByIdAsync(student.Id, ct);
    }

    public async Task<ApiResponse<StudentDto>> UpdateStudentAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return ApiResponse<StudentDto>.Fail("Student not found.");

        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.DateOfBirth = DateTime.SpecifyKind(request.DateOfBirth, DateTimeKind.Utc);
        student.Gender = request.Gender;
        student.BloodGroup = request.BloodGroup;
        student.Status = request.Status;
        student.Email = request.Email;
        student.PhoneNumber = request.PhoneNumber;
        student.Address = request.Address;
        student.ProfilePhotoUrl = request.ProfilePhotoUrl;
        student.CurrentClassSectionId = request.ClassSectionId;
        student.EmergencyContactName = request.EmergencyContactName;
        student.EmergencyContactPhone = request.EmergencyContactPhone;
        student.EmergencyContactRelationship = request.EmergencyContactRelationship;

        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.Update, nameof(Student), student.Id.ToString(), $"Updated student {student.FullName}", ct: ct);

        return await GetStudentByIdAsync(student.Id, ct);
    }

    public async Task<ApiResponse<bool>> DeactivateStudentAsync(Guid id, CancellationToken ct = default)
    {
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return ApiResponse<bool>.Fail("Student not found.");

        student.Status = StudentStatus.Inactive;
        if (student.User != null)
        {
            student.User.IsActive = false;
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Update, nameof(Student), student.Id.ToString(), $"Deactivated student {student.FullName}", ct: ct);

        return ApiResponse<bool>.Ok(true, "Student deactivated successfully.");
    }

    public async Task<ApiResponse<bool>> LinkParentGuardianAsync(LinkStudentRequest request, CancellationToken ct = default)
    {
        var exists = await _context.StudentGuardians.AnyAsync(sg => sg.StudentId == request.StudentId && sg.GuardianId == request.GuardianId, ct);
        if (exists)
            return ApiResponse<bool>.Ok(true, "Guardian already linked.");

        var link = new StudentGuardian
        {
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            Relationship = request.Relationship,
            IsPrimaryContact = request.IsPrimaryContact
        };

        _context.StudentGuardians.Add(link);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Guardian linked successfully.");
    }

    public async Task<ApiResponse<List<ParentGuardianDto>>> GetStudentGuardiansAsync(Guid studentId, CancellationToken ct = default)
    {
        var guardians = await _context.StudentGuardians
            .Where(sg => sg.StudentId == studentId)
            .Include(sg => sg.Guardian)
            .Select(sg => new ParentGuardianDto(
                sg.Guardian!.Id,
                sg.Guardian.SchoolId,
                sg.Guardian.FirstName,
                sg.Guardian.LastName,
                sg.Guardian.FullName,
                sg.Guardian.Email,
                sg.Guardian.PhoneNumber,
                sg.Guardian.Occupation,
                sg.Guardian.Address,
                new List<LinkedStudentDto>()
            ))
            .ToListAsync(ct);

        return ApiResponse<List<ParentGuardianDto>>.Ok(guardians);
    }
}
