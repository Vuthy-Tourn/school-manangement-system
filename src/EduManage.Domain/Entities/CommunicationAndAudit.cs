using EduManage.Domain.Common;
using EduManage.Domain.Enums;

namespace EduManage.Domain.Entities;

public class Announcement : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementTarget TargetRole { get; set; } = AnnouncementTarget.All;
    public Guid? TargetClassSectionId { get; set; }
    public ClassSection? TargetClassSection { get; set; }

    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public bool IsArchived { get; set; }

    public Guid PublishedByUserId { get; set; }
    public User? PublishedByUser { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AnnouncementReadReceipt> ReadReceipts { get; set; } = new List<AnnouncementReadReceipt>();
}

public class AnnouncementReadReceipt : BaseEntity
{
    public Guid AnnouncementId { get; set; }
    public Announcement? Announcement { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}

public class Notification : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string? ActionUrl { get; set; }
}

public class AuditLog : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public AuditAction Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
