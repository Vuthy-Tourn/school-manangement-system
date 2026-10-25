using EduManage.Application.DTOs;
using FluentValidation;

namespace EduManage.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().WithMessage("Username or email is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.AdmissionNumber).NotEmpty().MaximumLength(50).WithMessage("Admission number is required.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("First name is required.");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Last name is required.");
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.UtcNow).WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.EnrollmentDate).NotEmpty().WithMessage("Enrollment date is required.");
    }
}

public class CreateTeacherRequestValidator : AbstractValidator<CreateTeacherRequest>
{
    public CreateTeacherRequestValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(50).WithMessage("Employee number is required.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("First name is required.");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Last name is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Valid email is required.");
    }
}

public class CreateTimetableEntryRequestValidator : AbstractValidator<CreateTimetableEntryRequest>
{
    public CreateTimetableEntryRequestValidator()
    {
        RuleFor(x => x.ClassSectionId).NotEmpty().WithMessage("Class section is required.");
        RuleFor(x => x.SubjectId).NotEmpty().WithMessage("Subject is required.");
        RuleFor(x => x.TeacherId).NotEmpty().WithMessage("Teacher is required.");
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("End time must be after start time.");
    }
}

public class BatchAttendanceRequestValidator : AbstractValidator<BatchAttendanceRequest>
{
    public BatchAttendanceRequestValidator()
    {
        RuleFor(x => x.ClassSectionId).NotEmpty().WithMessage("Class section is required.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Date is required.");
        RuleFor(x => x.Records).NotEmpty().WithMessage("At least one student attendance record is required.");
    }
}

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty().WithMessage("Student is required.");
        RuleFor(x => x.AcademicYearId).NotEmpty().WithMessage("Academic year is required.");
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("Due date cannot be in the past.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one fee line item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().WithMessage("Item description is required.");
            item.RuleFor(i => i.Amount).GreaterThan(0).WithMessage("Item amount must be greater than zero.");
        });
    }
}

public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty().WithMessage("Invoice is required.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
    }
}
