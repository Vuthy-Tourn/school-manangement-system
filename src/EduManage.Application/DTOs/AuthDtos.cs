using EduManage.Domain.Enums;

namespace EduManage.Application.DTOs;

public record LoginRequest(string UsernameOrEmail, string Password, Guid? SchoolId = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserProfileDto User
);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserProfileDto(
    Guid Id,
    Guid SchoolId,
    string SchoolName,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserRole Role,
    string? PhoneNumber,
    Guid? LinkedProfileId
);

public record CreateUserRequest(
    Guid SchoolId,
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role,
    string? PhoneNumber
);

public record UserDto(
    Guid Id,
    Guid SchoolId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserRole Role,
    string? PhoneNumber,
    bool IsActive,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc
);
