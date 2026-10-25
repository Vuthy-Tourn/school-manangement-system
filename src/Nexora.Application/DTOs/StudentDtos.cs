using Nexora.Domain.Enums;

namespace Nexora.Application.DTOs;

public record StudentDto(
    Guid Id,
    Guid SchoolId,
    string AdmissionNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string? BloodGroup,
    DateTime EnrollmentDate,
    StudentStatus Status,
    string? Email,
    string? PhoneNumber,
    string? Address,
    string? ProfilePhotoUrl,
    Guid? CurrentClassSectionId,
    string? CurrentClassName,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship
);

public record CreateStudentRequest(
    string AdmissionNumber,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Gender,
    string? BloodGroup,
    DateTime EnrollmentDate,
    string? Email,
    string? PhoneNumber,
    string? Address,
    string? ProfilePhotoUrl,
    Guid? ClassSectionId,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Username,
    string? Password
);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Gender,
    string? BloodGroup,
    StudentStatus Status,
    string? Email,
    string? PhoneNumber,
    string? Address,
    string? ProfilePhotoUrl,
    Guid? ClassSectionId,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship
);

public record StudentFilter(
    string? SearchTerm = null,
    Guid? ClassSectionId = null,
    StudentStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20
);

public record ParentGuardianDto(
    Guid Id,
    Guid SchoolId,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string PhoneNumber,
    string? Occupation,
    string? Address,
    List<LinkedStudentDto> LinkedStudents
);

public record LinkedStudentDto(
    Guid StudentId,
    string FullName,
    string AdmissionNumber,
    string? ClassName,
    string Relationship,
    bool IsPrimaryContact
);

public record CreateParentRequest(
    string FirstName,
    string LastName,
    string? Email,
    string PhoneNumber,
    string? Occupation,
    string? Address,
    string? Username,
    string? Password
);

public record LinkStudentRequest(
    Guid StudentId,
    Guid GuardianId,
    string Relationship,
    bool IsPrimaryContact
);
