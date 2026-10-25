namespace EduManage.Application.DTOs;

public record TeacherDto(
    Guid Id,
    Guid SchoolId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTime HireDate,
    string? Qualification,
    string? Specialization,
    bool IsActive,
    List<TeacherSubjectAssignmentDto> Assignments
);

public record TeacherSubjectAssignmentDto(
    Guid AssignmentId,
    Guid ClassSectionId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    string SubjectCode
);

public record CreateTeacherRequest(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    DateTime HireDate,
    string? Qualification,
    string? Specialization,
    string? Username,
    string? Password
);

public record UpdateTeacherRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Qualification,
    string? Specialization,
    bool IsActive
);
