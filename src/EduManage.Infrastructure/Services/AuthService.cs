using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, string ipAddress, CancellationToken ct = default)
    {
        var normalizedInput = request.UsernameOrEmail.Trim().ToLowerInvariant();

        // Query user ignoring query filters for login since user may not have tenant claim yet
        var userQuery = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.School)
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.ParentProfile)
            .AsQueryable();

        if (request.SchoolId.HasValue)
        {
            userQuery = userQuery.Where(u => u.SchoolId == request.SchoolId.Value);
        }

        var user = await userQuery.FirstOrDefaultAsync(
            u => u.Username.ToLower() == normalizedInput || u.Email.ToLower() == normalizedInput,
            ct);

        if (user == null)
        {
            return ApiResponse<LoginResponse>.Fail("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            return ApiResponse<LoginResponse>.Fail("Account is deactivated. Please contact your school administrator.");
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            return ApiResponse<LoginResponse>.Fail($"Account is temporarily locked. Try again after {user.LockoutEndUtc.Value.ToLocalTime():HH:mm}.");
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            }
            await _context.SaveChangesAsync(ct);
            return ApiResponse<LoginResponse>.Fail("Invalid credentials.");
        }

        // Reset lockout
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;

        var (accessToken, jwtId, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user);
        var refreshTokenString = _jwtTokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenString,
            JwtId = jwtId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        Guid? linkedProfileId = user.Role switch
        {
            UserRole.Student => user.StudentProfile?.Id,
            UserRole.Teacher => user.TeacherProfile?.Id,
            UserRole.Parent => user.ParentProfile?.Id,
            _ => null
        };

        var profileDto = new UserProfileDto(
            user.Id,
            user.SchoolId,
            user.School?.Name ?? "Nexora School",
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Role,
            user.PhoneNumber,
            linkedProfileId
        );

        return ApiResponse<LoginResponse>.Ok(new LoginResponse(accessToken, refreshTokenString, expiresAtUtc, profileDto), "Login successful.");
    }

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, CancellationToken ct = default)
    {
        var storedToken = await _context.RefreshTokens
            .Include(r => r.User)
            .ThenInclude(u => u!.School)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct);

        if (storedToken == null || !storedToken.IsActive || storedToken.User == null)
        {
            return ApiResponse<LoginResponse>.Fail("Invalid or expired refresh token.");
        }

        storedToken.IsUsed = true;
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        storedToken.ReplacedByToken = newRefreshToken;

        var (accessToken, jwtId, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(storedToken.User);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = newRefreshToken,
            JwtId = jwtId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        });

        await _context.SaveChangesAsync(ct);

        var profileDto = new UserProfileDto(
            storedToken.User.Id,
            storedToken.User.SchoolId,
            storedToken.User.School?.Name ?? "Nexora School",
            storedToken.User.Username,
            storedToken.User.Email,
            storedToken.User.FirstName,
            storedToken.User.LastName,
            $"{storedToken.User.FirstName} {storedToken.User.LastName}".Trim(),
            storedToken.User.Role,
            storedToken.User.PhoneNumber,
            null
        );

        return ApiResponse<LoginResponse>.Ok(new LoginResponse(accessToken, newRefreshToken, expiresAtUtc, profileDto));
    }

    public async Task<ApiResponse<bool>> RevokeTokenAsync(string token, string ipAddress, CancellationToken ct = default)
    {
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (storedToken == null || !storedToken.IsActive)
            return ApiResponse<bool>.Ok(true);

        storedToken.IsRevoked = true;
        storedToken.RevokedByIp = ipAddress;
        await _context.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Token revoked successfully.");
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null)
            return ApiResponse<bool>.Fail("User not found.");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return ApiResponse<bool>.Fail("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Password changed successfully.");
    }

    public async Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
            return ApiResponse<UserProfileDto>.Fail("Unauthorized.");

        var user = await _context.Users
            .Include(u => u.School)
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.ParentProfile)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user == null)
            return ApiResponse<UserProfileDto>.Fail("User not found.");

        Guid? linkedProfileId = user.Role switch
        {
            UserRole.Student => user.StudentProfile?.Id,
            UserRole.Teacher => user.TeacherProfile?.Id,
            UserRole.Parent => user.ParentProfile?.Id,
            _ => null
        };

        var dto = new UserProfileDto(
            user.Id,
            user.SchoolId,
            user.School?.Name ?? "Nexora School",
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Role,
            user.PhoneNumber,
            linkedProfileId
        );

        return ApiResponse<UserProfileDto>.Ok(dto);
    }
}
