using EduManage.Domain.Enums;

namespace EduManage.Application.DTOs;

public record AnnouncementDto(
    Guid Id,
    Guid SchoolId,
    string Title,
    string Content,
    AnnouncementTarget TargetRole,
    Guid? TargetClassSectionId,
    string? TargetClassName,
    AnnouncementPriority Priority,
    bool IsArchived,
    DateTime PublishedAtUtc,
    string PublishedByName,
    bool IsReadByMe
);

public record CreateAnnouncementRequest(
    string Title,
    string Content,
    AnnouncementTarget TargetRole,
    Guid? TargetClassSectionId,
    AnnouncementPriority Priority
);

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTime CreatedAtUtc,
    string? ActionUrl
);

public record AuditLogDto(
    Guid Id,
    Guid SchoolId,
    Guid? UserId,
    string? UserName,
    AuditAction Action,
    string EntityName,
    string? EntityId,
    string? Details,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime TimestampUtc
);
