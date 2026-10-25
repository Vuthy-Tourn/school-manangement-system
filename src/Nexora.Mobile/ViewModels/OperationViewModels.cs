using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;
using Nexora.Mobile.Services;

namespace Nexora.Mobile.ViewModels;

// 1. Students ViewModel
public partial class StudentsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StudentDto> _students = new();

    [ObservableProperty]
    private StudentDto? _selectedStudent;

    public StudentsViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Student Directory";
    }

    [RelayCommand]
    public async Task LoadStudentsAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetStudentsAsync(SearchTerm);
            if (res.Success && res.Data != null)
            {
                Students = new ObservableCollection<StudentDto>(res.Data.Items);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateStudentAsync(StudentDto student)
    {
        if (student == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Confirm Deactivation", $"Are you sure you want to deactivate {student.FullName}?", "Deactivate", "Cancel");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            var res = await _apiService.DeactivateStudentAsync(student.Id);
            if (res.Success)
            {
                await LoadStudentsAsync();
                await Shell.Current.DisplayAlert("Success", "Student account deactivated.", "OK");
            }
            else
            {
                SetError(res.Message);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditStudentAsync(StudentDto student)
    {
        if (student == null) return;
        await Shell.Current.Navigation.PushAsync(new Views.StudentFormPage(new StudentFormViewModel(_apiService, student)));
    }
}

// 2. Student Form ViewModel
public partial class StudentFormViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly StudentDto? _originalStudent;

    public bool IsEditMode => _originalStudent != null;
    public bool IsAddMode => !IsEditMode;

    [ObservableProperty] private string _admissionNumber = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private DateTime _dateOfBirth = DateTime.Today.AddYears(-15);
    [ObservableProperty] private string _gender = "Male";
    [ObservableProperty] private string _bloodGroup = "O+";
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _emergencyContactName = string.Empty;
    [ObservableProperty] private string _emergencyContactPhone = string.Empty;
    [ObservableProperty] private string _emergencyContactRelationship = "Parent";
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    public StudentFormViewModel(IApiService apiService, StudentDto? student = null)
    {
        _apiService = apiService;
        _originalStudent = student;

        if (student != null)
        {
            Title = $"Edit Student: {student.FullName}";
            AdmissionNumber = student.AdmissionNumber;
            FirstName = student.FirstName;
            LastName = student.LastName;
            DateOfBirth = student.DateOfBirth;
            Gender = student.Gender;
            BloodGroup = student.BloodGroup ?? "O+";
            Email = student.Email ?? string.Empty;
            PhoneNumber = student.PhoneNumber ?? string.Empty;
            Address = student.Address ?? string.Empty;
            EmergencyContactName = student.EmergencyContactName ?? string.Empty;
            EmergencyContactPhone = student.EmergencyContactPhone ?? string.Empty;
            EmergencyContactRelationship = student.EmergencyContactRelationship ?? "Parent";
        }
        else
        {
            Title = "Add New Student";
            AdmissionNumber = $"ADM-2026-{Random.Shared.Next(1000, 9999)}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            SetError("First name and Last name are required.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            if (IsEditMode && _originalStudent != null)
            {
                var req = new UpdateStudentRequest(
                    FirstName, LastName, DateOfBirth, Gender, BloodGroup,
                    _originalStudent.Status, Email, PhoneNumber, Address,
                    _originalStudent.ProfilePhotoUrl, _originalStudent.CurrentClassSectionId,
                    EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship);

                var res = await _apiService.UpdateStudentAsync(_originalStudent.Id, req);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Success", "Student details updated successfully!", "OK");
                    await PopPageAsync();
                }
                else
                {
                    SetError(res.Message);
                }
            }
            else
            {
                var req = new CreateStudentRequest(
                    AdmissionNumber, FirstName, LastName, DateOfBirth, Gender, BloodGroup,
                    DateTime.UtcNow, Email, PhoneNumber, Address, null, null,
                    EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
                    Username, Password);

                var res = await _apiService.CreateStudentAsync(req);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Success", "Student registered successfully!", "OK");
                    await PopPageAsync();
                }
                else
                {
                    SetError(res.Message);
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task PopPageAsync()
    {
        if (Shell.Current?.Navigation != null && Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.Navigation.PopAsync();
        }
        else
        {
            await Shell.Current!.GoToAsync("..");
        }
    }
}

// 3. Teachers ViewModel
public partial class TeachersViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<TeacherDto> _teachers = new();

    public TeachersViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Faculty & Staff";
    }

    [RelayCommand]
    public async Task LoadTeachersAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetTeachersAsync();
            if (res.Success && res.Data != null)
            {
                Teachers = new ObservableCollection<TeacherDto>(res.Data);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 4. Classes ViewModel
public partial class ClassesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ObservableCollection<SubjectDto> _subjects = new();

    public ClassesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Classes & Subjects";
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var classesRes = await _apiService.GetClassesAsync();
            if (classesRes.Success && classesRes.Data != null)
            {
                Classes = new ObservableCollection<ClassSectionDto>(classesRes.Data);
            }

            var subjectsRes = await _apiService.GetSubjectsAsync();
            if (subjectsRes.Success && subjectsRes.Data != null)
            {
                Subjects = new ObservableCollection<SubjectDto>(subjectsRes.Data);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 5. Attendance ViewModel
public partial class AttendanceItemViewModel : ObservableObject
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;

    [ObservableProperty]
    private AttendanceStatus _status = AttendanceStatus.Present;

    [ObservableProperty]
    private string? _remarks;

    [RelayCommand]
    private void SetStatus(string statusStr)
    {
        if (Enum.TryParse<AttendanceStatus>(statusStr, out var s))
        {
            Status = s;
        }
    }
}

public partial class AttendanceViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ClassSectionDto? _selectedClass;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ObservableCollection<AttendanceItemViewModel> _studentItems = new();
    [ObservableProperty] private AttendanceSummaryDto? _summary;

    public AttendanceViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Attendance Management";
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var classesRes = await _apiService.GetClassesAsync();
            if (classesRes.Success && classesRes.Data != null)
            {
                Classes = new ObservableCollection<ClassSectionDto>(classesRes.Data);
                SelectedClass = Classes.FirstOrDefault();
                if (SelectedClass != null)
                {
                    await LoadAttendanceAsync();
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadAttendanceAsync()
    {
        if (SelectedClass == null) return;
        try
        {
            IsBusy = true;
            ClearError();

            var date = DateOnly.FromDateTime(SelectedDate);
            var res = await _apiService.GetClassAttendanceAsync(SelectedClass.Id, date);
            if (res.Success && res.Data != null)
            {
                var items = res.Data.Select(r => new AttendanceItemViewModel
                {
                    StudentId = r.StudentId,
                    StudentName = r.StudentName,
                    AdmissionNumber = r.AdmissionNumber,
                    Status = r.Status,
                    Remarks = r.Remarks
                });
                StudentItems = new ObservableCollection<AttendanceItemViewModel>(items);
            }

            var sumRes = await _apiService.GetClassAttendanceSummaryAsync(SelectedClass.Id);
            if (sumRes.Success) Summary = sumRes.Data;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void MarkAllPresent()
    {
        foreach (var item in StudentItems)
        {
            item.Status = AttendanceStatus.Present;
        }
    }

    [RelayCommand]
    public async Task SaveAttendanceAsync()
    {
        if (SelectedClass == null || StudentItems.Count == 0) return;
        try
        {
            IsBusy = true;
            ClearError();

            var req = new BatchAttendanceRequest(
                SelectedClass.Id,
                DateOnly.FromDateTime(SelectedDate),
                StudentItems.Select(s => new StudentAttendanceItem(s.StudentId, s.Status, s.Remarks)).ToList()
            );

            var res = await _apiService.RecordBatchAttendanceAsync(req);
            if (res.Success)
            {
                await Shell.Current.DisplayAlert("Saved", "Daily attendance successfully recorded!", "OK");
                await LoadAttendanceAsync();
            }
            else
            {
                SetError(res.Message);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 6. Grades & Report Cards ViewModel
public partial class GradeEntryItemViewModel : ObservableObject
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _scoreObtained;

    [ObservableProperty]
    private string? _remarks;
}

public partial class GradesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<ExamDto> _exams = new();
    [ObservableProperty] private ExamDto? _selectedExam;
    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ClassSectionDto? _selectedClass;
    [ObservableProperty] private ObservableCollection<AssessmentComponentDto> _components = new();
    [ObservableProperty] private AssessmentComponentDto? _selectedComponent;
    [ObservableProperty] private ObservableCollection<GradeEntryItemViewModel> _studentGrades = new();
    [ObservableProperty] private ReportCardDto? _currentReportCard;
    [ObservableProperty] private bool _canEditGrades = true;
    [ObservableProperty] private bool _isReadOnlyGrades = false;
    [ObservableProperty] private bool _isStudentView = false;
    [ObservableProperty] private string _portalTitle = "Exams & Grading";

    public GradesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Academic Grading";
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var role = _apiService.CurrentUser?.Role ?? UserRole.SchoolAdmin;
            CanEditGrades = role is UserRole.SuperAdmin or UserRole.SchoolAdmin or UserRole.Teacher;
            IsReadOnlyGrades = !CanEditGrades;
            IsStudentView = role is UserRole.Student or UserRole.Parent;
            PortalTitle = role switch
            {
                UserRole.Student => "My Grades & Assessments",
                UserRole.Parent => "Student Academic Grades",
                _ => "Exams & Grading"
            };
            Title = PortalTitle;

            var examsRes = await _apiService.GetExamsAsync();
            if (examsRes.Success && examsRes.Data != null)
            {
                Exams = new ObservableCollection<ExamDto>(examsRes.Data);
                SelectedExam = Exams.FirstOrDefault();
            }

            var classesRes = await _apiService.GetClassesAsync();
            if (classesRes.Success && classesRes.Data != null)
            {
                Classes = new ObservableCollection<ClassSectionDto>(classesRes.Data);
                SelectedClass = Classes.FirstOrDefault();
            }

            await LoadComponentsAsync();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadComponentsAsync()
    {
        if (SelectedExam == null || SelectedClass == null) return;
        try
        {
            IsBusy = true;
            var res = await _apiService.GetAssessmentComponentsAsync(SelectedExam.Id, SelectedClass.Id);
            if (res.Success && res.Data != null)
            {
                Components = new ObservableCollection<AssessmentComponentDto>(res.Data);
                SelectedComponent = Components.FirstOrDefault();
                if (SelectedComponent != null)
                {
                    await LoadGradesAsync();
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadGradesAsync()
    {
        if (SelectedComponent == null) return;
        try
        {
            IsBusy = true;
            var res = await _apiService.GetComponentGradesAsync(SelectedComponent.Id);
            if (res.Success && res.Data != null)
            {
                var role = _apiService.CurrentUser?.Role ?? UserRole.SchoolAdmin;
                var data = res.Data;

                if (role == UserRole.Student)
                {
                    var studentName = _apiService.CurrentUser?.FullName ?? "Visal Chan";
                    var match = data.Where(g => g.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase)
                                             || g.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)).ToList();
                    data = match.Count > 0 ? match : data.Take(1).ToList();
                }
                else if (role == UserRole.Parent)
                {
                    var match = data.Where(g => g.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)).ToList();
                    data = match.Count > 0 ? match : data.Take(1).ToList();
                }

                var items = data.Select(g => new GradeEntryItemViewModel
                {
                    StudentId = g.StudentId,
                    StudentName = g.StudentName,
                    AdmissionNumber = g.AdmissionNumber,
                    ScoreObtained = g.ScoreObtained,
                    Remarks = g.Remarks
                });
                StudentGrades = new ObservableCollection<GradeEntryItemViewModel>(items);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveGradesAsync()
    {
        if (!CanEditGrades || SelectedComponent == null || StudentGrades.Count == 0) return;
        try
        {
            IsBusy = true;
            ClearError();

            var req = new BatchGradeEntryRequest(
                SelectedComponent.Id,
                StudentGrades.Select(g => new StudentGradeItem(g.StudentId, g.ScoreObtained, g.Remarks)).ToList()
            );

            var res = await _apiService.RecordBatchGradesAsync(req);
            if (res.Success)
            {
                await Shell.Current.DisplayAlert("Success", "Grades saved successfully!", "OK");
            }
            else
            {
                SetError(res.Message);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewReportCardAsync(GradeEntryItemViewModel student)
    {
        if (student == null || SelectedExam == null) return;
        try
        {
            IsBusy = true;
            var res = await _apiService.GetStudentReportCardAsync(student.StudentId, SelectedExam.Id);
            if (res.Success && res.Data != null)
            {
                CurrentReportCard = res.Data;
                await Shell.Current.DisplayAlert("Report Card: " + res.Data.StudentName,
                    $"Overall GPA: {res.Data.CumulativeGpa:F2}\nOverall Grade: {res.Data.OverallGradeLetter}\nPercentage: {res.Data.OverallPercentage:F1}%\nClass Rank: #{res.Data.RankInClass}",
                    "Close");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 7. Fees & Invoices ViewModel
public partial class FeesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<InvoiceDto> _invoices = new();
    [ObservableProperty] private FinancialSummaryDto? _summary;
    [ObservableProperty] private InvoiceDto? _selectedInvoice;
    [ObservableProperty] private bool _canRecordPayment = true;
    [ObservableProperty] private bool _isReadOnlyInvoice = false;
    [ObservableProperty] private bool _isAdminView = true;
    [ObservableProperty] private string _pageTitle = "Tuition & Fees";

    public FeesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Tuition & Fees";
    }

    [RelayCommand]
    public async Task LoadInvoicesAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var role = _apiService.CurrentUser?.Role ?? UserRole.SchoolAdmin;
            CanRecordPayment = role is UserRole.SuperAdmin or UserRole.SchoolAdmin;
            IsReadOnlyInvoice = !CanRecordPayment;
            IsAdminView = CanRecordPayment;
            PageTitle = role == UserRole.Student ? "My Tuition Invoices" : (role == UserRole.Parent ? "Family Tuition Statements" : "Tuition & Fees");
            Title = PageTitle;

            var res = await _apiService.GetInvoicesAsync();
            if (res.Success && res.Data != null)
            {
                var invoices = res.Data;
                if (role == UserRole.Student)
                {
                    var studentName = _apiService.CurrentUser?.FullName ?? "Visal Chan";
                    var match = invoices.Where(inv => inv.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase)
                                                   || inv.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)).ToList();
                    invoices = match.Count > 0 ? match : invoices.Take(1).ToList();
                }
                else if (role == UserRole.Parent)
                {
                    var match = invoices.Where(inv => inv.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)).ToList();
                    invoices = match.Count > 0 ? match : invoices.Take(1).ToList();
                }

                Invoices = new ObservableCollection<InvoiceDto>(invoices);
            }

            if (IsAdminView)
            {
                var sumRes = await _apiService.GetFinancialSummaryAsync();
                if (sumRes.Success) Summary = sumRes.Data;
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewInvoiceDetailsAsync(InvoiceDto invoice)
    {
        if (invoice == null) return;
        string statusText = invoice.RemainingBalance <= 0 ? "PAID IN FULL" : $"OUTSTANDING (${invoice.RemainingBalance:N2})";
        await Shell.Current.DisplayAlert(
            "Invoice: " + invoice.InvoiceNumber,
            $"Student: {invoice.StudentName}\n" +
            $"Status: {invoice.Status} ({statusText})\n" +
            $"Total Billed: ${invoice.TotalAmount:N2}\n" +
            $"Amount Paid: ${invoice.PaidAmount:N2}\n" +
            $"Balance Due: ${invoice.RemainingBalance:N2}\n" +
            $"Due Date: {invoice.DueDate:MMM dd, yyyy}\n\n" +
            "Payment Channel: ABA Bank Pay (Office Counter or Mobile Banking).",
            "Done");
    }

    [RelayCommand]
    public async Task RecordPaymentPromptAsync(InvoiceDto invoice)
    {
        if (!CanRecordPayment || invoice == null) return;
        if (invoice.RemainingBalance <= 0)
        {
            await Shell.Current.DisplayAlert("Paid", "This invoice is already fully paid.", "OK");
            return;
        }

        string result = await Shell.Current.DisplayPromptAsync(
            "Record Payment",
            $"Invoice: {invoice.InvoiceNumber}\nRemaining: ${invoice.RemainingBalance:N2}\nEnter payment amount in USD:",
            initialValue: invoice.RemainingBalance.ToString("0.00"),
            keyboard: Keyboard.Numeric);

        if (decimal.TryParse(result, out var amount) && amount > 0)
        {
            try
            {
                IsBusy = true;
                var req = new RecordPaymentRequest(invoice.Id, amount, PaymentMethod.ABA, "APP-PAY-" + Random.Shared.Next(100000, 999999), "Recorded via mobile app");
                var res = await _apiService.RecordPaymentAsync(req);
                if (res.Success && res.Data != null)
                {
                    await Shell.Current.DisplayAlert("Payment Recorded", $"Receipt Number: {res.Data.ReceiptNumber}\nAmount: ${res.Data.Amount:N2}\nStatus: Confirmed", "Done");
                    await LoadInvoicesAsync();
                }
                else
                {
                    SetError(res.Message);
                }
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

// 8. Timetables ViewModel
public partial class TimetablesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private DayOfWeek _selectedDay = DayOfWeek.Monday;
    [ObservableProperty] private ObservableCollection<TimetableEntryDto> _scheduleSlots = new();

    public TimetablesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Class Timetable";
    }

    [RelayCommand]
    public async Task LoadTimetableAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var classes = await _apiService.GetClassesAsync();
            var firstClass = classes.Data?.FirstOrDefault();
            if (firstClass != null)
            {
                var res = await _apiService.GetClassTimetableAsync(firstClass.Id);
                if (res.Success && res.Data != null)
                {
                    var filtered = res.Data.Where(s => s.DayOfWeek == SelectedDay).ToList();
                    ScheduleSlots = new ObservableCollection<TimetableEntryDto>(filtered);
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SelectDayAsync(string dayStr)
    {
        if (Enum.TryParse<DayOfWeek>(dayStr, out var day))
        {
            SelectedDay = day;
            await LoadTimetableAsync();
        }
    }
}

// 9. Announcements ViewModel
public partial class AnnouncementsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<AnnouncementDto> _announcements = new();

    public AnnouncementsViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Announcements";
    }

    [RelayCommand]
    public async Task LoadAnnouncementsAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetAnnouncementsAsync();
            if (res.Success && res.Data != null)
            {
                Announcements = new ObservableCollection<AnnouncementDto>(res.Data);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 10. Audit Logs ViewModel
public partial class AuditLogsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<AuditLogDto> _logs = new();

    public AuditLogsViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Audit Trail";
    }

    [RelayCommand]
    public async Task LoadLogsAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetAuditLogsAsync();
            if (res.Success && res.Data != null)
            {
                Logs = new ObservableCollection<AuditLogDto>(res.Data);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

// 11. Profile & Settings ViewModel
public partial class ProfileViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private UserProfileDto? _profile;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _roleBadge = string.Empty;
    [ObservableProperty] private string _roleDepartment = string.Empty;
    [ObservableProperty] private string _schoolName = "Angkor International Academy";
    [ObservableProperty] private string _appVersion = "1.0.0 (Build 2026)";
    [ObservableProperty] private string _selectedLanguage = "English (US)";

    public ProfileViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Profile & Settings";
        RefreshProfile();
    }

    public void RefreshProfile()
    {
        Profile = _apiService.CurrentUser;
        if (Profile != null)
        {
            FullName = Profile.FullName;
            Email = Profile.Email;
            PhoneNumber = Profile.PhoneNumber ?? string.Empty;
            SchoolName = !string.IsNullOrWhiteSpace(Profile.SchoolName) ? Profile.SchoolName : "Angkor International Academy";
            RoleBadge = Profile.Role switch
            {
                UserRole.SuperAdmin => "Super Administrator",
                UserRole.SchoolAdmin => "School Administrator",
                UserRole.Teacher => "Faculty / Teacher",
                UserRole.Student => "Enrolled Student",
                UserRole.Parent => "Parent / Guardian",
                _ => "User"
            };
            RoleDepartment = Profile.Role switch
            {
                UserRole.SuperAdmin => "Executive Board & Governance",
                UserRole.SchoolAdmin => "Academic & Campus Administration",
                UserRole.Teacher => "Faculty & Instruction Department",
                UserRole.Student => "Upper Secondary • Grade 10A",
                UserRole.Parent => "Family & Guardian Association",
                _ => "Institutional Portal"
            };
        }
        else
        {
            FullName = "Administrator";
            Email = "admin@angkoracademy.edu.kh";
            PhoneNumber = "+855 23 888 101";
            RoleBadge = "School Administrator";
            RoleDepartment = "Academic & Campus Administration";
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
        {
            await Shell.Current.DisplayAlert("Validation", "Please provide a valid full name and email address.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            var res = await _apiService.UpdateProfileAsync(FullName, Email, PhoneNumber);
            if (res.Success && res.Data != null)
            {
                Profile = res.Data;
                RefreshProfile();
                await Shell.Current.DisplayAlert("Profile Updated", "Your profile information has been saved successfully.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Update Failed", res.Message ?? "Could not save profile.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleLanguageAsync()
    {
        SelectedLanguage = SelectedLanguage.StartsWith("English") ? "Khmer (ភាសាខ្មែរ)" : "English (US)";
        await Shell.Current.DisplayAlert("Language Changed", $"Interface language set to {SelectedLanguage}", "OK");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;

        await _apiService.LogoutAsync();
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.MainPage = new NavigationPage(new Views.LoginPage(new LoginViewModel(_apiService)));
        }
    }
}
