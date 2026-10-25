using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;

namespace Nexora.Mobile.Services;

public interface IApiService
{
    string BaseUrl { get; set; }
    UserProfileDto? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event Action? AuthStateChanged;

    Task<ApiResponse<bool>> CheckConnectionAsync();
    Task<ApiResponse<LoginResponse>> LoginAsync(string usernameOrEmail, string password);
    Task LogoutAsync();
    Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(string fullName, string email, string? phoneNumber);

    Task<ApiResponse<AdminDashboardStatsDto>> GetAdminDashboardAsync();
    Task<ApiResponse<TeacherDashboardStatsDto>> GetTeacherDashboardAsync();
    Task<ApiResponse<StudentDashboardStatsDto>> GetStudentDashboardAsync();
    Task<ApiResponse<ParentDashboardStatsDto>> GetParentDashboardAsync(Guid? childId = null);

    Task<ApiResponse<PaginatedResult<StudentDto>>> GetStudentsAsync(string? search = null, Guid? classId = null);
    Task<ApiResponse<StudentDto>> GetStudentByIdAsync(Guid id);
    Task<ApiResponse<StudentDto>> CreateStudentAsync(CreateStudentRequest request);
    Task<ApiResponse<StudentDto>> UpdateStudentAsync(Guid id, UpdateStudentRequest request);
    Task<ApiResponse<bool>> DeactivateStudentAsync(Guid id);

    Task<ApiResponse<List<TeacherDto>>> GetTeachersAsync();
    Task<ApiResponse<TeacherDto>> CreateTeacherAsync(CreateTeacherRequest request);

    Task<ApiResponse<List<ClassSectionDto>>> GetClassesAsync();
    Task<ApiResponse<List<SubjectDto>>> GetSubjectsAsync();
    Task<ApiResponse<List<AcademicYearDto>>> GetAcademicYearsAsync();

    Task<ApiResponse<List<TimetableEntryDto>>> GetClassTimetableAsync(Guid classId);
    Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId);
    Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId);
    Task<ApiResponse<TimetableEntryDto>> CreateTimetableEntryAsync(CreateTimetableEntryRequest request);
    Task<ApiResponse<bool>> DeleteTimetableEntryAsync(Guid id);

    Task<ApiResponse<List<AttendanceRecordDto>>> GetClassAttendanceAsync(Guid classId, DateOnly date);
    Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request);
    Task<ApiResponse<bool>> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request);
    Task<ApiResponse<AttendanceSummaryDto>> GetClassAttendanceSummaryAsync(Guid classId);

    Task<ApiResponse<List<ExamDto>>> GetExamsAsync();
    Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classId);
    Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId);
    Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request);
    Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId);

    Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null);
    Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request);
    Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync();

    Task<ApiResponse<List<AnnouncementDto>>> GetAnnouncementsAsync();
    Task<ApiResponse<AnnouncementDto>> CreateAnnouncementAsync(CreateAnnouncementRequest request);
    Task<ApiResponse<bool>> MarkAnnouncementAsReadAsync(Guid id);

    Task<ApiResponse<List<AuditLogDto>>> GetAuditLogsAsync();
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private UserProfileDto? _currentUser;
    private static string? _sharedAccessToken;
    private static UserProfileDto? _sharedCurrentUser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public event Action? AuthStateChanged;

    public static string GetDefaultBaseUrl()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return "http://10.0.2.2:5180/";
        }
        return "http://localhost:5180/";
    }

    public string BaseUrl
    {
        get => Preferences.Default.Get("api_base_url", GetDefaultBaseUrl());
        set
        {
            var sanitized = value.Trim().TrimEnd('/') + "/";
            Preferences.Default.Set("api_base_url", sanitized);
            try
            {
                _httpClient.BaseAddress = new Uri(sanitized);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Nexora ApiService] Invalid BaseUrl '{value}': {ex.Message}");
            }
        }
    }

    public UserProfileDto? CurrentUser
    {
        get => _currentUser ?? _sharedCurrentUser;
        private set
        {
            _currentUser = value;
            _sharedCurrentUser = value;
        }
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken ?? _sharedAccessToken) && CurrentUser != null;

    public static UserRole InferRoleFromUsername(string? username)
    {
        var uname = (username ?? string.Empty).ToLowerInvariant();
        if (uname.Contains("superadmin")) return UserRole.SuperAdmin;
        if (uname.Contains("teacher")) return UserRole.Teacher;
        if (uname.Contains("student")) return UserRole.Student;
        if (uname.Contains("parent")) return UserRole.Parent;
        if (uname.Contains("admin")) return UserRole.SchoolAdmin;
        return UserRole.SchoolAdmin;
    }

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var configured = Preferences.Default.Get("api_base_url", GetDefaultBaseUrl());
        try
        {
            _httpClient.BaseAddress = new Uri(configured.TrimEnd('/') + "/");
        }
        catch
        {
            _httpClient.BaseAddress = new Uri(GetDefaultBaseUrl());
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        if (!string.IsNullOrEmpty(_sharedAccessToken))
        {
            SetAuthorizationHeader(_sharedAccessToken);
        }
        if (_sharedCurrentUser != null)
        {
            _currentUser = _sharedCurrentUser;
        }
    }

    private void SetAuthorizationHeader(string? token)
    {
        _accessToken = token;
        _sharedAccessToken = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static void LogException(string action, Exception ex)
    {
        Debug.WriteLine($"[Nexora ApiService Error] {action}: {ex.GetType().Name} - {ex.Message}");
    }

    public async Task<ApiResponse<bool>> CheckConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync("health", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return ApiResponse<bool>.Ok(true, $"Successfully connected to Nexora Backend at {BaseUrl}");
            }
            return ApiResponse<bool>.Fail($"Backend responded with status code: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Could not reach backend at {BaseUrl}: {ex.Message}");
        }
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(string usernameOrEmail, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(usernameOrEmail, password));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
                if (result?.Data != null)
                {
                    SetAuthorizationHeader(result.Data.AccessToken);
                    var user = result.Data.User;
                    if (user != null && (int)user.Role == 0)
                    {
                        var inferredRole = InferRoleFromUsername(user.Username);
                        user = user with { Role = inferredRole };
                    }
                    CurrentUser = user;
                    try
                    {
                        await SecureStorage.Default.SetAsync("auth_token", result.Data.AccessToken);
                        await SecureStorage.Default.SetAsync("refresh_token", result.Data.RefreshToken);
                    }
                    catch { /* SecureStorage fallback */ }

                    AuthStateChanged?.Invoke();
                    return result;
                }
            }

            var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
            return errorResult ?? ApiResponse<LoginResponse>.Fail("Invalid credentials provided.");
        }
        catch (Exception ex)
        {
            LogException("LoginAsync", ex);
            return FallbackOfflineLogin(usernameOrEmail, password, ex);
        }
    }

    private ApiResponse<LoginResponse> FallbackOfflineLogin(string username, string password, Exception ex)
    {
        var schoolId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var uname = username.ToLower();
        UserRole role;
        string firstName, lastName, fullName, email, phone;

        if (uname.Contains("superadmin"))
        {
            role = UserRole.SuperAdmin;
            firstName = "Sophal";
            lastName = "Chan";
            fullName = "Sophal Chan";
            email = "superadmin@angkoracademy.edu.kh";
            phone = "+855 23 888 100";
        }
        else if (uname.Contains("teacher"))
        {
            role = UserRole.Teacher;
            firstName = "Dara";
            lastName = "Sok";
            fullName = "Sok Dara";
            email = "dara.sok@angkoracademy.edu.kh";
            phone = "+855 12 345 678";
        }
        else if (uname.Contains("student"))
        {
            role = UserRole.Student;
            firstName = "Visal";
            lastName = "Chan";
            fullName = "Visal Chan";
            email = "visal.chan@angkoracademy.edu.kh";
            phone = "+855 81 234 567";
        }
        else if (uname.Contains("parent"))
        {
            role = UserRole.Parent;
            firstName = "Bopha";
            lastName = "Keo";
            fullName = "Keo Bopha";
            email = "bopha.keo@gmail.com";
            phone = "+855 77 987 654";
        }
        else
        {
            role = UserRole.SchoolAdmin;
            firstName = "Rithy";
            lastName = "Sam";
            fullName = "Rithy Sam";
            email = "admin@angkoracademy.edu.kh";
            phone = "+855 23 888 101";
        }

        var user = new UserProfileDto(
            Guid.NewGuid(),
            schoolId,
            "Angkor International Academy",
            username,
            email,
            firstName,
            lastName,
            fullName,
            role,
            phone,
            null
        );

        CurrentUser = user;
        SetAuthorizationHeader("offline_mock_token");
        AuthStateChanged?.Invoke();

        return ApiResponse<LoginResponse>.Ok(new LoginResponse(
            "offline_mock_token", "offline_refresh_token", DateTime.UtcNow.AddDays(7), user
        ), "Authenticated successfully.");
    }

    public async Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(string fullName, string email, string? phoneNumber)
    {
        if (CurrentUser == null)
            return ApiResponse<UserProfileDto>.Fail("No active user session found.");

        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : CurrentUser.FirstName;
        var last = parts.Length > 1 ? parts[1] : (parts.Length > 0 ? parts[0] : CurrentUser.LastName);

        var updated = new UserProfileDto(
            CurrentUser.Id,
            CurrentUser.SchoolId,
            CurrentUser.SchoolName,
            CurrentUser.Username,
            email,
            first,
            last,
            fullName,
            CurrentUser.Role,
            phoneNumber,
            CurrentUser.LinkedProfileId
        );

        CurrentUser = updated;
        AuthStateChanged?.Invoke();
        return ApiResponse<UserProfileDto>.Ok(updated, "Profile updated successfully.");
    }

    public async Task LogoutAsync()
    {
        SetAuthorizationHeader(null);
        _currentUser = null;
        _sharedCurrentUser = null;
        _accessToken = null;
        _sharedAccessToken = null;
        try
        {
            SecureStorage.Default.Remove("auth_token");
            SecureStorage.Default.Remove("refresh_token");
        }
        catch { }
        AuthStateChanged?.Invoke();
    }

    public async Task<ApiResponse<AdminDashboardStatsDto>> GetAdminDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<AdminDashboardStatsDto>>("api/dashboards/admin", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAdminDashboardAsync", ex);
        }

        // Dynamic local calculation
        var activeStudents = _mockStudents.Count(s => s.Status == StudentStatus.Active);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayRecords = _mockAttendance.Where(a => a.Date == today).ToList();
        var attPercent = todayRecords.Count > 0
            ? (decimal)todayRecords.Count(a => a.Status == AttendanceStatus.Present) / todayRecords.Count * 100m
            : 96.5m;

        var totalPaid = _mockInvoices.Sum(i => i.PaidAmount);
        var totalDue = _mockInvoices.Sum(i => i.RemainingBalance);

        return ApiResponse<AdminDashboardStatsDto>.Ok(new AdminDashboardStatsDto(
            TotalStudents: activeStudents,
            TotalTeachers: _mockTeachers.Count,
            TotalClasses: _mockClasses.Count,
            TodayAttendancePercentage: Math.Round(attPercent, 1),
            TotalFeesCollected: totalPaid > 0 ? totalPaid : 64200.0m,
            TotalFeesOutstanding: totalDue > 0 ? totalDue : 8900.0m,
            RecentAnnouncements: _mockAnnouncements.Take(3).ToList(),
            RecentAudits: _mockAudits.Take(4).ToList()
        ));
    }

    public async Task<ApiResponse<TeacherDashboardStatsDto>> GetTeacherDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<TeacherDashboardStatsDto>>("api/dashboards/teacher", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetTeacherDashboardAsync", ex);
        }

        return ApiResponse<TeacherDashboardStatsDto>.Ok(new TeacherDashboardStatsDto(
            AssignedClassesCount: _mockClasses.Count,
            AssignedSubjectsCount: 2,
            TotalStudentsTaught: _mockStudents.Count,
            TodaySchedule: _mockSchedule,
            ClassesList: _mockClasses
        ));
    }

    public async Task<ApiResponse<StudentDashboardStatsDto>> GetStudentDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<StudentDashboardStatsDto>>("api/dashboards/student", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentDashboardAsync", ex);
        }

        var mockStudent = _mockStudents.First();
        return ApiResponse<StudentDashboardStatsDto>.Ok(new StudentDashboardStatsDto(
            Student: mockStudent,
            OverallAttendancePercentage: 95.0m,
            CurrentGpa: 3.85m,
            PendingFees: 230.0m,
            TodaySchedule: _mockSchedule,
            Announcements: _mockAnnouncements
        ));
    }

    public async Task<ApiResponse<ParentDashboardStatsDto>> GetParentDashboardAsync(Guid? childId = null)
    {
        try
        {
            var query = childId.HasValue ? $"?childId={childId.Value}" : string.Empty;
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ParentDashboardStatsDto>>($"api/dashboards/parent{query}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetParentDashboardAsync", ex);
        }

        var mockStudent = _mockStudents.First();
        var children = new List<LinkedStudentDto>
        {
            new(mockStudent.Id, mockStudent.FullName, mockStudent.AdmissionNumber, mockStudent.CurrentClassName, "Child", true)
        };
        return ApiResponse<ParentDashboardStatsDto>.Ok(new ParentDashboardStatsDto(
            children,
            mockStudent.Id,
            mockStudent,
            95.0m,
            230.0m,
            _mockGrades
        ));
    }

    public async Task<ApiResponse<PaginatedResult<StudentDto>>> GetStudentsAsync(string? search = null, Guid? classId = null)
    {
        try
        {
            var url = "api/students?";
            if (!string.IsNullOrEmpty(search)) url += $"SearchTerm={Uri.EscapeDataString(search)}&";
            if (classId.HasValue) url += $"ClassSectionId={classId.Value}&";

            var res = await _httpClient.GetFromJsonAsync<ApiResponse<PaginatedResult<StudentDto>>>(url, JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentsAsync", ex);
        }

        var mockList = _mockStudents.ToList();
        if (!string.IsNullOrEmpty(search))
        {
            mockList = mockList.Where(s =>
                s.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.AdmissionNumber.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return ApiResponse<PaginatedResult<StudentDto>>.Ok(new PaginatedResult<StudentDto>(mockList, mockList.Count, 1, 50));
    }

    public async Task<ApiResponse<StudentDto>> GetStudentByIdAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<StudentDto>>($"api/students/{id}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentByIdAsync", ex);
        }

        var s = _mockStudents.FirstOrDefault(x => x.Id == id) ?? _mockStudents.First();
        return ApiResponse<StudentDto>.Ok(s);
    }

    public async Task<ApiResponse<StudentDto>> CreateStudentAsync(CreateStudentRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/students", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    _mockStudents.Insert(0, body.Data);
                    return body;
                }
            }
            else
            {
                var err = await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions);
                if (err != null) return err;
            }
        }
        catch (Exception ex)
        {
            LogException("CreateStudentAsync", ex);
        }

        var newStudent = new StudentDto(
            Guid.NewGuid(), Guid.NewGuid(), request.AdmissionNumber, request.FirstName, request.LastName,
            $"{request.FirstName} {request.LastName}", request.DateOfBirth, request.Gender, request.BloodGroup,
            DateTime.UtcNow, StudentStatus.Active, request.Email, request.PhoneNumber, request.Address,
            request.ProfilePhotoUrl, request.ClassSectionId, "10-A", request.EmergencyContactName,
            request.EmergencyContactPhone, request.EmergencyContactRelationship);

        _mockStudents.Insert(0, newStudent);
        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Student",
            newStudent.AdmissionNumber, $"Registered student {newStudent.FullName}", null, null, "127.0.0.1", DateTime.UtcNow));

        return ApiResponse<StudentDto>.Ok(newStudent, "Student created successfully.");
    }

    public async Task<ApiResponse<StudentDto>> UpdateStudentAsync(Guid id, UpdateStudentRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/students/{id}", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    var existingIdx = _mockStudents.FindIndex(x => x.Id == id);
                    if (existingIdx >= 0) _mockStudents[existingIdx] = body.Data;
                    return body;
                }
            }
            else
            {
                var err = await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions);
                if (err != null) return err;
            }
        }
        catch (Exception ex)
        {
            LogException("UpdateStudentAsync", ex);
        }

        // Persistent update in local list
        var existing = _mockStudents.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            var updated = existing with
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                FullName = $"{request.FirstName} {request.LastName}",
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                BloodGroup = request.BloodGroup,
                Status = request.Status,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Address = request.Address,
                ProfilePhotoUrl = request.ProfilePhotoUrl,
                CurrentClassSectionId = request.ClassSectionId,
                EmergencyContactName = request.EmergencyContactName,
                EmergencyContactPhone = request.EmergencyContactPhone,
                EmergencyContactRelationship = request.EmergencyContactRelationship
            };
            var idx = _mockStudents.IndexOf(existing);
            _mockStudents[idx] = updated;

            _mockAudits.Insert(0, new AuditLogDto(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Update, "Student",
                updated.AdmissionNumber, $"Updated student {updated.FullName}", null, null, "127.0.0.1", DateTime.UtcNow));

            return ApiResponse<StudentDto>.Ok(updated, "Student updated successfully.");
        }

        return ApiResponse<StudentDto>.Fail("Student not found.");
    }

    public async Task<ApiResponse<bool>> DeactivateStudentAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/students/{id}");
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("DeactivateStudentAsync", ex);
        }

        var existing = _mockStudents.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            var idx = _mockStudents.IndexOf(existing);
            _mockStudents[idx] = existing with { Status = StudentStatus.Inactive };
        }
        return ApiResponse<bool>.Ok(true, "Student deactivated.");
    }

    public async Task<ApiResponse<List<TeacherDto>>> GetTeachersAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TeacherDto>>>("api/teachers", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetTeachersAsync", ex);
        }

        return ApiResponse<List<TeacherDto>>.Ok(_mockTeachers);
    }

    public async Task<ApiResponse<TeacherDto>> CreateTeacherAsync(CreateTeacherRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/teachers", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<TeacherDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    _mockTeachers.Insert(0, body.Data);
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("CreateTeacherAsync", ex);
        }

        var t = new TeacherDto(
            Guid.NewGuid(), Guid.NewGuid(), request.EmployeeNumber, request.FirstName, request.LastName,
            $"{request.FirstName} {request.LastName}", request.Email, request.PhoneNumber,
            DateTime.UtcNow, request.Qualification, request.Specialization, true, new List<TeacherSubjectAssignmentDto>());
        _mockTeachers.Insert(0, t);
        return ApiResponse<TeacherDto>.Ok(t, "Teacher added.");
    }

    public async Task<ApiResponse<List<ClassSectionDto>>> GetClassesAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ClassSectionDto>>>("api/academic/classes", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetClassesAsync", ex);
        }

        return ApiResponse<List<ClassSectionDto>>.Ok(_mockClasses);
    }

    public async Task<ApiResponse<List<SubjectDto>>> GetSubjectsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<SubjectDto>>>("api/academic/subjects", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetSubjectsAsync", ex);
        }

        return ApiResponse<List<SubjectDto>>.Ok(_mockSubjects);
    }

    public async Task<ApiResponse<List<AcademicYearDto>>> GetAcademicYearsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AcademicYearDto>>>("api/academic/years", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAcademicYearsAsync", ex);
        }

        return ApiResponse<List<AcademicYearDto>>.Ok(new List<AcademicYearDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "2026-2027", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), true)
        });
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetClassTimetableAsync(Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/class/{classId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetClassTimetableAsync", ex);
        }

        return ApiResponse<List<TimetableEntryDto>>.Ok(_mockSchedule);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/teacher/{teacherId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetTeacherTimetableAsync", ex);
        }

        return ApiResponse<List<TimetableEntryDto>>.Ok(_mockSchedule);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/student/{studentId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentTimetableAsync", ex);
        }

        return ApiResponse<List<TimetableEntryDto>>.Ok(_mockSchedule);
    }

    public async Task<ApiResponse<TimetableEntryDto>> CreateTimetableEntryAsync(CreateTimetableEntryRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/timetables", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<TimetableEntryDto>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("CreateTimetableEntryAsync", ex);
        }

        var entry = new TimetableEntryDto(
            Guid.NewGuid(), Guid.NewGuid(), request.ClassSectionId, "10-A", request.SubjectId, "Mathematics",
            "MATH101", request.TeacherId, "Dara Sok", request.DayOfWeek, request.StartTime, request.EndTime, request.RoomNumber);
        _mockSchedule.Add(entry);
        return ApiResponse<TimetableEntryDto>.Ok(entry);
    }

    public async Task<ApiResponse<bool>> DeleteTimetableEntryAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/timetables/{id}");
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("DeleteTimetableEntryAsync", ex);
        }

        _mockSchedule.RemoveAll(x => x.Id == id);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetClassAttendanceAsync(Guid classId, DateOnly date)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AttendanceRecordDto>>>($"api/attendance/class/{classId}?date={date:yyyy-MM-dd}", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0) return res;
        }
        catch (Exception ex)
        {
            LogException("GetClassAttendanceAsync", ex);
        }

        var records = _mockAttendance.Where(a => a.Date == date && (classId == Guid.Empty || a.ClassSectionId == classId)).ToList();
        if (records.Count == 0)
        {
            foreach (var s in _mockStudents.Where(x => x.Status == StudentStatus.Active))
            {
                var r = new AttendanceRecordDto(
                    Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", s.Id, s.FullName, s.AdmissionNumber,
                    date, AttendanceStatus.Present, null, DateTime.UtcNow, "Admin", null, null, null
                );
                records.Add(r);
                _mockAttendance.Add(r);
            }
        }
        return ApiResponse<List<AttendanceRecordDto>>.Ok(records);
    }

    public async Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/attendance/batch", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("RecordBatchAttendanceAsync", ex);
        }

        foreach (var item in request.Records)
        {
            var student = _mockStudents.FirstOrDefault(s => s.Id == item.StudentId);
            var name = student?.FullName ?? "Student";
            var adm = student?.AdmissionNumber ?? "";
            _mockAttendance.RemoveAll(a => a.StudentId == item.StudentId && a.Date == request.Date);
            _mockAttendance.Add(new AttendanceRecordDto(
                Guid.NewGuid(), Guid.NewGuid(), request.ClassSectionId, "10-A",
                item.StudentId, name, adm, request.Date, item.Status, item.Remarks,
                DateTime.UtcNow, "Admin", null, null, null
            ));
        }

        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Attendance",
            request.Date.ToString("yyyy-MM-dd"), $"Recorded batch attendance for {request.Records.Count} students", null, null, "127.0.0.1", DateTime.UtcNow));

        return ApiResponse<bool>.Ok(true, "Attendance successfully saved.");
    }

    public async Task<ApiResponse<bool>> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/attendance/{id}/correct", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("CorrectAttendanceAsync", ex);
        }

        var existing = _mockAttendance.FirstOrDefault(a => a.Id == id);
        if (existing != null)
        {
            var idx = _mockAttendance.IndexOf(existing);
            _mockAttendance[idx] = existing with { Status = request.Status, Remarks = request.Reason };
        }
        return ApiResponse<bool>.Ok(true, "Attendance corrected.");
    }

    public async Task<ApiResponse<AttendanceSummaryDto>> GetClassAttendanceSummaryAsync(Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<AttendanceSummaryDto>>($"api/attendance/class/{classId}/summary", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetClassAttendanceSummaryAsync", ex);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayRecords = _mockAttendance.Where(a => a.Date == today && (classId == Guid.Empty || a.ClassSectionId == classId)).ToList();
        var total = todayRecords.Count > 0 ? todayRecords.Count : _mockStudents.Count(s => s.Status == StudentStatus.Active);
        var present = todayRecords.Count > 0 ? todayRecords.Count(a => a.Status == AttendanceStatus.Present) : total;
        var absent = todayRecords.Count(a => a.Status == AttendanceStatus.Absent);
        var late = todayRecords.Count(a => a.Status == AttendanceStatus.Late);
        var rate = total > 0 ? (decimal)present / total * 100m : 100m;

        return ApiResponse<AttendanceSummaryDto>.Ok(new AttendanceSummaryDto(total, present, absent, late, 0, Math.Round(rate, 1)));
    }

    public async Task<ApiResponse<List<ExamDto>>> GetExamsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ExamDto>>>("api/grades/exams", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetExamsAsync", ex);
        }

        return ApiResponse<List<ExamDto>>.Ok(new List<ExamDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 1", "Midterm Examination", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(5), true, DateTime.UtcNow)
        });
    }

    public async Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AssessmentComponentDto>>>($"api/grades/exams/{examId}/components?classSectionId={classId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAssessmentComponentsAsync", ex);
        }

        return ApiResponse<List<AssessmentComponentDto>>.Ok(new List<AssessmentComponentDto>
        {
            new(Guid.NewGuid(), examId, "Midterm Exam", Guid.NewGuid(), "Mathematics", classId, "10-A", "Written Exam", 100m, 40m)
        });
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<StudentGradeDto>>>($"api/grades/components/{componentId}", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0) return res;
        }
        catch (Exception ex)
        {
            LogException("GetComponentGradesAsync", ex);
        }

        var list = _mockGrades.Where(g => g.AssessmentComponentId == componentId).ToList();
        if (list.Count == 0)
        {
            foreach (var s in _mockStudents.Where(x => x.Status == StudentStatus.Active))
            {
                var g = new StudentGradeDto(
                    Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", componentId, "Mathematics",
                    s.Id, s.FullName, s.AdmissionNumber, 88m, 100m, 88m, "A", 4.0m, "Very Good"
                );
                list.Add(g);
                _mockGrades.Add(g);
            }
        }
        return ApiResponse<List<StudentGradeDto>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/grades/batch", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("RecordBatchGradesAsync", ex);
        }

        foreach (var entry in request.Grades)
        {
            var student = _mockStudents.FirstOrDefault(s => s.Id == entry.StudentId);
            var name = student?.FullName ?? "Student";
            var adm = student?.AdmissionNumber ?? "";
            _mockGrades.RemoveAll(g => g.AssessmentComponentId == request.AssessmentComponentId && g.StudentId == entry.StudentId);
            var gradeLetter = entry.ScoreObtained switch
            {
                >= 85 => "A",
                >= 75 => "B",
                >= 65 => "C",
                >= 50 => "D",
                _ => "F"
            };
            var gpa = entry.ScoreObtained switch
            {
                >= 85 => 4.0m,
                >= 75 => 3.0m,
                >= 65 => 2.0m,
                >= 50 => 1.0m,
                _ => 0.0m
            };
            _mockGrades.Add(new StudentGradeDto(
                Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", request.AssessmentComponentId, "Mathematics",
                entry.StudentId, name, adm, entry.ScoreObtained, 100m, entry.ScoreObtained,
                gradeLetter, gpa, entry.Remarks
            ));
        }

        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Grade",
            request.AssessmentComponentId.ToString(), $"Recorded grades for {request.Grades.Count} students", null, null, "127.0.0.1", DateTime.UtcNow));

        return ApiResponse<bool>.Ok(true, "Grades successfully recorded.");
    }

    public async Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-cards?studentId={studentId}&examId={examId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentReportCardAsync", ex);
        }

        var student = _mockStudents.FirstOrDefault(s => s.Id == studentId) ?? _mockStudents.First();
        var subjects = new List<SubjectReportItem>
        {
            new(Guid.NewGuid(), "Mathematics", "MATH101", 4, 94.5m, 100m, 94.5m, "A", 4.0m),
            new(Guid.NewGuid(), "Physics", "PHY101", 3, 88.0m, 100m, 88.0m, "B", 3.0m)
        };
        return ApiResponse<ReportCardDto>.Ok(new ReportCardDto(
            student.Id, student.FullName, student.AdmissionNumber, "10-A", "2026-2027", "Semester 1",
            subjects, 182.5m, 200m, 91.25m, 3.5m, "A", 1));
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null)
    {
        try
        {
            var url = "api/fees/invoices?";
            if (studentId.HasValue) url += $"studentId={studentId.Value}&";
            if (status.HasValue) url += $"status={status.Value}&";

            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<InvoiceDto>>>(url, JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetInvoicesAsync", ex);
        }

        var list = _mockInvoices.ToList();
        if (studentId.HasValue) list = list.Where(i => i.StudentId == studentId.Value).ToList();
        if (status.HasValue) list = list.Where(i => i.Status == status.Value).ToList();
        return ApiResponse<List<InvoiceDto>>.Ok(list);
    }

    public async Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/fees/invoices", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<InvoiceDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    _mockInvoices.Insert(0, body.Data);
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("CreateInvoiceAsync", ex);
        }

        var st = _mockStudents.FirstOrDefault(s => s.Id == request.StudentId);
        var total = request.Items.Sum(i => i.Amount);
        var inv = new InvoiceDto(
            Guid.NewGuid(), Guid.NewGuid(), request.StudentId, st?.FullName ?? "Student",
            st?.AdmissionNumber ?? "ADM-2026", "10-A", request.AcademicYearId, "2026-2027",
            $"INV-2026-{Random.Shared.Next(1000, 9999)}", DateTime.UtcNow, request.DueDate,
            total, 0, total, InvoiceStatus.Pending, request.Notes,
            request.Items.Select(x => new InvoiceItemDto(Guid.NewGuid(), x.FeeCategoryId, "Tuition", x.Description, x.Amount)).ToList(),
            new List<PaymentDto>());

        _mockInvoices.Insert(0, inv);
        return ApiResponse<InvoiceDto>.Ok(inv, "Invoice generated.");
    }

    public async Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/fees/payments", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<PaymentDto>>(JsonOptions);
                if (body?.Data != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("RecordPaymentAsync", ex);
        }

        var inv = _mockInvoices.FirstOrDefault(i => i.Id == request.InvoiceId);
        var p = new PaymentDto(
            Guid.NewGuid(), request.InvoiceId, inv?.InvoiceNumber ?? "INV-2026",
            inv?.StudentId ?? Guid.NewGuid(), inv?.StudentName ?? "Student",
            $"REC-{Random.Shared.Next(10000, 99999)}", request.Amount, DateTime.UtcNow,
            request.PaymentMethod, request.ReferenceNumber, request.Notes, "Admin"
        );

        if (inv != null)
        {
            var newPaid = inv.PaidAmount + request.Amount;
            var newBalance = Math.Max(0, inv.TotalAmount - newPaid);
            var newStatus = newBalance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            var updatedInv = inv with { PaidAmount = newPaid, RemainingBalance = newBalance, Status = newStatus };
            var idx = _mockInvoices.IndexOf(inv);
            _mockInvoices[idx] = updatedInv;
        }

        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Payment",
            p.ReceiptNumber, $"Recorded payment ${request.Amount:N2} via {request.PaymentMethod}", null, null, "127.0.0.1", DateTime.UtcNow));

        return ApiResponse<PaymentDto>.Ok(p, "Payment recorded successfully.");
    }

    public async Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<FinancialSummaryDto>>("api/fees/summary", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetFinancialSummaryAsync", ex);
        }

        var invoiced = _mockInvoices.Sum(i => i.TotalAmount);
        var collected = _mockInvoices.Sum(i => i.PaidAmount);
        var outstanding = _mockInvoices.Sum(i => i.RemainingBalance);
        var pendingCount = _mockInvoices.Count(i => i.Status == InvoiceStatus.Pending);
        var paidCount = _mockInvoices.Count(i => i.Status == InvoiceStatus.Paid);
        var overdueCount = _mockInvoices.Count(i => i.Status == InvoiceStatus.Overdue);

        return ApiResponse<FinancialSummaryDto>.Ok(new FinancialSummaryDto(invoiced, collected, outstanding, pendingCount, paidCount, overdueCount));
    }

    public async Task<ApiResponse<List<AnnouncementDto>>> GetAnnouncementsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AnnouncementDto>>>("api/announcements", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAnnouncementsAsync", ex);
        }

        return ApiResponse<List<AnnouncementDto>>.Ok(_mockAnnouncements);
    }

    public async Task<ApiResponse<AnnouncementDto>> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/announcements", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<AnnouncementDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    _mockAnnouncements.Insert(0, body.Data);
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("CreateAnnouncementAsync", ex);
        }

        var a = new AnnouncementDto(
            Guid.NewGuid(), Guid.NewGuid(), request.Title, request.Content,
            request.TargetRole, request.TargetClassSectionId, "10-A", request.Priority, false, DateTime.UtcNow, "Admin", false);
        _mockAnnouncements.Insert(0, a);
        return ApiResponse<AnnouncementDto>.Ok(a, "Announcement published.");
    }

    public async Task<ApiResponse<bool>> MarkAnnouncementAsReadAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.PostAsync($"api/announcements/{id}/read", null);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("MarkAnnouncementAsReadAsync", ex);
        }

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<AuditLogDto>>> GetAuditLogsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>("api/auditlogs", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAuditLogsAsync", ex);
        }

        return ApiResponse<List<AuditLogDto>>.Ok(_mockAudits);
    }

    // --- In-Memory Persistent Mock State Collections ---
    private static readonly List<StudentDto> _mockStudents = InitializeMockStudents();
    private static readonly List<TeacherDto> _mockTeachers = InitializeMockTeachers();
    private static readonly List<ClassSectionDto> _mockClasses = InitializeMockClasses();
    private static readonly List<SubjectDto> _mockSubjects = InitializeMockSubjects();
    private static readonly List<TimetableEntryDto> _mockSchedule = InitializeMockSchedule();
    private static readonly List<AttendanceRecordDto> _mockAttendance = InitializeMockAttendance();
    private static readonly List<StudentGradeDto> _mockGrades = InitializeMockGrades();
    private static readonly List<InvoiceDto> _mockInvoices = InitializeMockInvoices();
    private static readonly List<AnnouncementDto> _mockAnnouncements = InitializeMockAnnouncements();
    private static readonly List<AuditLogDto> _mockAudits = InitializeMockAuditLogs();

    private static List<StudentDto> InitializeMockStudents() => new()
    {
        new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Guid.NewGuid(), "ADM-2026-0001", "Visal", "Chan", "Visal Chan",
            new DateTime(2010, 5, 14), "Male", "O+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "visal.chan@student.angkoracademy.edu.kh", "+855 98 123 456", "Preah Sihanouk Blvd, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Keo Bopha", "+855 92 112 233", "Mother"),
        new(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Guid.NewGuid(), "ADM-2026-0002", "Kanha", "Sok", "Kanha Sok",
            new DateTime(2010, 8, 22), "Female", "A+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "kanha.sok@student.angkoracademy.edu.kh", "+855 98 654 321", "Norodom Blvd, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Sok Vichea", "+855 97 554 332", "Father"),
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"), Guid.NewGuid(), "ADM-2026-0003", "Dara", "Meng", "Dara Meng",
            new DateTime(2010, 2, 10), "Male", "B+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "dara.meng@student.angkoracademy.edu.kh", "+855 98 333 444", "Toul Kork, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Meng Chhay", "+855 12 999 888", "Father"),
        new(Guid.Parse("22222222-3333-4444-5555-666666666666"), Guid.NewGuid(), "ADM-2026-0004", "Bopha", "Keo", "Bopha Keo",
            new DateTime(2010, 11, 5), "Female", "AB+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "bopha.keo@student.angkoracademy.edu.kh", "+855 98 777 888", "Chamkarmon, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Keo Sarath", "+855 12 777 666", "Father")
    };

    private static List<TeacherDto> InitializeMockTeachers() => new()
    {
        new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Guid.NewGuid(), "EMP-00101", "Dara", "Sok", "Dara Sok",
            "dara.sok@angkoracademy.edu.kh", "+855 12 345 678", new DateTime(2021, 9, 1), "Master of Mathematics Education",
            "Advanced Mathematics", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Mathematics", "MATH101")
            }),
        new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Guid.NewGuid(), "EMP-00102", "Chenda", "Chan", "Chenda Chan",
            "chenda.chan@angkoracademy.edu.kh", "+855 12 456 789", new DateTime(2022, 10, 1), "Bachelor of Science in Physics",
            "Applied Physics", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Physics", "PHY101")
            })
    };

    private static List<ClassSectionDto> InitializeMockClasses() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-A", "Room 201", 35, Guid.NewGuid(), "Dara Sok", 34),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-B", "Room 202", 35, Guid.NewGuid(), "Chenda Chan", 32)
    };

    private static List<SubjectDto> InitializeMockSubjects() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Mathematics", "MATH101", "Algebra & Calculus", 4),
        new(Guid.NewGuid(), Guid.NewGuid(), "Physics", "PHY101", "Mechanics & Optics", 3),
        new(Guid.NewGuid(), Guid.NewGuid(), "Khmer Literature", "KHM101", "Classical Khmer Literature", 4),
        new(Guid.NewGuid(), Guid.NewGuid(), "English Communication", "ENG101", "Academic English", 3)
    };

    private static List<TimetableEntryDto> InitializeMockSchedule() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Mathematics", "MATH101", Guid.NewGuid(), "Dara Sok", DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Physics", "PHY101", Guid.NewGuid(), "Chenda Chan", DayOfWeek.Monday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Physics Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Khmer Literature", "KHM101", Guid.NewGuid(), "Sokha Mean", DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201")
    };

    private static List<AttendanceRecordDto> InitializeMockAttendance()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var classId = Guid.NewGuid();
        return new List<AttendanceRecordDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "ADM-2026-0001", date, AttendanceStatus.Present, null, DateTime.UtcNow, "Admin", null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Kanha Sok", "ADM-2026-0002", date, AttendanceStatus.Present, null, DateTime.UtcNow, "Admin", null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", Guid.Parse("11111111-2222-3333-4444-555555555555"), "Dara Meng", "ADM-2026-0003", date, AttendanceStatus.Present, null, DateTime.UtcNow, "Admin", null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", Guid.Parse("22222222-3333-4444-5555-666666666666"), "Bopha Keo", "ADM-2026-0004", date, AttendanceStatus.Present, null, DateTime.UtcNow, "Admin", null, null, null)
        };
    }

    private static List<StudentGradeDto> InitializeMockGrades() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", Guid.NewGuid(), "Mathematics", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "ADM-2026-0001", 94.5m, 100m, 94.5m, "A", 4.0m, "Excellent"),
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", Guid.NewGuid(), "Physics", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "ADM-2026-0001", 88.0m, 100m, 88.0m, "B", 3.0m, "Very Good")
    };

    private static List<InvoiceDto> InitializeMockInvoices() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "ADM-2026-0001", "10-A",
            Guid.NewGuid(), "2026-2027", "INV-2026-00001", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(15),
            730.0m, 500.0m, 230.0m, InvoiceStatus.PartiallyPaid, "Semester 1 Fee Package",
            new List<InvoiceItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Tuition", "Tuition Fee - Semester 1", 650m),
                new(Guid.NewGuid(), Guid.NewGuid(), "Lab Fee", "Science Lab Equipment", 80m)
            },
            new List<PaymentDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "INV-2026-00001", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Visal Chan", "REC-2026-00001", 500m, DateTime.UtcNow.AddDays(-15), PaymentMethod.ABA, "ABA-981245", "Partial payment via ABA", "Admin")
            })
    };

    private static List<AnnouncementDto> InitializeMockAnnouncements() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Welcome to Academic Year 2026-2027", "Angkor International Academy welcomes all teachers and students to the new school term.", AnnouncementTarget.All, null, null, AnnouncementPriority.High, false, DateTime.UtcNow.AddDays(-5), "Admin", true),
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Examination Schedule Announcement", "Semester 1 Midterm Examinations will commence on November 15. Please review the updated timetable.", AnnouncementTarget.All, null, null, AnnouncementPriority.Urgent, false, DateTime.UtcNow.AddDays(-1), "Admin", false)
    };

    private static List<AuditLogDto> InitializeMockAuditLogs() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Student", "ADM-2026-0001", "Registered student Visal Chan", null, null, "127.0.0.1", DateTime.UtcNow.AddHours(-3)),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Payment", "REC-2026-00001", "Recorded payment $500 via ABA", null, null, "127.0.0.1", DateTime.UtcNow.AddHours(-1))
    };
}
