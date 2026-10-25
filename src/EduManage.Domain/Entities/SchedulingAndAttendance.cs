using EduManage.Domain.Common;
using EduManage.Domain.Enums;

namespace EduManage.Domain.Entities;

public class TimetableEntry : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid ClassSectionId { get; set; }
    public ClassSection? ClassSection { get; set; }

    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public Guid TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? RoomNumber { get; set; }
    public DateTime? EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
}

public class AttendanceRecord : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid ClassSectionId { get; set; }
    public ClassSection? ClassSection { get; set; }

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }

    public Guid RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CorrectedByUserId { get; set; }
    public User? CorrectedByUser { get; set; }
    public DateTime? CorrectedAtUtc { get; set; }
    public string? CorrectionReason { get; set; }
}
