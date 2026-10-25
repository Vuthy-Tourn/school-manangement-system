using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? SchoolId
    {
        get
        {
            var schoolClaim = User?.FindFirstValue("school_id");
            if (Guid.TryParse(schoolClaim, out var id))
                return id;

            // Optional check header "X-School-Id" if superadmin or onboarding
            var headerVal = _httpContextAccessor.HttpContext?.Request.Headers["X-School-Id"].FirstOrDefault();
            return Guid.TryParse(headerVal, out var headerId) ? headerId : null;
        }
    }

    public string? Username => User?.FindFirstValue(ClaimTypes.Name) ?? User?.FindFirstValue(JwtRegisteredClaimNames.Name);

    public UserRole? Role
    {
        get
        {
            var roleClaim = User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role");
            return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : null;
        }
    }

    public bool IsSuperAdmin => Role == UserRole.SuperAdmin;
}

public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(AuditAction action, string entityName, string? entityId, string? details, string? oldValues = null, string? newValues = null, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue) return;

        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        var log = new AuditLog
        {
            SchoolId = schoolId.Value,
            UserId = _currentUserService.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ip,
            TimestampUtc = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ApiResponse<List<AuditLogDto>>> GetLogsAsync(int count = 50, CancellationToken ct = default)
    {
        var logs = await _context.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.TimestampUtc)
            .Take(count)
            .Select(a => new AuditLogDto(
                a.Id,
                a.SchoolId,
                a.UserId,
                a.User != null ? a.User.Username : "System",
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.OldValues,
                a.NewValues,
                a.IpAddress,
                a.TimestampUtc
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AuditLogDto>>.Ok(logs);
    }
}
