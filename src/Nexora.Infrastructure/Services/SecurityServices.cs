using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Nexora.Application.Common;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Nexora.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
    }
}

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, string JwtId, DateTime ExpiresAtUtc) GenerateAccessToken(User user)
    {
        var secret = _configuration["Jwt:Secret"] ?? "NexoraSchoolSuperSecretKey2026!MustBeVeryLongAndSecureForHmacSha256";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtId = Guid.NewGuid().ToString();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var min) ? min : 120);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Username),
            new("school_id", user.SchoolId.ToString()),
            new("role", user.Role.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("first_name", user.FirstName),
            new("last_name", user.LastName)
        };

        if (user.StudentProfile != null)
        {
            claims.Add(new("student_id", user.StudentProfile.Id.ToString()));
        }
        else if (user.TeacherProfile != null)
        {
            claims.Add(new("teacher_id", user.TeacherProfile.Id.ToString()));
        }
        else if (user.ParentProfile != null)
        {
            claims.Add(new("parent_id", user.ParentProfile.Id.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            Issuer = _configuration["Jwt:Issuer"] ?? "NexoraApi",
            Audience = _configuration["Jwt:Audience"] ?? "NexoraClients",
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(token), jwtId, expiresAtUtc);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
