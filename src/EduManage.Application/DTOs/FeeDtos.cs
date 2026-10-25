using EduManage.Domain.Enums;

namespace EduManage.Application.DTOs;

public record FeeCategoryDto(
    Guid Id,
    Guid SchoolId,
    string Name,
    string? Description,
    decimal DefaultAmount,
    bool IsActive
);

public record CreateFeeCategoryRequest(
    string Name,
    string? Description,
    decimal DefaultAmount
);

public record InvoiceDto(
    Guid Id,
    Guid SchoolId,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string ClassName,
    Guid AcademicYearId,
    string AcademicYearName,
    string InvoiceNumber,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingBalance,
    InvoiceStatus Status,
    string? Notes,
    List<InvoiceItemDto> Items,
    List<PaymentDto> Payments
);

public record InvoiceItemDto(
    Guid Id,
    Guid? FeeCategoryId,
    string FeeCategoryName,
    string Description,
    decimal Amount
);

public record CreateInvoiceRequest(
    Guid StudentId,
    Guid AcademicYearId,
    DateTime DueDate,
    string? Notes,
    List<CreateInvoiceItemRequest> Items
);

public record CreateInvoiceItemRequest(
    Guid? FeeCategoryId,
    string Description,
    decimal Amount
);

public record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid StudentId,
    string StudentName,
    string ReceiptNumber,
    decimal Amount,
    DateTime PaymentDate,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    string ReceivedByName
);

public record RecordPaymentRequest(
    Guid InvoiceId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes
);

public record FinancialSummaryDto(
    decimal TotalInvoiced,
    decimal TotalCollected,
    decimal TotalOutstanding,
    int PendingInvoicesCount,
    int PaidInvoicesCount,
    int OverdueInvoicesCount
);
