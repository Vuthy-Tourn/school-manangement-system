using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly Action<GradeEntryItemViewModel>? _onScoreChanged;

    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public bool IsStudentView { get; set; } = false;

    public string DisplayTitle => (IsStudentView && !string.IsNullOrWhiteSpace(SubjectName))
        ? SubjectName
        : StudentName;

    public string DisplaySubtitle => (IsStudentView && !string.IsNullOrWhiteSpace(ComponentName))
        ? ComponentName
        : AdmissionNumber;

    [ObservableProperty]
    private decimal _scoreObtained;

    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private string _gradeLetter = "A";

    [ObservableProperty]
    private Color _gradeBadgeColor = Color.FromArgb("#15803D");

    [ObservableProperty]
    private Color _gradeBadgeBg = Color.FromArgb("#DCFCE7");

    [ObservableProperty]
    private string? _remarks;

    [ObservableProperty]
    private bool _isSaved = true;

    public GradeEntryItemViewModel(Action<GradeEntryItemViewModel>? onScoreChanged = null)
    {
        _onScoreChanged = onScoreChanged;
    }

    public void SetScore(decimal score)
    {
        ScoreObtained = score;
        ScoreText = score.ToString("0.#", CultureInfo.InvariantCulture);
        RecalculateGrade(score);
        IsSaved = true;
    }

    partial void OnScoreTextChanged(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            if (parsed > 100) parsed = 100;
            if (parsed < 0) parsed = 0;
            ScoreObtained = parsed;
            RecalculateGrade(parsed);
            IsSaved = false;
            _onScoreChanged?.Invoke(this);
        }
    }

    public void RecalculateGrade(decimal score)
    {
        if (score >= 85)
        {
            GradeLetter = "A";
            GradeBadgeColor = Color.FromArgb("#15803D");
            GradeBadgeBg = Color.FromArgb("#DCFCE7");
        }
        else if (score >= 75)
        {
            GradeLetter = "B";
            GradeBadgeColor = Color.FromArgb("#1D4ED8");
            GradeBadgeBg = Color.FromArgb("#DBEAFE");
        }
        else if (score >= 65)
        {
            GradeLetter = "C";
            GradeBadgeColor = Color.FromArgb("#B45309");
            GradeBadgeBg = Color.FromArgb("#FEF3C7");
        }
        else if (score >= 50)
        {
            GradeLetter = "D";
            GradeBadgeColor = Color.FromArgb("#C2410C");
            GradeBadgeBg = Color.FromArgb("#FFEDD5");
        }
        else
        {
            GradeLetter = "F";
            GradeBadgeColor = Color.FromArgb("#B91C1C");
            GradeBadgeBg = Color.FromArgb("#FEE2E2");
        }
    }
}

public partial class SubjectChipViewModel : ObservableObject
{
    public AssessmentComponentDto Component { get; set; } = null!;
    public string SubjectName => Component.SubjectName;
    public string ComponentName => Component.Name;
    public string DisplayTitle => string.IsNullOrWhiteSpace(SubjectName) ? ComponentName : SubjectName;

    [ObservableProperty]
    private bool _isSelected;

    public SubjectChipViewModel(AssessmentComponentDto component, bool isSelected = false)
    {
        Component = component;
        _isSelected = isSelected;
    }
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
    [ObservableProperty] private ObservableCollection<SubjectChipViewModel> _subjectChips = new();
    [ObservableProperty] private ObservableCollection<GradeEntryItemViewModel> _studentGrades = new();
    [ObservableProperty] private ObservableCollection<GradeEntryItemViewModel> _filteredGrades = new();
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeSubjectHeader = "Khmer Literature";
    [ObservableProperty] private string _classSummary = "4 Students Enrolled";
    [ObservableProperty] private bool _hasUnsavedChanges = false;
    [ObservableProperty] private ReportCardDto? _currentReportCard;
    [ObservableProperty] private bool _canEditGrades = false;
    [ObservableProperty] private bool _isReadOnlyGrades = true;
    [ObservableProperty] private bool _isStudentView = false;
    [ObservableProperty] private string _portalTitle = "Exams & Grading";

    public GradesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Academic Grading";
        ApplyRolePermissions();
    }

    private void ApplyRolePermissions()
    {
        var role = _apiService.CurrentUser?.Role;
        bool isFacultyOrAdmin = role is UserRole.SuperAdmin or UserRole.SchoolAdmin or UserRole.Teacher;
        CanEditGrades = isFacultyOrAdmin;
        IsReadOnlyGrades = !isFacultyOrAdmin;
        IsStudentView = role is UserRole.Student or UserRole.Parent;
        PortalTitle = role switch
        {
            UserRole.Student => "My Grades & Assessments",
            UserRole.Parent => "Student Academic Grades",
            _ => "Exams & Grading"
        };
        Title = PortalTitle;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            ApplyRolePermissions();

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
            ApplyRolePermissions();

            var res = await _apiService.GetAssessmentComponentsAsync(SelectedExam.Id, SelectedClass.Id);
            if (res.Success && res.Data != null)
            {
                Components = new ObservableCollection<AssessmentComponentDto>(res.Data);

                var chips = new List<SubjectChipViewModel>();
                if (IsStudentView)
                {
                    var allChipDto = new AssessmentComponentDto(Guid.Empty, SelectedExam.Id, "All Subjects", Guid.Empty, "All Subjects", SelectedClass.Id, SelectedClass.Name, "All Subjects", 100m, 100m);
                    chips.Add(new SubjectChipViewModel(allChipDto, true));
                    SelectedComponent = allChipDto;
                }
                else
                {
                    SelectedComponent = Components.FirstOrDefault();
                }

                foreach (var c in Components)
                {
                    chips.Add(new SubjectChipViewModel(c, c.Id == SelectedComponent?.Id));
                }
                SubjectChips = new ObservableCollection<SubjectChipViewModel>(chips);

                if (SelectedComponent != null)
                {
                    ActiveSubjectHeader = string.IsNullOrWhiteSpace(SelectedComponent.SubjectName) ? SelectedComponent.Name : SelectedComponent.SubjectName;
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
    public async Task SelectSubjectChipAsync(SubjectChipViewModel chip)
    {
        if (chip == null) return;
        foreach (var c in SubjectChips)
            c.IsSelected = c.Component.Id == chip.Component.Id;

        SelectedComponent = chip.Component;
        ActiveSubjectHeader = chip.DisplayTitle;
        await LoadGradesAsync();
    }

    [RelayCommand]
    public async Task LoadGradesAsync()
    {
        if (SelectedComponent == null) return;
        try
        {
            IsBusy = true;
            ApplyRolePermissions();

            var items = new List<GradeEntryItemViewModel>();
            var role = _apiService.CurrentUser?.Role;

            if (IsStudentView && SelectedComponent.Id == Guid.Empty)
            {
                // Load all subject grades for student or parent to review complete curriculum
                var studentName = (role == UserRole.Student) ? (_apiService.CurrentUser?.FullName ?? "Visal Chan") : "Visal Chan";
                var firstName = studentName.Split(' ').FirstOrDefault() ?? "Visal";

                foreach (var comp in Components)
                {
                    var compGradesRes = await _apiService.GetComponentGradesAsync(comp.Id);
                    if (compGradesRes.Success && compGradesRes.Data != null)
                    {
                        var match = compGradesRes.Data.FirstOrDefault(g =>
                            g.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase) ||
                            g.StudentName.Contains(firstName, StringComparison.OrdinalIgnoreCase) ||
                            (_apiService.CurrentUser?.LinkedProfileId.HasValue == true && g.StudentId == _apiService.CurrentUser.LinkedProfileId.Value));

                        if (match != null)
                        {
                            var vm = new GradeEntryItemViewModel(_ => UpdateSummary())
                            {
                                StudentId = match.StudentId,
                                StudentName = match.StudentName,
                                AdmissionNumber = match.AdmissionNumber,
                                SubjectName = string.IsNullOrWhiteSpace(match.SubjectName) ? comp.SubjectName : match.SubjectName,
                                ComponentName = comp.Name,
                                Remarks = match.Remarks,
                                IsStudentView = true
                            };
                            vm.SetScore(match.ScoreObtained);
                            items.Add(vm);
                        }
                    }
                }
            }
            else
            {
                var res = await _apiService.GetComponentGradesAsync(SelectedComponent.Id);
                if (res.Success && res.Data != null)
                {
                    var data = res.Data;

                    if (role == UserRole.Student)
                    {
                        var studentName = _apiService.CurrentUser?.FullName ?? "Visal Chan";
                        var firstName = studentName.Split(' ').FirstOrDefault() ?? "Visal";
                        var match = data.Where(g => g.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase)
                                                 || g.StudentName.Contains(firstName, StringComparison.OrdinalIgnoreCase)
                                                 || (_apiService.CurrentUser?.LinkedProfileId.HasValue == true && g.StudentId == _apiService.CurrentUser.LinkedProfileId.Value)).ToList();
                        data = match.Count > 0 ? match : data.Take(1).ToList();
                    }
                    else if (role == UserRole.Parent)
                    {
                        var match = data.Where(g => g.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)
                                                 || (_apiService.CurrentUser?.LinkedProfileId.HasValue == true && g.StudentId == _apiService.CurrentUser.LinkedProfileId.Value)).ToList();
                        data = match.Count > 0 ? match : data.Take(1).ToList();
                    }

                    foreach (var g in data)
                    {
                        var vm = new GradeEntryItemViewModel(_ => UpdateSummary())
                        {
                            StudentId = g.StudentId,
                            StudentName = g.StudentName,
                            AdmissionNumber = g.AdmissionNumber,
                            SubjectName = string.IsNullOrWhiteSpace(g.SubjectName) ? SelectedComponent.SubjectName : g.SubjectName,
                            ComponentName = SelectedComponent.Name,
                            Remarks = g.Remarks,
                            IsStudentView = IsStudentView
                        };
                        vm.SetScore(g.ScoreObtained);
                        items.Add(vm);
                    }
                }
            }

            StudentGrades = new ObservableCollection<GradeEntryItemViewModel>(items);
            ApplySearchFilter();
            UpdateSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredGrades = new ObservableCollection<GradeEntryItemViewModel>(StudentGrades);
        }
        else
        {
            var term = SearchText.Trim();
            var matches = StudentGrades.Where(s =>
                s.StudentName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.AdmissionNumber.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            FilteredGrades = new ObservableCollection<GradeEntryItemViewModel>(matches);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
    }

    private void UpdateSummary()
    {
        if (StudentGrades.Count == 0)
        {
            ClassSummary = "No students enrolled";
            HasUnsavedChanges = false;
            return;
        }
        var avg = StudentGrades.Average(s => s.ScoreObtained);
        var unsavedCount = StudentGrades.Count(s => !s.IsSaved);
        HasUnsavedChanges = unsavedCount > 0;
        ClassSummary = $"{StudentGrades.Count} Students • Avg: {avg:F1} • {(HasUnsavedChanges ? $"{unsavedCount} Unsaved" : "All Saved")}";
    }

    [RelayCommand]
    public async Task SaveSingleGradeAsync(GradeEntryItemViewModel item)
    {
        if (item == null || SelectedComponent == null) return;
        try
        {
            IsBusy = true;
            var res = await _apiService.RecordSingleGradeAsync(SelectedComponent.Id, item.StudentId, item.ScoreObtained, item.Remarks);
            if (res.Success)
            {
                item.IsSaved = true;
                UpdateSummary();
                await Shell.Current.DisplayAlert("Score Saved", $"{item.StudentName}'s grade recorded as {item.ScoreObtained}.", "OK");
            }
            else
            {
                SetError(res.Message);
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
        var role = _apiService.CurrentUser?.Role;
        if (!CanEditGrades || role is not (UserRole.SuperAdmin or UserRole.SchoolAdmin or UserRole.Teacher))
        {
            await Shell.Current.DisplayAlert("Access Denied", "Only teachers and administrators are authorized to save exam grades.", "OK");
            return;
        }

        if (SelectedComponent == null || StudentGrades.Count == 0) return;
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
                foreach (var s in StudentGrades)
                    s.IsSaved = true;

                UpdateSummary();
                await Shell.Current.DisplayAlert("Grades Saved", $"All grades for {ActiveSubjectHeader} have been successfully recorded!", "OK");
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
    [ObservableProperty] private bool _canRecordPayment = false; // FAIL-SAFE: Deny recording payment by default
    [ObservableProperty] private bool _isReadOnlyInvoice = true; // FAIL-SAFE: Read-only by default
    [ObservableProperty] private bool _isAdminView = false;      // FAIL-SAFE: Hide admin summaries by default
    [ObservableProperty] private string _pageTitle = "Tuition & Fees";

    public FeesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Tuition & Fees";
        ApplyRolePermissions();
    }

    private void ApplyRolePermissions()
    {
        var role = _apiService.CurrentUser?.Role;
        bool isAdmin = role is UserRole.SuperAdmin or UserRole.SchoolAdmin;
        CanRecordPayment = isAdmin;
        IsReadOnlyInvoice = !isAdmin;
        IsAdminView = isAdmin;
        PageTitle = role == UserRole.Student 
            ? "My Tuition Invoices" 
            : (role == UserRole.Parent ? "Family Tuition Statements" : "Tuition & Fees");
        Title = PageTitle;
    }

    [RelayCommand]
    public async Task LoadInvoicesAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            ApplyRolePermissions();

            var role = _apiService.CurrentUser?.Role;
            var res = await _apiService.GetInvoicesAsync();
            if (res.Success && res.Data != null)
            {
                var invoices = res.Data;
                if (role == UserRole.Student)
                {
                    var studentName = _apiService.CurrentUser?.FullName ?? "Visal Chan";
                    var firstName = studentName.Split(' ').FirstOrDefault() ?? "Visal";
                    var match = invoices.Where(inv => inv.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase)
                                                   || inv.StudentName.Contains(firstName, StringComparison.OrdinalIgnoreCase)
                                                   || (_apiService.CurrentUser?.LinkedProfileId.HasValue == true && inv.StudentId == _apiService.CurrentUser.LinkedProfileId.Value)).ToList();
                    invoices = match.Count > 0 ? match : invoices.Take(1).ToList();
                }
                else if (role == UserRole.Parent)
                {
                    var match = invoices.Where(inv => inv.StudentName.Contains("Visal", StringComparison.OrdinalIgnoreCase)
                                                   || (_apiService.CurrentUser?.LinkedProfileId.HasValue == true && inv.StudentId == _apiService.CurrentUser.LinkedProfileId.Value)).ToList();
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
        var role = _apiService.CurrentUser?.Role;
        if (!CanRecordPayment || role is not (UserRole.SuperAdmin or UserRole.SchoolAdmin))
        {
            await Shell.Current.DisplayAlert("Access Denied", "Only school finance administrators are authorized to record tuition fee payments.", "OK");
            return;
        }

        if (invoice == null) return;
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

public partial class DayChipViewModel : ObservableObject
{
    public DayOfWeek Day { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public DayChipViewModel(DayOfWeek day, string name, bool isSelected = false)
    {
        Day = day;
        Name = name;
        _isSelected = isSelected;
    }
}

// 8. Timetables ViewModel
public partial class TimetablesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private List<TimetableEntryDto> _cachedClassEntries = new();

    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ClassSectionDto? _selectedClass;
    [ObservableProperty] private DayOfWeek _selectedDay = DayOfWeek.Monday;
    [ObservableProperty] private ObservableCollection<DayChipViewModel> _days = new();
    [ObservableProperty] private ObservableCollection<TimetableEntryDto> _scheduleSlots = new();
    [ObservableProperty] private bool _hasNoSlots;
    [ObservableProperty] private string _dayHeaderTitle = "Monday Schedule";

    public TimetablesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Class Timetable";

        var today = DateTime.Today.DayOfWeek;
        _selectedDay = (today >= DayOfWeek.Monday && today <= DayOfWeek.Friday) ? today : DayOfWeek.Monday;

        _days = new ObservableCollection<DayChipViewModel>
        {
            new(DayOfWeek.Monday, "Monday", _selectedDay == DayOfWeek.Monday),
            new(DayOfWeek.Tuesday, "Tuesday", _selectedDay == DayOfWeek.Tuesday),
            new(DayOfWeek.Wednesday, "Wednesday", _selectedDay == DayOfWeek.Wednesday),
            new(DayOfWeek.Thursday, "Thursday", _selectedDay == DayOfWeek.Thursday),
            new(DayOfWeek.Friday, "Friday", _selectedDay == DayOfWeek.Friday)
        };
        _dayHeaderTitle = $"{_selectedDay} Schedule";
    }

    partial void OnSelectedClassChanged(ClassSectionDto? value)
    {
        if (value != null && !IsBusy)
        {
            _ = Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await FetchClassTimetableInternalAsync(value.Id);
                });
            });
        }
    }

    [RelayCommand]
    public async Task LoadTimetableAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            if (Classes.Count == 0)
            {
                var classesRes = await _apiService.GetClassesAsync();
                if (classesRes.Success && classesRes.Data != null && classesRes.Data.Count > 0)
                {
                    Classes = new ObservableCollection<ClassSectionDto>(classesRes.Data);
                }
                else
                {
                    Classes = new ObservableCollection<ClassSectionDto>
                    {
                        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-A", "Room 201", 35, Guid.NewGuid(), "Dara Sok", 34),
                        new(Guid.Parse("a0000000-0000-0000-0000-000000000002"), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-B", "Room 202", 35, Guid.NewGuid(), "Chenda Chan", 32)
                    };
                }

                // Default to Class 10-A
                SelectedClass = Classes.FirstOrDefault(c => c.Name.Equals("10-A", StringComparison.OrdinalIgnoreCase)) ?? Classes.FirstOrDefault();
            }

            if (SelectedClass != null)
            {
                await FetchClassTimetableInternalAsync(SelectedClass.Id);
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

    private async Task FetchClassTimetableInternalAsync(Guid classId)
    {
        var res = await _apiService.GetClassTimetableAsync(classId);
        if (res.Success && res.Data != null && res.Data.Count > 0)
        {
            _cachedClassEntries = res.Data;
        }
        else
        {
            var fallback = await _apiService.GetStudentTimetableAsync(Guid.Empty);
            _cachedClassEntries = fallback.Data ?? new();
        }
        FilterSlotsForDay();
    }

    [RelayCommand]
    public void SelectDay(DayChipViewModel chip)
    {
        if (chip == null) return;
        SelectedDay = chip.Day;
        foreach (var d in Days)
        {
            d.IsSelected = (d.Day == chip.Day);
        }
        DayHeaderTitle = $"{SelectedDay} Schedule";
        FilterSlotsForDay();
    }

    private void FilterSlotsForDay()
    {
        var filtered = _cachedClassEntries
            .Where(s => s.DayOfWeek == SelectedDay)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (filtered.Count == 0 && _cachedClassEntries.Count > 0)
        {
            filtered = _cachedClassEntries
                .Where(s => s.DayOfWeek == DayOfWeek.Monday)
                .OrderBy(s => s.StartTime)
                .ToList();
        }

        ScheduleSlots = new ObservableCollection<TimetableEntryDto>(filtered);
        HasNoSlots = (ScheduleSlots.Count == 0);
    }
}

// 9. Announcements ViewModel
public partial class AnnouncementsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private ObservableCollection<AnnouncementDto> _announcements = new();
    [ObservableProperty] private bool _canManage;
    [ObservableProperty] private bool _isFormVisible;
    [ObservableProperty] private string _formDialogTitle = "New Announcement";
    [ObservableProperty] private Guid? _editingId;
    [ObservableProperty] private string _titleInput = string.Empty;
    [ObservableProperty] private string _contentInput = string.Empty;
    [ObservableProperty] private string _selectedPriority = "Normal";
    [ObservableProperty] private string _selectedTargetRole = "All";

    public ObservableCollection<string> PriorityOptions { get; } = new() { "Normal", "High", "Urgent" };
    public ObservableCollection<string> TargetRoleOptions { get; } = new() { "All", "Teachers", "Students", "Parents" };

    public AnnouncementsViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Announcements";
        UpdatePermissions();
    }

    private void UpdatePermissions()
    {
        var role = _apiService.CurrentUser?.Role;
        CanManage = role is UserRole.SuperAdmin or UserRole.SchoolAdmin or UserRole.Teacher;
    }

    [RelayCommand]
    public async Task LoadAnnouncementsAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            UpdatePermissions();

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

    [RelayCommand]
    public void OpenCreateDialog()
    {
        EditingId = null;
        FormDialogTitle = "New Announcement";
        TitleInput = string.Empty;
        ContentInput = string.Empty;
        SelectedPriority = "Normal";
        SelectedTargetRole = "All";
        IsFormVisible = true;
    }

    [RelayCommand]
    public void OpenEditDialog(AnnouncementDto item)
    {
        if (item == null) return;
        EditingId = item.Id;
        FormDialogTitle = "Edit Announcement";
        TitleInput = item.Title;
        ContentInput = item.Content;
        SelectedPriority = item.Priority.ToString();
        SelectedTargetRole = item.TargetRole.ToString();
        IsFormVisible = true;
    }

    [RelayCommand]
    public void CloseDialog()
    {
        IsFormVisible = false;
        EditingId = null;
    }

    [RelayCommand]
    public async Task SaveAnnouncementAsync()
    {
        if (string.IsNullOrWhiteSpace(TitleInput) || string.IsNullOrWhiteSpace(ContentInput))
        {
            await Shell.Current.DisplayAlert("Validation", "Please enter both title and content.", "OK");
            return;
        }

        var priority = Enum.TryParse<AnnouncementPriority>(SelectedPriority, true, out var p) ? p : AnnouncementPriority.Normal;
        var targetRole = Enum.TryParse<AnnouncementTarget>(SelectedTargetRole, true, out var t) ? t : AnnouncementTarget.All;

        try
        {
            IsBusy = true;
            if (EditingId.HasValue)
            {
                var req = new UpdateAnnouncementRequest(TitleInput.Trim(), ContentInput.Trim(), targetRole, null, priority, false);
                var res = await _apiService.UpdateAnnouncementAsync(EditingId.Value, req);
                if (res.Success && res.Data != null)
                {
                    var idx = Announcements.ToList().FindIndex(a => a.Id == EditingId.Value);
                    if (idx >= 0) Announcements[idx] = res.Data;
                    IsFormVisible = false;
                    await Shell.Current.DisplayAlert("Updated", "Announcement updated successfully.", "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", res.Message ?? "Failed to update announcement.", "OK");
                }
            }
            else
            {
                var req = new CreateAnnouncementRequest(TitleInput.Trim(), ContentInput.Trim(), targetRole, null, priority);
                var res = await _apiService.CreateAnnouncementAsync(req);
                if (res.Success && res.Data != null)
                {
                    Announcements.Insert(0, res.Data);
                    IsFormVisible = false;
                    await Shell.Current.DisplayAlert("Published", "Announcement published successfully.", "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", res.Message ?? "Failed to publish announcement.", "OK");
                }
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
    public async Task DeleteAnnouncementAsync(AnnouncementDto item)
    {
        if (item == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Delete Announcement", $"Are you sure you want to delete '{item.Title}'?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            var res = await _apiService.DeleteAnnouncementAsync(item.Id);
            if (res.Success)
            {
                Announcements.Remove(item);
                await Shell.Current.DisplayAlert("Deleted", "Announcement removed.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", res.Message ?? "Failed to delete announcement.", "OK");
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
            FullName = "Guest User";
            Email = "guest@nexora.edu";
            PhoneNumber = string.Empty;
            RoleBadge = "Unauthenticated";
            RoleDepartment = "Guest Portal";
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
            var loginPage = new NavigationPage(new Views.LoginPage(new LoginViewModel(_apiService)));
            if (Microsoft.Maui.Controls.Application.Current.Windows.Count > 0)
            {
                Microsoft.Maui.Controls.Application.Current.Windows[0].Page = loginPage;
            }
            else
            {
                Microsoft.Maui.Controls.Application.Current.MainPage = loginPage;
            }
        }
    }
}
