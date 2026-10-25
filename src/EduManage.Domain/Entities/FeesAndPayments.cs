using EduManage.Domain.Common;
using EduManage.Domain.Enums;

namespace EduManage.Domain.Entities;

public class FeeCategory : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Tuition Fee", "Registration Fee", "Uniform & Books", "Lab Fee"
    public string? Description { get; set; }
    public decimal DefaultAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Invoice : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public Guid AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty; // e.g. "INV-2026-0001"
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance => TotalAmount - PaidAmount;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public string? Notes { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? FeeCategoryId { get; set; }
    public FeeCategory? FeeCategory { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class Payment : BaseEntity, ITenantEntity
{
    public Guid SchoolId { get; set; }
    public School? School { get; set; }

    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty; // e.g. "REC-2026-0001"
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public Guid ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
}
