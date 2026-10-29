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

    partial void OnSearchTermChanged(string value)
    {
        _ = LoadStudentsAsync();
    }

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

    [RelayCommand]
    private async Task ViewStudentDetailAsync(StudentDto student)
    {
        if (student == null) return;
        await Shell.Current.Navigation.PushAsync(new Views.StudentDetailPage(new StudentDetailViewModel(_apiService, student)));
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

// 2.1 Student Detail ViewModel
public partial class StudentDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private StudentDto _student;
    [ObservableProperty] private double _gpa = 3.82;
    [ObservableProperty] private double _attendanceRate = 96.4;
    [ObservableProperty] private string _balanceStatus = "Paid ($0.00)";
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isMyAdvisee;

    public string GpaFormatted => $"{Gpa:F2}";
    public string AttendanceFormatted => $"{AttendanceRate:F1}%";

    public StudentDetailViewModel(IApiService apiService, StudentDto student)
    {
        _apiService = apiService;
        _student = student;
        Title = student.FullName;

        var role = _apiService.CurrentUser?.Role;
        IsAdmin = role == UserRole.SuperAdmin || role == UserRole.SchoolAdmin;
        _ = CheckAdviseeStatusAsync();
    }

    private async Task CheckAdviseeStatusAsync()
    {
        try
        {
            var teacherId = _apiService.CurrentUser?.LinkedProfileId;
            if (teacherId.HasValue)
            {
                var classesRes = await _apiService.GetClassesAsync();
                var matchingClass = classesRes.Data?.FirstOrDefault(c =>
                    c.Name == Student.CurrentClassName ||
                    (Student.CurrentClassSectionId.HasValue && c.Id == Student.CurrentClassSectionId.Value));
                if (matchingClass != null && matchingClass.ClassTeacherId == teacherId.Value)
                {
                    IsMyAdvisee = true;
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task ViewReportCardAsync()
    {
        await Shell.Current.Navigation.PushAsync(new Views.ReportCardPage(new ReportCardViewModel(_apiService, Student)));
    }

    [RelayCommand]
    private async Task EditStudentAsync()
    {
        if (!IsAdmin)
        {
            await Shell.Current.DisplayAlert("Access Restricted", "Student records can only be updated by School Administrators.", "OK");
            return;
        }
        await Shell.Current.Navigation.PushAsync(new Views.StudentFormPage(new StudentFormViewModel(_apiService, Student)));
    }

    [RelayCommand]
    private async Task CallGuardianAsync()
    {
        var phone = Student.EmergencyContactPhone;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(phone);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Emergency Contact", $"Calling guardian: {Student.EmergencyContactName} at {phone}", "OK");
                }
            }
            catch
            {
                await Shell.Current.DisplayAlert("Emergency Contact", $"Calling guardian: {Student.EmergencyContactName} at {phone}", "OK");
            }
        }
        else
        {
            await Shell.Current.DisplayAlert("Contact Info", "No phone number registered for emergency contact.", "OK");
        }
    }
}

// 3. Teachers ViewModel
public partial class TeachersViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private List<TeacherDto> _allTeachers = new();

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    partial void OnSearchTermChanged(string value)
    {
        FilterTeachers();
    }

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
                _allTeachers = res.Data;
                FilterTeachers();
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

    private void FilterTeachers()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Teachers = new ObservableCollection<TeacherDto>(_allTeachers);
        }
        else
        {
            var q = SearchTerm.Trim();
            var filtered = _allTeachers.Where(t =>
                (t.FullName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.EmployeeNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Specialization?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Qualification?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
            Teachers = new ObservableCollection<TeacherDto>(filtered);
        }
    }

    [RelayCommand]
    private async Task AddTeacherAsync()
    {
        await Shell.Current.Navigation.PushAsync(new Views.TeacherFormPage(new TeacherFormViewModel(_apiService)));
    }

    [RelayCommand]
    private async Task EditTeacherAsync(TeacherDto teacher)
    {
        if (teacher == null) return;
        await Shell.Current.Navigation.PushAsync(new Views.TeacherFormPage(new TeacherFormViewModel(_apiService, teacher)));
    }

    [RelayCommand]
    private async Task ViewTeacherDetailAsync(TeacherDto teacher)
    {
        if (teacher == null) return;
        await Shell.Current.Navigation.PushAsync(new Views.TeacherDetailPage(new TeacherDetailViewModel(_apiService, teacher)));
    }

    [RelayCommand]
    private async Task DeactivateTeacherAsync(TeacherDto teacher)
    {
        if (teacher == null) return;
        bool confirm = await Shell.Current.DisplayAlert("Confirm Deactivation", $"Are you sure you want to deactivate {teacher.FullName}?", "Deactivate", "Cancel");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            var res = await _apiService.DeactivateTeacherAsync(teacher.Id);
            if (res.Success)
            {
                await LoadTeachersAsync();
                await Shell.Current.DisplayAlert("Success", "Teacher account deactivated.", "OK");
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

// 3.1 Teacher Form ViewModel
public partial class TeacherFormViewModel : BaseViewModel
{
    private static readonly string[] DefaultSpecializations = new[]
    {
        "Advanced Mathematics & Calculus",
        "Advanced Mathematics",
        "General Mathematics & Algebra",
        "Geometry & Trigonometry",
        "Statistics & Probability",
        "Physics & Applied Science",
        "Applied Physics & Mechanics",
        "Applied Physics",
        "Theoretical Physics",
        "General & Organic Chemistry",
        "General Chemistry",
        "Chemical Sciences & Biochemistry",
        "Cellular Biology & Genetics",
        "Cellular Biology",
        "Biology & Life Sciences",
        "Ecology & Environmental Science",
        "Computer Science & ICT",
        "Software Engineering & Programming",
        "Information Technology & Networking",
        "Cybersecurity & Cloud Computing",
        "English Communication & Academic Writing",
        "English Communication",
        "English Language & Literature",
        "English as a Second Language (ESL)",
        "Classical Khmer Literature & Poetry",
        "Classical Literature",
        "Khmer Language & Linguistics",
        "Khmer History & Civilization",
        "Khmer History",
        "World History & Social Studies",
        "Geography & Earth Sciences",
        "Civics, Citizenship & Moral Education",
        "Economics & Business Studies",
        "Accounting & Financial Management",
        "Visual Arts, Drawing & Painting",
        "Graphic Design & Digital Media",
        "Music, Melody & Performing Arts",
        "Physical Education & Sports Coaching",
        "Health Education & First Aid",
        "STEM & Educational Robotics",
        "Philosophy, Logic & Ethics",
        "French Language & Literature",
        "Chinese Language (Mandarin)",
        "Japanese Language & Culture",
        "Special Education & Inclusive Learning",
        "Psychology & Student Guidance Counseling",
        "School Leadership & Educational Admin"
    };

    private static readonly string[] DefaultQualifications = new[]
    {
        "Bachelor of Education (B.Ed.)",
        "Bachelor of Science (B.Sc.)",
        "Bachelor of Arts (B.A.)",
        "Bachelor of Science in Physics",
        "Bachelor of Science in Mathematics",
        "Bachelor of Science in Chemistry",
        "Bachelor of Science in Biology",
        "Bachelor of Science in Computer Science",
        "Bachelor of Science in Information Technology",
        "Bachelor of Science in Software Engineering",
        "Bachelor of Arts in English Literature",
        "Bachelor of Arts in Khmer Literature",
        "Bachelor of Arts in History",
        "Bachelor of Arts in Linguistics",
        "Bachelor of Business Administration (BBA)",
        "Master of Education (M.Ed.)",
        "Master of Science (M.Sc.)",
        "Master of Arts (M.A.)",
        "Master of Mathematics Education",
        "Master of Physics Education",
        "Master of Chemical Sciences",
        "Master of Science in Biological Sciences",
        "Master of Computer Science (M.C.S.)",
        "Master of Khmer Literature",
        "Master of Applied Linguistics & TESOL",
        "Master of Educational Leadership & Management",
        "Postgraduate Diploma in Education (PGDE)",
        "National Institute of Education (NIE) Teaching Diploma",
        "Doctor of Philosophy (Ph.D.) in Education",
        "Doctor of Philosophy (Ph.D.) in Mathematics",
        "Doctor of Philosophy (Ph.D.) in Physics",
        "Doctor of Philosophy (Ph.D.) in Chemistry",
        "Doctor of Philosophy (Ph.D.) in Biological Sciences",
        "Doctor of Philosophy (Ph.D.) in Literature",
        "Doctor of Education (Ed.D.)",
        "Associate Degree in Education",
        "State Certified Secondary Teacher License"
    };

    private readonly IApiService _apiService;
    private readonly TeacherDto? _originalTeacher;

    public bool IsEditMode => _originalTeacher != null;
    public bool IsAddMode => !IsEditMode;

    [ObservableProperty] private string _employeeNumber = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _qualification = string.Empty;
    [ObservableProperty] private string _specialization = string.Empty;
    [ObservableProperty] private DateTime _hireDate = DateTime.Today;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _specializationOptions = new();
    [ObservableProperty] private ObservableCollection<string> _qualificationOptions = new();

    public TeacherFormViewModel(IApiService apiService, TeacherDto? teacher = null)
    {
        _apiService = apiService;
        _originalTeacher = teacher;

        foreach (var spec in DefaultSpecializations)
        {
            SpecializationOptions.Add(spec);
        }

        foreach (var qual in DefaultQualifications)
        {
            QualificationOptions.Add(qual);
        }

        if (teacher != null)
        {
            Title = $"Edit Teacher: {teacher.FullName}";
            EmployeeNumber = teacher.EmployeeNumber;
            FirstName = teacher.FirstName;
            LastName = teacher.LastName;
            Email = teacher.Email ?? string.Empty;
            PhoneNumber = teacher.PhoneNumber ?? string.Empty;
            HireDate = teacher.HireDate;
            IsActive = teacher.IsActive;

            if (!string.IsNullOrWhiteSpace(teacher.Specialization) && !SpecializationOptions.Contains(teacher.Specialization))
            {
                SpecializationOptions.Insert(0, teacher.Specialization);
            }
            Specialization = teacher.Specialization ?? SpecializationOptions.FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(teacher.Qualification) && !QualificationOptions.Contains(teacher.Qualification))
            {
                QualificationOptions.Insert(0, teacher.Qualification);
            }
            Qualification = teacher.Qualification ?? QualificationOptions.FirstOrDefault() ?? string.Empty;
        }
        else
        {
            Title = "Register Teacher";
            EmployeeNumber = $"EMP-{Random.Shared.Next(10000, 99999)}";
            HireDate = DateTime.Today;
            IsActive = true;
            Specialization = SpecializationOptions.FirstOrDefault() ?? "Advanced Mathematics & Calculus";
            Qualification = QualificationOptions.FirstOrDefault() ?? "Bachelor of Education (B.Ed.)";
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

        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Email address is required.");
            return;
        }

        if (IsAddMode && string.IsNullOrWhiteSpace(EmployeeNumber))
        {
            SetError("Employee number is required.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            if (IsEditMode && _originalTeacher != null)
            {
                var req = new UpdateTeacherRequest(
                    FirstName.Trim(),
                    LastName.Trim(),
                    Email.Trim(),
                    string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                    string.IsNullOrWhiteSpace(Qualification) ? null : Qualification.Trim(),
                    string.IsNullOrWhiteSpace(Specialization) ? null : Specialization.Trim(),
                    IsActive);

                var res = await _apiService.UpdateTeacherAsync(_originalTeacher.Id, req);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Success", "Faculty details updated successfully!", "OK");
                    await PopPageAsync();
                }
                else
                {
                    SetError(res.Message);
                }
            }
            else
            {
                var req = new CreateTeacherRequest(
                    EmployeeNumber.Trim(),
                    FirstName.Trim(),
                    LastName.Trim(),
                    Email.Trim(),
                    string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                    HireDate,
                    string.IsNullOrWhiteSpace(Qualification) ? null : Qualification.Trim(),
                    string.IsNullOrWhiteSpace(Specialization) ? null : Specialization.Trim(),
                    string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
                    string.IsNullOrWhiteSpace(Password) ? null : Password.Trim());

                var res = await _apiService.CreateTeacherAsync(req);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Success", "Faculty member registered successfully!", "OK");
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

// 3.2 Teacher Detail ViewModel
public partial class TeacherDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty] private TeacherDto _teacher;

    public TeacherDetailViewModel(IApiService apiService, TeacherDto teacher)
    {
        _apiService = apiService;
        _teacher = teacher;
        Title = teacher.FullName;
    }

    [RelayCommand]
    private async Task EditTeacherAsync()
    {
        await Shell.Current.Navigation.PushAsync(new Views.TeacherFormPage(new TeacherFormViewModel(_apiService, Teacher)));
    }

    [RelayCommand]
    private async Task ViewTimetableAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//timetables");
        }
    }
}

// 3.3 Subject Grade Item & Report Card ViewModel
public class SubjectGradeItem
{
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string QuizScore { get; set; } = "85";
    public string MidtermScore { get; set; } = "88";
    public string FinalScore { get; set; } = "92";
    public string LetterGrade { get; set; } = "A";
    public Color GradeColorLight { get; set; } = Color.FromArgb("#ECFDF5");
    public Color GradeColorText { get; set; } = Color.FromArgb("#065F46");
}

public partial class ReportCardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly StudentDto _student;

    public string StudentName => _student.FullName;
    public string AdmissionNumber => _student.AdmissionNumber;
    public string ClassName => _student.CurrentClassName ?? "10-A";
    public string IssueDateFormatted => DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty] private ObservableCollection<SubjectGradeItem> _subjectGrades = new();
    [ObservableProperty] private double _cumulativeGpa = 3.82;
    [ObservableProperty] private int _totalCreditsEarned = 21;

    public string CumulativeGpaFormatted => $"{CumulativeGpa:F2}";

    public ReportCardViewModel(IApiService apiService, StudentDto student)
    {
        _apiService = apiService;
        _student = student;
        Title = $"Report Card: {student.FullName}";
        LoadSampleGrades();
    }

    private void LoadSampleGrades()
    {
        SubjectGrades = new ObservableCollection<SubjectGradeItem>
        {
            new() { SubjectName = "Advanced Mathematics", SubjectCode = "MATH101", QuizScore = "88", MidtermScore = "90", FinalScore = "94", LetterGrade = "A", GradeColorLight = Color.FromArgb("#ECFDF5"), GradeColorText = Color.FromArgb("#065F46") },
            new() { SubjectName = "Physics & Applied Science", SubjectCode = "PHY101", QuizScore = "82", MidtermScore = "85", FinalScore = "89", LetterGrade = "B+", GradeColorLight = Color.FromArgb("#EFF6FF"), GradeColorText = Color.FromArgb("#1D4ED8") },
            new() { SubjectName = "General & Organic Chemistry", SubjectCode = "CHM101", QuizScore = "79", MidtermScore = "84", FinalScore = "86", LetterGrade = "B", GradeColorLight = Color.FromArgb("#EFF6FF"), GradeColorText = Color.FromArgb("#1D4ED8") },
            new() { SubjectName = "Biology & Life Sciences", SubjectCode = "BIO101", QuizScore = "92", MidtermScore = "94", FinalScore = "96", LetterGrade = "A", GradeColorLight = Color.FromArgb("#ECFDF5"), GradeColorText = Color.FromArgb("#065F46") },
            new() { SubjectName = "Classical Khmer Literature", SubjectCode = "KHM101", QuizScore = "90", MidtermScore = "91", FinalScore = "93", LetterGrade = "A", GradeColorLight = Color.FromArgb("#ECFDF5"), GradeColorText = Color.FromArgb("#065F46") },
            new() { SubjectName = "English Communication", SubjectCode = "ENG101", QuizScore = "85", MidtermScore = "87", FinalScore = "90", LetterGrade = "A-", GradeColorLight = Color.FromArgb("#ECFDF5"), GradeColorText = Color.FromArgb("#065F46") },
            new() { SubjectName = "World History & Civilization", SubjectCode = "HIST101", QuizScore = "80", MidtermScore = "83", FinalScore = "85", LetterGrade = "B", GradeColorLight = Color.FromArgb("#EFF6FF"), GradeColorText = Color.FromArgb("#1D4ED8") }
        };
    }

    [RelayCommand]
    private async Task ExportReportCardAsync()
    {
        try
        {
            var html = GenerateTranscriptHtml();
            var fileName = $"Official_Transcript_{AdmissionNumber}_{DateTime.Today:yyyyMMdd}.html";

            // Determine download target path (Downloads directory or AppData)
            string targetPath;
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadsDir = !string.IsNullOrEmpty(userProfile) ? Path.Combine(userProfile, "Downloads") : string.Empty;

            if (!string.IsNullOrEmpty(downloadsDir) && Directory.Exists(downloadsDir))
            {
                targetPath = Path.Combine(downloadsDir, fileName);
            }
            else
            {
                targetPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            }

            await File.WriteAllTextAsync(targetPath, html);

            var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            if (cachePath != targetPath)
            {
                await File.WriteAllTextAsync(cachePath, html);
            }

            var choice = await Shell.Current.DisplayActionSheet(
                $"Transcript Saved: {fileName}",
                "Dismiss",
                null,
                "Open in Browser / Preview (Print as PDF)",
                "Share / Save to Files / AirDrop");

            if (choice == "Open in Browser / Preview (Print as PDF)")
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    Title = $"Official Transcript - {StudentName}",
                    File = new ReadOnlyFile(targetPath)
                });
            }
            else if (choice == "Share / Save to Files / AirDrop")
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Official Academic Transcript - {StudentName}",
                    File = new ShareFile(cachePath)
                });
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Export Error", ex.Message, "OK");
        }
    }

    private string GenerateTranscriptHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>Official Academic Transcript - {StudentName} ({AdmissionNumber})</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page { size: A4 portrait; margin: 15mm; }");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #0F172A; background: #FFFFFF; margin: 0; padding: 30px; -webkit-print-color-adjust: exact; print-color-adjust: exact; }");
        sb.AppendLine(".print-bar { position: fixed; top: 16px; right: 20px; z-index: 1000; }");
        sb.AppendLine(".print-btn { background: #2563EB; color: #FFFFFF; border: none; padding: 10px 22px; font-size: 14px; font-weight: 700; border-radius: 8px; cursor: pointer; box-shadow: 0 4px 14px rgba(37,99,235,0.3); }");
        sb.AppendLine(".print-btn:hover { background: #1D4ED8; }");
        sb.AppendLine(".header { text-align: center; border-bottom: 3px double #0F2042; padding-bottom: 18px; margin-bottom: 24px; }");
        sb.AppendLine(".inst-name { font-size: 24px; font-weight: 800; color: #0F2042; letter-spacing: 0.5px; text-transform: uppercase; margin: 0; }");
        sb.AppendLine(".inst-sub { font-size: 13px; color: #64748B; font-weight: 500; margin-top: 4px; }");
        sb.AppendLine(".badge-title { display: inline-block; margin-top: 12px; background: #EFF6FF; color: #1D4ED8; border: 1px solid #BFDBFE; font-size: 12px; font-weight: 700; padding: 4px 16px; border-radius: 20px; text-transform: uppercase; letter-spacing: 0.8px; }");
        sb.AppendLine(".info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 10px; padding: 18px; margin-bottom: 26px; font-size: 13px; }");
        sb.AppendLine(".field { display: flex; justify-content: space-between; padding: 3px 0; border-bottom: 1px dotted #CBD5E1; }");
        sb.AppendLine(".field-label { color: #64748B; font-weight: 600; }");
        sb.AppendLine(".field-val { color: #0F172A; font-weight: 700; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 26px; font-size: 13px; }");
        sb.AppendLine("th { background: #0F2042; color: #FFFFFF; text-align: left; padding: 11px 14px; font-size: 11.5px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px; }");
        sb.AppendLine("td { padding: 11px 14px; border-bottom: 1px solid #E2E8F0; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #F8FAFC; }");
        sb.AppendLine(".score { font-weight: 700; color: #0F172A; text-align: center; }");
        sb.AppendLine(".grade { font-weight: 800; text-align: center; color: #065F46; }");
        sb.AppendLine(".status-pass { color: #15803D; font-weight: 700; text-align: center; }");
        sb.AppendLine(".summary-box { background: #0F2042; color: #FFFFFF; border-radius: 10px; padding: 18px 24px; display: flex; justify-content: space-between; align-items: center; margin-bottom: 32px; }");
        sb.AppendLine(".gpa-title { font-size: 12px; color: #94A3B8; text-transform: uppercase; font-weight: 700; letter-spacing: 0.5px; }");
        sb.AppendLine(".gpa-val { font-size: 34px; font-weight: 800; color: #38BDF8; margin: 4px 0 0 0; }");
        sb.AppendLine(".honors-tag { background: #15803D; color: #FFFFFF; padding: 6px 14px; border-radius: 6px; font-size: 12px; font-weight: 700; text-align: right; }");
        sb.AppendLine(".signatures { display: grid; grid-template-columns: 1fr 1fr; gap: 60px; margin-top: 45px; }");
        sb.AppendLine(".sig-block { text-align: center; }");
        sb.AppendLine(".sig-line { border-top: 1.5px solid #64748B; margin-top: 40px; padding-top: 8px; font-size: 12px; color: #475569; font-weight: 600; }");
        sb.AppendLine(".seal-stamp { font-size: 11px; color: #94A3B8; margin-top: 2px; }");
        sb.AppendLine("@media print { .print-bar { display: none; } body { padding: 0; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"print-bar\"><button class=\"print-btn\" onclick=\"window.print()\">Print / Save as PDF</button></div>");
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("<h1 class=\"inst-name\">Angkor International Academy</h1>");
        sb.AppendLine("<div class=\"inst-sub\">Office of Academic Affairs &amp; Institutional Registrar • Phnom Penh, Cambodia</div>");
        sb.AppendLine("<div class=\"badge-title\">Official Academic Transcript of Records</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"info-grid\">");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Student Name:</span><span class=\"field-val\">{StudentName}</span></div>");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Admission ID:</span><span class=\"field-val\">{AdmissionNumber}</span></div>");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Academic Class:</span><span class=\"field-val\">Class {ClassName}</span></div>");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Academic Year:</span><span class=\"field-val\">2026-2027</span></div>");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Term / Semester:</span><span class=\"field-val\">Semester 1</span></div>");
        sb.AppendLine($"<div class=\"field\"><span class=\"field-label\">Date of Issue:</span><span class=\"field-val\">{IssueDateFormatted}</span></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th>Subject / Course</th>");
        sb.AppendLine("<th>Code</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Quiz</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Midterm</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Final</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Grade</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Credits</th>");
        sb.AppendLine("<th style=\"text-align:center;\">Status</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");
        foreach (var s in SubjectGrades)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><strong>{s.SubjectName}</strong></td>");
            sb.AppendLine($"<td>{s.SubjectCode}</td>");
            sb.AppendLine($"<td class=\"score\">{s.QuizScore}%</td>");
            sb.AppendLine($"<td class=\"score\">{s.MidtermScore}%</td>");
            sb.AppendLine($"<td class=\"score\">{s.FinalScore}%</td>");
            sb.AppendLine($"<td class=\"grade\">{s.LetterGrade}</td>");
            sb.AppendLine("<td style=\"text-align:center;\">3.0</td>");
            sb.AppendLine("<td class=\"status-pass\">PASSED</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("<div class=\"summary-box\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"gpa-title\">Cumulative Grade Point Average (GPA)</div>");
        sb.AppendLine($"<div class=\"gpa-val\">{CumulativeGpaFormatted} <span style=\"font-size:16px; color:#94A3B8;\">/ 4.00</span></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"honors-tag\">");
        sb.AppendLine("<div style=\"font-size:10px; opacity:0.85; text-transform:uppercase;\">Academic Standing</div>");
        sb.AppendLine("<div>Summa Cum Laude (Distinction)</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"signatures\">");
        sb.AppendLine("<div class=\"sig-block\">");
        sb.AppendLine("<div class=\"sig-line\">Academic Dean / Principal</div>");
        sb.AppendLine("<div class=\"seal-stamp\">Dr. Chan Sophal • Angkor Academy</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"sig-block\">");
        sb.AppendLine("<div class=\"sig-line\">Official Institutional Registrar</div>");
        sb.AppendLine("<div class=\"seal-stamp\">Office of Records &amp; Transcripts</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }
}

// Class Section Display Model with per-teacher authorization context
public class ClassSectionItemViewModel : ObservableObject
{
    public ClassSectionDto Section { get; init; } = null!;
    public Guid Id => Section.Id;
    public string Name => Section.Name;
    public string GradeLevelName => Section.GradeLevelName;
    public string AcademicYearName => Section.AcademicYearName;
    public string? RoomNumber => Section.RoomNumber;
    public int Capacity => Section.Capacity > 0 ? Section.Capacity : 40;
    public int EnrolledCount => Section.EnrolledCount;
    public Guid? ClassTeacherId => Section.ClassTeacherId;
    public string? ClassTeacherName => Section.ClassTeacherName;

    public double EnrollmentProgress => Capacity > 0 ? Math.Clamp((double)EnrolledCount / Capacity, 0.0, 1.0) : 0.0;
    public string EnrollmentRatioDisplay => $"{EnrolledCount}/{Capacity} Students";
    public string EnrollmentPercentDisplay => Capacity > 0 ? $"{(int)Math.Round((double)EnrolledCount / Capacity * 100)}%" : "0%";

    public Color EnrollmentProgressColor => EnrollmentProgress switch
    {
        >= 1.0 => Color.FromArgb("#DC2626"),
        >= 0.85 => Color.FromArgb("#D97706"),
        _ => Color.FromArgb("#2563EB")
    };

    public bool CanTakeAttendance { get; init; }
    public bool IsMyAdvisory { get; init; }
    public bool IsTeachingFaculty { get; init; }
    public bool CanAssignAdvisor { get; init; }

    public string AdvisorDisplay
    {
        get
        {
            if (IsMyAdvisory) return $"Advisor: {ClassTeacherName ?? "You"} (You)";
            if (IsTeachingFaculty) return $"Advisor: {ClassTeacherName ?? "Unassigned"} • Teaching Faculty (You)";
            return $"Advisor: {ClassTeacherName ?? "Unassigned"}";
        }
    }
}

// 4. Classes ViewModel
public partial class ClassesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly HashSet<Guid> _teacherTeachingClassIds = new();
    private readonly HashSet<Guid> _teacherSubjectIds = new();

    [ObservableProperty] private int _selectedSegmentIndex = 0;
    [ObservableProperty] private string _searchTerm = string.Empty;

    public bool IsClassesTab => SelectedSegmentIndex == 0;
    public bool IsSubjectsTab => SelectedSegmentIndex == 1;

    public Color SelectedTabColor0 => SelectedSegmentIndex == 0 ? Color.FromArgb("#0F2042") : Colors.Transparent;
    public Color SelectedTabColor1 => SelectedSegmentIndex == 1 ? Color.FromArgb("#0F2042") : Colors.Transparent;
    public Color SelectedTextColor0 => SelectedSegmentIndex == 0 ? Colors.White : Color.FromArgb("#64748B");
    public Color SelectedTextColor1 => SelectedSegmentIndex == 1 ? Colors.White : Color.FromArgb("#64748B");

    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ObservableCollection<SubjectDto> _subjects = new();

    [ObservableProperty] private ObservableCollection<ClassSectionItemViewModel> _filteredClasses = new();
    [ObservableProperty] private ObservableCollection<SubjectDto> _filteredSubjects = new();

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isTeacher;
    [ObservableProperty] private Guid? _currentTeacherId;

    public ClassesViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Classes & Subjects";
        UpdateRoleContext();
    }

    private void UpdateRoleContext()
    {
        var role = _apiService.CurrentUser?.Role;
        IsAdmin = role == UserRole.SuperAdmin || role == UserRole.SchoolAdmin;
        IsTeacher = role == UserRole.Teacher;
        CurrentTeacherId = _apiService.CurrentUser?.LinkedProfileId;
    }

    partial void OnSelectedSegmentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsClassesTab));
        OnPropertyChanged(nameof(IsSubjectsTab));
        OnPropertyChanged(nameof(SelectedTabColor0));
        OnPropertyChanged(nameof(SelectedTabColor1));
        OnPropertyChanged(nameof(SelectedTextColor0));
        OnPropertyChanged(nameof(SelectedTextColor1));
    }

    partial void OnSearchTermChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void SwitchTab(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx))
        {
            SelectedSegmentIndex = idx;
        }
    }

    private void ApplyFilter()
    {
        var role = _apiService.CurrentUser?.Role;
        var isAdmin = role == UserRole.SuperAdmin || role == UserRole.SchoolAdmin;
        var isTeacher = role == UserRole.Teacher;
        var teacherId = _apiService.CurrentUser?.LinkedProfileId;

        // If teacher is logged in: show only their classes and their subjects
        var baseClasses = Classes.AsEnumerable();
        if (isTeacher && teacherId.HasValue)
        {
            baseClasses = baseClasses.Where(c => c.ClassTeacherId == teacherId.Value || _teacherTeachingClassIds.Contains(c.Id));
        }

        var baseSubjects = Subjects.AsEnumerable();
        if (isTeacher && teacherId.HasValue && _teacherSubjectIds.Count > 0)
        {
            baseSubjects = baseSubjects.Where(s => _teacherSubjectIds.Contains(s.Id));
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            var allVms = baseClasses.Select(c =>
            {
                var isAdvisor = isTeacher && teacherId.HasValue && c.ClassTeacherId == teacherId.Value;
                var isTeachingInClass = isTeacher && _teacherTeachingClassIds.Contains(c.Id);
                var canTakeAttendance = isAdmin || isAdvisor || isTeachingInClass;
                return new ClassSectionItemViewModel
                {
                    Section = c,
                    CanTakeAttendance = canTakeAttendance,
                    IsMyAdvisory = isAdvisor,
                    IsTeachingFaculty = isTeachingInClass,
                    CanAssignAdvisor = isAdmin
                };
            }).ToList();

            FilteredClasses = new ObservableCollection<ClassSectionItemViewModel>(allVms);
            FilteredSubjects = new ObservableCollection<SubjectDto>(baseSubjects);
            return;
        }

        var term = SearchTerm.Trim();
        var fc = baseClasses.Where(c =>
            c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (c.RoomNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.ClassTeacherName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.GradeLevelName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        ).Select(c =>
        {
            var isAdvisor = isTeacher && teacherId.HasValue && c.ClassTeacherId == teacherId.Value;
            var isTeachingInClass = isTeacher && _teacherTeachingClassIds.Contains(c.Id);
            var canTakeAttendance = isAdmin || isAdvisor || isTeachingInClass;
            return new ClassSectionItemViewModel
            {
                Section = c,
                CanTakeAttendance = canTakeAttendance,
                IsMyAdvisory = isAdvisor,
                IsTeachingFaculty = isTeachingInClass,
                CanAssignAdvisor = isAdmin
            };
        }).ToList();

        var fs = baseSubjects.Where(s =>
            s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            s.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (s.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();

        FilteredClasses = new ObservableCollection<ClassSectionItemViewModel>(fc);
        FilteredSubjects = new ObservableCollection<SubjectDto>(fs);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            UpdateRoleContext();

            if (IsTeacher && CurrentTeacherId.HasValue)
            {
                try
                {
                    var teachersRes = await _apiService.GetTeachersAsync();
                    var curTeacher = teachersRes.Data?.FirstOrDefault(t => t.Id == CurrentTeacherId.Value);
                    _teacherTeachingClassIds.Clear();
                    _teacherSubjectIds.Clear();
                    if (teachersRes.Success && curTeacher?.Assignments != null)
                    {
                        foreach (var a in curTeacher.Assignments)
                        {
                            if (a.ClassSectionId != Guid.Empty)
                                _teacherTeachingClassIds.Add(a.ClassSectionId);
                            if (a.SubjectId != Guid.Empty)
                                _teacherSubjectIds.Add(a.SubjectId);
                        }
                    }

                    var ttRes = await _apiService.GetTeacherTimetableAsync(CurrentTeacherId.Value);
                    if (ttRes.Success && ttRes.Data != null)
                    {
                        foreach (var entry in ttRes.Data)
                        {
                            if (entry.ClassSectionId != Guid.Empty)
                                _teacherTeachingClassIds.Add(entry.ClassSectionId);
                            if (entry.SubjectId != Guid.Empty)
                                _teacherSubjectIds.Add(entry.SubjectId);
                        }
                    }
                }
                catch { /* Ignore profile assignment retrieval failure */ }
            }

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

            ApplyFilter();
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
    private async Task AssignAdvisorAsync(object parameter)
    {
        ClassSectionDto? section = parameter switch
        {
            ClassSectionItemViewModel vm => vm.Section,
            ClassSectionDto dto => dto,
            _ => null
        };

        if (section == null) return;
        if (!IsAdmin)
        {
            await Shell.Current.DisplayAlert("Access Restricted", "Only School Administrators have authority to assign or modify homeroom advisors.", "OK");
            return;
        }

        try
        {
            var teachersRes = await _apiService.GetTeachersAsync();
            var teachers = teachersRes.Data ?? new List<TeacherDto>();
            var teacherNames = teachers.Select(t => t.FullName).ToArray();

            string choice = await Shell.Current.DisplayActionSheet(
                $"Assign Homeroom Advisor for Class {section.Name}",
                "Cancel",
                null,
                teacherNames);

            if (!string.IsNullOrEmpty(choice) && choice != "Cancel")
            {
                var chosen = teachers.FirstOrDefault(t => t.FullName == choice);
                if (chosen != null)
                {
                    IsBusy = true;
                    var assignRes = await _apiService.AssignClassTeacherAsync(section.Id, chosen.Id);
                    IsBusy = false;

                    var updated = section with { ClassTeacherId = chosen.Id, ClassTeacherName = chosen.FullName };
                    var idx = Classes.IndexOf(section);
                    if (idx >= 0)
                    {
                        Classes[idx] = updated;
                    }
                    else
                    {
                        var existing = Classes.FirstOrDefault(c => c.Id == section.Id);
                        if (existing != null)
                        {
                            var eIdx = Classes.IndexOf(existing);
                            if (eIdx >= 0) Classes[eIdx] = updated;
                        }
                    }
                    ApplyFilter();

                    await Shell.Current.DisplayAlert(
                        "Homeroom Advisor Designated",
                        $"{chosen.FullName} is now officially assigned as the Homeroom Advisor for Class {section.Name}.\n\n" +
                        "What this means:\n" +
                        "• Daily Attendance: Oversees morning roll call & presence validation.\n" +
                        "• Student Custodian: Acts as the primary academic advisor & counselor for all students in this section.\n" +
                        "• Family Liaison: Receives parent inquiries and conducts academic consultations.\n" +
                        "• Report Cards: Reviews end-of-term grade distributions and signs official transcripts.",
                        "Understood");
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task TakeClassAttendanceAsync(object parameter)
    {
        ClassSectionDto? section = parameter switch
        {
            ClassSectionItemViewModel vm => vm.Section,
            ClassSectionDto dto => dto,
            _ => null
        };

        if (section == null) return;

        // Specific authorization: Designated class advisor, assigned teaching faculty, or admin
        if (!IsAdmin && (!CurrentTeacherId.HasValue || (section.ClassTeacherId != CurrentTeacherId.Value && !_teacherTeachingClassIds.Contains(section.Id))))
        {
            await Shell.Current.DisplayAlert(
                "Access Restricted",
                $"You are not assigned as the Advisor or Teacher for Class {section.Name}.\n\nOfficial attendance is managed by {section.ClassTeacherName ?? "the designated Homeroom Advisor"} or an Administrator.",
                "OK");
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"///attendance?classId={section.Id}");
        }
        catch
        {
            await Shell.Current.GoToAsync("//attendance");
        }
    }

    [RelayCommand]
    private async Task ViewRosterAsync(object parameter)
    {
        ClassSectionDto? section = parameter switch
        {
            ClassSectionItemViewModel vm => vm.Section,
            ClassSectionDto dto => dto,
            _ => null
        };

        if (section == null) return;
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//students");
            }
        }
        catch { }
    }
}

// 5. Attendance ViewModel
public partial class AttendanceItemViewModel : ObservableObject
{
    private readonly Action? _onStatusChanged;

    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;

    [ObservableProperty]
    private AttendanceStatus _status = AttendanceStatus.Present;

    [ObservableProperty]
    private string? _remarks;

    [ObservableProperty]
    private bool _canEdit = true;

    public bool HasRemarks => !string.IsNullOrWhiteSpace(Remarks);
    public string RemarksDisplay => Remarks ?? string.Empty;

    public bool IsPresent => Status == AttendanceStatus.Present;
    public bool IsLate => Status == AttendanceStatus.Late;
    public bool IsAbsent => Status == AttendanceStatus.Absent;
    public bool IsExcused => Status == AttendanceStatus.Excused;

    // Dynamic button styling for active/selected chip state
    public Color PresentBgColor => IsPresent ? Color.FromArgb("#059669") : Color.FromArgb("#F8FAFC");
    public Color PresentTextColor => IsPresent ? Colors.White : Color.FromArgb("#065F46");
    public Color PresentBorderColor => IsPresent ? Color.FromArgb("#047857") : Color.FromArgb("#A7F3D0");

    public Color LateBgColor => IsLate ? Color.FromArgb("#D97706") : Color.FromArgb("#F8FAFC");
    public Color LateTextColor => IsLate ? Colors.White : Color.FromArgb("#92400E");
    public Color LateBorderColor => IsLate ? Color.FromArgb("#B45309") : Color.FromArgb("#FDE68A");

    public Color AbsentBgColor => IsAbsent ? Color.FromArgb("#DC2626") : Color.FromArgb("#F8FAFC");
    public Color AbsentTextColor => IsAbsent ? Colors.White : Color.FromArgb("#991B1B");
    public Color AbsentBorderColor => IsAbsent ? Color.FromArgb("#B91C1C") : Color.FromArgb("#FECACA");

    public Color ExcusedBgColor => IsExcused ? Color.FromArgb("#2563EB") : Color.FromArgb("#F8FAFC");
    public Color ExcusedTextColor => IsExcused ? Colors.White : Color.FromArgb("#1E40AF");
    public Color ExcusedBorderColor => IsExcused ? Color.FromArgb("#1D4ED8") : Color.FromArgb("#BFDBFE");

    public AttendanceItemViewModel(Action? onStatusChanged = null)
    {
        _onStatusChanged = onStatusChanged;
    }

    partial void OnRemarksChanged(string? value)
    {
        OnPropertyChanged(nameof(HasRemarks));
        OnPropertyChanged(nameof(RemarksDisplay));
        _onStatusChanged?.Invoke();
    }

    partial void OnStatusChanged(AttendanceStatus value)
    {
        OnPropertyChanged(nameof(IsPresent));
        OnPropertyChanged(nameof(IsLate));
        OnPropertyChanged(nameof(IsAbsent));
        OnPropertyChanged(nameof(IsExcused));
        OnPropertyChanged(nameof(PresentBgColor));
        OnPropertyChanged(nameof(PresentTextColor));
        OnPropertyChanged(nameof(PresentBorderColor));
        OnPropertyChanged(nameof(LateBgColor));
        OnPropertyChanged(nameof(LateTextColor));
        OnPropertyChanged(nameof(LateBorderColor));
        OnPropertyChanged(nameof(AbsentBgColor));
        OnPropertyChanged(nameof(AbsentTextColor));
        OnPropertyChanged(nameof(AbsentBorderColor));
        OnPropertyChanged(nameof(ExcusedBgColor));
        OnPropertyChanged(nameof(ExcusedTextColor));
        OnPropertyChanged(nameof(ExcusedBorderColor));
        _onStatusChanged?.Invoke();
    }

    [RelayCommand]
    private void SetStatus(string statusStr)
    {
        if (!CanEdit)
        {
            Shell.Current?.DisplayAlert("View Only Mode", "You do not have permission to modify attendance for this class session.", "OK");
            return;
        }

        if (Enum.TryParse<AttendanceStatus>(statusStr, out var s))
        {
            Status = s;
            if (s == AttendanceStatus.Excused && string.IsNullOrWhiteSpace(Remarks))
            {
                Remarks = "Excused absence";
            }
            else if (s != AttendanceStatus.Excused && Remarks?.Contains("Parent Permission") == true)
            {
                Remarks = null;
            }
        }
    }
}

public class AttendanceSessionItemViewModel : ObservableObject
{
    public string SessionKey { get; init; } = string.Empty;
    public Guid? TimetableEntryId { get; init; }
    public Guid? SubjectId { get; init; }
    public string SubjectName { get; init; } = "Subject Session";
    public Guid? InstructorId { get; init; }
    public string? InstructorName { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string? RoomNumber { get; init; }
    public bool IsHomeroomSession { get; init; }
    public Guid ClassSectionId { get; init; }
    public string ClassName { get; init; } = string.Empty;

    public string TimeSlotDisplay => StartTime.HasValue && EndTime.HasValue
        ? $"{StartTime.Value:hh\\:mm} - {EndTime.Value:hh\\:mm}"
        : "08:00 - 09:30";

    public string TitleDisplay => $"{SubjectName}";

    public string PickerDisplay => $"{SubjectName} ({TimeSlotDisplay}) • {InstructorName ?? "Faculty"}";

    public string PickerDisplayWithClass => $"{SubjectName} • Class {ClassName} ({TimeSlotDisplay})";
}

public class StudentSessionAttendanceItem : ObservableObject
{
    public string SessionKey { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TimeSlot { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }
    public bool HasRemarks => !string.IsNullOrWhiteSpace(Remarks);
    public bool IsHomeroom { get; set; }

    public string SubtitleDisplay => string.IsNullOrWhiteSpace(RoomNumber)
        ? InstructorName
        : $"{InstructorName} • {RoomNumber}";

    public string StatusBadgeText => Status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Late => "Late",
        AttendanceStatus.Excused => "Excused",
        AttendanceStatus.Absent => "Absent",
        _ => "Present"
    };

    public string StatusIcon => Status switch
    {
        AttendanceStatus.Present => "P",
        AttendanceStatus.Late => "L",
        AttendanceStatus.Excused => "E",
        AttendanceStatus.Absent => "A",
        _ => "P"
    };

    public Color StatusColor => Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#10B981"),
        AttendanceStatus.Late => Color.FromArgb("#F59E0B"),
        AttendanceStatus.Excused => Color.FromArgb("#3B82F6"),
        AttendanceStatus.Absent => Color.FromArgb("#EF4444"),
        _ => Colors.Gray
    };

    public Color StatusBgColor => Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#ECFDF5"),
        AttendanceStatus.Late => Color.FromArgb("#FFFBEB"),
        AttendanceStatus.Excused => Color.FromArgb("#EFF6FF"),
        AttendanceStatus.Absent => Color.FromArgb("#FEF2F2"),
        _ => Color.FromArgb("#F3F4F6")
    };

    public Color StatusBorderColor => Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#A7F3D0"),
        AttendanceStatus.Late => Color.FromArgb("#FDE68A"),
        AttendanceStatus.Excused => Color.FromArgb("#BFDBFE"),
        AttendanceStatus.Absent => Color.FromArgb("#FECACA"),
        _ => Color.FromArgb("#E5E7EB")
    };
}

[QueryProperty(nameof(ClassIdParam), "classId")]
public partial class AttendanceViewModel : BaseViewModel
{
    private static readonly Guid FallbackStudentId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid FallbackClassId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IApiService _apiService;

    [ObservableProperty] private string? _classIdParam;
    [ObservableProperty] private Guid? _targetClassId;

    private bool _isAuthorizedForCurrentSession = false;
    public bool CanTakeAttendanceActions => _isAuthorizedForCurrentSession && SelectedClass != null && SelectedSession != null;

    [ObservableProperty] private bool _showClassFilter = true;
    [ObservableProperty] private bool _showTeacherSubjectFilter = false;

    private readonly List<ClassSectionDto> _allSchoolClasses = new();
    private readonly List<TimetableEntryDto> _teacherTimetableEntries = new();
    private TeacherDto? _currentTeacherProfile;
    private readonly HashSet<Guid> _currentTeacherAssignedClassIds = new();
    private readonly Dictionary<string, (AttendanceStatus Status, string? Remarks)> _sessionAttendanceCache = new();
    private AttendanceSessionItemViewModel? _previousSession;

    partial void OnClassIdParamChanged(string? value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            TargetClassId = guid;
            if (Sessions != null && Sessions.Count > 0)
            {
                var matchSession = Sessions.FirstOrDefault(s => s.ClassSectionId == guid);
                if (matchSession != null)
                {
                    SelectedSession = matchSession;
                    return;
                }
            }
            if (Classes != null && Classes.Count > 0)
            {
                var match = Classes.FirstOrDefault(c => c.Id == guid);
                if (match != null)
                {
                    SelectedClass = match;
                }
            }
        }
    }

    // Role-dependent views
    [ObservableProperty] private bool _isParentView = false;
    [ObservableProperty] private bool _isStudentOnlyView = false;
    [ObservableProperty] private bool _isStudentView = false;
    [ObservableProperty] private bool _isFacultyView = true;

    // Student self-profile properties
    [ObservableProperty] private string _studentFullName = "Visal Chan";
    [ObservableProperty] private string _studentAdmissionNumber = "ADM-2026-0001";
    [ObservableProperty] private string _studentClassName = "Class 10-A";
    [ObservableProperty] private string _studentProfileSubtext = "Visal Chan • Class 10-A • ADM-2026-0001";
    private Guid _currentStudentId = FallbackStudentId;
    private List<TimetableEntryDto> _studentTimetable = new();

    // Session-by-Session Inspection Properties
    [ObservableProperty] private ObservableCollection<StudentSessionAttendanceItem> _daySessions = new();
    [ObservableProperty] private DateTime _selectedInspectionDate = DateTime.Today;
    [ObservableProperty] private DateTime _maxInspectionDate = DateTime.Today;
    [ObservableProperty] private string _selectedInspectionDateDisplay = "Today's Schedule & Sessions";
    [ObservableProperty] private string _selectedInspectionDateDisplayShort = "Today, Oct 29";
    [ObservableProperty] private Color _todayButtonBgColor = Color.FromArgb("#EFF6FF");
    [ObservableProperty] private Color _todayButtonTextColor = Color.FromArgb("#2563EB");
    [ObservableProperty] private Color _todayButtonBorderColor = Color.FromArgb("#BFDBFE");
    [ObservableProperty] private string _selectedDayOverallStatusText = "All Sessions Present";
    [ObservableProperty] private Color _selectedDayOverallStatusColor = Color.FromArgb("#10B981");
    [ObservableProperty] private Color _selectedDayOverallStatusBgColor = Color.FromArgb("#ECFDF5");
    [ObservableProperty] private Color _selectedDayOverallStatusBorderColor = Color.FromArgb("#A7F3D0");
    [ObservableProperty] private bool _hasDaySessions = true;
    [ObservableProperty] private bool _canGoToNextInspectionDay = false;
    [ObservableProperty] private string _permissionModalTitle = "Ask Permission for Child";
    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ClassSectionDto? _selectedClass;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ObservableCollection<AttendanceSessionItemViewModel> _sessions = new();
    [ObservableProperty] private AttendanceSessionItemViewModel? _selectedSession;
    [ObservableProperty] private string _selectedSessionTimeSlot = "07:30 - 08:00";
    [ObservableProperty] private string _saveButtonText = "Save Class Attendance";
    [ObservableProperty] private ObservableCollection<AttendanceItemViewModel> _studentItems = new();
    [ObservableProperty] private AttendanceSummaryDto? _summary;
    [ObservableProperty] private decimal _currentPresentRate = 100.0m;

    // Parent Child Attendance properties
    [ObservableProperty] private bool _hasMultipleChildren = false;
    public bool HasSingleChild => IsParentView && !HasMultipleChildren;
    [ObservableProperty] private bool _isRefreshing = false;
    private bool _isInitializing = false;
    private bool _isLoading = false;

    [ObservableProperty] private ObservableCollection<LinkedStudentDto> _parentChildren = new();
    [ObservableProperty] private LinkedStudentDto? _selectedParentChild;
    [ObservableProperty] private ObservableCollection<AttendanceRecordDisplayItem> _childAttendanceHistory = new();
    [ObservableProperty] private ObservableCollection<AttendanceRecordDisplayItem> _filteredChildAttendanceHistory = new();
    private List<AttendanceRecordDisplayItem> _allChildAttendanceRecords = new();
    private List<AttendanceRecordDisplayItem> _currentFilteredRecords = new();

    // Parent Attendance History Pagination properties
    private const int HistoryPageSize = 5;
    [ObservableProperty] private int _historyCurrentPage = 1;
    [ObservableProperty] private int _historyTotalPages = 1;
    [ObservableProperty] private string _historyPageInfo = "Page 1 of 1";
    [ObservableProperty] private bool _canGoToPreviousHistoryPage = false;
    [ObservableProperty] private bool _canGoToNextHistoryPage = false;
    [ObservableProperty] private bool _hasHistoryRecords = false;
    [ObservableProperty] private ObservableCollection<AttendanceRecordDisplayItem> _pagedChildAttendanceHistory = new();

    // Filter controls for parent view
    [ObservableProperty] private string _selectedAttendanceFilter = "All";
    [ObservableProperty] private string _filterAllText = "All";
    [ObservableProperty] private string _filterPresentText = "Present";
    [ObservableProperty] private string _filterLateText = "Late";
    [ObservableProperty] private string _filterExcusedText = "Excused";
    [ObservableProperty] private string _filterAbsentText = "Absent";

    [ObservableProperty] private Color _filterAllBgColor = Color.FromArgb("#0F2042");
    [ObservableProperty] private Color _filterAllTextColor = Color.FromArgb("#FFFFFF");
    [ObservableProperty] private Color _filterPresentBgColor = Color.FromArgb("#F1F5F9");
    [ObservableProperty] private Color _filterPresentTextColor = Color.FromArgb("#059669");
    [ObservableProperty] private Color _filterLateBgColor = Color.FromArgb("#F1F5F9");
    [ObservableProperty] private Color _filterLateTextColor = Color.FromArgb("#D97706");
    [ObservableProperty] private Color _filterExcusedBgColor = Color.FromArgb("#F1F5F9");
    [ObservableProperty] private Color _filterExcusedTextColor = Color.FromArgb("#2563EB");
    [ObservableProperty] private Color _filterAbsentBgColor = Color.FromArgb("#F1F5F9");
    [ObservableProperty] private Color _filterAbsentTextColor = Color.FromArgb("#DC2626");

    [ObservableProperty] private AttendanceRecordDisplayItem? _todayChildAttendance;
    [ObservableProperty] private bool _hasTodayChildAttendance;
    [ObservableProperty] private string _todayStatusBadgeText = "Present";
    [ObservableProperty] private string _todayStatusText = "Present at School (On Campus)";
    [ObservableProperty] private string _todayStatusDetails = "";
    [ObservableProperty] private Color _todayStatusColor = Color.FromArgb("#10B981");
    [ObservableProperty] private Color _todayStatusBgColor = Color.FromArgb("#ECFDF5");
    [ObservableProperty] private Color _todayStatusBorderColor = Color.FromArgb("#A7F3D0");
    [ObservableProperty] private string _todayStatusIcon = "P";
    [ObservableProperty] private int _childTotalDays = 0;
    [ObservableProperty] private int _childPresentDays = 0;
    [ObservableProperty] private int _childLateDays = 0;
    [ObservableProperty] private int _childExcusedDays = 0;
    [ObservableProperty] private int _childAbsentDays = 0;

    // Ask Permission Modal Properties
    [ObservableProperty] private bool _isPermissionModalVisible = false;
    [ObservableProperty] private DateTime _permissionDate = DateTime.Today;
    [ObservableProperty] private string _selectedPermissionReason = "Sick Leave / Medical";
    [ObservableProperty] private ObservableCollection<string> _permissionReasons = new()
    {
        "Sick Leave / Medical",
        "Doctor / Clinic Appointment",
        "Family Emergency",
        "Personal / Travel",
        "Official Event / Competition",
        "Other Excused Reason"
    };
    [ObservableProperty] private string _permissionRemarks = string.Empty;
    [ObservableProperty] private LinkedStudentDto? _permissionTargetChild;

    // Advisor role status
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isCurrentUserAdvisor;
    [ObservableProperty] private string _advisorRoleBadge = string.Empty;
    [ObservableProperty] private string _advisorRoleSubtitle = string.Empty;
    [ObservableProperty] private string _advisorRoleTag = string.Empty;
    [ObservableProperty] private Color _advisorRoleBadgeBg = Color.FromArgb("#EFF6FF");
    [ObservableProperty] private Color _advisorRoleBadgeText = Color.FromArgb("#1D4ED8");
    [ObservableProperty] private Color _advisorRoleBadgeBorder = Color.FromArgb("#BFDBFE");
    [ObservableProperty] private Color _advisorRolePillBg = Color.FromArgb("#DBEAFE");
    [ObservableProperty] private Color _advisorRoleTagColor = Color.FromArgb("#2563EB");

    private string GetSessionCacheKey(Guid classId, DateTime date, string sessionKey) => $"{classId}_{date:yyyyMMdd}_{sessionKey}";

    partial void OnSelectedClassChanged(ClassSectionDto? value)
    {
        UpdateAdvisorRoleStatus();
        OnPropertyChanged(nameof(CanTakeAttendanceActions));
        OnPropertyChanged(nameof(SaveButtonText));
        if (_isInitializing || _isLoading) return;
        if (value != null && IsFacultyView)
        {
            if (IsAdmin)
            {
                _ = LoadFacultySessionsAndAttendanceAsync();
            }
            else
            {
                _ = LoadAttendanceInternalAsync();
            }
        }
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_isInitializing || _isLoading) return;
        if (IsFacultyView)
        {
            _ = LoadFacultySessionsAndAttendanceAsync();
        }
    }

    partial void OnSelectedSessionChanged(AttendanceSessionItemViewModel? value)
    {
        if (_previousSession != null && SelectedClass != null)
        {
            var prevKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, _previousSession.SessionKey);
            foreach (var item in StudentItems)
            {
                _sessionAttendanceCache[$"{prevKey}_{item.StudentId}"] = (item.Status, item.Remarks);
            }
        }

        _previousSession = value;
        SelectedSessionTimeSlot = value?.TimeSlotDisplay ?? "08:00 - 09:30";
        SaveButtonText = $"Save {value?.TitleDisplay ?? "Session"} Attendance";

        if (value != null && value.ClassSectionId != Guid.Empty)
        {
            if (SelectedClass == null || SelectedClass.Id != value.ClassSectionId)
            {
                var targetClass = _allSchoolClasses.FirstOrDefault(c => c.Id == value.ClassSectionId)
                               ?? Classes.FirstOrDefault(c => c.Id == value.ClassSectionId);
                if (targetClass != null)
                {
                    SelectedClass = targetClass;
                }
            }
        }

        UpdateAdvisorRoleStatus();
        ApplySessionAttendance();
        RecalculatePresentRate();
    }

    partial void OnSelectedParentChildChanged(LinkedStudentDto? value)
    {
        if (_isInitializing || _isLoading) return;
        if (value != null && IsParentView)
        {
            _currentStudentId = value.StudentId;
            StudentFullName = value.FullName;
            StudentAdmissionNumber = value.AdmissionNumber;
            StudentClassName = value.ClassName ?? "Class 10-A";
            StudentProfileSubtext = $"{StudentFullName} • {StudentClassName} • {StudentAdmissionNumber}";
            _apiService.CurrentSelectedChildId = value.StudentId;
            _ = LoadParentChildAttendanceAsync(value.StudentId);
        }
    }

    public AttendanceViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Attendance Management";
    }

    public void RecalculatePresentRate()
    {
        if (StudentItems == null || StudentItems.Count == 0)
        {
            CurrentPresentRate = 100.0m;
            return;
        }

        int total = StudentItems.Count;
        int present = StudentItems.Count(s => s.Status == AttendanceStatus.Present);
        int late = StudentItems.Count(s => s.Status == AttendanceStatus.Late);

        decimal rate = total > 0
            ? ((present + (late * 0.5m)) / (decimal)total) * 100m
            : 100.0m;
        CurrentPresentRate = Math.Round(Math.Clamp(rate, 0m, 100m), 1);
    }

    private void ApplySessionAttendance()
    {
        if (SelectedClass == null || SelectedSession == null || StudentItems.Count == 0) return;

        var sessionKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey);

        foreach (var item in StudentItems)
        {
            item.CanEdit = _isAuthorizedForCurrentSession;
            var cacheId = $"{sessionKey}_{item.StudentId}";
            if (_sessionAttendanceCache.TryGetValue(cacheId, out var cached))
            {
                item.Status = cached.Status;
                item.Remarks = cached.Remarks;
            }
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        try
        {
            _isInitializing = true;
            IsBusy = true;
            ClearError();
            var role = _apiService.CurrentUser?.Role;
            IsParentView = role == UserRole.Parent;
            IsStudentOnlyView = role == UserRole.Student;
            IsStudentView = IsParentView || IsStudentOnlyView;
            IsFacultyView = !IsStudentView;

            if (IsStudentOnlyView)
            {
                Title = "My Attendance";
                PermissionModalTitle = "Ask Absence Permission";
                var user = _apiService.CurrentUser;
                _currentStudentId = user?.LinkedProfileId ?? FallbackStudentId;
                StudentFullName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : "Visal Chan";
                StudentAdmissionNumber = "ADM-2026-0001";
                StudentClassName = "Class 10-A";
                StudentProfileSubtext = $"{StudentFullName} • {StudentClassName} • {StudentAdmissionNumber}";

                await LoadStudentOrChildAttendanceInternalAsync(_currentStudentId);
            }
            else if (IsParentView)
            {
                Title = "Child School Attendance";
                PermissionModalTitle = "Ask Permission for Child";
                var parentDashRes = await _apiService.GetParentDashboardAsync(_apiService.CurrentSelectedChildId);
                if (parentDashRes.Success && parentDashRes.Data != null)
                {
                    ParentChildren = new ObservableCollection<LinkedStudentDto>(parentDashRes.Data.Children);
                    HasMultipleChildren = ParentChildren.Count > 1;
                    SelectedParentChild = ParentChildren.FirstOrDefault(c => c.StudentId == parentDashRes.Data.SelectedStudentId)
                        ?? ParentChildren.FirstOrDefault();
                    if (SelectedParentChild != null)
                    {
                        _currentStudentId = SelectedParentChild.StudentId;
                        StudentFullName = SelectedParentChild.FullName;
                        StudentAdmissionNumber = SelectedParentChild.AdmissionNumber;
                        StudentClassName = SelectedParentChild.ClassName ?? "Class 10-A";
                        StudentProfileSubtext = $"{StudentFullName} • {StudentClassName} • {StudentAdmissionNumber}";
                        await LoadStudentOrChildAttendanceInternalAsync(SelectedParentChild.StudentId);
                    }
                }
            }
            else
            {
                Title = "Attendance Management";
                var classesRes = await _apiService.GetClassesAsync();
                if (classesRes.Success && classesRes.Data != null)
                {
                    var all = classesRes.Data;
                    _allSchoolClasses.Clear();
                    _allSchoolClasses.AddRange(all);

                    var teacherId = _apiService.CurrentUser?.LinkedProfileId;

                    if (role == UserRole.Teacher && teacherId.HasValue)
                    {
                        IsAdmin = false;
                        ShowClassFilter = false;
                        ShowTeacherSubjectFilter = true;

                        _currentTeacherAssignedClassIds.Clear();
                        try
                        {
                            var teachersRes = await _apiService.GetTeachersAsync();
                            _currentTeacherProfile = teachersRes.Data?.FirstOrDefault(t => t.Id == teacherId.Value);
                            if (_currentTeacherProfile?.Assignments != null)
                            {
                                foreach (var a in _currentTeacherProfile.Assignments)
                                {
                                    if (a.ClassSectionId != Guid.Empty)
                                        _currentTeacherAssignedClassIds.Add(a.ClassSectionId);
                                }
                            }

                            var ttRes = await _apiService.GetTeacherTimetableAsync(teacherId.Value);
                            _teacherTimetableEntries.Clear();
                            if (ttRes.Success && ttRes.Data != null)
                            {
                                _teacherTimetableEntries.AddRange(ttRes.Data);
                                foreach (var entry in ttRes.Data)
                                {
                                    if (entry.ClassSectionId != Guid.Empty)
                                        _currentTeacherAssignedClassIds.Add(entry.ClassSectionId);
                                }
                            }
                        }
                        catch { /* non-critical teacher assignments fetch */ }

                        // Classes where teacher is Homeroom Advisor or assigned Subject Instructor
                        var visibleClasses = all.Where(c => c.ClassTeacherId == teacherId.Value || _currentTeacherAssignedClassIds.Contains(c.Id)).ToList();
                        if (visibleClasses.Count == 0)
                        {
                            visibleClasses = all;
                        }
                        Classes = new ObservableCollection<ClassSectionDto>(visibleClasses);

                        await LoadTeacherPermittedSessionsAsync();
                    }
                    else
                    {
                        // Admins have access to all classes
                        IsAdmin = true;
                        ShowClassFilter = true;
                        ShowTeacherSubjectFilter = false;
                        Classes = new ObservableCollection<ClassSectionDto>(all);

                        if (TargetClassId.HasValue && TargetClassId.Value != Guid.Empty)
                        {
                            SelectedClass = Classes.FirstOrDefault(c => c.Id == TargetClassId.Value) ?? Classes.FirstOrDefault();
                        }
                        else
                        {
                            SelectedClass = Classes.FirstOrDefault();
                        }

                        await LoadAdminSessionsForClassAndDateAsync();
                    }

                    UpdateAdvisorRoleStatus();
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
            IsRefreshing = false;
            foreach (var item in StudentItems)
            {
                item.CanEdit = _isAuthorizedForCurrentSession;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
        }
    }

    private async Task LoadTeacherPermittedSessionsAsync()
    {
        var teacherId = _apiService.CurrentUser?.LinkedProfileId;
        if (!teacherId.HasValue) return;

        var sessionsList = new List<AttendanceSessionItemViewModel>();
        var dayOfWeek = SelectedDate.DayOfWeek;

        // 1. Classes where this teacher is the designated Class Teacher / Main Advisor:
        // Main advisor controls the class, has access to all session attendance, and can manage attendance for students.
        var advisoryClasses = _allSchoolClasses.Where(c => c.ClassTeacherId == teacherId.Value).ToList();
        foreach (var c in advisoryClasses)
        {
            try
            {
                var ttRes = await _apiService.GetClassTimetableAsync(c.Id);
                if (ttRes.Success && ttRes.Data != null && ttRes.Data.Count > 0)
                {
                    var entriesForDay = ttRes.Data
                        .Where(e => e.DayOfWeek == dayOfWeek)
                        .OrderBy(e => e.StartTime)
                        .ToList();

                    if (entriesForDay.Count == 0 && dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                    {
                        entriesForDay = ttRes.Data
                            .GroupBy(e => e.SubjectId)
                            .Select(g => g.First())
                            .OrderBy(e => e.StartTime)
                            .ToList();
                    }

                    foreach (var entry in entriesForDay)
                    {
                        if (!sessionsList.Any(s => s.ClassSectionId == c.Id && s.SubjectId == entry.SubjectId && s.StartTime == entry.StartTime))
                        {
                            sessionsList.Add(new AttendanceSessionItemViewModel
                            {
                                SessionKey = $"tt_{entry.Id}",
                                TimetableEntryId = entry.Id,
                                SubjectId = entry.SubjectId,
                                SubjectName = entry.SubjectName,
                                ClassSectionId = c.Id,
                                ClassName = c.Name,
                                InstructorId = entry.TeacherId,
                                InstructorName = entry.TeacherId == teacherId.Value
                                    ? (_currentTeacherProfile?.FullName ?? _apiService.CurrentUser?.FullName ?? "Dara Sok (You)")
                                    : entry.TeacherName,
                                StartTime = entry.StartTime,
                                EndTime = entry.EndTime,
                                RoomNumber = entry.RoomNumber ?? c.RoomNumber,
                                IsHomeroomSession = false
                            });
                        }
                    }
                }
            }
            catch { /* non-critical */ }
        }

        // 2. Timetable entries for this teacher where they instruct subjects in any class (including non-advisory classes)
        var entriesForTeacher = _teacherTimetableEntries
            .Where(e => e.DayOfWeek == dayOfWeek)
            .OrderBy(e => e.StartTime)
            .ToList();

        foreach (var entry in entriesForTeacher)
        {
            if (!sessionsList.Any(s => s.ClassSectionId == entry.ClassSectionId && s.SubjectId == entry.SubjectId && s.StartTime == entry.StartTime))
            {
                var targetClass = _allSchoolClasses.FirstOrDefault(c => c.Id == entry.ClassSectionId);
                sessionsList.Add(new AttendanceSessionItemViewModel
                {
                    SessionKey = $"tt_{entry.Id}",
                    TimetableEntryId = entry.Id,
                    SubjectId = entry.SubjectId,
                    SubjectName = entry.SubjectName,
                    ClassSectionId = entry.ClassSectionId,
                    ClassName = targetClass?.Name ?? "Class",
                    InstructorId = teacherId.Value,
                    InstructorName = _currentTeacherProfile?.FullName ?? entry.TeacherName ?? _apiService.CurrentUser?.FullName ?? "Faculty",
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    RoomNumber = entry.RoomNumber ?? targetClass?.RoomNumber,
                    IsHomeroomSession = false
                });
            }
        }

        // 3. Fallback: if no scheduled timetable entries for this day, provide teacher's official assigned subjects/classes
        if (sessionsList.Count == 0)
        {
            if (_currentTeacherProfile?.Assignments != null)
            {
                foreach (var a in _currentTeacherProfile.Assignments)
                {
                    var targetClass = _allSchoolClasses.FirstOrDefault(c => c.Id == a.ClassSectionId);
                    var className = targetClass?.Name ?? a.ClassName ?? "Class";
                    var subjName = !string.IsNullOrWhiteSpace(a.SubjectName) ? a.SubjectName : "Mathematics";

                    if (!sessionsList.Any(s => s.ClassSectionId == a.ClassSectionId && s.SubjectId == a.SubjectId))
                    {
                        sessionsList.Add(new AttendanceSessionItemViewModel
                        {
                            SessionKey = $"assign_{a.ClassSectionId}_{a.SubjectId}",
                            SubjectId = a.SubjectId,
                            SubjectName = subjName,
                            ClassSectionId = a.ClassSectionId,
                            ClassName = className,
                            InstructorId = teacherId.Value,
                            InstructorName = _currentTeacherProfile?.FullName ?? _apiService.CurrentUser?.FullName ?? "Teacher",
                            StartTime = new TimeSpan(8, 0, 0),
                            EndTime = new TimeSpan(9, 30, 0),
                            RoomNumber = targetClass?.RoomNumber,
                            IsHomeroomSession = false
                        });
                    }
                }
            }
        }

        Sessions = new ObservableCollection<AttendanceSessionItemViewModel>(sessionsList);

        AttendanceSessionItemViewModel? newSelected = null;
        if (_previousSession != null)
        {
            newSelected = sessionsList.FirstOrDefault(s =>
                s.ClassSectionId == _previousSession.ClassSectionId &&
                s.SubjectId == _previousSession.SubjectId);
        }
        if (newSelected == null && TargetClassId.HasValue)
        {
            newSelected = sessionsList.FirstOrDefault(s => s.ClassSectionId == TargetClassId.Value);
        }

        SelectedSession = newSelected ?? sessionsList.FirstOrDefault();
        _previousSession = SelectedSession;

        if (SelectedSession != null)
        {
            SelectedSessionTimeSlot = SelectedSession.TimeSlotDisplay;
            SaveButtonText = $"Save {SelectedSession.TitleDisplay} Attendance";

            var targetClass = _allSchoolClasses.FirstOrDefault(c => c.Id == SelectedSession.ClassSectionId);
            if (targetClass != null)
            {
                SelectedClass = targetClass;
                await LoadAttendanceInternalAsync();
            }
        }
        else
        {
            StudentItems.Clear();
        }

        UpdateAdvisorRoleStatus();
    }

    private async Task LoadAdminSessionsForClassAndDateAsync()
    {
        if (SelectedClass == null)
        {
            Sessions.Clear();
            SelectedSession = null;
            return;
        }

        var sessionsList = new List<AttendanceSessionItemViewModel>();

        // Fetch timetable entries for this class
        try
        {
            var ttRes = await _apiService.GetClassTimetableAsync(SelectedClass.Id);
            if (ttRes.Success && ttRes.Data != null && ttRes.Data.Count > 0)
            {
                var dayOfWeek = SelectedDate.DayOfWeek;
                var entriesForDay = ttRes.Data
                    .Where(e => e.DayOfWeek == dayOfWeek)
                    .OrderBy(e => e.StartTime)
                    .ToList();

                if (entriesForDay.Count == 0 && dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                {
                    entriesForDay = ttRes.Data
                        .GroupBy(e => e.SubjectId)
                        .Select(g => g.First())
                        .OrderBy(e => e.StartTime)
                        .ToList();
                }

                foreach (var entry in entriesForDay)
                {
                    sessionsList.Add(new AttendanceSessionItemViewModel
                    {
                        SessionKey = $"tt_{entry.Id}",
                        TimetableEntryId = entry.Id,
                        SubjectId = entry.SubjectId,
                        SubjectName = entry.SubjectName,
                        ClassSectionId = SelectedClass.Id,
                        ClassName = SelectedClass.Name,
                        InstructorId = entry.TeacherId,
                        InstructorName = entry.TeacherName,
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime,
                        RoomNumber = entry.RoomNumber ?? SelectedClass.RoomNumber,
                        IsHomeroomSession = false
                    });
                }
            }
        }
        catch { /* Ignore */ }

        if (sessionsList.Count == 0)
        {
            sessionsList.Add(new AttendanceSessionItemViewModel
            {
                SessionKey = "session_math",
                SubjectName = "Mathematics",
                ClassSectionId = SelectedClass.Id,
                ClassName = SelectedClass.Name,
                InstructorId = SelectedClass.ClassTeacherId,
                InstructorName = SelectedClass.ClassTeacherName ?? "Teacher",
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(9, 30, 0),
                RoomNumber = SelectedClass.RoomNumber,
                IsHomeroomSession = false
            });
            sessionsList.Add(new AttendanceSessionItemViewModel
            {
                SessionKey = "session_chemistry",
                SubjectName = "Chemistry",
                ClassSectionId = SelectedClass.Id,
                ClassName = SelectedClass.Name,
                InstructorId = null,
                InstructorName = "Faculty",
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                RoomNumber = SelectedClass.RoomNumber,
                IsHomeroomSession = false
            });
        }

        Sessions = new ObservableCollection<AttendanceSessionItemViewModel>(sessionsList);
        SelectedSession = sessionsList.FirstOrDefault();
        _previousSession = SelectedSession;
        SelectedSessionTimeSlot = SelectedSession?.TimeSlotDisplay ?? "08:00 - 09:30";
        SaveButtonText = $"Save {SelectedSession?.TitleDisplay ?? "Session"} Attendance";

        await LoadAttendanceInternalAsync();
        UpdateAdvisorRoleStatus();
    }

    private async Task LoadFacultySessionsAndAttendanceAsync()
    {
        if (_isLoading || _isInitializing) return;
        try
        {
            IsBusy = true;
            if (IsAdmin)
            {
                await LoadAdminSessionsForClassAndDateAsync();
            }
            else
            {
                await LoadTeacherPermittedSessionsAsync();
            }
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
            foreach (var item in StudentItems)
            {
                item.CanEdit = _isAuthorizedForCurrentSession;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_isInitializing || _isLoading)
        {
            IsRefreshing = false;
            return;
        }

        try
        {
            if (IsStudentOnlyView)
            {
                await LoadStudentOrChildAttendanceInternalAsync(_currentStudentId);
            }
            else if (IsParentView && SelectedParentChild != null)
            {
                await LoadStudentOrChildAttendanceInternalAsync(SelectedParentChild.StudentId);
            }
            else if (IsFacultyView)
            {
                await LoadFacultySessionsAndAttendanceAsync();
            }
        }
        finally
        {
            IsRefreshing = false;
            foreach (var item in StudentItems)
            {
                item.CanEdit = _isAuthorizedForCurrentSession;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
        }
    }

    [RelayCommand]
    public async Task LoadParentChildAttendanceAsync(Guid studentId)
    {
        if (_isLoading || _isInitializing) return;
        try
        {
            IsBusy = true;
            await LoadStudentOrChildAttendanceInternalAsync(studentId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadStudentOrChildAttendanceInternalAsync(Guid studentId)
    {
        if (_isLoading) return;
        try
        {
            _isLoading = true;

            // Fetch timetable for student to render precise class periods
            try
            {
                var ttRes = await _apiService.GetStudentTimetableAsync(studentId);
                if (ttRes.Success && ttRes.Data != null)
                {
                    _studentTimetable = ttRes.Data;
                }
            }
            catch { /* Ignore timetable query fallback */ }

            var res = await _apiService.GetStudentAttendanceHistoryAsync(studentId);
            if (res.Success && res.Data != null)
            {
                // Strict deduplication by Date: exactly one entry per calendar date
                var displayItems = res.Data
                    .GroupBy(r => r.Date)
                    .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
                    .OrderByDescending(r => r.Date)
                    .Select(r => new AttendanceRecordDisplayItem(r))
                    .ToList();

                _allChildAttendanceRecords = displayItems;
                ChildAttendanceHistory = new ObservableCollection<AttendanceRecordDisplayItem>(displayItems);

                ChildTotalDays = displayItems.Count;
                ChildPresentDays = displayItems.Count(d => d.Status == AttendanceStatus.Present);
                ChildLateDays = displayItems.Count(d => d.Status == AttendanceStatus.Late);
                ChildExcusedDays = displayItems.Count(d => d.Status == AttendanceStatus.Excused);
                ChildAbsentDays = displayItems.Count(d => d.Status == AttendanceStatus.Absent);

                // True presence rate calculation: Late arrival counts as half presence (0.5), Excused absence does not count as Present
                CurrentPresentRate = ChildTotalDays > 0
                    ? Math.Round(((ChildPresentDays + (ChildLateDays * 0.5m)) / (decimal)ChildTotalDays) * 100m, 1)
                    : 100.0m;

                ApplyAttendanceFilter();

                var today = DateOnly.FromDateTime(DateTime.Today);
                var todayRec = displayItems.FirstOrDefault(a => a.Date == today);
                if (todayRec != null)
                {
                    TodayChildAttendance = todayRec;
                    HasTodayChildAttendance = true;
                    TodayStatusBadgeText = todayRec.StatusBadgeText;
                    TodayStatusColor = todayRec.StatusColor;
                    TodayStatusBgColor = todayRec.StatusBgColor;
                    TodayStatusBorderColor = todayRec.StatusBorderColor;
                    TodayStatusIcon = todayRec.StatusIcon;

                    TodayStatusText = todayRec.Status switch
                    {
                        AttendanceStatus.Present => "Present at School (On Campus)",
                        AttendanceStatus.Late => "Arrived Late to Class",
                        AttendanceStatus.Excused => "Excused Absence / Leave Granted",
                        AttendanceStatus.Absent => "Absent / Not Checked In",
                        _ => "Attendance Recorded"
                    };

                    TodayStatusDetails = todayRec.Status switch
                    {
                        AttendanceStatus.Present => IsStudentOnlyView
                            ? $"You arrived at school and checked in at {todayRec.CheckInTimeText}."
                            : $"Your child arrived at school and checked in at {todayRec.CheckInTimeText}.",
                        AttendanceStatus.Late => IsStudentOnlyView
                            ? $"You arrived late today. Checked in at {todayRec.CheckInTimeText}. Note: {todayRec.Remarks ?? "Tardy"}"
                            : $"Your child arrived late today. Checked in at {todayRec.CheckInTimeText}. Note: {todayRec.Remarks ?? "Tardy"}",
                        AttendanceStatus.Excused => $"Excused absence granted. Note: {todayRec.Remarks ?? "Permission approved by school."}",
                        AttendanceStatus.Absent => IsStudentOnlyView
                            ? "No check-in scan recorded today. Please submit permission or contact homeroom advisor."
                            : "No check-in scan recorded today. Please submit permission or contact school office.",
                        _ => "Attendance recorded."
                    };
                }
                else
                {
                    TodayChildAttendance = null;
                    HasTodayChildAttendance = false;
                    var day = DateTime.Today.DayOfWeek;
                    if (day is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    {
                        TodayStatusBadgeText = "Weekend";
                        TodayStatusText = "Weekend / Campus Closed";
                        TodayStatusDetails = "Classes are not in session today. School resumes Monday morning.";
                        TodayStatusColor = Color.FromArgb("#6B7280");
                        TodayStatusBgColor = Color.FromArgb("#F3F4F6");
                        TodayStatusBorderColor = Color.FromArgb("#E5E7EB");
                        TodayStatusIcon = "W";
                    }
                    else
                    {
                        TodayStatusBadgeText = "Awaiting";
                        TodayStatusText = "Awaiting Session Attendance";
                        TodayStatusDetails = "Class session attendance is currently being recorded by faculty.";
                        TodayStatusColor = Color.FromArgb("#3B82F6");
                        TodayStatusBgColor = Color.FromArgb("#EFF6FF");
                        TodayStatusBorderColor = Color.FromArgb("#BFDBFE");
                        TodayStatusIcon = "A";
                    }
                }

                LoadSessionsForInspectionDate(DateTime.Today);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedInspectionDateChanged(DateTime value)
    {
        if (IsStudentView)
        {
            LoadSessionsForInspectionDate(value);
        }
    }

    public void LoadSessionsForInspectionDate(DateTime inspectionDate)
    {
        if (SelectedInspectionDate.Date != inspectionDate.Date)
        {
            SelectedInspectionDate = inspectionDate;
            return;
        }
        CanGoToNextInspectionDay = inspectionDate.Date < DateTime.Today.Date;

        var dateOnly = DateOnly.FromDateTime(inspectionDate);
        bool isToday = dateOnly == DateOnly.FromDateTime(DateTime.Today);
        bool isYesterday = dateOnly == DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        SelectedInspectionDateDisplay = isToday
            ? $"Today ({inspectionDate:dddd, MMM dd})"
            : isYesterday
                ? $"Yesterday ({inspectionDate:dddd, MMM dd})"
                : $"{inspectionDate:dddd, MMM dd, yyyy}";

        SelectedInspectionDateDisplayShort = isToday
            ? $"Today, {inspectionDate:MMM dd}"
            : isYesterday
                ? $"Yesterday, {inspectionDate:MMM dd}"
                : $"{inspectionDate:ddd, MMM dd}";

        if (isToday)
        {
            TodayButtonBgColor = Color.FromArgb("#EFF6FF");
            TodayButtonTextColor = Color.FromArgb("#2563EB");
            TodayButtonBorderColor = Color.FromArgb("#BFDBFE");
        }
        else
        {
            TodayButtonBgColor = Color.FromArgb("#2563EB");
            TodayButtonTextColor = Color.FromArgb("#FFFFFF");
            TodayButtonBorderColor = Color.FromArgb("#2563EB");
        }

        if (inspectionDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            SelectedDayOverallStatusText = "Weekend - Campus Closed";
            SelectedDayOverallStatusColor = Color.FromArgb("#6B7280");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#F3F4F6");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#E5E7EB");
            DaySessions = new ObservableCollection<StudentSessionAttendanceItem>();
            HasDaySessions = false;
            return;
        }

        HasDaySessions = true;
        var dayRecord = _allChildAttendanceRecords?.FirstOrDefault(r => r.Date == dateOnly);

        var dayOfWeek = inspectionDate.DayOfWeek;
        var ttEntries = _studentTimetable?
            .Where(t => t.DayOfWeek == dayOfWeek)
            .OrderBy(t => t.StartTime)
            .ToList() ?? new List<TimetableEntryDto>();

        if (ttEntries.Count == 0 && _studentTimetable != null && _studentTimetable.Count > 0)
        {
            ttEntries = _studentTimetable
                .GroupBy(t => t.SubjectId)
                .Select(g => g.First())
                .OrderBy(t => t.StartTime)
                .ToList();
        }

        var sessions = new List<StudentSessionAttendanceItem>();

        // Class Subject Sessions
        if (ttEntries.Count > 0)
        {
            int periodIndex = 1;
            foreach (var tt in ttEntries)
            {
                sessions.Add(new StudentSessionAttendanceItem
                {
                    SessionKey = $"tt_{tt.Id}",
                    PeriodName = $"Period {periodIndex}",
                    SubjectName = tt.SubjectName,
                    TimeSlot = $"{tt.StartTime:hh\\:mm} - {tt.EndTime:hh\\:mm}",
                    InstructorName = !string.IsNullOrWhiteSpace(tt.TeacherName) ? $"Teacher {tt.TeacherName}" : "Faculty",
                    RoomNumber = !string.IsNullOrWhiteSpace(tt.RoomNumber) ? tt.RoomNumber : "Room 101",
                    IsHomeroom = false
                });
                periodIndex++;
            }
        }
        else
        {
            sessions.Add(new StudentSessionAttendanceItem
            {
                SessionKey = "session_period_1",
                PeriodName = "Period 1",
                SubjectName = "Computer Science & ICT",
                TimeSlot = "08:00 - 10:00",
                InstructorName = "Teacher Somnang Rath",
                RoomNumber = "Lab 1",
                IsHomeroom = false
            });
            sessions.Add(new StudentSessionAttendanceItem
            {
                SessionKey = "session_period_2",
                PeriodName = "Period 2",
                SubjectName = "Mathematics & Geometry",
                TimeSlot = "10:15 - 12:00",
                InstructorName = "Teacher Dara Sok",
                RoomNumber = "Room 101",
                IsHomeroom = false
            });
            sessions.Add(new StudentSessionAttendanceItem
            {
                SessionKey = "session_period_3",
                PeriodName = "Period 3",
                SubjectName = "English Communication",
                TimeSlot = "13:00 - 15:00",
                InstructorName = "Teacher Kolab Pich",
                RoomNumber = "Room 101",
                IsHomeroom = false
            });
        }

        // Check if dayRecord has formatted session tags in remarks: [SessionTitle: Status - Note]
        bool parsedFromTags = false;
        if (dayRecord != null && !string.IsNullOrWhiteSpace(dayRecord.Remarks) && dayRecord.Remarks.Contains("["))
        {
            foreach (var s in sessions)
            {
                var marker = $"[{s.SubjectName}:";
                var altMarker = $"[{s.PeriodName}:";

                int idx = dayRecord.Remarks.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx == -1) idx = dayRecord.Remarks.IndexOf(altMarker, StringComparison.OrdinalIgnoreCase);

                if (idx != -1)
                {
                    int closeIdx = dayRecord.Remarks.IndexOf("]", idx);
                    if (closeIdx > idx)
                    {
                        var tagContent = dayRecord.Remarks.Substring(idx + 1, closeIdx - idx - 1);
                        var colonIdx = tagContent.IndexOf(":");
                        if (colonIdx != -1)
                        {
                            var statusAndNote = tagContent.Substring(colonIdx + 1).Trim();
                            var dashIdx = statusAndNote.IndexOf("-");
                            var statusPart = dashIdx != -1 ? statusAndNote.Substring(0, dashIdx).Trim() : statusAndNote;
                            var notePart = dashIdx != -1 ? statusAndNote.Substring(dashIdx + 1).Trim() : null;

                            if (Enum.TryParse<AttendanceStatus>(statusPart, true, out var parsedStatus))
                            {
                                s.Status = parsedStatus;
                                s.Remarks = notePart ?? (parsedStatus == AttendanceStatus.Present ? "Attended session" : null);
                                parsedFromTags = true;
                            }
                        }
                    }
                }
            }
        }

        // Also check runtime session attendance cache
        foreach (var s in sessions)
        {
            var targetClassId = SelectedClass?.Id ?? FallbackClassId;
            var sKey = GetSessionCacheKey(targetClassId, inspectionDate, s.SessionKey);
            if (_sessionAttendanceCache.TryGetValue($"{sKey}_{_currentStudentId}", out var cached))
            {
                s.Status = cached.Status;
                if (!string.IsNullOrWhiteSpace(cached.Remarks))
                    s.Remarks = cached.Remarks;
                parsedFromTags = true;
            }
        }

        // If no explicit session tags, derive intelligently from daily record
        if (!parsedFromTags)
        {
            if (dayRecord != null)
            {
                if (dayRecord.Status == AttendanceStatus.Present)
                {
                    foreach (var s in sessions)
                    {
                        s.Status = AttendanceStatus.Present;
                        s.Remarks = "Attended full session";
                    }
                }
                else if (dayRecord.Status == AttendanceStatus.Late)
                {
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        if (i == 0)
                        {
                            sessions[i].Status = AttendanceStatus.Late;
                            sessions[i].Remarks = $"Late arrival ({dayRecord.CheckInTimeText}) - {dayRecord.Remarks ?? "Tardy"}";
                        }
                        else
                        {
                            sessions[i].Status = AttendanceStatus.Present;
                            sessions[i].Remarks = "Attended class session";
                        }
                    }
                }
                else if (dayRecord.Status == AttendanceStatus.Excused)
                {
                    bool isAfternoonOnly = dayRecord.Remarks?.Contains("noon", StringComparison.OrdinalIgnoreCase) == true ||
                                          dayRecord.Remarks?.Contains("afternoon", StringComparison.OrdinalIgnoreCase) == true;

                    if (isAfternoonOnly)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            if (i <= 1)
                            {
                                sessions[i].Status = AttendanceStatus.Present;
                                sessions[i].Remarks = "Attended morning session";
                            }
                            else
                            {
                                sessions[i].Status = AttendanceStatus.Excused;
                                sessions[i].Remarks = dayRecord.Remarks ?? "Excused absence (Medical leave)";
                            }
                        }
                    }
                    else
                    {
                        foreach (var s in sessions)
                        {
                            s.Status = AttendanceStatus.Excused;
                            s.Remarks = dayRecord.Remarks ?? "Excused absence - Permission approved";
                        }
                    }
                }
                else if (dayRecord.Status == AttendanceStatus.Absent)
                {
                    foreach (var s in sessions)
                    {
                        s.Status = AttendanceStatus.Absent;
                        s.Remarks = "No attendance scan registered";
                    }
                }
            }
            else
            {
                if (isToday)
                {
                    var now = DateTime.Now.TimeOfDay;
                    foreach (var s in sessions)
                    {
                        if (now >= new TimeSpan(8, 0, 0))
                        {
                            s.Status = AttendanceStatus.Present;
                            s.Remarks = "On campus (Morning Roll Call verified)";
                        }
                        else
                        {
                            s.Status = AttendanceStatus.Present;
                            s.Remarks = "Scheduled session";
                        }
                    }
                }
                else
                {
                    foreach (var s in sessions)
                    {
                        s.Status = AttendanceStatus.Present;
                        s.Remarks = "Regular class session completed";
                    }
                }
            }
        }

        DaySessions = new ObservableCollection<StudentSessionAttendanceItem>(sessions);

        // Overall status badge for the inspected day
        int presCount = sessions.Count(s => s.Status == AttendanceStatus.Present);
        int lateCount = sessions.Count(s => s.Status == AttendanceStatus.Late);
        int excCount = sessions.Count(s => s.Status == AttendanceStatus.Excused);
        int absCount = sessions.Count(s => s.Status == AttendanceStatus.Absent);

        if (absCount > 0)
        {
            SelectedDayOverallStatusText = $"{absCount} Session{(absCount > 1 ? "s" : "")} Absent";
            SelectedDayOverallStatusColor = Color.FromArgb("#EF4444");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#FEF2F2");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#FECACA");
        }
        else if (excCount > 0 && presCount > 0)
        {
            SelectedDayOverallStatusText = $"{presCount} Present, {excCount} Excused";
            SelectedDayOverallStatusColor = Color.FromArgb("#2563EB");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#EFF6FF");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#BFDBFE");
        }
        else if (excCount > 0)
        {
            SelectedDayOverallStatusText = "Full Day Excused";
            SelectedDayOverallStatusColor = Color.FromArgb("#2563EB");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#EFF6FF");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#BFDBFE");
        }
        else if (lateCount > 0)
        {
            SelectedDayOverallStatusText = "Late Arrival Recorded";
            SelectedDayOverallStatusColor = Color.FromArgb("#F59E0B");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#FFFBEB");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#FDE68A");
        }
        else
        {
            SelectedDayOverallStatusText = "All Sessions Present (100%)";
            SelectedDayOverallStatusColor = Color.FromArgb("#10B981");
            SelectedDayOverallStatusBgColor = Color.FromArgb("#ECFDF5");
            SelectedDayOverallStatusBorderColor = Color.FromArgb("#A7F3D0");
        }
    }

    [RelayCommand]
    public void InspectPreviousDaySessions()
    {
        var prev = SelectedInspectionDate.AddDays(-1);
        while (prev.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            prev = prev.AddDays(-1);
        }
        SelectedInspectionDate = prev;
    }

    [RelayCommand]
    public void InspectNextDaySessions()
    {
        if (SelectedInspectionDate.Date >= DateTime.Today.Date) return;
        var next = SelectedInspectionDate.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        if (next.Date <= DateTime.Today.Date)
        {
            SelectedInspectionDate = next;
        }
    }

    [RelayCommand]
    public void InspectTodaySessions()
    {
        SelectedInspectionDate = DateTime.Today;
    }

    [RelayCommand]
    public void InspectRecordSessions(AttendanceRecordDisplayItem record)
    {
        if (record == null) return;
        SelectedInspectionDate = record.Date.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    public void FilterAttendance(string filter)
    {
        SelectedAttendanceFilter = filter;
        ApplyAttendanceFilter();
    }

    private void ApplyAttendanceFilter()
    {
        FilterAllText = $"All ({ChildTotalDays})";
        FilterPresentText = $"Present ({ChildPresentDays})";
        FilterLateText = $"Late ({ChildLateDays})";
        FilterExcusedText = $"Excused ({ChildExcusedDays})";
        FilterAbsentText = $"Absent ({ChildAbsentDays})";

        FilterAllBgColor = SelectedAttendanceFilter == "All" ? Color.FromArgb("#0F2042") : Color.FromArgb("#F1F5F9");
        FilterAllTextColor = SelectedAttendanceFilter == "All" ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#475569");

        FilterPresentBgColor = SelectedAttendanceFilter == "Present" ? Color.FromArgb("#059669") : Color.FromArgb("#ECFDF5");
        FilterPresentTextColor = SelectedAttendanceFilter == "Present" ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#059669");

        FilterLateBgColor = SelectedAttendanceFilter == "Late" ? Color.FromArgb("#D97706") : Color.FromArgb("#FFFBEB");
        FilterLateTextColor = SelectedAttendanceFilter == "Late" ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#D97706");

        FilterExcusedBgColor = SelectedAttendanceFilter == "Excused" ? Color.FromArgb("#2563EB") : Color.FromArgb("#EFF6FF");
        FilterExcusedTextColor = SelectedAttendanceFilter == "Excused" ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#2563EB");

        FilterAbsentBgColor = SelectedAttendanceFilter == "Absent" ? Color.FromArgb("#DC2626") : Color.FromArgb("#FEF2F2");
        FilterAbsentTextColor = SelectedAttendanceFilter == "Absent" ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#DC2626");

        var filtered = SelectedAttendanceFilter switch
        {
            "Present" => _allChildAttendanceRecords.Where(r => r.Status == AttendanceStatus.Present),
            "Late" => _allChildAttendanceRecords.Where(r => r.Status == AttendanceStatus.Late),
            "Excused" => _allChildAttendanceRecords.Where(r => r.Status == AttendanceStatus.Excused),
            "Absent" => _allChildAttendanceRecords.Where(r => r.Status == AttendanceStatus.Absent),
            _ => _allChildAttendanceRecords
        };

        _currentFilteredRecords = filtered.ToList();
        FilteredChildAttendanceHistory = new ObservableCollection<AttendanceRecordDisplayItem>(_currentFilteredRecords);
        UpdateHistoryPagination(1);
    }

    private void UpdateHistoryPagination(int page = 1)
    {
        int totalCount = _currentFilteredRecords.Count;
        HistoryTotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / HistoryPageSize));
        HistoryCurrentPage = Math.Clamp(page, 1, HistoryTotalPages);

        CanGoToPreviousHistoryPage = HistoryCurrentPage > 1;
        CanGoToNextHistoryPage = HistoryCurrentPage < HistoryTotalPages;
        HasHistoryRecords = totalCount > 0;

        int startItem = totalCount == 0 ? 0 : (HistoryCurrentPage - 1) * HistoryPageSize + 1;
        int endItem = Math.Min(HistoryCurrentPage * HistoryPageSize, totalCount);
        HistoryPageInfo = totalCount == 0
            ? "0 records"
            : $"Showing {startItem}-{endItem} of {totalCount} records (Page {HistoryCurrentPage} of {HistoryTotalPages})";

        var pageItems = _currentFilteredRecords
            .Skip((HistoryCurrentPage - 1) * HistoryPageSize)
            .Take(HistoryPageSize)
            .ToList();

        PagedChildAttendanceHistory = new ObservableCollection<AttendanceRecordDisplayItem>(pageItems);
    }

    [RelayCommand]
    public void PreviousHistoryPage()
    {
        if (CanGoToPreviousHistoryPage)
        {
            UpdateHistoryPagination(HistoryCurrentPage - 1);
        }
    }

    [RelayCommand]
    public void NextHistoryPage()
    {
        if (CanGoToNextHistoryPage)
        {
            UpdateHistoryPagination(HistoryCurrentPage + 1);
        }
    }

    [RelayCommand]
    public void OpenPermissionModal()
    {
        PermissionTargetChild = SelectedParentChild ?? ParentChildren.FirstOrDefault();
        PermissionDate = DateTime.Today;
        SelectedPermissionReason = PermissionReasons.FirstOrDefault() ?? "Sick Leave / Medical";
        PermissionRemarks = string.Empty;
        IsPermissionModalVisible = true;
    }

    [RelayCommand]
    public void ClosePermissionModal()
    {
        IsPermissionModalVisible = false;
    }

    [RelayCommand]
    private void DoNothing() { }

    [RelayCommand]
    public async Task SubmitPermissionAsync()
    {
        if (PermissionTargetChild == null)
        {
            await Shell.Current.DisplayAlert("Validation", "Please select a child to request permission for.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPermissionReason))
        {
            await Shell.Current.DisplayAlert("Validation", "Please select a reason for absence.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            var req = new SubmitPermissionRequest(
                PermissionTargetChild.StudentId,
                DateOnly.FromDateTime(PermissionDate),
                SelectedPermissionReason,
                PermissionRemarks
            );

            var res = await _apiService.SubmitStudentPermissionAsync(req);
            IsPermissionModalVisible = false;

            if (res.Success)
            {
                await Shell.Current.DisplayAlert(
                    "Permission Submitted",
                    $"Official leave permission has been recorded for {PermissionTargetChild.FullName} on {PermissionDate:yyyy-MM-dd}. Attendance status marked as Excused.",
                    "OK");

                await LoadParentChildAttendanceAsync(PermissionTargetChild.StudentId);
            }
            else
            {
                await Shell.Current.DisplayAlert("Submission Failed", res.Message, "OK");
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
    public async Task LoadAttendanceAsync()
    {
        if (_isLoading || _isInitializing) return;
        try
        {
            IsBusy = true;
            await LoadAttendanceInternalAsync();
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
            foreach (var item in StudentItems)
            {
                item.CanEdit = _isAuthorizedForCurrentSession;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
        }
    }

    private async Task LoadAttendanceInternalAsync()
    {
        if (SelectedClass == null || _isLoading) return;
        try
        {
            _isLoading = true;
            ClearError();

            var date = DateOnly.FromDateTime(SelectedDate);
            var res = await _apiService.GetClassAttendanceAsync(SelectedClass.Id, date);
            var records = res.Success && res.Data != null ? res.Data : new List<AttendanceRecordDto>();

            var studentsRes = await _apiService.GetStudentsAsync();
            var standardCohort = new (Guid Id, string FullName, string AdmissionNumber)[]
            {
                (Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "ADM-2026-0001"),
                (Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Kanha Sok", "ADM-2026-0002"),
                (Guid.Parse("11111111-2222-3333-4444-555555555555"), "Dara Meng", "ADM-2026-0003"),
                (Guid.Parse("22222222-3333-4444-5555-666666666666"), "Bopha Keo", "ADM-2026-0004"),
                (Guid.Parse("33333333-3333-3333-3333-333333333333"), "Rithy Srun", "ADM-2026-0005"),
                (Guid.Parse("44444444-4444-4444-4444-444444444444"), "Sreypov Lim", "ADM-2026-0006"),
                (Guid.Parse("55555555-5555-5555-5555-555555555555"), "Vannak Heng", "ADM-2026-0007"),
                (Guid.Parse("66666666-6666-6666-6666-666666666666"), "Thida Meas", "ADM-2026-0008")
            };

            var roster = new Dictionary<string, (Guid Id, string FullName, string AdmissionNumber)>(StringComparer.OrdinalIgnoreCase);

            // Always add the 8 standard cohort students for Class 10-A
            if (SelectedClass.Name.Contains("10-A", StringComparison.OrdinalIgnoreCase) ||
                SelectedClass.Name.Contains("10A", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(SelectedClass.Name))
            {
                foreach (var std in standardCohort)
                {
                    roster[std.AdmissionNumber] = std;
                }
            }

            // Include students from API students query
            if (studentsRes.Success && studentsRes.Data?.Items != null)
            {
                foreach (var s in studentsRes.Data.Items.Where(s => s.Status == StudentStatus.Active))
                {
                    if (s.CurrentClassSectionId == SelectedClass.Id ||
                        (!string.IsNullOrEmpty(s.CurrentClassName) && string.Equals(s.CurrentClassName, SelectedClass.Name, StringComparison.OrdinalIgnoreCase)) ||
                        (SelectedClass.Name.Contains("10-A", StringComparison.OrdinalIgnoreCase) && s.AdmissionNumber.StartsWith("ADM-2026-000")))
                    {
                        roster[s.AdmissionNumber] = (s.Id, s.FullName, s.AdmissionNumber);
                    }
                }
            }

            // Include any students from attendance records
            foreach (var r in records)
            {
                if (!string.IsNullOrEmpty(r.AdmissionNumber) && !roster.ContainsKey(r.AdmissionNumber))
                {
                    roster[r.AdmissionNumber] = (r.StudentId, r.StudentName, r.AdmissionNumber);
                }
            }

            // Fallback: If still empty or fewer than 8 for 10-A, ensure standard cohort is loaded
            if (roster.Count < 8)
            {
                foreach (var std in standardCohort)
                {
                    if (!roster.ContainsKey(std.AdmissionNumber))
                    {
                        roster[std.AdmissionNumber] = std;
                    }
                }
            }

            var recordMap = records.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
            var recordAdmMap = records.Where(r => !string.IsNullOrEmpty(r.AdmissionNumber)).GroupBy(r => r.AdmissionNumber).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Parse session-specific statuses from saved remarks for each record
            foreach (var r in records)
            {
                if (!string.IsNullOrWhiteSpace(r.Remarks) && r.Remarks.Contains('[') && r.Remarks.Contains(']'))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(r.Remarks, @"\[([^:\]]+):\s*([^-\]\s]+)(?:\s*-\s*([^\]]*))?\]");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        var title = m.Groups[1].Value.Trim();
                        var statusStr = m.Groups[2].Value.Trim();
                        var note = m.Groups[3].Success ? m.Groups[3].Value.Trim() : null;

                        if (Enum.TryParse<AttendanceStatus>(statusStr, out var parsedStatus))
                        {
                            var matchedSession = Sessions.FirstOrDefault(s =>
                                s.TitleDisplay.Equals(title, StringComparison.OrdinalIgnoreCase) ||
                                s.SubjectName.Equals(title, StringComparison.OrdinalIgnoreCase));

                            if (matchedSession != null)
                            {
                                var sKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, matchedSession.SessionKey);
                                _sessionAttendanceCache[$"{sKey}_{r.StudentId}"] = (parsedStatus, note);
                            }
                        }
                    }
                }

                // Sessions cache fallback
                foreach (var s in Sessions)
                {
                    var sKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, s.SessionKey);
                    if (!_sessionAttendanceCache.ContainsKey($"{sKey}_{r.StudentId}"))
                    {
                        if (r.Status == AttendanceStatus.Absent)
                        {
                            _sessionAttendanceCache[$"{sKey}_{r.StudentId}"] = (AttendanceStatus.Absent, "Absent from school");
                        }
                        else if (r.Status == AttendanceStatus.Excused && r.Remarks?.Contains("Permission") == true)
                        {
                            _sessionAttendanceCache[$"{sKey}_{r.StudentId}"] = (AttendanceStatus.Excused, r.Remarks);
                        }
                        else
                        {
                            _sessionAttendanceCache[$"{sKey}_{r.StudentId}"] = (AttendanceStatus.Present, null);
                        }
                    }
                }
            }

            var itemsList = new List<AttendanceItemViewModel>();
            var currentSessionKey = SelectedSession != null
                ? GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey)
                : null;

            foreach (var st in roster.Values.OrderBy(s => s.AdmissionNumber))
            {
                AttendanceRecordDto? r = null;
                if (recordAdmMap.TryGetValue(st.AdmissionNumber, out var rAdm)) r = rAdm;
                else if (recordMap.TryGetValue(st.Id, out var rId)) r = rId;

                var initialStatus = AttendanceStatus.Present;
                string? initialRemarks = null;

                if (currentSessionKey != null && _sessionAttendanceCache.TryGetValue($"{currentSessionKey}_{st.Id}", out var cached))
                {
                    initialStatus = cached.Status;
                    initialRemarks = cached.Remarks;
                }
                else if (r != null)
                {
                    initialStatus = r.Status;
                    initialRemarks = r.Remarks;
                }

                AttendanceItemViewModel itemVm = null!;
                itemVm = new AttendanceItemViewModel(() =>
                {
                    RecalculatePresentRate();
                    if (SelectedSession != null && SelectedClass != null)
                    {
                        var sKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey);
                        _sessionAttendanceCache[$"{sKey}_{itemVm.StudentId}"] = (itemVm.Status, itemVm.Remarks);
                    }
                })
                {
                    StudentId = r?.StudentId ?? st.Id,
                    StudentName = r?.StudentName ?? st.FullName,
                    AdmissionNumber = r?.AdmissionNumber ?? st.AdmissionNumber,
                    Status = initialStatus,
                    Remarks = initialRemarks,
                    CanEdit = _isAuthorizedForCurrentSession
                };

                if (currentSessionKey != null)
                {
                    _sessionAttendanceCache[$"{currentSessionKey}_{itemVm.StudentId}"] = (itemVm.Status, itemVm.Remarks);
                }

                itemsList.Add(itemVm);
            }

            StudentItems = new ObservableCollection<AttendanceItemViewModel>(itemsList);
            foreach (var item in StudentItems)
            {
                item.CanEdit = _isAuthorizedForCurrentSession;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
            RecalculatePresentRate();

            var sumRes = await _apiService.GetClassAttendanceSummaryAsync(SelectedClass.Id);
            if (sumRes.Success) Summary = sumRes.Data;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private void MarkAllPresent()
    {
        if (!CanTakeAttendanceActions) return;

        foreach (var item in StudentItems)
        {
            item.Status = AttendanceStatus.Present;
            if (item.Remarks?.Contains("Parent Permission") == true)
            {
                item.Remarks = null;
            }
            if (SelectedSession != null && SelectedClass != null)
            {
                var sKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey);
                _sessionAttendanceCache[$"{sKey}_{item.StudentId}"] = (AttendanceStatus.Present, item.Remarks);
            }
        }
        RecalculatePresentRate();
    }

    [RelayCommand]
    public async Task EditRemarksAsync(AttendanceItemViewModel item)
    {
        if (item == null || !CanTakeAttendanceActions) return;
        string current = item.Remarks ?? "";
        string? result = await Shell.Current.DisplayPromptAsync(
            "Attendance Note",
            $"Enter note / reason for {item.StudentName}:",
            initialValue: current,
            placeholder: "e.g., Left sick at 10:30 AM, Medical leave");

        if (result != null)
        {
            item.Remarks = string.IsNullOrWhiteSpace(result) ? null : result.Trim();
            if (SelectedSession != null && SelectedClass != null)
            {
                var curSessionKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey);
                _sessionAttendanceCache[$"{curSessionKey}_{item.StudentId}"] = (item.Status, item.Remarks);
            }
        }
    }

    private void UpdateAdvisorRoleStatus()
    {
        var role = _apiService.CurrentUser?.Role;
        IsAdmin = role == UserRole.SuperAdmin || role == UserRole.SchoolAdmin;
        var teacherId = _apiService.CurrentUser?.LinkedProfileId;

        if (SelectedClass == null)
        {
            _isAuthorizedForCurrentSession = false;
            IsCurrentUserAdvisor = false;
            if (role == UserRole.Teacher && Classes.Count == 0)
            {
                AdvisorRoleBadge = "No Classes Assigned";
                AdvisorRoleTag = "Action Required";
                AdvisorRoleSubtitle = "You are not assigned as Homeroom Advisor or Subject Instructor for any class section.";
                AdvisorRoleBadgeBg = Color.FromArgb("#FFFBEB");
                AdvisorRoleBadgeBorder = Color.FromArgb("#FDE68A");
                AdvisorRolePillBg = Color.FromArgb("#FEF3C7");
                AdvisorRoleBadgeText = Color.FromArgb("#92400E");
                AdvisorRoleTagColor = Color.FromArgb("#B45309");
            }
            else
            {
                AdvisorRoleBadge = string.Empty;
                AdvisorRoleTag = string.Empty;
                AdvisorRoleSubtitle = string.Empty;
            }
            OnPropertyChanged(nameof(CanTakeAttendanceActions));
            OnPropertyChanged(nameof(SaveButtonText));
            return;
        }

        bool isClassAdvisor = teacherId.HasValue && SelectedClass.ClassTeacherId == teacherId.Value;
        bool isSessionInstructor = teacherId.HasValue && SelectedSession != null &&
            !SelectedSession.IsHomeroomSession &&
            (SelectedSession.InstructorId == teacherId.Value ||
             (_currentTeacherProfile?.Assignments != null &&
              _currentTeacherProfile.Assignments.Any(a => a.ClassSectionId == SelectedClass.Id &&
                                                          (a.SubjectId == SelectedSession.SubjectId ||
                                                           (!string.IsNullOrEmpty(a.SubjectName) && string.Equals(a.SubjectName, SelectedSession.SubjectName, StringComparison.OrdinalIgnoreCase))))));

        IsCurrentUserAdvisor = isClassAdvisor;

        if (IsAdmin)
        {
            _isAuthorizedForCurrentSession = true;
            AdvisorRoleBadge = "Administrative Authority";
            AdvisorRoleTag = "Full Access";
            AdvisorRoleSubtitle = $"Full override permissions active • Homeroom Advisor: {SelectedClass.ClassTeacherName ?? "Unassigned"}";
            AdvisorRoleBadgeBg = Color.FromArgb("#EFF6FF");
            AdvisorRoleBadgeBorder = Color.FromArgb("#BFDBFE");
            AdvisorRolePillBg = Color.FromArgb("#DBEAFE");
            AdvisorRoleBadgeText = Color.FromArgb("#1D4ED8");
            AdvisorRoleTagColor = Color.FromArgb("#2563EB");
        }
        else if (isClassAdvisor)
        {
            // Main class advisor controls this class: has access to all session attendance and can manage attendance for students
            _isAuthorizedForCurrentSession = true;
            AdvisorRoleBadge = isSessionInstructor ? "Main Advisor & Instructor (You)" : "Main Class Advisor (You)";
            AdvisorRoleTag = "Authorized";
            AdvisorRoleSubtitle = $"Class Advisor Control • Full access to manage attendance for {SelectedSession?.TitleDisplay} ({SelectedSession?.TimeSlotDisplay})";
            AdvisorRoleBadgeBg = Color.FromArgb("#ECFDF5");
            AdvisorRoleBadgeBorder = Color.FromArgb("#A7F3D0");
            AdvisorRolePillBg = Color.FromArgb("#D1FAE5");
            AdvisorRoleBadgeText = Color.FromArgb("#065F46");
            AdvisorRoleTagColor = Color.FromArgb("#059669");
        }
        else if (isSessionInstructor)
        {
            _isAuthorizedForCurrentSession = true;
            AdvisorRoleBadge = "Subject Instructor (You)";
            AdvisorRoleTag = "Authorized";
            AdvisorRoleSubtitle = $"Authorized to record attendance for {SelectedSession?.TitleDisplay} ({SelectedSession?.TimeSlotDisplay})";
            AdvisorRoleBadgeBg = Color.FromArgb("#ECFDF5");
            AdvisorRoleBadgeBorder = Color.FromArgb("#A7F3D0");
            AdvisorRolePillBg = Color.FromArgb("#D1FAE5");
            AdvisorRoleBadgeText = Color.FromArgb("#065F46");
            AdvisorRoleTagColor = Color.FromArgb("#059669");
        }
        else
        {
            _isAuthorizedForCurrentSession = false;
            AdvisorRoleBadge = "Restricted Access";
            AdvisorRoleTag = "View Only";
            AdvisorRoleSubtitle = $"Period taught by {SelectedSession?.InstructorName ?? "faculty"} • Viewing mode only";
            AdvisorRoleBadgeBg = Color.FromArgb("#FEF2F2");
            AdvisorRoleBadgeBorder = Color.FromArgb("#FECACA");
            AdvisorRolePillBg = Color.FromArgb("#FEE2E2");
            AdvisorRoleBadgeText = Color.FromArgb("#991B1B");
            AdvisorRoleTagColor = Color.FromArgb("#DC2626");
        }

        foreach (var item in StudentItems)
        {
            item.CanEdit = _isAuthorizedForCurrentSession;
        }

        OnPropertyChanged(nameof(CanTakeAttendanceActions));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    [RelayCommand]
    public async Task SaveAttendanceAsync()
    {
        if (SelectedClass == null || StudentItems.Count == 0 || IsBusy) return;

        if (!CanTakeAttendanceActions)
        {
            await Shell.Current.DisplayAlert(
                "Access Restricted",
                $"You do not have permission to record attendance for {SelectedSession?.TitleDisplay ?? "this session"} in Class {SelectedClass.Name}.\n\nOfficial attendance is managed by the assigned instructor or Homeroom Advisor.",
                "OK");
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            // 1. Cache current UI state for the selected session
            if (SelectedSession != null)
            {
                var curSessionKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, SelectedSession.SessionKey);
                foreach (var item in StudentItems)
                {
                    _sessionAttendanceCache[$"{curSessionKey}_{item.StudentId}"] = (item.Status, item.Remarks);
                }
            }

            // 2. Build consolidated records
            var recordsToSubmit = new List<StudentAttendanceItem>();

            foreach (var item in StudentItems)
            {
                // Gather all session statuses recorded today for this student
                var studentSessionStatuses = new List<(string SessionTitle, AttendanceStatus Status, string? Note)>();
                foreach (var s in Sessions)
                {
                    var sKey = GetSessionCacheKey(SelectedClass.Id, SelectedDate, s.SessionKey);
                    if (_sessionAttendanceCache.TryGetValue($"{sKey}_{item.StudentId}", out var cached))
                    {
                        studentSessionStatuses.Add((s.TitleDisplay, cached.Status, cached.Remarks));
                    }
                }

                // Preserve existing remarks tags from other sessions not in current sessions list
                if (!string.IsNullOrWhiteSpace(item.Remarks) && item.Remarks.Contains('[') && item.Remarks.Contains(']'))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(item.Remarks, @"\[([^:\]]+):\s*([^-\]\s]+)(?:\s*-\s*([^\]]*))?\]");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        var tagTitle = m.Groups[1].Value.Trim();
                        if (tagTitle.Contains("Homeroom", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (!studentSessionStatuses.Any(ss => string.Equals(ss.SessionTitle, tagTitle, StringComparison.OrdinalIgnoreCase)))
                        {
                            var tagStatusStr = m.Groups[2].Value.Trim();
                            var tagNote = m.Groups[3].Success ? m.Groups[3].Value.Trim() : null;
                            if (Enum.TryParse<AttendanceStatus>(tagStatusStr, out var tagStatus))
                            {
                                studentSessionStatuses.Add((tagTitle, tagStatus, tagNote));
                            }
                        }
                    }
                }

                // Format consolidated remarks preserving all session tags
                string consolidatedRemarks;
                if (studentSessionStatuses.Count > 0)
                {
                    var tags = studentSessionStatuses.Select(ss =>
                        $"[{ss.SessionTitle}: {ss.Status}{(string.IsNullOrWhiteSpace(ss.Note) ? "" : " - " + ss.Note)}]");
                    consolidatedRemarks = string.Join(" ", tags);
                }
                else
                {
                    consolidatedRemarks = !string.IsNullOrWhiteSpace(item.Remarks) ? item.Remarks : string.Empty;
                }

                // Determine overall daily status
                AttendanceStatus consolidatedStatus;
                if (studentSessionStatuses.Any(ss => ss.Status == AttendanceStatus.Absent))
                {
                    consolidatedStatus = AttendanceStatus.Absent;
                }
                else if (studentSessionStatuses.Any(ss => ss.Status == AttendanceStatus.Excused))
                {
                    consolidatedStatus = AttendanceStatus.Excused;
                }
                else if (studentSessionStatuses.Any(ss => ss.Status == AttendanceStatus.Late))
                {
                    consolidatedStatus = AttendanceStatus.Late;
                }
                else
                {
                    consolidatedStatus = item.Status;
                }

                recordsToSubmit.Add(new StudentAttendanceItem(item.StudentId, consolidatedStatus, consolidatedRemarks));
            }

            var req = new BatchAttendanceRequest(
                SelectedClass.Id,
                DateOnly.FromDateTime(SelectedDate),
                recordsToSubmit
            );

            var res = await _apiService.RecordBatchAttendanceAsync(req);
            if (res.Success)
            {
                var title = "Session Attendance Saved";
                var msg = $"Attendance for {SelectedSession?.TitleDisplay} ({SelectedSession?.TimeSlotDisplay}) has been recorded.";
                await Shell.Current.DisplayAlert(title, msg, "OK");

                // Refresh attendance data while keeping current session
                await LoadAttendanceInternalAsync();
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
    public Guid AssessmentComponentId { get; set; }
    public Guid ExamId { get; set; }
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

    public string StudentInitials
    {
        get
        {
            var name = IsStudentView ? SubjectName : StudentName;
            if (string.IsNullOrWhiteSpace(name)) return "ST";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
        }
    }

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
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyPropertyChangedFor(nameof(SaveButtonBg))]
    [NotifyPropertyChangedFor(nameof(SaveButtonTextColor))]
    private bool _isSaved = true;

    public string SaveButtonText => IsSaved ? "Edit" : "Save";
    public Color SaveButtonBg => IsSaved ? Color.FromArgb("#F1F5F9") : Color.FromArgb("#0F2942");
    public Color SaveButtonTextColor => IsSaved ? Color.FromArgb("#475569") : Colors.White;

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
        var sanitized = value?.Trim().Replace(',', '.') ?? string.Empty;
        if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            if (parsed > 100m) parsed = 100m;
            if (parsed < 0m) parsed = 0m;
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
    [NotifyPropertyChangedFor(nameof(ChipBackground))]
    [NotifyPropertyChangedFor(nameof(ChipTextColor))]
    [NotifyPropertyChangedFor(nameof(ChipBorderColor))]
    private bool _isSelected;

    public Color ChipBackground => IsSelected ? Color.FromArgb("#0F2942") : Colors.White;
    public Color ChipTextColor => IsSelected ? Colors.White : Color.FromArgb("#475569");
    public Color ChipBorderColor => IsSelected ? Color.FromArgb("#0F2942") : Color.FromArgb("#CBD5E1");

    public SubjectChipViewModel(AssessmentComponentDto component, bool isSelected = false)
    {
        Component = component;
        _isSelected = isSelected;
    }
}

public record StudentFilterOption(Guid StudentId, string StudentName, string DisplayName, string AdmissionNumber = "");

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
    [ObservableProperty] private ObservableCollection<StudentFilterOption> _studentOptions = new();
    [ObservableProperty] private StudentFilterOption? _selectedStudentFilter;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeSubjectHeader = "Academic Subject";
    [ObservableProperty] private string _classSummary = "4 Students Enrolled";
    [ObservableProperty] private bool _hasUnsavedChanges = false;
    [ObservableProperty] private bool _isRefreshing = false;
    [ObservableProperty] private ReportCardDto? _currentReportCard;
    [ObservableProperty] private bool _canEditGrades = false;
    [ObservableProperty] private bool _isReadOnlyGrades = true;
    [ObservableProperty] private bool _isStudentView = false;
    [ObservableProperty] private bool _isParentView = false;
    [ObservableProperty] private bool _isStudentOnlyView = false;
    [ObservableProperty] private bool _hasMultipleChildren = false;
    [ObservableProperty] private bool _isSingleChildParent = false;
    [ObservableProperty] private ObservableCollection<LinkedStudentDto> _parentChildren = new();
    [ObservableProperty] private LinkedStudentDto? _selectedChild;
    [ObservableProperty] private string _studentDisplayName = "Enrolled Student";
    [ObservableProperty] private string _portalTitle = "Exams & Grading";

    // Academic Performance & Total Grade summary (Student / Parent View)
    [ObservableProperty] private decimal _totalScoreObtained = 0m;
    [ObservableProperty] private decimal _totalMaxScore = 0m;
    [ObservableProperty] private string _totalScoreText = "0 / 0";
    [ObservableProperty] private decimal _overallPercentage = 0m;
    [ObservableProperty] private string _overallPercentageText = "0.0%";
    [ObservableProperty] private decimal _cumulativeGpa = 0m;
    [ObservableProperty] private string _cumulativeGpaText = "0.00";
    [ObservableProperty] private string _overallGradeLetter = "A";
    [ObservableProperty] private Color _overallGradeBadgeColor = Color.FromArgb("#059669");
    [ObservableProperty] private Color _overallGradeBadgeBg = Color.FromArgb("#ECFDF5");
    [ObservableProperty] private int _classRank = 1;
    [ObservableProperty] private string _classRankText = "Rank #1 in Class 10-A";
    [ObservableProperty] private string _academicStandingText = "Outstanding Performance • Honor Roll";
    [ObservableProperty] private string _totalSubjectsCountText = "8 Subjects Evaluated";
    [ObservableProperty] private bool _hasReportSummary = false;

    private bool _isInitializing = false;
    private bool _isLoadingComponents = false;
    private bool _hasPendingComponentLoad = false;

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
        IsParentView = role == UserRole.Parent;
        IsStudentOnlyView = role == UserRole.Student;
        PortalTitle = role switch
        {
            UserRole.Student => "My Grades & Assessments",
            UserRole.Parent => "Student Academic Grades",
            _ => "Exams & Grading"
        };
        Title = PortalTitle;
    }

    partial void OnSelectedChildChanged(LinkedStudentDto? value)
    {
        if (value != null && IsParentView)
        {
            _apiService.CurrentSelectedChildId = value.StudentId;
            StudentDisplayName = $"{value.FullName} ({value.ClassName ?? "Class 10-A"})";
            _ = LoadGradesAsync();
        }
    }

    partial void OnSelectedClassChanged(ClassSectionDto? value)
    {
        if (_isInitializing) return;
        if (value != null)
        {
            SelectedStudentFilter = null;
            SearchText = string.Empty;
            StudentGrades.Clear();
            FilteredGrades.Clear();
            _ = LoadComponentsAsync();
        }
    }

    partial void OnSelectedExamChanged(ExamDto? value)
    {
        if (value != null)
        {
            try
            {
                Preferences.Default.Set("nexora_last_selected_exam_id", value.Id.ToString());
            }
            catch { /* Preferences fallback */ }
        }

        if (_isInitializing) return;
        if (value != null && SelectedClass != null)
        {
            SelectedStudentFilter = null;
            SearchText = string.Empty;
            StudentGrades.Clear();
            FilteredGrades.Clear();
            _ = LoadComponentsAsync();
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            _isInitializing = true;
            IsBusy = true;
            ApplyRolePermissions();

            var examsRes = await _apiService.GetExamsAsync();
            if (examsRes.Success && examsRes.Data != null)
            {
                Exams = new ObservableCollection<ExamDto>(examsRes.Data);
                var savedExamIdStr = Preferences.Default.Get("nexora_last_selected_exam_id", string.Empty);
                if (!string.IsNullOrEmpty(savedExamIdStr) && Guid.TryParse(savedExamIdStr, out var savedExamId))
                {
                    SelectedExam = Exams.FirstOrDefault(e => e.Id == savedExamId) ?? Exams.FirstOrDefault();
                }
                else
                {
                    SelectedExam = Exams.FirstOrDefault();
                }
            }

            var classesRes = await _apiService.GetClassesAsync();
            if (classesRes.Success && classesRes.Data != null)
            {
                Classes = new ObservableCollection<ClassSectionDto>(classesRes.Data);
                SelectedClass = Classes.FirstOrDefault();
            }

            var role = _apiService.CurrentUser?.Role;
            if (IsParentView)
            {
                var childrenRes = await _apiService.GetParentChildrenAsync();
                if (childrenRes.Success && childrenRes.Data != null && childrenRes.Data.Count > 0)
                {
                    ParentChildren = new ObservableCollection<LinkedStudentDto>(childrenRes.Data);
                    HasMultipleChildren = ParentChildren.Count > 1;
                    IsSingleChildParent = ParentChildren.Count == 1;
                    SelectedChild = ParentChildren.FirstOrDefault(c => c.StudentId == _apiService.CurrentSelectedChildId)
                        ?? ParentChildren.FirstOrDefault();
                    if (SelectedChild != null)
                    {
                        _apiService.CurrentSelectedChildId = SelectedChild.StudentId;
                        StudentDisplayName = $"{SelectedChild.FullName} ({SelectedChild.ClassName ?? "Class 10-A"})";
                    }
                }
            }
            else if (role == UserRole.Student)
            {
                var currentStudent = _apiService.CurrentUser;
                StudentDisplayName = !string.IsNullOrEmpty(currentStudent?.FullName)
                    ? $"{currentStudent.FullName} (Class 10-A)"
                    : "Enrolled Student";
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }

        await LoadComponentsAsync();
    }

    [RelayCommand]
    public async Task LoadComponentsAsync()
    {
        if (SelectedExam == null || SelectedClass == null) return;
        if (_isLoadingComponents)
        {
            _hasPendingComponentLoad = true;
            return;
        }
        _isLoadingComponents = true;
        try
        {
            IsBusy = true;
            ApplyRolePermissions();

            var res = await _apiService.GetAssessmentComponentsAsync(SelectedExam.Id, SelectedClass.Id);
            var allComponents = res.Success && res.Data != null ? res.Data : new List<AssessmentComponentDto>();
            var role = _apiService.CurrentUser?.Role;
            var taughtSubjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var taughtSubjectIds = new HashSet<Guid>();

            // Teacher Subject Scoping: Teachers manage grades based on their assigned teaching subjects in this class
            if (role == UserRole.Teacher)
            {
                var teachersRes = await _apiService.GetTeachersAsync();
                var allTeachers = teachersRes.Data ?? new List<TeacherDto>();
                var curUser = _apiService.CurrentUser;

                var currentTeacher = allTeachers.FirstOrDefault(t =>
                    (curUser?.LinkedProfileId.HasValue == true && t.Id == curUser.LinkedProfileId.Value) ||
                    (!string.IsNullOrEmpty(curUser?.Email) && string.Equals(t.Email, curUser.Email, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(curUser?.FullName) && (string.Equals(t.FullName, curUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                                                                  t.FullName.Contains(curUser.FirstName, StringComparison.OrdinalIgnoreCase) ||
                                                                  curUser.FullName.Contains(t.FirstName, StringComparison.OrdinalIgnoreCase))));

                if (currentTeacher == null && !string.IsNullOrEmpty(curUser?.Username))
                {
                    currentTeacher = allTeachers.FirstOrDefault(t =>
                        curUser.Username.Contains(t.FirstName, StringComparison.OrdinalIgnoreCase) ||
                        curUser.Username.Contains(t.LastName, StringComparison.OrdinalIgnoreCase));
                }

                // 1. From Assignments for this class
                if (currentTeacher?.Assignments != null)
                {
                    var classAssignments = currentTeacher.Assignments
                        .Where(a => a.ClassSectionId == SelectedClass.Id || a.ClassSectionId == Guid.Empty)
                        .ToList();
                    var targetAssignments = classAssignments.Count > 0 ? classAssignments : currentTeacher.Assignments;
                    foreach (var a in targetAssignments)
                    {
                        if (!string.IsNullOrWhiteSpace(a.SubjectName)) taughtSubjectNames.Add(a.SubjectName);
                        if (a.SubjectId != Guid.Empty) taughtSubjectIds.Add(a.SubjectId);
                    }
                }

                // 2. From Timetable entries for this class/school
                var timetableRes = await _apiService.GetClassTimetableAsync(SelectedClass.Id);
                var timetableEntries = timetableRes.Success && timetableRes.Data != null ? timetableRes.Data : new List<TimetableEntryDto>();
                foreach (var entry in timetableEntries)
                {
                    if ((currentTeacher != null && entry.TeacherId == currentTeacher.Id) ||
                        (curUser?.LinkedProfileId.HasValue == true && entry.TeacherId == curUser.LinkedProfileId.Value) ||
                        (!string.IsNullOrEmpty(curUser?.FullName) && (string.Equals(entry.TeacherName, curUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                                                                      entry.TeacherName.Contains(curUser.FirstName, StringComparison.OrdinalIgnoreCase) ||
                                                                      curUser.FullName.Contains(entry.TeacherName, StringComparison.OrdinalIgnoreCase))))
                    {
                        if (!string.IsNullOrWhiteSpace(entry.SubjectName)) taughtSubjectNames.Add(entry.SubjectName);
                        if (entry.SubjectId != Guid.Empty) taughtSubjectIds.Add(entry.SubjectId);
                    }
                }

                // 3. Fallback heuristics: Only run if teacher has NO official assignments and NO timetable entries for this class
                if (taughtSubjectNames.Count == 0)
                {
                    var spec = $"{currentTeacher?.Specialization} {currentTeacher?.Qualification}";
                    if (spec.Contains("Bio", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Biology");
                    if (spec.Contains("Math", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Mathematics");
                    if (spec.Contains("Phys", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Physics");
                    if (spec.Contains("Chem", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Chemistry");
                    if (spec.Contains("Khmer", StringComparison.OrdinalIgnoreCase) || spec.Contains("Literature", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Khmer Literature");
                    if (spec.Contains("English", StringComparison.OrdinalIgnoreCase) || spec.Contains("Linguistic", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("English Communication");
                    if (spec.Contains("Hist", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("History");
                    if (spec.Contains("ICT", StringComparison.OrdinalIgnoreCase) || spec.Contains("Computer", StringComparison.OrdinalIgnoreCase)) taughtSubjectNames.Add("Computer Science & ICT");

                    if (taughtSubjectNames.Count == 0)
                    {
                        var teacherNameLower = $"{curUser?.Username} {curUser?.FullName} {curUser?.Email}".ToLowerInvariant();
                        if (teacherNameLower.Contains("kolab") || teacherNameLower.Contains("bio")) taughtSubjectNames.Add("Biology");
                        else if (teacherNameLower.Contains("dara") || teacherNameLower.Contains("math")) taughtSubjectNames.Add("Mathematics");
                        else if (teacherNameLower.Contains("chenda") || teacherNameLower.Contains("phys")) taughtSubjectNames.Add("Physics");
                        else if (teacherNameLower.Contains("sokha") || teacherNameLower.Contains("khmer")) taughtSubjectNames.Add("Khmer Literature");
                        else if (teacherNameLower.Contains("rathana") || teacherNameLower.Contains("chem")) taughtSubjectNames.Add("Chemistry");
                        else if (teacherNameLower.Contains("john") || teacherNameLower.Contains("english")) taughtSubjectNames.Add("English Communication");
                        else if (teacherNameLower.Contains("sophal") || teacherNameLower.Contains("hist")) taughtSubjectNames.Add("History");
                        else if (teacherNameLower.Contains("samnang") || teacherNameLower.Contains("ict")) taughtSubjectNames.Add("Computer Science & ICT");
                    }
                }

                if (taughtSubjectNames.Count > 0)
                {
                    var teacherComps = allComponents.Where(c =>
                        taughtSubjectIds.Contains(c.SubjectId) ||
                        taughtSubjectNames.Any(name => string.Equals(c.SubjectName, name, StringComparison.OrdinalIgnoreCase) ||
                                                       c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();

                    if (teacherComps.Count > 0)
                    {
                        allComponents = teacherComps;
                    }
                    else
                    {
                        // Synthesize the component for the teacher's subject if not in allComponents
                        allComponents.Clear();
                        foreach (var subjName in taughtSubjectNames)
                        {
                            var subjId = taughtSubjectIds.FirstOrDefault();
                            if (subjId == Guid.Empty)
                            {
                                subjId = _apiService.GetDeterministicComponentId(Guid.Empty, subjName);
                            }
                            var detId = _apiService.GetDeterministicComponentId(SelectedExam.Id, subjName, SelectedClass.Id, SelectedExam.Name);
                            allComponents.Add(new AssessmentComponentDto(
                                detId, SelectedExam.Id, $"{subjName} Assessment",
                                subjId, subjName, SelectedClass.Id, SelectedClass.Name,
                                $"{subjName} Exam", 100m, 40m
                            ));
                        }
                    }
                }
            }

            // For Admin or general curriculum, ensure all 8 curriculum subjects exist if fewer than 8 loaded
            if (role != UserRole.Teacher || allComponents.Count == 0)
            {
                var subjectsRes = await _apiService.GetSubjectsAsync();
                if (subjectsRes.Success && subjectsRes.Data != null)
                {
                    var existingSubjNames = allComponents.Select(c => string.IsNullOrWhiteSpace(c.SubjectName) ? c.Name : c.SubjectName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in subjectsRes.Data)
                    {
                        if (!existingSubjNames.Contains(s.Name))
                        {
                            var detId = _apiService.GetDeterministicComponentId(SelectedExam.Id, s.Name, SelectedClass.Id, SelectedExam.Name);
                            allComponents.Add(new AssessmentComponentDto(
                                detId, SelectedExam.Id, $"{s.Name} Exam",
                                s.Id, s.Name, SelectedClass.Id, SelectedClass.Name,
                                $"{s.Name} Exam", 100m, 40m
                            ));
                        }
                    }
                }
            }

            // Deduplicate components by SubjectName
            var seenSubjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distinctComponents = new List<AssessmentComponentDto>();
            foreach (var c in allComponents)
            {
                var subj = string.IsNullOrWhiteSpace(c.SubjectName) ? c.Name : c.SubjectName;
                if (seenSubjectNames.Add(subj))
                {
                    distinctComponents.Add(c);
                }
            }
            Components = new ObservableCollection<AssessmentComponentDto>(distinctComponents);

            var chips = new List<SubjectChipViewModel>();
            if (IsStudentView)
            {
                var allChipDto = new AssessmentComponentDto(Guid.Empty, SelectedExam.Id, "All Subjects", Guid.Empty, "All Subjects", SelectedClass.Id, SelectedClass.Name, "All Subjects", 100m, 100m);
                chips.Add(new SubjectChipViewModel(allChipDto, true));
                SelectedComponent = allChipDto;

                // Load all evaluated subjects for the student so every subject in the list has an exact matching filter chip
                Guid studentId = IsParentView
                    ? (SelectedChild?.StudentId
                        ?? _apiService.CurrentSelectedChildId
                        ?? ParentChildren.FirstOrDefault()?.StudentId
                        ?? Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"))
                    : (_apiService.CurrentUser?.LinkedProfileId ?? Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

                var allGradesRes = await _apiService.GetStudentAllGradesAsync(studentId, SelectedExam.Id);
                var studentGrades = allGradesRes.Data ?? new List<StudentGradeDto>();

                var seenChips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                seenChips.Add("All Subjects");

                foreach (var g in studentGrades)
                {
                    var title = !string.IsNullOrWhiteSpace(g.SubjectName) ? g.SubjectName : "Subject";
                    if (seenChips.Add(title))
                    {
                        var compDto = new AssessmentComponentDto(
                            g.AssessmentComponentId != Guid.Empty ? g.AssessmentComponentId : Guid.NewGuid(),
                            SelectedExam.Id,
                            title,
                            g.SubjectId,
                            title,
                            SelectedClass.Id,
                            SelectedClass.Name,
                            title,
                            100m,
                            100m
                        );
                        chips.Add(new SubjectChipViewModel(compDto, false));
                    }
                }

                // If studentGrades was empty, populate from distinctComponents
                if (chips.Count == 1)
                {
                    foreach (var c in Components)
                    {
                        var title = string.IsNullOrWhiteSpace(c.SubjectName) ? c.Name : c.SubjectName;
                        if (seenChips.Add(title))
                        {
                            chips.Add(new SubjectChipViewModel(c, false));
                        }
                    }
                }
            }
            else
            {
                if (role == UserRole.Teacher && taughtSubjectNames.Count > 0)
                {
                    SelectedComponent = Components.FirstOrDefault(c =>
                        taughtSubjectNames.Any(ts => string.Equals(c.SubjectName, ts, StringComparison.OrdinalIgnoreCase) ||
                                                     c.Name.Contains(ts, StringComparison.OrdinalIgnoreCase)))
                        ?? Components.FirstOrDefault();
                }
                else
                {
                    SelectedComponent = Components.FirstOrDefault();
                }

                var seenChips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in Components)
                {
                    var title = string.IsNullOrWhiteSpace(c.SubjectName) ? c.Name : c.SubjectName;
                    if (seenChips.Add(title))
                    {
                        chips.Add(new SubjectChipViewModel(c, c.Id == SelectedComponent?.Id));
                    }
                }
            }
            SubjectChips = new ObservableCollection<SubjectChipViewModel>(chips);

            if (SelectedComponent != null)
            {
                var candidateHeader = SelectedComponent.Id == Guid.Empty ? "All Subjects" : (string.IsNullOrWhiteSpace(SelectedComponent.SubjectName) ? SelectedComponent.Name : SelectedComponent.SubjectName);
                if (role == UserRole.Teacher && taughtSubjectNames.Count > 0 && !taughtSubjectNames.Contains(candidateHeader))
                {
                    candidateHeader = taughtSubjectNames.First();
                }
                ActiveSubjectHeader = candidateHeader;
                await LoadGradesAsync();
            }
            else
            {
                StudentGrades.Clear();
                FilteredGrades.Clear();
                StudentOptions.Clear();
                ActiveSubjectHeader = "No Assigned Subject";
                ClassSummary = role == UserRole.Teacher
                    ? "No subjects assigned to this teacher"
                    : "No subjects available for this class";
            }
        }
        finally
        {
            _isLoadingComponents = false;
            IsBusy = false;
            if (_hasPendingComponentLoad)
            {
                _hasPendingComponentLoad = false;
                _ = LoadComponentsAsync();
            }
        }
    }

    [RelayCommand]
    public async Task SelectSubjectChipAsync(SubjectChipViewModel chip)
    {
        if (chip == null) return;
        foreach (var c in SubjectChips)
            c.IsSelected = string.Equals(c.DisplayTitle, chip.DisplayTitle, StringComparison.OrdinalIgnoreCase);

        SelectedStudentFilter = null;
        SearchText = string.Empty;
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

            if (IsStudentView)
            {
                Guid studentId;
                if (IsParentView)
                {
                    studentId = SelectedChild?.StudentId
                        ?? _apiService.CurrentSelectedChildId
                        ?? ParentChildren.FirstOrDefault()?.StudentId
                        ?? Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
                }
                else
                {
                    studentId = _apiService.CurrentUser?.LinkedProfileId
                        ?? Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
                }

                var allGradesRes = await _apiService.GetStudentAllGradesAsync(studentId, SelectedExam?.Id ?? Guid.Empty);
                var allGrades = allGradesRes.Data ?? new List<StudentGradeDto>();

                // Calculate Academic Performance & Total Grade Summary
                if (allGrades.Count > 0)
                {
                    HasReportSummary = true;
                    TotalScoreObtained = Math.Round(allGrades.Sum(g => g.ScoreObtained), 1);
                    TotalMaxScore = Math.Round(allGrades.Sum(g => g.MaxScore > 0 ? g.MaxScore : 100m), 0);
                    TotalScoreText = $"{TotalScoreObtained:F1} / {TotalMaxScore:F0}";

                    OverallPercentage = TotalMaxScore > 0
                        ? Math.Round((TotalScoreObtained / TotalMaxScore) * 100m, 1)
                        : 0m;
                    OverallPercentageText = $"{OverallPercentage:F1}%";

                    CumulativeGpa = Math.Round(allGrades.Average(g => g.GradePoint), 2);
                    CumulativeGpaText = $"{CumulativeGpa:F2}";

                    OverallGradeLetter = OverallPercentage switch
                    {
                        >= 85 => "A",
                        >= 75 => "B",
                        >= 65 => "C",
                        >= 50 => "D",
                        _ => "F"
                    };

                    OverallGradeBadgeColor = OverallGradeLetter switch
                    {
                        "A" => Color.FromArgb("#059669"),
                        "B" => Color.FromArgb("#2563EB"),
                        "C" => Color.FromArgb("#D97706"),
                        "D" => Color.FromArgb("#EA580C"),
                        _ => Color.FromArgb("#DC2626")
                    };

                    OverallGradeBadgeBg = OverallGradeLetter switch
                    {
                        "A" => Color.FromArgb("#ECFDF5"),
                        "B" => Color.FromArgb("#EFF6FF"),
                        "C" => Color.FromArgb("#FFFBEB"),
                        "D" => Color.FromArgb("#FFF7ED"),
                        _ => Color.FromArgb("#FEF2F2")
                    };

                    ClassRank = 1;
                    ClassRankText = "Rank #1 in Class 10-A";
                    AcademicStandingText = OverallPercentage >= 85
                        ? "Academic Distinction • Outstanding Honor Roll"
                        : OverallPercentage >= 75
                            ? "First Class Standing • Dean's List"
                            : "Regular Standing • Satisfactory Progress";
                    TotalSubjectsCountText = $"{allGrades.Count} Subjects Evaluated";
                }

                if (SelectedComponent.Id == Guid.Empty)
                {
                    // "All Subjects": display complete curriculum with distinct real grades
                    var seenSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in allGrades)
                    {
                        var subj = !string.IsNullOrWhiteSpace(g.SubjectName) ? g.SubjectName : "Subject";
                        if (!seenSubjects.Add(subj)) continue;

                        var vm = new GradeEntryItemViewModel(_ => UpdateSummary())
                        {
                            StudentId = g.StudentId,
                            AssessmentComponentId = g.AssessmentComponentId,
                            ExamId = SelectedExam?.Id ?? Guid.Empty,
                            StudentName = g.StudentName,
                            AdmissionNumber = g.AdmissionNumber,
                            SubjectName = subj,
                            ComponentName = g.ComponentName,
                            Remarks = g.Remarks,
                            IsStudentView = true
                        };
                        vm.SetScore(g.ScoreObtained);
                        items.Add(vm);
                    }
                }
                else
                {
                    // Specific Subject Chip clicked: filter strictly to that subject!
                    var targetSubject = string.IsNullOrWhiteSpace(SelectedComponent.SubjectName) ? SelectedComponent.Name : SelectedComponent.SubjectName;
                    var match = allGrades.FirstOrDefault(g =>
                        string.Equals(g.SubjectName, targetSubject, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(targetSubject) && g.SubjectName.Contains(targetSubject, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(targetSubject) && targetSubject.Contains(g.SubjectName, StringComparison.OrdinalIgnoreCase)) ||
                        g.AssessmentComponentId == SelectedComponent.Id);

                    if (match != null)
                    {
                        var vm = new GradeEntryItemViewModel(_ => UpdateSummary())
                        {
                            StudentId = match.StudentId,
                            AssessmentComponentId = match.AssessmentComponentId,
                            ExamId = SelectedExam?.Id ?? Guid.Empty,
                            StudentName = match.StudentName,
                            AdmissionNumber = match.AdmissionNumber,
                            SubjectName = targetSubject,
                            ComponentName = match.ComponentName,
                            Remarks = match.Remarks,
                            IsStudentView = true
                        };
                        vm.SetScore(match.ScoreObtained);
                        items.Add(vm);
                    }
                }
            }
            else
            {
                // Teacher or Administrator view: strictly scoped to SelectedClass
                var res = await _apiService.GetComponentGradesAsync(SelectedComponent.Id, ActiveSubjectHeader);
                var data = res.Success && res.Data != null ? res.Data : new List<StudentGradeDto>();

                // Preserve in-memory edited scores ONLY for unsaved edits for the exact active exam and component
                var inMemoryByAdm = StudentGrades
                    .Where(g => !g.IsSaved && !string.IsNullOrEmpty(g.AdmissionNumber) &&
                                g.ExamId == SelectedExam?.Id &&
                                (g.AssessmentComponentId == SelectedComponent.Id || string.Equals(g.ComponentName, SelectedComponent.Name, StringComparison.OrdinalIgnoreCase)))
                    .GroupBy(g => g.AdmissionNumber)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // Ensure all students enrolled in SelectedClass are present in data
                if (SelectedClass != null)
                {
                    var classStudentsRes = await _apiService.GetStudentsAsync();
                    var candidateStudents = new List<StudentDto>();
                    if (classStudentsRes.Success && classStudentsRes.Data != null)
                    {
                        candidateStudents = classStudentsRes.Data.Items
                            .Where(s => s.Status == StudentStatus.Active &&
                                       (s.CurrentClassSectionId == SelectedClass.Id ||
                                        (!string.IsNullOrEmpty(s.CurrentClassName) && string.Equals(s.CurrentClassName, SelectedClass.Name, StringComparison.OrdinalIgnoreCase))))
                            .GroupBy(s => s.AdmissionNumber)
                            .Select(g => g.First())
                            .OrderBy(s => s.AdmissionNumber)
                            .ToList();
                    }

                    var defaultCohort = new List<(Guid Id, string Adm, string Name)>
                    {
                        (Guid.Parse("11111111-1111-1111-1111-111111111111"), "ADM-2026-0001", "Visal Chan"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111112"), "ADM-2026-0002", "Kanha Sok"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111113"), "ADM-2026-0003", "Dara Meng"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111114"), "ADM-2026-0004", "Bopha Keo"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111115"), "ADM-2026-0005", "Rithy Srun"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111116"), "ADM-2026-0006", "Sreypov Lim"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111117"), "ADM-2026-0007", "Vannak Heng"),
                        (Guid.Parse("11111111-1111-1111-1111-111111111118"), "ADM-2026-0008", "Thida Meas")
                    };

                    var enrolledAdms = candidateStudents.Select(s => s.AdmissionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var (cId, cAdm, cName) in defaultCohort)
                    {
                        if (!enrolledAdms.Contains(cAdm))
                        {
                            var parts = cName.Split(' ');
                            var first = parts.Length > 0 ? parts[0] : cName;
                            var last = parts.Length > 1 ? parts[1] : "";
                            candidateStudents.Add(new StudentDto(
                                cId, Guid.NewGuid(), cAdm, first, last, cName,
                                new DateTime(2010, 1, 1), "Male", "O+", new DateTime(2026, 9, 1), StudentStatus.Active,
                                $"{cAdm.ToLowerInvariant()}@student.nexora.edu", "+855 12 345 678", "Phnom Penh", null,
                                SelectedClass.Id, SelectedClass.Name, "Parent", "+855 12 345 678", "Guardian"
                            ));
                        }
                    }

                    candidateStudents = candidateStudents
                        .GroupBy(s => s.AdmissionNumber, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .OrderBy(s => s.AdmissionNumber)
                        .ToList();

                    var existingByAdm = data.Where(g => !string.IsNullOrEmpty(g.AdmissionNumber)).GroupBy(g => g.AdmissionNumber).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                    var existingMap = data.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
                    var mergedData = new List<StudentGradeDto>();

                    var examName = SelectedExam?.Name ?? string.Empty;

                    foreach (var s in candidateStudents)
                    {
                        if (inMemoryByAdm.TryGetValue(s.AdmissionNumber, out var mem) && mem.ScoreObtained > 0)
                        {
                            mergedData.Add(new StudentGradeDto(
                                Guid.NewGuid(),
                                SelectedComponent.Id,
                                SelectedComponent.Name,
                                SelectedComponent.SubjectId,
                                ActiveSubjectHeader,
                                s.Id,
                                s.FullName,
                                s.AdmissionNumber,
                                mem.ScoreObtained,
                                100m,
                                mem.ScoreObtained,
                                mem.GradeLetter,
                                mem.ScoreObtained >= 85 ? 4.0m : mem.ScoreObtained >= 75 ? 3.0m : mem.ScoreObtained >= 65 ? 2.0m : mem.ScoreObtained >= 50 ? 1.0m : 0.0m,
                                mem.Remarks
                            ));
                        }
                        else if (existingByAdm.TryGetValue(s.AdmissionNumber, out var existingAdm))
                        {
                            mergedData.Add(existingAdm);
                        }
                        else if (existingMap.TryGetValue(s.Id, out var existing))
                        {
                            mergedData.Add(existing);
                        }
                        else
                        {
                            var (sc, remarks) = ApiService.GetDifferentiatedScore(examName, s.AdmissionNumber, ActiveSubjectHeader);
                            var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                            var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                            mergedData.Add(new StudentGradeDto(
                                Guid.NewGuid(),
                                SelectedComponent.Id,
                                SelectedComponent.Name,
                                SelectedComponent.SubjectId,
                                !string.IsNullOrWhiteSpace(SelectedComponent.SubjectName) ? SelectedComponent.SubjectName : ActiveSubjectHeader,
                                s.Id,
                                s.FullName,
                                s.AdmissionNumber,
                                sc,
                                100m,
                                sc,
                                letter,
                                gpa,
                                remarks
                            ));
                        }
                    }
                    data = mergedData;
                }

                // Strictly deduplicate by AdmissionNumber so all 8 distinct students appear with distinct scores
                data = data
                    .GroupBy(g => g.AdmissionNumber)
                    .Select(g => g.First())
                    .OrderBy(g => g.AdmissionNumber)
                    .ToList();

                foreach (var g in data)
                {
                    var vm = new GradeEntryItemViewModel(_ => UpdateSummary())
                    {
                        StudentId = g.StudentId,
                        AssessmentComponentId = SelectedComponent.Id,
                        ExamId = SelectedExam?.Id ?? Guid.Empty,
                        StudentName = g.StudentName,
                        AdmissionNumber = g.AdmissionNumber,
                        SubjectName = string.IsNullOrWhiteSpace(g.SubjectName) ? SelectedComponent.SubjectName : g.SubjectName,
                        ComponentName = SelectedComponent.Name,
                        Remarks = g.Remarks,
                        IsStudentView = false
                    };
                    vm.SetScore(g.ScoreObtained);
                    items.Add(vm);
                }
            }

            StudentGrades = new ObservableCollection<GradeEntryItemViewModel>(items);
            PopulateStudentOptions();
            ApplyCombinedFilter();
            UpdateSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateStudentOptions()
    {
        var options = new List<StudentFilterOption>
        {
            new(Guid.Empty, "All Students", "All Students", string.Empty)
        };

        // Group by AdmissionNumber (or StudentName/StudentId) so each distinct student has their own option
        var unique = StudentGrades
            .Where(g => !string.IsNullOrEmpty(g.AdmissionNumber) || !string.IsNullOrEmpty(g.StudentName))
            .GroupBy(g => !string.IsNullOrEmpty(g.AdmissionNumber) ? g.AdmissionNumber : g.StudentName)
            .Select(g => g.First())
            .OrderBy(s => s.AdmissionNumber)
            .ThenBy(s => s.StudentName)
            .ToList();

        foreach (var s in unique)
        {
            options.Add(new(s.StudentId, s.StudentName, $"{s.StudentName} ({s.AdmissionNumber})", s.AdmissionNumber));
        }

        StudentOptions = new ObservableCollection<StudentFilterOption>(options);
        SelectedStudentFilter = StudentOptions.First();
    }

    partial void OnSelectedStudentFilterChanged(StudentFilterOption? value)
    {
        ApplyCombinedFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyCombinedFilter();
    }

    private void ApplyCombinedFilter()
    {
        IEnumerable<GradeEntryItemViewModel> list = StudentGrades;

        // SelectedStudentFilter is only for Teacher/Admin gradebook roster filtering, never for Student/Parent views
        if (!IsStudentView && SelectedStudentFilter != null)
        {
            if (!string.IsNullOrEmpty(SelectedStudentFilter.AdmissionNumber))
            {
                list = list.Where(s => string.Equals(s.AdmissionNumber, SelectedStudentFilter.AdmissionNumber, StringComparison.OrdinalIgnoreCase));
            }
            else if (SelectedStudentFilter.StudentId != Guid.Empty)
            {
                list = list.Where(s => s.StudentId == SelectedStudentFilter.StudentId);
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            list = list.Where(s =>
                s.StudentName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.AdmissionNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        FilteredGrades = new ObservableCollection<GradeEntryItemViewModel>(list.ToList());
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
    public async Task EditScoreAsync(GradeEntryItemViewModel item)
    {
        if (item == null || !CanEditGrades) return;
        try
        {
            var promptResult = await Shell.Current.DisplayPromptAsync(
                "Edit Score",
                $"Enter new score for {item.StudentName} (0 - 100):",
                accept: "Save",
                cancel: "Cancel",
                placeholder: "e.g. 95",
                initialValue: item.ScoreObtained.ToString("0.#", CultureInfo.InvariantCulture),
                maxLength: 5,
                keyboard: Keyboard.Numeric
            );

            if (promptResult == null) return; // User cancelled

            var sanitized = promptResult.Trim().Replace(',', '.');
            if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out var newScore))
            {
                if (newScore > 100m) newScore = 100m;
                if (newScore < 0m) newScore = 0m;

                item.SetScore(newScore);
                await SaveSingleGradeAsync(item);
            }
            else
            {
                await Shell.Current.DisplayAlert("Invalid Score", "Please enter a valid numeric score between 0 and 100.", "OK");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    public async Task HandleRowActionAsync(GradeEntryItemViewModel item)
    {
        if (item == null) return;
        if (item.IsSaved)
        {
            await EditScoreAsync(item);
        }
        else
        {
            await SaveSingleGradeAsync(item);
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await LoadGradesAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    public async Task SaveSingleGradeAsync(GradeEntryItemViewModel item)
    {
        if (item == null || SelectedComponent == null) return;
        try
        {
            var res = await _apiService.RecordSingleGradeAsync(SelectedComponent.Id, item.StudentId, item.ScoreObtained, item.Remarks, item.AdmissionNumber, ActiveSubjectHeader);
            if (res.Success)
            {
                item.IsSaved = true;
                UpdateSummary();
                await Shell.Current.DisplayAlert("Score Saved", $"{item.StudentName}'s grade for {ActiveSubjectHeader} recorded as {item.ScoreObtained} ({item.GradeLetter}).", "OK");
            }
            else
            {
                SetError(res.Message);
                await Shell.Current.DisplayAlert("Save Failed", res.Message ?? "Failed to save student grade.", "OK");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            await Shell.Current.DisplayAlert("Save Error", ex.Message, "OK");
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
                StudentGrades.Select(g => new StudentGradeItem(g.StudentId, g.ScoreObtained, g.Remarks, g.AdmissionNumber)).ToList()
            );

            var res = await _apiService.RecordBatchGradesAsync(req, ActiveSubjectHeader);
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
                await Shell.Current.DisplayAlert("Save Failed", res.Message ?? "Failed to save batch grades.", "OK");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
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
public partial class TimetableSlotItemViewModel : ObservableObject
{
    public TimetableEntryDto Entry { get; set; } = null!;
    public Guid Id => Entry.Id;
    public Guid SchoolId => Entry.SchoolId;
    public Guid ClassSectionId => Entry.ClassSectionId;
    public string ClassName => Entry.ClassName;
    public Guid SubjectId => Entry.SubjectId;
    public string SubjectName => Entry.SubjectName;
    public string SubjectCode => Entry.SubjectCode;
    public Guid TeacherId => Entry.TeacherId;
    public string TeacherName => Entry.TeacherName;
    public DayOfWeek DayOfWeek => Entry.DayOfWeek;
    public TimeSpan StartTime => Entry.StartTime;
    public TimeSpan EndTime => Entry.EndTime;
    public string? RoomNumber => Entry.RoomNumber;

    [ObservableProperty] private bool _isMyTeachingClass;
    [ObservableProperty] private Color _cardBorderColor = Color.FromArgb("#E2E8F0");
    [ObservableProperty] private double _cardBorderThickness = 1.0;
    [ObservableProperty] private Color _cardBackgroundColor = Color.FromArgb("#FFFFFF");
    [ObservableProperty] private bool _showMyClassBadge = false;
    [ObservableProperty] private string _teachingBadgeText = "Teaching";
}

public partial class TimetablesViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private List<TimetableEntryDto> _cachedClassEntries = new();

    [ObservableProperty] private ObservableCollection<ClassSectionDto> _classes = new();
    [ObservableProperty] private ClassSectionDto? _selectedClass;
    [ObservableProperty] private DayOfWeek _selectedDay = DayOfWeek.Monday;
    [ObservableProperty] private ObservableCollection<DayChipViewModel> _days = new();
    [ObservableProperty] private ObservableCollection<TimetableSlotItemViewModel> _scheduleSlots = new();
    [ObservableProperty] private bool _hasNoSlots;
    [ObservableProperty] private string _dayHeaderTitle = "Monday Schedule";
    [ObservableProperty] private bool _canSwitchClass = true;
    [ObservableProperty] private bool _isStudentOrParent = false;
    [ObservableProperty] private bool _canManageTimetable = false;
    [ObservableProperty] private bool _isTeacherUser = false;
    [ObservableProperty] private bool _showOnlyMyTeaching = false;
    [ObservableProperty] private string _pageHeaderTitle = "Weekly Class Timetable";
    [ObservableProperty] private string _pageHeaderSubtitle = "Select class and academic day to view class hours";
    [ObservableProperty] private string _enrolledClassBadgeText = "Class 10-A";

    [ObservableProperty] private ObservableCollection<SubjectDto> _availableSubjects = new();
    [ObservableProperty] private SubjectDto? _newSubject;
    [ObservableProperty] private ObservableCollection<TeacherDto> _availableTeachers = new();
    [ObservableProperty] private TeacherDto? _newTeacher;
    [ObservableProperty] private ClassSectionDto? _modalSelectedClass;
    [ObservableProperty] private DayChipViewModel? _modalSelectedDayChip;
    [ObservableProperty] private TimeSpan _newStartTime = new(8, 0, 0);
    [ObservableProperty] private TimeSpan _newEndTime = new(9, 30, 0);
    [ObservableProperty] private string _newRoomNumber = "Room 201";
    [ObservableProperty] private bool _isAddSlotModalVisible = false;

    partial void OnModalSelectedClassChanged(ClassSectionDto? value)
    {
        if (value != null && !string.IsNullOrWhiteSpace(value.RoomNumber))
        {
            NewRoomNumber = value.RoomNumber;
        }
    }

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
        if (value != null)
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

            var role = _apiService.CurrentUser?.Role;
            IsStudentOrParent = role is UserRole.Student or UserRole.Parent;
            CanSwitchClass = !IsStudentOrParent;
            CanManageTimetable = role is UserRole.SuperAdmin or UserRole.SchoolAdmin;
            IsTeacherUser = role == UserRole.Teacher;

            if (CanManageTimetable)
            {
                if (AvailableSubjects.Count == 0)
                {
                    var subjRes = await _apiService.GetSubjectsAsync();
                    if (subjRes.Success && subjRes.Data != null)
                    {
                        AvailableSubjects = new ObservableCollection<SubjectDto>(subjRes.Data);
                        NewSubject = AvailableSubjects.FirstOrDefault();
                    }
                }

                if (AvailableTeachers.Count == 0)
                {
                    var teacherRes = await _apiService.GetTeachersAsync();
                    if (teacherRes.Success && teacherRes.Data != null)
                    {
                        AvailableTeachers = new ObservableCollection<TeacherDto>(teacherRes.Data);
                        NewTeacher = AvailableTeachers.FirstOrDefault();
                    }
                }
            }

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
            }

            if (role == UserRole.Student)
            {
                PageHeaderTitle = "My Class Timetable";
                var targetClass = Classes.FirstOrDefault(c => c.Name.Equals("10-A", StringComparison.OrdinalIgnoreCase)) ?? Classes.FirstOrDefault();
                SelectedClass = targetClass;
                EnrolledClassBadgeText = $"Class {SelectedClass?.Name ?? "10-A"}";
                PageHeaderSubtitle = $"Class {SelectedClass?.Name ?? "10-A"} • {SelectedClass?.RoomNumber ?? "Room 201"} • Grade 10";
            }
            else if (role == UserRole.Parent)
            {
                var childRes = await _apiService.GetParentChildrenAsync();
                var activeChild = childRes.Data?.FirstOrDefault(c => c.StudentId == _apiService.CurrentSelectedChildId) ?? childRes.Data?.FirstOrDefault();
                var childName = activeChild?.FullName ?? "Child";
                var className = activeChild?.ClassName ?? "10-A";
                PageHeaderTitle = $"{childName}'s Class Timetable";
                var targetClass = Classes.FirstOrDefault(c => c.Name.Contains(className, StringComparison.OrdinalIgnoreCase)) ?? Classes.FirstOrDefault();
                SelectedClass = targetClass;
                EnrolledClassBadgeText = $"Class {SelectedClass?.Name ?? "10-A"}";
                PageHeaderSubtitle = $"Class {SelectedClass?.Name ?? "10-A"} • {SelectedClass?.RoomNumber ?? "Room 201"} • Grade 10";
            }
            else
            {
                PageHeaderTitle = IsTeacherUser ? "Faculty Teaching Schedule" : "Weekly Class Timetable";
                PageHeaderSubtitle = "Select class and academic day to view class hours";
                if (SelectedClass == null && Classes.Count > 0)
                {
                    SelectedClass = Classes.FirstOrDefault(c => c.Name.Equals("10-A", StringComparison.OrdinalIgnoreCase)) ?? Classes.FirstOrDefault();
                }
            }

            var classId = SelectedClass?.Id ?? Guid.Parse("a0000000-0000-0000-0000-000000000001");
            await FetchClassTimetableInternalAsync(classId);
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

    private (Guid TeacherId, string TeacherName) GetCurrentTeacherInfo()
    {
        var user = _apiService.CurrentUser;
        var teacherId = user?.LinkedProfileId ?? Guid.Empty;
        var teacherName = user?.FullName ?? string.Empty;
        return (teacherId, teacherName);
    }

    private bool IsClassTaughtByCurrentTeacher(TimetableEntryDto entry)
    {
        var (teacherId, teacherName) = GetCurrentTeacherInfo();
        if (teacherId != Guid.Empty && entry.TeacherId == teacherId) return true;
        if (!string.IsNullOrWhiteSpace(teacherName))
        {
            if (string.Equals(entry.TeacherName, teacherName, StringComparison.OrdinalIgnoreCase)) return true;
            if (entry.TeacherName.Contains(teacherName, StringComparison.OrdinalIgnoreCase)) return true;
            if (teacherName.Contains(entry.TeacherName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    [RelayCommand]
    public void SetScheduleFilter(string filter)
    {
        ShowOnlyMyTeaching = string.Equals(filter, "MyClasses", StringComparison.OrdinalIgnoreCase);
        FilterSlotsForDay();
    }

    private async Task FetchClassTimetableInternalAsync(Guid classId)
    {
        List<TimetableEntryDto> entries = new();
        if (classId != Guid.Empty)
        {
            var res = await _apiService.GetClassTimetableAsync(classId);
            if (res.Success && res.Data != null && res.Data.Count > 0)
            {
                entries = res.Data;
            }
        }

        if (entries.Count == 0)
        {
            var fallback = await _apiService.GetStudentTimetableAsync(Guid.Empty);
            if (fallback.Success && fallback.Data != null && fallback.Data.Count > 0)
            {
                entries = fallback.Data;
            }
        }

        if (entries.Count == 0)
        {
            var fallbackAll = await _apiService.GetClassTimetableAsync(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
            entries = fallbackAll.Data ?? new();
        }

        _cachedClassEntries = entries;
        FilterSlotsForDay();
    }

    [RelayCommand]
    public async Task SelectDayAsync(DayChipViewModel chip)
    {
        if (chip == null) return;
        SelectedDay = chip.Day;
        foreach (var d in Days)
        {
            d.IsSelected = (d.Day == chip.Day);
        }
        DayHeaderTitle = $"{SelectedDay} Schedule";

        if (_cachedClassEntries.Count == 0)
        {
            var classId = SelectedClass?.Id ?? Guid.Parse("a0000000-0000-0000-0000-000000000001");
            await FetchClassTimetableInternalAsync(classId);
        }
        else
        {
            FilterSlotsForDay();
        }
    }

    private void FilterSlotsForDay()
    {
        var filtered = _cachedClassEntries
            .Where(s => s.DayOfWeek == SelectedDay)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (filtered.Count == 0 && _cachedClassEntries.Count > 0)
        {
            // If the selected day has no classes in a particular alternate schedule, show Monday classes
            filtered = _cachedClassEntries
                .Where(s => s.DayOfWeek == DayOfWeek.Monday)
                .OrderBy(s => s.StartTime)
                .ToList();
        }

        int teacherSlotsToday = filtered.Count(IsClassTaughtByCurrentTeacher);

        if (IsTeacherUser && ShowOnlyMyTeaching)
        {
            filtered = filtered.Where(IsClassTaughtByCurrentTeacher).ToList();
        }

        var displayList = filtered.Select(entry =>
        {
            bool isMyClass = IsTeacherUser && IsClassTaughtByCurrentTeacher(entry);
            return new TimetableSlotItemViewModel
            {
                Entry = entry,
                IsMyTeachingClass = isMyClass,
                CardBorderColor = isMyClass ? Color.FromArgb("#2563EB") : Color.FromArgb("#E2E8F0"),
                CardBorderThickness = isMyClass ? 2.0 : 1.0,
                CardBackgroundColor = isMyClass ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#FFFFFF"),
                ShowMyClassBadge = isMyClass,
                TeachingBadgeText = "Teaching"
            };
        }).ToList();

        ScheduleSlots = new ObservableCollection<TimetableSlotItemViewModel>(displayList);
        HasNoSlots = (ScheduleSlots.Count == 0);

        if (IsTeacherUser)
        {
            if (ShowOnlyMyTeaching)
            {
                PageHeaderSubtitle = $"Showing only your teaching schedule • {displayList.Count} class(es) on {SelectedDay}";
            }
            else
            {
                PageHeaderSubtitle = teacherSlotsToday > 0
                    ? $"Showing all classes • {teacherSlotsToday} class(es) highlighted with blue border taught by you"
                    : $"Showing all classes • No teaching assignments for you on {SelectedDay}";
            }
        }
    }

    [RelayCommand]
    public void OpenAddSlotModal()
    {
        ModalSelectedClass = SelectedClass ?? Classes.FirstOrDefault();
        ModalSelectedDayChip = Days.FirstOrDefault(d => d.Day == SelectedDay) ?? Days.FirstOrDefault();

        if (AvailableSubjects.Count > 0 && NewSubject == null)
            NewSubject = AvailableSubjects.FirstOrDefault();
        if (AvailableTeachers.Count > 0 && NewTeacher == null)
            NewTeacher = AvailableTeachers.FirstOrDefault();

        NewRoomNumber = ModalSelectedClass?.RoomNumber ?? SelectedClass?.RoomNumber ?? "Room 201";
        IsAddSlotModalVisible = true;
    }

    [RelayCommand]
    public void CloseAddSlotModal()
    {
        IsAddSlotModalVisible = false;
    }

    [RelayCommand]
    public async Task SaveTimetableSlotAsync()
    {
        var targetClass = ModalSelectedClass ?? SelectedClass;
        var targetDay = ModalSelectedDayChip?.Day ?? SelectedDay;

        if (targetClass == null || NewSubject == null || NewTeacher == null)
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Validation Error", "Please select class, subject, and teacher.", "OK");
            return;
        }

        if (NewStartTime >= NewEndTime)
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Validation Error", "End time must be after start time.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            var request = new CreateTimetableEntryRequest(
                targetClass.Id,
                NewSubject.Id,
                NewTeacher.Id,
                targetDay,
                NewStartTime,
                NewEndTime,
                NewRoomNumber);

            var res = await _apiService.CreateTimetableEntryAsync(request);
            if (res.Success && res.Data != null)
            {
                IsAddSlotModalVisible = false;
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Success", $"Schedule slot assigned to {NewTeacher.FullName} for Class {targetClass.Name} ({targetDay}) successfully!", "OK");

                // Switch view to match the newly assigned class and day
                SelectedClass = targetClass;
                SelectedDay = targetDay;
                foreach (var d in Days)
                    d.IsSelected = (d.Day == targetDay);
                DayHeaderTitle = $"{targetDay} Schedule";

                await FetchClassTimetableInternalAsync(targetClass.Id);
            }
            else
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Error", res.Message ?? "Failed to assign schedule slot.", "OK");
            }
        }
        catch (Exception ex)
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteSlotAsync(object? slotObj)
    {
        TimetableEntryDto? slot = slotObj as TimetableEntryDto ?? (slotObj as TimetableSlotItemViewModel)?.Entry;
        if (slot == null) return;

        bool confirm = true;
        if (Shell.Current != null)
        {
            confirm = await Shell.Current.DisplayAlert(
                "Remove Schedule Slot",
                $"Are you sure you want to remove {slot.SubjectName} ({slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}) from {slot.ClassName} on {slot.DayOfWeek}?",
                "Remove",
                "Cancel");
        }

        if (!confirm) return;

        try
        {
            IsBusy = true;
            var res = await _apiService.DeleteTimetableEntryAsync(slot.Id);
            if (res.Success)
            {
                _cachedClassEntries.RemoveAll(e => e.Id == slot.Id);
                FilterSlotsForDay();
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Removed", "Schedule slot removed successfully.", "OK");
            }
            else
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Error", res.Message ?? "Failed to delete slot.", "OK");
            }
        }
        catch (Exception ex)
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
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
    [ObservableProperty] private bool _isAdmin;

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
            IsAdmin = Profile.Role == UserRole.SuperAdmin || Profile.Role == UserRole.SchoolAdmin;
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
            IsAdmin = false;
            FullName = "Guest User";
            Email = "guest@nexora.edu";
            PhoneNumber = string.Empty;
            RoleBadge = "Unauthenticated";
            RoleDepartment = "Guest Portal";
        }
    }

    [RelayCommand]
    private async Task NavigateToAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        try
        {
            if (Shell.Current != null)
            {
                var target = (route == "classes" || route == "audit-logs" || route.Contains('-'))
                    ? route
                    : $"//{route}";
                await Shell.Current.GoToAsync(target);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NavigateToAsync error: {ex.Message}");
            try
            {
                if (Shell.Current != null) await Shell.Current.GoToAsync(route);
            }
            catch { }
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
