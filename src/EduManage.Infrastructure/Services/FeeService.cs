using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using EduManage.Domain.Entities;
using EduManage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduManage.Infrastructure.Services;

public class FeeService : IFeeService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public FeeService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<FeeCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var list = await _context.FeeCategories
            .OrderBy(c => c.Name)
            .Select(c => new FeeCategoryDto(c.Id, c.SchoolId, c.Name, c.Description, c.DefaultAmount, c.IsActive))
            .ToListAsync(ct);

        return ApiResponse<List<FeeCategoryDto>>.Ok(list);
    }

    public async Task<ApiResponse<FeeCategoryDto>> CreateCategoryAsync(CreateFeeCategoryRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<FeeCategoryDto>.Fail("School context required.");

        var category = new FeeCategory
        {
            SchoolId = schoolId.Value,
            Name = request.Name,
            Description = request.Description,
            DefaultAmount = request.DefaultAmount,
            IsActive = true
        };

        _context.FeeCategories.Add(category);
        await _context.SaveChangesAsync(ct);

        return ApiResponse<FeeCategoryDto>.Ok(new FeeCategoryDto(category.Id, category.SchoolId, category.Name, category.Description, category.DefaultAmount, category.IsActive));
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.Invoices
            .Include(i => i.Student)
                .ThenInclude(s => s!.CurrentClassSection)
            .Include(i => i.AcademicYear)
            .Include(i => i.Items)
                .ThenInclude(it => it.FeeCategory)
            .Include(i => i.Payments)
                .ThenInclude(p => p.ReceivedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (studentId.HasValue)
        {
            query = query.Where(i => i.StudentId == studentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Select(i => new InvoiceDto(
                i.Id,
                i.SchoolId,
                i.StudentId,
                i.Student != null ? i.Student.FullName : string.Empty,
                i.Student != null ? i.Student.AdmissionNumber : string.Empty,
                i.Student != null && i.Student.CurrentClassSection != null ? i.Student.CurrentClassSection.Name : string.Empty,
                i.AcademicYearId,
                i.AcademicYear != null ? i.AcademicYear.Name : string.Empty,
                i.InvoiceNumber,
                i.IssueDate,
                i.DueDate,
                i.TotalAmount,
                i.PaidAmount,
                i.RemainingBalance,
                i.Status,
                i.Notes,
                i.Items.Select(item => new InvoiceItemDto(
                    item.Id,
                    item.FeeCategoryId,
                    item.FeeCategory != null ? item.FeeCategory.Name : "Other",
                    item.Description,
                    item.Amount
                )).ToList(),
                i.Payments.Select(p => new PaymentDto(
                    p.Id,
                    p.InvoiceId,
                    i.InvoiceNumber,
                    p.StudentId,
                    i.Student != null ? i.Student.FullName : string.Empty,
                    p.ReceiptNumber,
                    p.Amount,
                    p.PaymentDate,
                    p.PaymentMethod,
                    p.ReferenceNumber,
                    p.Notes,
                    p.ReceivedByUser != null ? p.ReceivedByUser.FullName : "Admin"
                )).ToList()
            ))
            .ToListAsync(ct);

        return ApiResponse<List<InvoiceDto>>.Ok(invoices);
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var list = (await GetInvoicesAsync(null, null, ct)).Data;
        var invoice = list?.FirstOrDefault(i => i.Id == id);
        return invoice != null
            ? ApiResponse<InvoiceDto>.Ok(invoice)
            : ApiResponse<InvoiceDto>.Fail("Invoice not found.");
    }

    public async Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<InvoiceDto>.Fail("School context required.");

        var student = await _context.Students.FindAsync(new object[] { request.StudentId }, ct);
        if (student == null)
            return ApiResponse<InvoiceDto>.Fail("Student not found.");

        var count = await _context.Invoices.CountAsync(ct) + 1;
        var invoiceNumber = $"INV-{DateTime.UtcNow.Year}-{count:D5}";

        decimal totalAmount = request.Items.Sum(i => i.Amount);

        var invoice = new Invoice
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            InvoiceNumber = invoiceNumber,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc),
            TotalAmount = totalAmount,
            PaidAmount = 0m,
            Status = InvoiceStatus.Pending,
            Notes = request.Notes
        };

        foreach (var item in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                FeeCategoryId = item.FeeCategoryId,
                Description = item.Description,
                Amount = item.Amount
            });
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.Create, nameof(Invoice), invoice.Id.ToString(), $"Created invoice {invoice.InvoiceNumber} for {student.FullName} amount ${totalAmount}", ct: ct);

        return await GetInvoiceByIdAsync(invoice.Id, ct);
    }

    public async Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        var userId = _currentUserService.UserId ?? Guid.Empty;
        if (!schoolId.HasValue)
            return ApiResponse<PaymentDto>.Fail("School context required.");

        var invoice = await _context.Invoices
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);

        if (invoice == null)
            return ApiResponse<PaymentDto>.Fail("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Paid)
            return ApiResponse<PaymentDto>.Fail("Invoice is already fully paid.");

        if (request.Amount <= 0)
            return ApiResponse<PaymentDto>.Fail("Payment amount must be greater than zero.");

        if (request.Amount > invoice.RemainingBalance)
            return ApiResponse<PaymentDto>.Fail($"Payment amount (${request.Amount}) cannot exceed remaining balance (${invoice.RemainingBalance}).");

        var count = await _context.Payments.CountAsync(ct) + 1;
        var receiptNumber = $"REC-{DateTime.UtcNow.Year}-{count:D5}";

        var payment = new Payment
        {
            SchoolId = schoolId.Value,
            InvoiceId = invoice.Id,
            StudentId = invoice.StudentId,
            ReceiptNumber = receiptNumber,
            Amount = request.Amount,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            ReceivedByUserId = userId
        };

        _context.Payments.Add(payment);

        invoice.PaidAmount += request.Amount;
        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }
        else
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.Create, nameof(Payment), payment.Id.ToString(),
            $"Recorded payment {payment.ReceiptNumber} of ${payment.Amount} for invoice {invoice.InvoiceNumber}", ct: ct);

        var receiver = await _context.Users.FindAsync(new object[] { userId }, ct);

        var dto = new PaymentDto(
            payment.Id,
            payment.InvoiceId,
            invoice.InvoiceNumber,
            payment.StudentId,
            invoice.Student?.FullName ?? string.Empty,
            payment.ReceiptNumber,
            payment.Amount,
            payment.PaymentDate,
            payment.PaymentMethod,
            payment.ReferenceNumber,
            payment.Notes,
            receiver?.FullName ?? "Admin"
        );

        return ApiResponse<PaymentDto>.Ok(dto, "Payment recorded successfully.");
    }

    public async Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync(CancellationToken ct = default)
    {
        var invoices = await _context.Invoices.ToListAsync(ct);

        decimal totalInvoiced = invoices.Sum(i => i.TotalAmount);
        decimal totalCollected = invoices.Sum(i => i.PaidAmount);
        decimal totalOutstanding = invoices.Sum(i => i.RemainingBalance);

        int pendingCount = invoices.Count(i => i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.PartiallyPaid);
        int paidCount = invoices.Count(i => i.Status == InvoiceStatus.Paid);
        int overdueCount = invoices.Count(i => i.Status != InvoiceStatus.Paid && i.DueDate < DateTime.UtcNow);

        return ApiResponse<FinancialSummaryDto>.Ok(new FinancialSummaryDto(
            totalInvoiced,
            totalCollected,
            totalOutstanding,
            pendingCount,
            paidCount,
            overdueCount
        ));
    }
}
