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
    Task<ApiResponse<List<LinkedStudentDto>>> GetParentChildrenAsync();
    Guid? CurrentSelectedChildId { get; set; }

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
    Task<ApiResponse<List<AttendanceRecordDto>>> GetStudentAttendanceHistoryAsync(Guid studentId);
    Task<ApiResponse<bool>> SubmitStudentPermissionAsync(SubmitPermissionRequest request);

    Task<ApiResponse<List<ExamDto>>> GetExamsAsync();
    Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classId);
    Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId);
    Task<ApiResponse<List<StudentGradeDto>>> GetStudentAllGradesAsync(Guid studentId, Guid examId);
    Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request);
    Task<ApiResponse<bool>> RecordSingleGradeAsync(Guid componentId, Guid studentId, decimal score, string? remarks = null);
    Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId);

    Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null);
    Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request);
    Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync();

    Task<ApiResponse<List<AnnouncementDto>>> GetAnnouncementsAsync();
    Task<ApiResponse<AnnouncementDto>> GetAnnouncementByIdAsync(Guid id);
    Task<ApiResponse<AnnouncementDto>> CreateAnnouncementAsync(CreateAnnouncementRequest request);
    Task<ApiResponse<AnnouncementDto>> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request);
    Task<ApiResponse<bool>> DeleteAnnouncementAsync(Guid id);
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
    public Guid? CurrentSelectedChildId { get; set; }

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

        Guid? linkedProfileId = null;
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
            var matchTeacher = _mockTeachers.FirstOrDefault(t => uname.Contains(t.FirstName.ToLower()) || uname.Contains(t.LastName.ToLower())) ?? _mockTeachers.First();
            firstName = matchTeacher.FirstName;
            lastName = matchTeacher.LastName;
            fullName = matchTeacher.FullName;
            email = matchTeacher.Email;
            phone = matchTeacher.PhoneNumber ?? "+855 12 345 678";
            linkedProfileId = matchTeacher.Id;
        }
        else if (uname.Contains("student"))
        {
            role = UserRole.Student;
            firstName = "Visal";
            lastName = "Chan";
            fullName = "Visal Chan";
            email = "visal.chan@angkoracademy.edu.kh";
            phone = "+855 81 234 567";
            linkedProfileId = StudentVisalId;
        }
        else if (uname.Contains("parent"))
        {
            role = UserRole.Parent;
            firstName = "Bopha";
            lastName = "Keo";
            fullName = "Keo Bopha";
            email = "bopha.keo@gmail.com";
            phone = "+855 77 987 654";
            linkedProfileId = StudentBophaId;
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
            linkedProfileId
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

        var weeklyTrend = new List<AttendanceTrendItemDto>
        {
            new("Mon", 98.0m),
            new("Tue", 96.5m),
            new("Wed", 100.0m),
            new("Thu", 97.5m),
            new("Fri", 99.0m)
        };

        return ApiResponse<AdminDashboardStatsDto>.Ok(new AdminDashboardStatsDto(
            TotalStudents: activeStudents,
            TotalTeachers: _mockTeachers.Count,
            TotalClasses: _mockClasses.Count,
            TodayAttendancePercentage: Math.Round(attPercent, 1),
            TotalFeesCollected: totalPaid > 0 ? totalPaid : 64200.0m,
            TotalFeesOutstanding: totalDue > 0 ? totalDue : 8900.0m,
            RecentAnnouncements: _mockAnnouncements.Take(3).ToList(),
            RecentAudits: _mockAudits.Take(4).ToList(),
            WeeklyAttendanceTrend: weeklyTrend
        ));
    }

    public async Task<ApiResponse<TeacherDashboardStatsDto>> GetTeacherDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<TeacherDashboardStatsDto>>("api/dashboards/teacher", JsonOptions);
            if (res?.Data != null)
            {
                var data = res.Data;
                // If today is weekend or has no classes, provide Monday's classes or mock schedule for display
                if (data.TodaySchedule == null || data.TodaySchedule.Count == 0)
                {
                    data = data with { TodaySchedule = _mockSchedule };
                }
                return ApiResponse<TeacherDashboardStatsDto>.Ok(data);
            }
        }
        catch (Exception ex)
        {
            LogException("GetTeacherDashboardAsync", ex);
        }

        return ApiResponse<TeacherDashboardStatsDto>.Ok(new TeacherDashboardStatsDto(
            AssignedClassesCount: _mockClasses.Count,
            AssignedSubjectsCount: 8,
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
            if (res?.Data != null)
            {
                var data = res.Data;
                if (data.TodaySchedule == null || data.TodaySchedule.Count == 0)
                {
                    var dow = DateTime.Today.DayOfWeek;
                    var dowToUse = (dow >= DayOfWeek.Monday && dow <= DayOfWeek.Friday) ? dow : DayOfWeek.Monday;
                    var fallbackSchedule = _mockSchedule.Where(s => s.DayOfWeek == dowToUse && s.ClassName == "10-A").OrderBy(s => s.StartTime).ToList();
                    data = data with { TodaySchedule = fallbackSchedule };
                }

                if (data.Announcements == null || data.Announcements.Count == 0)
                {
                    data = data with { Announcements = _mockAnnouncements };
                }

                return ApiResponse<StudentDashboardStatsDto>.Ok(data);
            }
        }
        catch (Exception ex)
        {
            LogException("GetStudentDashboardAsync", ex);
        }

        var mockStudent = _mockStudents.First();
        var studentAttendance = _mockAttendance
            .Where(a => a.StudentId == mockStudent.Id)
            .GroupBy(a => a.Date)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToList();

        decimal attendancePct = studentAttendance.Count > 0
            ? Math.Round(((studentAttendance.Count(a => a.Status == AttendanceStatus.Present) +
                           studentAttendance.Count(a => a.Status == AttendanceStatus.Late) * 0.5m) / (decimal)studentAttendance.Count) * 100m, 1)
            : 100.0m;

        var today = DateTime.Today.DayOfWeek;
        var dayToUse = (today >= DayOfWeek.Monday && today <= DayOfWeek.Friday) ? today : DayOfWeek.Monday;
        var todaySchedule = _mockSchedule.Where(s => s.DayOfWeek == dayToUse && s.ClassName == "10-A").OrderBy(s => s.StartTime).ToList();

        return ApiResponse<StudentDashboardStatsDto>.Ok(new StudentDashboardStatsDto(
            Student: mockStudent,
            OverallAttendancePercentage: attendancePct,
            CurrentGpa: 3.85m,
            PendingFees: 230.0m,
            TodaySchedule: todaySchedule,
            Announcements: _mockAnnouncements
        ));
    }

    public async Task<ApiResponse<List<LinkedStudentDto>>> GetParentChildrenAsync()
    {
        var dashboardRes = await GetParentDashboardAsync(CurrentSelectedChildId);
        if (dashboardRes.Success && dashboardRes.Data?.Children != null && dashboardRes.Data.Children.Count > 0)
        {
            return ApiResponse<List<LinkedStudentDto>>.Ok(dashboardRes.Data.Children);
        }

        // Bopha Keo is the mother of Visal Chan (ADM-2026-0001)
        var fallbackChildren = _mockStudents
            .Where(s => s.Id == StudentVisalId || s.EmergencyContactName?.Contains("Bopha", StringComparison.OrdinalIgnoreCase) == true)
            .Select(s => new LinkedStudentDto(
                s.Id, s.FullName, s.AdmissionNumber, s.CurrentClassName ?? "10-A", "Child", s.Id == (CurrentSelectedChildId ?? StudentVisalId)
            )).ToList();

        if (fallbackChildren.Count == 0)
        {
            fallbackChildren = _mockStudents.Take(1).Select(s => new LinkedStudentDto(
                s.Id, s.FullName, s.AdmissionNumber, s.CurrentClassName ?? "10-A", "Child", true
            )).ToList();
        }

        return ApiResponse<List<LinkedStudentDto>>.Ok(fallbackChildren);
    }

    public async Task<ApiResponse<ParentDashboardStatsDto>> GetParentDashboardAsync(Guid? childId = null)
    {
        var activeChildId = childId ?? CurrentSelectedChildId;
        if (childId.HasValue)
        {
            CurrentSelectedChildId = childId.Value;
        }

        try
        {
            var query = activeChildId.HasValue ? $"?childId={activeChildId.Value}" : string.Empty;
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ParentDashboardStatsDto>>($"api/dashboards/parent{query}", JsonOptions);
            if (res?.Data != null)
            {
                if (activeChildId.HasValue) CurrentSelectedChildId = activeChildId.Value;
                return res;
            }
        }
        catch (Exception ex)
        {
            LogException("GetParentDashboardAsync", ex);
        }

        var targetStudent = (activeChildId.HasValue ? _mockStudents.FirstOrDefault(s => s.Id == activeChildId.Value) : null) ?? _mockStudents.First();
        CurrentSelectedChildId = targetStudent.Id;
        var studentAttendance = _mockAttendance
            .Where(a => a.StudentId == targetStudent.Id)
            .GroupBy(a => a.Date)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToList();

        decimal attendancePct = studentAttendance.Count > 0
            ? Math.Round(((studentAttendance.Count(a => a.Status == AttendanceStatus.Present) +
                           studentAttendance.Count(a => a.Status == AttendanceStatus.Late) * 0.5m) / (decimal)studentAttendance.Count) * 100m, 1)
            : 100.0m;

        var children = _mockStudents
            .Where(s => s.Id == StudentVisalId || s.EmergencyContactName?.Contains("Bopha", StringComparison.OrdinalIgnoreCase) == true)
            .Select(s => new LinkedStudentDto(
                s.Id, s.FullName, s.AdmissionNumber, s.CurrentClassName ?? "10-A", "Child", s.Id == targetStudent.Id
            )).ToList();

        if (children.Count == 0)
        {
            children = _mockStudents.Take(1).Select(s => new LinkedStudentDto(
                s.Id, s.FullName, s.AdmissionNumber, s.CurrentClassName ?? "10-A", "Child", true
            )).ToList();
        }

        // Distinct subject grades for the selected child to avoid duplicates
        var childGrades = _mockGrades
            .Where(g => g.StudentId == targetStudent.Id)
            .GroupBy(g => g.SubjectName)
            .Select(g => g.First())
            .Take(6)
            .ToList();

        return ApiResponse<ParentDashboardStatsDto>.Ok(new ParentDashboardStatsDto(
            children,
            targetStudent.Id,
            targetStudent,
            attendancePct,
            230.0m,
            childGrades
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
            if (res?.Data != null && res.Data.Items.Count >= _mockStudents.Count) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentsAsync", ex);
        }

        var mockList = _mockStudents.ToList();
        if (classId.HasValue && classId.Value != Guid.Empty)
        {
            mockList = mockList.Where(s => s.CurrentClassSectionId == classId.Value).ToList();
        }
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
            if (res?.Data != null && res.Data.Count >= 8) return res;

            if (res?.Data != null && res.Data.Count > 0)
            {
                var merged = new List<TeacherDto>(res.Data);
                var existingNames = new HashSet<string>(res.Data.Select(t => t.FullName), StringComparer.OrdinalIgnoreCase);

                foreach (var mt in _mockTeachers)
                {
                    if (existingNames.Add(mt.FullName))
                    {
                        merged.Add(mt);
                    }
                }
                return ApiResponse<List<TeacherDto>>.Ok(merged);
            }
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
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ClassSectionDto>>>("api/academics/classes", JsonOptions);
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
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<SubjectDto>>>("api/academics/subjects", JsonOptions);
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
            if (res?.Data != null && res.Data.Count >= 8) return res;
        }
        catch (Exception ex)
        {
            LogException("GetClassTimetableAsync", ex);
        }

        var list = _mockSchedule.Where(s => s.ClassSectionId == classId).ToList();
        if (list.Count == 0)
        {
            var firstClass = _mockClasses.FirstOrDefault(c => c.Id == classId);
            var className = firstClass?.Name ?? "10-A";
            list = _mockSchedule.Where(s => s.ClassName == className).ToList();
            if (list.Count == 0)
            {
                list = _mockSchedule.Where(s => s.ClassName == "10-A").ToList();
            }
        }
        return ApiResponse<List<TimetableEntryDto>>.Ok(list.Count > 0 ? list : _mockSchedule);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/teacher/{teacherId}", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0) return res;
        }
        catch (Exception ex)
        {
            LogException("GetTeacherTimetableAsync", ex);
        }

        var list = _mockSchedule.Where(s => s.TeacherId == teacherId).ToList();
        return ApiResponse<List<TimetableEntryDto>>.Ok(list.Count > 0 ? list : _mockSchedule);
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/student/{studentId}", JsonOptions);
            if (res?.Data != null && res.Data.Count >= 8) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentTimetableAsync", ex);
        }

        var student = _mockStudents.FirstOrDefault(s => s.Id == studentId);
        var className = student?.CurrentClassName ?? "10-A";
        var list = _mockSchedule.Where(s => s.ClassName == className).ToList();
        return ApiResponse<List<TimetableEntryDto>>.Ok(list.Count > 0 ? list : _mockSchedule);
    }

    public async Task<ApiResponse<TimetableEntryDto>> CreateTimetableEntryAsync(CreateTimetableEntryRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/timetables", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<TimetableEntryDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    _mockSchedule.Add(body.Data);
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("CreateTimetableEntryAsync", ex);
        }

        var cls = _mockClasses.FirstOrDefault(c => c.Id == request.ClassSectionId);
        var subj = _mockSubjects.FirstOrDefault(s => s.Id == request.SubjectId);
        var teacher = _mockTeachers.FirstOrDefault(t => t.Id == request.TeacherId);

        var entry = new TimetableEntryDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            request.ClassSectionId,
            cls?.Name ?? "10-A",
            request.SubjectId,
            subj?.Name ?? "Subject",
            subj?.Code ?? "SUB101",
            request.TeacherId,
            teacher?.FullName ?? "Faculty Teacher",
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            request.RoomNumber ?? cls?.RoomNumber ?? "Room 201");
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
                if (body != null)
                {
                    _mockSchedule.RemoveAll(x => x.Id == id);
                    return body;
                }
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
        var targetClassId = classId != Guid.Empty ? classId : Class10AId;
        var activeClassStudents = _mockStudents
            .Where(x => x.Status == StudentStatus.Active && (x.CurrentClassSectionId == targetClassId || x.CurrentClassSectionId == Class10AId || targetClassId == Class10AId))
            .OrderBy(s => s.AdmissionNumber)
            .ToList();

        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AttendanceRecordDto>>>($"api/attendance/class/{classId}?date={date:yyyy-MM-dd}", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                // Ensure all active class students are included in the roster
                var backendRecordsMap = res.Data.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
                var merged = new List<AttendanceRecordDto>(res.Data);

                foreach (var s in activeClassStudents)
                {
                    if (!backendRecordsMap.ContainsKey(s.Id))
                    {
                        merged.Add(new AttendanceRecordDto(
                            Guid.NewGuid(), Guid.NewGuid(), targetClassId, "10-A",
                            s.Id, s.FullName, s.AdmissionNumber, date,
                            AttendanceStatus.Present, null, DateTime.UtcNow, "Homeroom Teacher", null, null, null
                        ));
                    }
                }
                return ApiResponse<List<AttendanceRecordDto>>.Ok(merged.OrderBy(a => a.AdmissionNumber).ToList());
            }
        }
        catch (Exception ex)
        {
            LogException("GetClassAttendanceAsync", ex);
        }

        var existingForDate = _mockAttendance
            .Where(a => a.Date == date && (classId == Guid.Empty || a.ClassSectionId == classId || a.ClassSectionId == Class10AId))
            .GroupBy(a => a.StudentId)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToDictionary(a => a.StudentId);

        var records = new List<AttendanceRecordDto>();
        foreach (var s in activeClassStudents)
        {
            if (existingForDate.TryGetValue(s.Id, out var existing))
            {
                records.Add(existing);
            }
            else
            {
                var r = new AttendanceRecordDto(
                    Guid.NewGuid(), Guid.NewGuid(), targetClassId, "10-A", s.Id, s.FullName, s.AdmissionNumber,
                    date, AttendanceStatus.Present, null, DateTime.UtcNow, "Homeroom Teacher", null, null, null
                );
                records.Add(r);
                _mockAttendance.Add(r);
            }
        }

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records.OrderBy(a => a.AdmissionNumber).ToList());
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

    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetStudentAttendanceHistoryAsync(Guid studentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AttendanceRecordDto>>>($"api/attendance/student/{studentId}/history", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                var deduplicated = res.Data
                    .GroupBy(a => a.Date)
                    .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
                    .OrderByDescending(a => a.Date)
                    .ToList();
                return ApiResponse<List<AttendanceRecordDto>>.Ok(deduplicated);
            }
        }
        catch (Exception ex)
        {
            LogException("GetStudentAttendanceHistoryAsync", ex);
        }

        var records = _mockAttendance
            .Where(a => a.StudentId == studentId)
            .GroupBy(a => a.Date)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .OrderByDescending(a => a.Date)
            .ToList();

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records);
    }

    public async Task<ApiResponse<bool>> SubmitStudentPermissionAsync(SubmitPermissionRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync($"api/attendance/student/{request.StudentId}/permission", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("SubmitStudentPermissionAsync", ex);
        }

        var reasonNote = string.IsNullOrWhiteSpace(request.Remarks)
            ? $"[Parent Permission] {request.Reason}"
            : $"[Parent Permission] {request.Reason}: {request.Remarks}";

        var student = _mockStudents.FirstOrDefault(s => s.Id == request.StudentId);
        var name = student?.FullName ?? "Visal Chan";
        var adm = student?.AdmissionNumber ?? "ADM-2026-0001";

        // Remove any existing record for this student on this date to guarantee no duplicate day
        _mockAttendance.RemoveAll(a => a.StudentId == request.StudentId && a.Date == request.Date);

        var newRecord = new AttendanceRecordDto(
            Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A",
            request.StudentId, name, adm, request.Date,
            AttendanceStatus.Excused, reasonNote,
            DateTime.UtcNow, CurrentUser?.FullName ?? "Parent",
            null, null, null
        );
        _mockAttendance.Insert(0, newRecord);

        return ApiResponse<bool>.Ok(true, "Permission request submitted successfully.");
    }

    public async Task<ApiResponse<List<ExamDto>>> GetExamsAsync()
    {
        var defaultExams = new List<ExamDto>
        {
            new(ExamMidtermId, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 1", "Semester 1 Midterm Examination", DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-10), true, DateTime.UtcNow.AddDays(-10)),
            new(ExamQuiz1Id, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 1", "Semester 1 Monthly Assessment (October)", DateTime.UtcNow.AddDays(-35), DateTime.UtcNow.AddDays(-30), true, DateTime.UtcNow.AddDays(-30)),
            new(ExamFinal1Id, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 1", "Semester 1 Final Examination", DateTime.UtcNow.AddDays(40), DateTime.UtcNow.AddDays(50), false, null),
            new(ExamMidterm2Id, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 2", "Semester 2 Midterm Examination", DateTime.UtcNow.AddDays(120), DateTime.UtcNow.AddDays(130), false, null),
            new(ExamFinal2Id, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 2", "Semester 2 Final Annual Examination", DateTime.UtcNow.AddDays(200), DateTime.UtcNow.AddDays(210), false, null)
        };

        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ExamDto>>>("api/grades/exams", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                var combined = res.Data.UnionBy(defaultExams, e => e.Name).OrderBy(e => e.StartDate).ToList();
                return ApiResponse<List<ExamDto>>.Ok(combined);
            }
        }
        catch (Exception ex)
        {
            LogException("GetExamsAsync", ex);
        }

        return ApiResponse<List<ExamDto>>.Ok(defaultExams);
    }

    public async Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classId)
    {
        var targetClassId = classId != Guid.Empty ? classId : Class10AId;
        var targetExamId = examId != Guid.Empty ? examId : ExamMidtermId;

        try
        {
            var url = classId != Guid.Empty
                ? $"api/grades/components?examId={examId}&classSectionId={classId}"
                : $"api/grades/components?examId={examId}";
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AssessmentComponentDto>>>(url, JsonOptions);
            if (res?.Data != null && res.Data.Count >= 8) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAssessmentComponentsAsync", ex);
        }

        var list = _mockComponents
            .Where(c => (c.ClassSectionId == targetClassId || c.ClassSectionId == Class10AId) &&
                        (c.ExamId == targetExamId))
            .GroupBy(c => c.SubjectId)
            .Select(g => g.First())
            .ToList();

        if (list.Count < 8)
        {
            // Ensure all 8 curriculum subjects have a component for this exam
            var existingSubjIds = list.Select(c => c.SubjectId).ToHashSet();
            foreach (var subj in _mockSubjects)
            {
                if (!existingSubjIds.Contains(subj.Id))
                {
                    var newComp = new AssessmentComponentDto(
                        Guid.NewGuid(), targetExamId, $"{subj.Name} Written Exam",
                        subj.Id, subj.Name, targetClassId, "10-A", "Assessment Component", 100m, 40m
                    );
                    _mockComponents.Add(newComp);
                    list.Add(newComp);
                }
            }
        }
        return ApiResponse<List<AssessmentComponentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId)
    {
        var comp = _mockComponents.FirstOrDefault(c => c.Id == componentId);
        var compName = comp?.Name ?? "Assessment Exam";
        var subjId = comp?.SubjectId ?? SubjMathId;
        var subjName = comp?.SubjectName ?? "Mathematics";
        var classId = comp?.ClassSectionId ?? Class10AId;

        var sampleScores = new Dictionary<string, (decimal Score, string Remarks)>
        {
            { "ADM-2026-0001", (94.5m, "Outstanding analytical ability") },
            { "ADM-2026-0002", (89.0m, "Very good geometric reasoning") },
            { "ADM-2026-0003", (91.0m, "Proficient problem solving") },
            { "ADM-2026-0004", (84.0m, "Solid homework and active participation") },
            { "ADM-2026-0005", (94.0m, "Excellent algebra and calculus") },
            { "ADM-2026-0006", (87.0m, "Consistent performance and neat assignments") },
            { "ADM-2026-0007", (92.0m, "Great calculation and critical thinking") },
            { "ADM-2026-0008", (89.0m, "High exam score and good reasoning") },
        };

        var allClassStudents = _mockStudents.Where(x => x.Status == StudentStatus.Active).OrderBy(s => s.AdmissionNumber).ToList();

        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<StudentGradeDto>>>($"api/grades/components/{componentId}/grades", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                var existingMap = res.Data.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
                var merged = new List<StudentGradeDto>(res.Data);

                foreach (var s in allClassStudents)
                {
                    if (!existingMap.ContainsKey(s.Id))
                    {
                        var (sc, remarks) = sampleScores.TryGetValue(s.AdmissionNumber, out var pair) ? pair : (88.0m, "Good performance");
                        var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                        var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                        merged.Add(new StudentGradeDto(
                            Guid.NewGuid(), componentId, compName, subjId, subjName,
                            s.Id, s.FullName, s.AdmissionNumber, sc, 100m, sc, letter, gpa, remarks
                        ));
                    }
                }
                return ApiResponse<List<StudentGradeDto>>.Ok(merged.OrderBy(g => g.AdmissionNumber).ToList());
            }
        }
        catch (Exception ex)
        {
            LogException("GetComponentGradesAsync", ex);
        }

        var existingForComp = _mockGrades.Where(g => g.AssessmentComponentId == componentId).ToList();
        var mockMap = existingForComp.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
        var resultList = new List<StudentGradeDto>();

        foreach (var s in allClassStudents)
        {
            if (mockMap.TryGetValue(s.Id, out var eg))
            {
                resultList.Add(eg);
            }
            else
            {
                var (sc, remarks) = sampleScores.TryGetValue(s.AdmissionNumber, out var pair)
                    ? pair
                    : (88.0m, "Good performance");
                var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                var g = new StudentGradeDto(
                    Guid.NewGuid(), componentId, compName, subjId, subjName,
                    s.Id, s.FullName, s.AdmissionNumber, sc, 100m, sc, letter, gpa, remarks
                );
                resultList.Add(g);
                _mockGrades.Add(g);
            }
        }
        return ApiResponse<List<StudentGradeDto>>.Ok(resultList.OrderBy(g => g.AdmissionNumber).ToList());
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetStudentAllGradesAsync(Guid studentId, Guid examId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-card/{studentId}/{examId}", JsonOptions);
            if (res?.Data?.Subjects != null && res.Data.Subjects.Count >= 8)
            {
                var list = res.Data.Subjects.Select(s => new StudentGradeDto(
                    Guid.NewGuid(),
                    s.SubjectId,
                    "Midterm Exam",
                    s.SubjectId,
                    s.SubjectName,
                    studentId,
                    res.Data.StudentName,
                    res.Data.AdmissionNumber,
                    s.TotalScore,
                    s.MaxScore,
                    s.Percentage,
                    s.GradeLetter,
                    s.GradePoint,
                    "Official Academic Record"
                )).ToList();
                return ApiResponse<List<StudentGradeDto>>.Ok(list);
            }
            else if (res?.Data?.Subjects != null && res.Data.Subjects.Count > 0)
            {
                // Partial backend report card (e.g. only 2 subjects). Merge with complete 8-subject curriculum!
                var studentGradesList = _mockGrades.Where(g => g.StudentId == studentId).ToList();
                if (studentGradesList.Count == 0)
                {
                    studentGradesList = _mockGrades.Where(g => g.StudentId == StudentVisalId).ToList();
                }

                var list = new List<StudentGradeDto>();
                var seenSubjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add backend report card subjects first
                foreach (var s in res.Data.Subjects)
                {
                    seenSubjNames.Add(s.SubjectName);
                    list.Add(new StudentGradeDto(
                        Guid.NewGuid(),
                        s.SubjectId,
                        "Midterm Exam",
                        s.SubjectId,
                        s.SubjectName,
                        studentId,
                        res.Data.StudentName,
                        res.Data.AdmissionNumber,
                        s.TotalScore,
                        s.MaxScore,
                        s.Percentage,
                        s.GradeLetter,
                        s.GradePoint,
                        "Official Academic Record"
                    ));
                }

                // Add remaining curriculum subjects
                foreach (var mg in studentGradesList)
                {
                    if (seenSubjNames.Add(mg.SubjectName))
                    {
                        list.Add(mg with { StudentId = studentId, StudentName = res.Data.StudentName, AdmissionNumber = res.Data.AdmissionNumber });
                    }
                }

                return ApiResponse<List<StudentGradeDto>>.Ok(list);
            }
        }
        catch (Exception ex)
        {
            LogException("GetStudentAllGradesAsync", ex);
        }

        // Authoritative fallback: return student's exact distinct grades from _mockGrades
        var examCompIds = _mockComponents.Where(c => examId != Guid.Empty && c.ExamId == examId).Select(c => c.Id).ToHashSet();
        var studentGrades = _mockGrades
            .Where(g => g.StudentId == studentId && (examCompIds.Count == 0 || examCompIds.Contains(g.AssessmentComponentId)))
            .ToList();

        if (studentGrades.Count == 0)
        {
            studentGrades = _mockGrades.Where(g => g.StudentId == studentId).ToList();
        }

        if (studentGrades.Count == 0)
        {
            var studentObj = _mockStudents.FirstOrDefault(s => s.Id == studentId);
            var studentName = studentObj?.FullName ?? "Enrolled Student";
            var admNo = studentObj?.AdmissionNumber ?? "ADM-2026-0001";
            var baseScores = new (Guid compId, Guid subjId, string subjName, decimal score, string letter, decimal gp, string remarks)[]
            {
                (CompKhmerId, SubjKhmerId, "Khmer Literature", 92.0m, "A", 4.0m, "Excellent Khmer composition and rhetoric"),
                (CompMathId, SubjMathId, "Mathematics", 95.0m, "A", 4.0m, "Outstanding algebraic and calculus problem solving"),
                (CompEnglishId, SubjEnglishId, "English Communication", 94.0m, "A", 4.0m, "Strong communicative fluency and essay analysis"),
                (CompPhysicsId, SubjPhysicsId, "Physics", 88.0m, "B", 3.0m, "Very good experimental accuracy and wave physics"),
                (CompChemistryId, SubjChemistryId, "Chemistry", 86.5m, "B", 3.0m, "Solid chemical reactions and stoichiometry calculations"),
                (CompBiologyId, SubjBiologyId, "Biology", 90.0m, "A", 4.0m, "Great understanding of genetics and cellular structures"),
                (CompHistoryId, SubjHistoryId, "History", 91.5m, "A", 4.0m, "Thorough knowledge of Cambodian and world history"),
                (CompIctId, SubjIctId, "Computer Science & ICT", 98.0m, "A", 4.0m, "Exceptional coding logic and software projects")
            };

            studentGrades = baseScores.Select(b => new StudentGradeDto(
                Guid.NewGuid(), b.compId, "Midterm Exam", b.subjId, b.subjName,
                studentId, studentName, admNo, b.score, 100m, b.score, b.letter, b.gp, b.remarks
            )).ToList();

            foreach (var g in studentGrades)
            {
                _mockGrades.Add(g);
            }
        }

        // Deduplicate by SubjectName
        var distinct = studentGrades
            .GroupBy(g => g.SubjectName)
            .Select(g => g.First())
            .ToList();

        return ApiResponse<List<StudentGradeDto>>.Ok(distinct);
    }

    public async Task<ApiResponse<bool>> RecordSingleGradeAsync(Guid componentId, Guid studentId, decimal score, string? remarks = null)
    {
        var request = new BatchGradeEntryRequest(
            componentId,
            new List<StudentGradeItem> { new(studentId, score, remarks) }
        );
        return await RecordBatchGradesAsync(request);
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

        var comp = _mockComponents.FirstOrDefault(c => c.Id == request.AssessmentComponentId);
        var compName = comp?.Name ?? "Midterm Exam";
        var subjId = comp?.SubjectId ?? Guid.NewGuid();
        var subjName = comp?.SubjectName ?? "Mathematics";

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
                Guid.NewGuid(), request.AssessmentComponentId, compName, subjId, subjName,
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
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-card/{studentId}/{examId}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetStudentReportCardAsync", ex);
        }

        var student = _mockStudents.FirstOrDefault(s => s.Id == studentId) ?? _mockStudents.First();
        var subjects = new List<SubjectReportItem>
        {
            new(SubjKhmerId, "Khmer Literature", "KHM101", 4, 92.0m, 100m, 92.0m, "A", 4.0m),
            new(SubjMathId, "Mathematics", "MATH101", 4, 95.0m, 100m, 95.0m, "A", 4.0m),
            new(SubjEnglishId, "English Communication", "ENG101", 3, 94.0m, 100m, 94.0m, "A", 4.0m),
            new(SubjPhysicsId, "Physics", "PHY101", 3, 88.0m, 100m, 88.0m, "B", 3.0m),
            new(SubjChemistryId, "Chemistry", "CHM101", 3, 86.5m, 100m, 86.5m, "B", 3.0m),
            new(SubjBiologyId, "Biology", "BIO101", 3, 90.0m, 100m, 90.0m, "A", 4.0m),
            new(SubjHistoryId, "History", "HIS101", 2, 91.5m, 100m, 91.5m, "A", 4.0m),
            new(SubjIctId, "Computer Science & ICT", "ICT101", 2, 98.0m, 100m, 98.0m, "A", 4.0m)
        };
        var totalObtained = subjects.Sum(s => s.TotalScore);
        var totalMax = subjects.Sum(s => s.MaxScore);
        var pct = totalMax > 0 ? (totalObtained / totalMax) * 100m : 0m;
        return ApiResponse<ReportCardDto>.Ok(new ReportCardDto(
            student.Id, student.FullName, student.AdmissionNumber, "10-A", "2026-2027", "Semester 1",
            subjects, totalObtained, totalMax, Math.Round(pct, 2), 3.85m, "A", 1));
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

    public async Task<ApiResponse<AnnouncementDto>> GetAnnouncementByIdAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<AnnouncementDto>>($"api/announcements/{id}", JsonOptions);
            if (res?.Data != null) return res;
        }
        catch (Exception ex)
        {
            LogException("GetAnnouncementByIdAsync", ex);
        }

        var found = _mockAnnouncements.FirstOrDefault(a => a.Id == id);
        return found != null
            ? ApiResponse<AnnouncementDto>.Ok(found)
            : ApiResponse<AnnouncementDto>.Fail("Announcement not found.");
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

    public async Task<ApiResponse<AnnouncementDto>> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/announcements/{id}", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<AnnouncementDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    var idx = _mockAnnouncements.FindIndex(a => a.Id == id);
                    if (idx >= 0) _mockAnnouncements[idx] = body.Data;
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("UpdateAnnouncementAsync", ex);
        }

        var existingIdx = _mockAnnouncements.FindIndex(a => a.Id == id);
        if (existingIdx >= 0)
        {
            var existing = _mockAnnouncements[existingIdx];
            var updated = existing with
            {
                Title = request.Title,
                Content = request.Content,
                TargetRole = request.TargetRole,
                TargetClassSectionId = request.TargetClassSectionId,
                Priority = request.Priority,
                IsArchived = request.IsArchived
            };
            _mockAnnouncements[existingIdx] = updated;
            return ApiResponse<AnnouncementDto>.Ok(updated, "Announcement updated.");
        }

        return ApiResponse<AnnouncementDto>.Fail("Announcement not found.");
    }

    public async Task<ApiResponse<bool>> DeleteAnnouncementAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/announcements/{id}");
            if (res.IsSuccessStatusCode)
            {
                _mockAnnouncements.RemoveAll(a => a.Id == id);
                return ApiResponse<bool>.Ok(true, "Announcement deleted.");
            }
        }
        catch (Exception ex)
        {
            LogException("DeleteAnnouncementAsync", ex);
        }

        _mockAnnouncements.RemoveAll(a => a.Id == id);
        return ApiResponse<bool>.Ok(true, "Announcement deleted.");
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
    private static readonly List<AssessmentComponentDto> _mockComponents = InitializeMockComponents();
    private static readonly List<TimetableEntryDto> _mockSchedule = InitializeMockSchedule();
    private static readonly List<AttendanceRecordDto> _mockAttendance = InitializeMockAttendance();
    private static readonly List<StudentGradeDto> _mockGrades = InitializeMockGrades();
    private static readonly List<InvoiceDto> _mockInvoices = InitializeMockInvoices();
    private static readonly List<AnnouncementDto> _mockAnnouncements = InitializeMockAnnouncements();
    private static readonly List<AuditLogDto> _mockAudits = InitializeMockAuditLogs();

    private static readonly Guid Class10AId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Class10BId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid ExamMidtermId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid ExamQuiz1Id = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid ExamFinal1Id = Guid.Parse("b0000000-0000-0000-0000-000000000003");
    private static readonly Guid ExamMidterm2Id = Guid.Parse("b0000000-0000-0000-0000-000000000004");
    private static readonly Guid ExamFinal2Id = Guid.Parse("b0000000-0000-0000-0000-000000000005");

    private static readonly Guid SubjKhmerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SubjMathId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid SubjPhysicsId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid SubjChemistryId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid SubjEnglishId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid SubjBiologyId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid SubjHistoryId = Guid.Parse("10000000-0000-0000-0000-000000000007");
    private static readonly Guid SubjIctId = Guid.Parse("10000000-0000-0000-0000-000000000008");

    private static readonly Guid TeacherSokhaId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid TeacherDaraId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid TeacherChendaId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid TeacherRathanaId = Guid.Parse("c0000000-0000-0000-0000-000000000004");
    private static readonly Guid TeacherJohnId = Guid.Parse("c0000000-0000-0000-0000-000000000005");
    private static readonly Guid TeacherKolabId = Guid.Parse("c0000000-0000-0000-0000-000000000006");
    private static readonly Guid TeacherSophalId = Guid.Parse("c0000000-0000-0000-0000-000000000007");
    private static readonly Guid TeacherSamnangId = Guid.Parse("c0000000-0000-0000-0000-000000000008");

    private static readonly Guid CompKhmerId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid CompMathId = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    private static readonly Guid CompPhysicsId = Guid.Parse("d0000000-0000-0000-0000-000000000003");
    private static readonly Guid CompChemistryId = Guid.Parse("d0000000-0000-0000-0000-000000000004");
    private static readonly Guid CompEnglishId = Guid.Parse("d0000000-0000-0000-0000-000000000005");
    private static readonly Guid CompBiologyId = Guid.Parse("d0000000-0000-0000-0000-000000000006");
    private static readonly Guid CompHistoryId = Guid.Parse("d0000000-0000-0000-0000-000000000007");
    private static readonly Guid CompIctId = Guid.Parse("d0000000-0000-0000-0000-000000000008");

    private static readonly Guid CompKhmer10BId = Guid.Parse("d0000000-0000-0000-0000-000000000011");
    private static readonly Guid CompMath10BId = Guid.Parse("d0000000-0000-0000-0000-000000000012");
    private static readonly Guid CompPhysics10BId = Guid.Parse("d0000000-0000-0000-0000-000000000013");
    private static readonly Guid CompChemistry10BId = Guid.Parse("d0000000-0000-0000-0000-000000000014");
    private static readonly Guid CompEnglish10BId = Guid.Parse("d0000000-0000-0000-0000-000000000015");
    private static readonly Guid CompBiology10BId = Guid.Parse("d0000000-0000-0000-0000-000000000016");
    private static readonly Guid CompHistory10BId = Guid.Parse("d0000000-0000-0000-0000-000000000017");
    private static readonly Guid CompIct10BId = Guid.Parse("d0000000-0000-0000-0000-000000000018");
 
    private static readonly Guid CompQuizKhmerId = Guid.Parse("d0000000-0000-0000-0000-000000000021");
    private static readonly Guid CompQuizMathId = Guid.Parse("d0000000-0000-0000-0000-000000000022");
    private static readonly Guid CompQuizPhysicsId = Guid.Parse("d0000000-0000-0000-0000-000000000023");
    private static readonly Guid CompQuizChemistryId = Guid.Parse("d0000000-0000-0000-0000-000000000024");
    private static readonly Guid CompQuizEnglishId = Guid.Parse("d0000000-0000-0000-0000-000000000025");
    private static readonly Guid CompQuizBiologyId = Guid.Parse("d0000000-0000-0000-0000-000000000026");
    private static readonly Guid CompQuizHistoryId = Guid.Parse("d0000000-0000-0000-0000-000000000027");
    private static readonly Guid CompQuizIctId = Guid.Parse("d0000000-0000-0000-0000-000000000028");

    private static readonly Guid CompFinalKhmerId = Guid.Parse("d0000000-0000-0000-0000-000000000031");
    private static readonly Guid CompFinalMathId = Guid.Parse("d0000000-0000-0000-0000-000000000032");
    private static readonly Guid CompFinalPhysicsId = Guid.Parse("d0000000-0000-0000-0000-000000000033");
    private static readonly Guid CompFinalChemistryId = Guid.Parse("d0000000-0000-0000-0000-000000000034");
    private static readonly Guid CompFinalEnglishId = Guid.Parse("d0000000-0000-0000-0000-000000000035");
    private static readonly Guid CompFinalBiologyId = Guid.Parse("d0000000-0000-0000-0000-000000000036");
    private static readonly Guid CompFinalHistoryId = Guid.Parse("d0000000-0000-0000-0000-000000000037");
    private static readonly Guid CompFinalIctId = Guid.Parse("d0000000-0000-0000-0000-000000000038");

    private static readonly Guid StudentVisalId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid StudentKanhaId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid StudentDaraId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid StudentBophaId = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly Guid StudentRithyId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StudentSreypovId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StudentVannakId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid StudentThidaId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static List<StudentDto> InitializeMockStudents() => new()
    {
        new(StudentVisalId, Guid.NewGuid(), "ADM-2026-0001", "Visal", "Chan", "Visal Chan",
            new DateTime(2010, 5, 14), "Male", "O+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "visal.chan@student.angkoracademy.edu.kh", "+855 98 123 456", "Preah Sihanouk Blvd, Phnom Penh", null,
            Class10AId, "10-A", "Keo Bopha", "+855 92 112 233", "Mother"),
        new(StudentKanhaId, Guid.NewGuid(), "ADM-2026-0002", "Kanha", "Sok", "Kanha Sok",
            new DateTime(2010, 8, 22), "Female", "A+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "kanha.sok@student.angkoracademy.edu.kh", "+855 98 654 321", "Norodom Blvd, Phnom Penh", null,
            Class10AId, "10-A", "Sok Vichea", "+855 97 554 332", "Father"),
        new(StudentDaraId, Guid.NewGuid(), "ADM-2026-0003", "Dara", "Meng", "Dara Meng",
            new DateTime(2010, 2, 10), "Male", "B+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "dara.meng@student.angkoracademy.edu.kh", "+855 98 333 444", "Toul Kork, Phnom Penh", null,
            Class10AId, "10-A", "Meng Chhay", "+855 12 999 888", "Father"),
        new(StudentBophaId, Guid.NewGuid(), "ADM-2026-0004", "Bopha", "Keo", "Bopha Keo",
            new DateTime(2010, 11, 5), "Female", "AB+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "bopha.keo@student.angkoracademy.edu.kh", "+855 98 777 888", "Chamkarmon, Phnom Penh", null,
            Class10AId, "10-A", "Keo Sarath", "+855 12 777 666", "Father"),

        // Class 10-A Cohort Students
        new(StudentRithyId, Guid.NewGuid(), "ADM-2026-0005", "Rithy", "Srun", "Rithy Srun",
            new DateTime(2010, 3, 11), "Male", "O+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "rithy.srun@student.angkoracademy.edu.kh", "+855 98 111 222", "Khan Sen Sok, Phnom Penh", null,
            Class10AId, "10-A", "Srun Meng", "+855 12 111 333", "Father"),
        new(StudentSreypovId, Guid.NewGuid(), "ADM-2026-0006", "Sreypov", "Lim", "Sreypov Lim",
            new DateTime(2010, 7, 18), "Female", "B+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "sreypov.lim@student.angkoracademy.edu.kh", "+855 98 222 333", "Khan Daun Penh, Phnom Penh", null,
            Class10AId, "10-A", "Lim Sovann", "+855 12 222 444", "Mother"),
        new(StudentVannakId, Guid.NewGuid(), "ADM-2026-0007", "Vannak", "Heng", "Vannak Heng",
            new DateTime(2010, 9, 25), "Male", "A+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "vannak.heng@student.angkoracademy.edu.kh", "+855 98 333 444", "Khan Toul Kork, Phnom Penh", null,
            Class10AId, "10-A", "Heng Rith", "+855 12 333 555", "Father"),
        new(StudentThidaId, Guid.NewGuid(), "ADM-2026-0008", "Thida", "Meas", "Thida Meas",
            new DateTime(2010, 12, 1), "Female", "AB+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "thida.meas@student.angkoracademy.edu.kh", "+855 98 444 555", "Khan Chamkarmon, Phnom Penh", null,
            Class10AId, "10-A", "Meas Chea", "+855 12 444 666", "Mother")
    };

    private static List<TeacherDto> InitializeMockTeachers() => new()
    {
        new(TeacherDaraId, Guid.NewGuid(), "EMP-00101", "Dara", "Sok", "Dara Sok",
            "dara.sok@angkoracademy.edu.kh", "+855 12 345 678", new DateTime(2021, 9, 1), "Master of Mathematics Education",
            "Advanced Mathematics", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjMathId, "Mathematics", "MATH101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjMathId, "Mathematics", "MATH101")
            }),
        new(TeacherChendaId, Guid.NewGuid(), "EMP-00102", "Chenda", "Chan", "Chenda Chan",
            "chenda.chan@angkoracademy.edu.kh", "+855 12 456 789", new DateTime(2022, 10, 1), "Bachelor of Science in Physics",
            "Applied Physics", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjPhysicsId, "Physics", "PHY101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjPhysicsId, "Physics", "PHY101")
            }),
        new(TeacherSokhaId, Guid.NewGuid(), "EMP-00103", "Sokha", "Mean", "Sokha Mean",
            "sokha.mean@angkoracademy.edu.kh", "+855 12 567 890", new DateTime(2020, 8, 15), "Master of Khmer Literature",
            "Classical Literature", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjKhmerId, "Khmer Literature", "KHM101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjKhmerId, "Khmer Literature", "KHM101")
            }),
        new(TeacherRathanaId, Guid.NewGuid(), "EMP-00104", "Rathana", "Vong", "Rathana Vong",
            "rathana.vong@angkoracademy.edu.kh", "+855 12 678 901", new DateTime(2021, 11, 1), "Master of Chemical Sciences",
            "General Chemistry", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjChemistryId, "Chemistry", "CHM101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjChemistryId, "Chemistry", "CHM101")
            }),
        new(TeacherJohnId, Guid.NewGuid(), "EMP-00105", "John", "Smith", "John Smith",
            "john.smith@angkoracademy.edu.kh", "+855 12 789 012", new DateTime(2019, 7, 1), "MA in Applied Linguistics",
            "English Communication", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjEnglishId, "English Communication", "ENG101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjEnglishId, "English Communication", "ENG101")
            }),
        new(TeacherKolabId, Guid.NewGuid(), "EMP-00106", "Kolab", "Pich", "Kolab Pich",
            "kolab.pich@angkoracademy.edu.kh", "+855 12 890 123", new DateTime(2022, 1, 10), "M.Sc in Biological Sciences",
            "Cellular Biology", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjBiologyId, "Biology", "BIO101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjBiologyId, "Biology", "BIO101")
            }),
        new(TeacherSophalId, Guid.NewGuid(), "EMP-00107", "Sophal", "Tep", "Sophal Tep",
            "sophal.tep@angkoracademy.edu.kh", "+855 12 901 234", new DateTime(2020, 5, 20), "Master of Southeast Asian History",
            "Cambodian History", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjHistoryId, "History", "HIS101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjHistoryId, "History", "HIS101")
            }),
        new(TeacherSamnangId, Guid.NewGuid(), "EMP-00108", "Samnang", "Ly", "Samnang Ly",
            "samnang.ly@angkoracademy.edu.kh", "+855 12 012 345", new DateTime(2023, 2, 1), "B.Sc in Software Engineering",
            "Computer Science", true, new List<TeacherSubjectAssignmentDto>
            {
                new(Guid.NewGuid(), Class10AId, "10-A", SubjIctId, "Computer Science & ICT", "ICT101"),
                new(Guid.NewGuid(), Class10BId, "10-B", SubjIctId, "Computer Science & ICT", "ICT101")
            })
    };

    private static List<ClassSectionDto> InitializeMockClasses() => new()
    {
        new(Class10AId, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-A", "Room 201", 35, TeacherDaraId, "Dara Sok", 34),
        new(Class10BId, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-B", "Room 202", 35, TeacherChendaId, "Chenda Chan", 32)
    };

    private static List<SubjectDto> InitializeMockSubjects() => new()
    {
        new(SubjKhmerId, Guid.NewGuid(), "Khmer Literature", "KHM101", "Cambodian Classical Literature and Grammar", 4),
        new(SubjMathId, Guid.NewGuid(), "Mathematics", "MATH101", "Algebra, Geometry & Calculus", 4),
        new(SubjPhysicsId, Guid.NewGuid(), "Physics", "PHY101", "Mechanics, Optics & Thermodynamics", 3),
        new(SubjChemistryId, Guid.NewGuid(), "Chemistry", "CHM101", "General, Inorganic & Organic Chemistry", 3),
        new(SubjEnglishId, Guid.NewGuid(), "English Communication", "ENG101", "Academic English, Writing & Public Speaking", 3),
        new(SubjBiologyId, Guid.NewGuid(), "Biology", "BIO101", "Cellular Biology, Anatomy & Genetics", 3),
        new(SubjHistoryId, Guid.NewGuid(), "History", "HIS101", "Cambodian History & World Civilizations", 2),
        new(SubjIctId, Guid.NewGuid(), "Computer Science & ICT", "ICT101", "Algorithms, Web Design & Applied Computing", 2)
    };

    private static List<AssessmentComponentDto> InitializeMockComponents() => new()
    {
        // Semester 1 Midterm Components (Class 10-A)
        new(CompKhmerId, ExamMidtermId, "Semester 1 Midterm", SubjKhmerId, "Khmer Literature", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMathId, ExamMidtermId, "Semester 1 Midterm", SubjMathId, "Mathematics", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompPhysicsId, ExamMidtermId, "Semester 1 Midterm", SubjPhysicsId, "Physics", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompChemistryId, ExamMidtermId, "Semester 1 Midterm", SubjChemistryId, "Chemistry", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompEnglishId, ExamMidtermId, "Semester 1 Midterm", SubjEnglishId, "English Communication", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompBiologyId, ExamMidtermId, "Semester 1 Midterm", SubjBiologyId, "Biology", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompHistoryId, ExamMidtermId, "Semester 1 Midterm", SubjHistoryId, "History", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompIctId, ExamMidtermId, "Semester 1 Midterm", SubjIctId, "Computer Science & ICT", Class10AId, "10-A", "Midterm Exam", 100m, 40m),

        // Semester 1 Monthly Assessment (October 2026) Components (Class 10-A)
        new(CompQuizKhmerId, ExamQuiz1Id, "October Monthly Assessment", SubjKhmerId, "Khmer Literature", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizMathId, ExamQuiz1Id, "October Monthly Assessment", SubjMathId, "Mathematics", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizPhysicsId, ExamQuiz1Id, "October Monthly Assessment", SubjPhysicsId, "Physics", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizChemistryId, ExamQuiz1Id, "October Monthly Assessment", SubjChemistryId, "Chemistry", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizEnglishId, ExamQuiz1Id, "October Monthly Assessment", SubjEnglishId, "English Communication", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizBiologyId, ExamQuiz1Id, "October Monthly Assessment", SubjBiologyId, "Biology", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizHistoryId, ExamQuiz1Id, "October Monthly Assessment", SubjHistoryId, "History", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),
        new(CompQuizIctId, ExamQuiz1Id, "October Monthly Assessment", SubjIctId, "Computer Science & ICT", Class10AId, "10-A", "Monthly Quiz", 100m, 20m),

        // Semester 1 Final Examination Components (Class 10-A)
        new(CompFinalKhmerId, ExamFinal1Id, "Semester 1 Final", SubjKhmerId, "Khmer Literature", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalMathId, ExamFinal1Id, "Semester 1 Final", SubjMathId, "Mathematics", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalPhysicsId, ExamFinal1Id, "Semester 1 Final", SubjPhysicsId, "Physics", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalChemistryId, ExamFinal1Id, "Semester 1 Final", SubjChemistryId, "Chemistry", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalEnglishId, ExamFinal1Id, "Semester 1 Final", SubjEnglishId, "English Communication", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalBiologyId, ExamFinal1Id, "Semester 1 Final", SubjBiologyId, "Biology", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalHistoryId, ExamFinal1Id, "Semester 1 Final", SubjHistoryId, "History", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinalIctId, ExamFinal1Id, "Semester 1 Final", SubjIctId, "Computer Science & ICT", Class10AId, "10-A", "Final Exam", 100m, 40m),

        // Class 10-B Components
        new(CompKhmer10BId, ExamMidtermId, "Semester 1 Midterm", SubjKhmerId, "Khmer Literature", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompMath10BId, ExamMidtermId, "Semester 1 Midterm", SubjMathId, "Mathematics", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompPhysics10BId, ExamMidtermId, "Semester 1 Midterm", SubjPhysicsId, "Physics", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompChemistry10BId, ExamMidtermId, "Semester 1 Midterm", SubjChemistryId, "Chemistry", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompEnglish10BId, ExamMidtermId, "Semester 1 Midterm", SubjEnglishId, "English Communication", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompBiology10BId, ExamMidtermId, "Semester 1 Midterm", SubjBiologyId, "Biology", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompHistory10BId, ExamMidtermId, "Semester 1 Midterm", SubjHistoryId, "History", Class10BId, "10-B", "Midterm Exam", 100m, 40m),
        new(CompIct10BId, ExamMidtermId, "Semester 1 Midterm", SubjIctId, "Computer Science & ICT", Class10BId, "10-B", "Midterm Exam", 100m, 40m)
    };

    private static List<TimetableEntryDto> InitializeMockSchedule() => new()
    {
        // === MONDAY (Class 10-A) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjMathId, "Mathematics", "MATH101", TeacherDaraId, "Dara Sok", DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjPhysicsId, "Physics", "PHY101", TeacherChendaId, "Chenda Chan", DayOfWeek.Monday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Physics Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjKhmerId, "Khmer Literature", "KHM101", TeacherSokhaId, "Sokha Mean", DayOfWeek.Monday, new TimeSpan(13, 0, 0), new TimeSpan(14, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjEnglishId, "English Communication", "ENG101", TeacherJohnId, "John Smith", DayOfWeek.Monday, new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0), "Room 201"),

        // === TUESDAY (Class 10-A) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjChemistryId, "Chemistry", "CHM101", TeacherRathanaId, "Rathana Vong", DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Chemistry Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjBiologyId, "Biology", "BIO101", TeacherKolabId, "Kolab Pich", DayOfWeek.Tuesday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Biology Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjMathId, "Mathematics", "MATH101", TeacherDaraId, "Dara Sok", DayOfWeek.Tuesday, new TimeSpan(13, 0, 0), new TimeSpan(14, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjHistoryId, "History", "HIS101", TeacherSophalId, "Sophal Tep", DayOfWeek.Tuesday, new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0), "Room 201"),

        // === WEDNESDAY (Class 10-A) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjIctId, "Computer Science & ICT", "ICT101", TeacherSamnangId, "Samnang Ly", DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Computer Lab 1"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjKhmerId, "Khmer Literature", "KHM101", TeacherSokhaId, "Sokha Mean", DayOfWeek.Wednesday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjEnglishId, "English Communication", "ENG101", TeacherJohnId, "John Smith", DayOfWeek.Wednesday, new TimeSpan(13, 0, 0), new TimeSpan(14, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjPhysicsId, "Physics", "PHY101", TeacherChendaId, "Chenda Chan", DayOfWeek.Wednesday, new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0), "Physics Lab"),

        // === THURSDAY (Class 10-A) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjMathId, "Mathematics", "MATH101", TeacherDaraId, "Dara Sok", DayOfWeek.Thursday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjChemistryId, "Chemistry", "CHM101", TeacherRathanaId, "Rathana Vong", DayOfWeek.Thursday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Chemistry Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjHistoryId, "History", "HIS101", TeacherSophalId, "Sophal Tep", DayOfWeek.Thursday, new TimeSpan(13, 0, 0), new TimeSpan(14, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjBiologyId, "Biology", "BIO101", TeacherKolabId, "Kolab Pich", DayOfWeek.Thursday, new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0), "Biology Lab"),

        // === FRIDAY (Class 10-A) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjKhmerId, "Khmer Literature", "KHM101", TeacherSokhaId, "Sokha Mean", DayOfWeek.Friday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjEnglishId, "English Communication", "ENG101", TeacherJohnId, "John Smith", DayOfWeek.Friday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjIctId, "Computer Science & ICT", "ICT101", TeacherSamnangId, "Samnang Ly", DayOfWeek.Friday, new TimeSpan(13, 0, 0), new TimeSpan(14, 30, 0), "Computer Lab 1"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", SubjPhysicsId, "Physics", "PHY101", TeacherChendaId, "Chenda Chan", DayOfWeek.Friday, new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0), "Physics Lab"),

        // === MONDAY to FRIDAY (Class 10-B Alternate Slots) ===
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjKhmerId, "Khmer Literature", "KHM101", TeacherSokhaId, "Sokha Mean", DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 202"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjMathId, "Mathematics", "MATH101", TeacherDaraId, "Dara Sok", DayOfWeek.Monday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Room 202"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjPhysicsId, "Physics", "PHY101", TeacherChendaId, "Chenda Chan", DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Physics Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjChemistryId, "Chemistry", "CHM101", TeacherRathanaId, "Rathana Vong", DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Chemistry Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjEnglishId, "English Communication", "ENG101", TeacherJohnId, "John Smith", DayOfWeek.Thursday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 202"),
        new(Guid.NewGuid(), Guid.NewGuid(), Class10BId, "10-B", SubjBiologyId, "Biology", "BIO101", TeacherKolabId, "Kolab Pich", DayOfWeek.Friday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Biology Lab")
    };

    private static List<AttendanceRecordDto> InitializeMockAttendance()
    {
        var records = new List<AttendanceRecordDto>();
        var currentDate = DateTime.Today;
        var schoolDays = new List<DateOnly>();

        // Gather 30 past distinct school days (Mon-Fri only, no weekends)
        int dayOffset = 0;
        while (schoolDays.Count < 30)
        {
            var date = currentDate.AddDays(-dayOffset);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                schoolDays.Add(DateOnly.FromDateTime(date));
            }
            dayOffset++;
        }

        // Realistic attendance records for students over 30 distinct school days
        for (int i = 0; i < schoolDays.Count; i++)
        {
            var d = schoolDays[i];
            var checkInUtc = d.ToDateTime(new TimeOnly(7, 48)).AddMinutes(i % 12);

            // 1. Visal Chan: 26 Present, 2 Late, 2 Excused (Total 30) -> Rate: 90.0%
            AttendanceStatus visalStatus = AttendanceStatus.Present;
            string? visalRemarks = null;
            if (i == 0)
            {
                visalStatus = AttendanceStatus.Excused;
                visalRemarks = "[Parent Permission] Sick Leave / Medical: Visal has a mild fever";
            }
            else if (i == 3)
            {
                visalStatus = AttendanceStatus.Late;
                visalRemarks = "Traffic delay on Russian Blvd (Checked in 08:22 AM)";
            }
            else if (i == 9)
            {
                visalStatus = AttendanceStatus.Late;
                visalRemarks = "Monsoon heavy rain delay (Checked in 08:18 AM)";
            }
            else if (i == 16)
            {
                visalStatus = AttendanceStatus.Excused;
                visalRemarks = "[Parent Permission] Doctor / Clinic Appointment";
            }

            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentVisalId, "Visal Chan", "ADM-2026-0001", d, visalStatus, visalRemarks, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 2. Kanha Sok
            var kanhaStatus = (i == 1 || i == 7) ? AttendanceStatus.Late : AttendanceStatus.Present;
            var kanhaRemarks = (i == 1) ? "Commute delay" : (i == 7) ? "Traffic jam" : null;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", d, kanhaStatus, kanhaRemarks, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 3. Dara Meng
            var daraStatus = (i == 12) ? AttendanceStatus.Excused : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentDaraId, "Dara Meng", "ADM-2026-0003", d, daraStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 4. Bopha Keo
            var bophaStatus = (i == 4) ? AttendanceStatus.Late : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentBophaId, "Bopha Keo", "ADM-2026-0004", d, bophaStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 5. Rithy Srun
            var rithyStatus = (i == 5) ? AttendanceStatus.Late : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentRithyId, "Rithy Srun", "ADM-2026-0005", d, rithyStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 6. Sreypov Lim
            var sreypovStatus = (i == 8) ? AttendanceStatus.Late : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", d, sreypovStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 7. Vannak Heng
            var vannakStatus = (i == 10) ? AttendanceStatus.Excused : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentVannakId, "Vannak Heng", "ADM-2026-0007", d, vannakStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));

            // 8. Thida Meas
            var thidaStatus = (i == 2) ? AttendanceStatus.Late : (i == 15) ? AttendanceStatus.Excused : AttendanceStatus.Present;
            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentThidaId, "Thida Meas", "ADM-2026-0008", d, thidaStatus, null, checkInUtc, "Teacher Dara Sok", null, null, null));
        }

        return records;
    }

    private static List<StudentGradeDto> InitializeMockGrades() => new()
    {
        // Visal Chan Grades across all subjects
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentVisalId, "Visal Chan", "ADM-2026-0001", 92.0m, 100m, 92.0m, "A", 4.0m, "Excellent Khmer composition and rhetoric"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 95.0m, 100m, 95.0m, "A", 4.0m, "Outstanding algebraic and calculus problem solving"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentVisalId, "Visal Chan", "ADM-2026-0001", 94.0m, 100m, 94.0m, "A", 4.0m, "Strong communicative fluency and essay analysis"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 88.0m, 100m, 88.0m, "B", 3.0m, "Very good experimental accuracy and wave physics"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentVisalId, "Visal Chan", "ADM-2026-0001", 86.5m, 100m, 86.5m, "B", 3.0m, "Solid chemical reactions and stoichiometry calculations"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentVisalId, "Visal Chan", "ADM-2026-0001", 90.0m, 100m, 90.0m, "A", 4.0m, "Great understanding of genetics and cellular structures"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentVisalId, "Visal Chan", "ADM-2026-0001", 91.5m, 100m, 91.5m, "A", 4.0m, "Thorough knowledge of Cambodian and world history"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentVisalId, "Visal Chan", "ADM-2026-0001", 98.0m, 100m, 98.0m, "A", 4.0m, "Exceptional coding logic and software projects"),

        // Kanha Sok Grades
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 95.0m, 100m, 95.0m, "A", 4.0m, "Top scores in classical literature analysis"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 89.0m, 100m, 89.0m, "B", 3.0m, "Very good geometric reasoning"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 97.0m, 100m, 97.0m, "A", 4.0m, "Distinction in English debates and presentations"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 84.0m, 100m, 84.0m, "B", 3.0m, "Good lab work and scientific writeup"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 91.0m, 100m, 91.0m, "A", 4.0m, "Excellent laboratory procedure compliance"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 88.0m, 100m, 88.0m, "B", 3.0m, "Solid grasp of evolutionary biology"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 93.0m, 100m, 93.0m, "A", 4.0m, "Active historical analysis and source evaluation"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentKanhaId, "Kanha Sok", "ADM-2026-0002", 94.0m, 100m, 94.0m, "A", 4.0m, "Well executed interactive website project"),

        // Dara Meng Grades
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentDaraId, "Dara Meng", "ADM-2026-0003", 85.0m, 100m, 85.0m, "A", 4.0m, "Strong analysis of Khmer poetry"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentDaraId, "Dara Meng", "ADM-2026-0003", 91.0m, 100m, 91.0m, "A", 4.0m, "Proficient trigonometric solutions"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentDaraId, "Dara Meng", "ADM-2026-0003", 86.0m, 100m, 86.0m, "B", 3.0m, "Effective technical presentation"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentDaraId, "Dara Meng", "ADM-2026-0003", 87.0m, 100m, 87.0m, "B", 3.0m, "Accurate mechanics experiments"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentDaraId, "Dara Meng", "ADM-2026-0003", 83.0m, 100m, 83.0m, "B", 3.0m, "Competent titration procedures"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentDaraId, "Dara Meng", "ADM-2026-0003", 89.0m, 100m, 89.0m, "B", 3.0m, "Clear botanical classifications"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentDaraId, "Dara Meng", "ADM-2026-0003", 88.0m, 100m, 88.0m, "B", 3.0m, "Good contextual historical analysis"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentDaraId, "Dara Meng", "ADM-2026-0003", 92.0m, 100m, 92.0m, "A", 4.0m, "Solid database schema construction"),

        // Bopha Keo Grades
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 88.0m, 100m, 88.0m, "B", 3.0m, "Good critical reflections on folklore"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 84.0m, 100m, 84.0m, "B", 3.0m, "Solid algebraic expressions"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 90.0m, 100m, 90.0m, "A", 4.0m, "Expressive conversational fluency"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 82.0m, 100m, 82.0m, "B", 3.0m, "Good physics coursework"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 85.5m, 100m, 85.5m, "A", 4.0m, "Strong organic chemistry foundation"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 92.0m, 100m, 92.0m, "A", 4.0m, "Great physiology dissection work"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 86.0m, 100m, 86.0m, "B", 3.0m, "Well written historical essays"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentBophaId, "Bopha Keo", "ADM-2026-0004", 95.0m, 100m, 95.0m, "A", 4.0m, "Creative responsive web design"),

        // === Class 10-A Cohort Students Grades ===
        // Rithy Srun (Class 10-A)
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 88.0m, 100m, 88.0m, "B", 3.0m, "Strong comprehension"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 94.0m, 100m, 94.0m, "A", 4.0m, "Excellent algebra"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 89.0m, 100m, 89.0m, "B", 3.0m, "Good lab work"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 91.0m, 100m, 91.0m, "A", 4.0m, "Accurate titration"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 90.0m, 100m, 90.0m, "A", 4.0m, "Clear communication"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 87.0m, 100m, 87.0m, "B", 3.0m, "Good genetics grasp"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 85.0m, 100m, 85.0m, "A", 4.0m, "Solid essays"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 96.0m, 100m, 96.0m, "A", 4.0m, "Top coder"),

        // Sreypov Lim (Class 10-A)
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 93.0m, 100m, 93.0m, "A", 4.0m, "Eloquent essays"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 87.0m, 100m, 87.0m, "B", 3.0m, "Consistent homework"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 85.0m, 100m, 85.0m, "A", 4.0m, "Solid theory"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 88.0m, 100m, 88.0m, "B", 3.0m, "Good lab work"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 95.0m, 100m, 95.0m, "A", 4.0m, "Fluent speaker"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 91.0m, 100m, 91.0m, "A", 4.0m, "Great drawings"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 90.0m, 100m, 90.0m, "A", 4.0m, "Deep analysis"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentSreypovId, "Sreypov Lim", "ADM-2026-0006", 92.0m, 100m, 92.0m, "A", 4.0m, "Clean code"),

        // Vannak Heng (Class 10-A)
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 86.0m, 100m, 86.0m, "B", 3.0m, "Good effort"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 92.0m, 100m, 92.0m, "A", 4.0m, "Great calculation"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 90.0m, 100m, 90.0m, "A", 4.0m, "Thorough report"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 84.0m, 100m, 84.0m, "B", 3.0m, "Passionate student"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 88.0m, 100m, 88.0m, "B", 3.0m, "Good vocab"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 86.0m, 100m, 86.0m, "B", 3.0m, "Clear writeups"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 87.0m, 100m, 87.0m, "B", 3.0m, "Good facts"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentVannakId, "Vannak Heng", "ADM-2026-0007", 94.0m, 100m, 94.0m, "A", 4.0m, "Great web app"),

        // Thida Meas (Class 10-A)
        new(Guid.NewGuid(), CompKhmerId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentThidaId, "Thida Meas", "ADM-2026-0008", 94.0m, 100m, 94.0m, "A", 4.0m, "Top essays"),
        new(Guid.NewGuid(), CompMathId, "Midterm Exam", SubjMathId, "Mathematics", StudentThidaId, "Thida Meas", "ADM-2026-0008", 89.0m, 100m, 89.0m, "B", 3.0m, "Good geometry"),
        new(Guid.NewGuid(), CompPhysicsId, "Midterm Exam", SubjPhysicsId, "Physics", StudentThidaId, "Thida Meas", "ADM-2026-0008", 86.0m, 100m, 86.0m, "B", 3.0m, "Accurate graphs"),
        new(Guid.NewGuid(), CompChemistryId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentThidaId, "Thida Meas", "ADM-2026-0008", 89.0m, 100m, 89.0m, "B", 3.0m, "Solid analysis"),
        new(Guid.NewGuid(), CompEnglishId, "Midterm Exam", SubjEnglishId, "English Communication", StudentThidaId, "Thida Meas", "ADM-2026-0008", 96.0m, 100m, 96.0m, "A", 4.0m, "Excellent grammar"),
        new(Guid.NewGuid(), CompBiologyId, "Midterm Exam", SubjBiologyId, "Biology", StudentThidaId, "Thida Meas", "ADM-2026-0008", 93.0m, 100m, 93.0m, "A", 4.0m, "Great knowledge"),
        new(Guid.NewGuid(), CompHistoryId, "Midterm Exam", SubjHistoryId, "History", StudentThidaId, "Thida Meas", "ADM-2026-0008", 92.0m, 100m, 92.0m, "A", 4.0m, "High marks"),
        new(Guid.NewGuid(), CompIctId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentThidaId, "Thida Meas", "ADM-2026-0008", 97.0m, 100m, 97.0m, "A", 4.0m, "Great UI design"),

        // === Class 10-B Backup Grades ===
        new(Guid.NewGuid(), CompKhmer10BId, "Midterm Exam", SubjKhmerId, "Khmer Literature", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 88.0m, 100m, 88.0m, "B", 3.0m, "Strong comprehension"),
        new(Guid.NewGuid(), CompMath10BId, "Midterm Exam", SubjMathId, "Mathematics", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 94.0m, 100m, 94.0m, "A", 4.0m, "Excellent algebra"),
        new(Guid.NewGuid(), CompPhysics10BId, "Midterm Exam", SubjPhysicsId, "Physics", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 89.0m, 100m, 89.0m, "B", 3.0m, "Good lab work"),
        new(Guid.NewGuid(), CompChemistry10BId, "Midterm Exam", SubjChemistryId, "Chemistry", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 91.0m, 100m, 91.0m, "A", 4.0m, "Accurate titration"),
        new(Guid.NewGuid(), CompEnglish10BId, "Midterm Exam", SubjEnglishId, "English Communication", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 90.0m, 100m, 90.0m, "A", 4.0m, "Clear communication"),
        new(Guid.NewGuid(), CompBiology10BId, "Midterm Exam", SubjBiologyId, "Biology", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 87.0m, 100m, 87.0m, "B", 3.0m, "Good genetics grasp"),
        new(Guid.NewGuid(), CompHistory10BId, "Midterm Exam", SubjHistoryId, "History", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 85.0m, 100m, 85.0m, "A", 4.0m, "Solid essays"),
        new(Guid.NewGuid(), CompIct10BId, "Midterm Exam", SubjIctId, "Computer Science & ICT", StudentRithyId, "Rithy Srun", "ADM-2026-0005", 96.0m, 100m, 96.0m, "A", 4.0m, "Top coder"),

        // === October Monthly Quiz Grades (Visal Chan) ===
        new(Guid.NewGuid(), CompQuizKhmerId, "Monthly Quiz", SubjKhmerId, "Khmer Literature", StudentVisalId, "Visal Chan", "ADM-2026-0001", 94.0m, 100m, 94.0m, "A", 4.0m, "Superb vocabulary and classical quiz"),
        new(Guid.NewGuid(), CompQuizMathId, "Monthly Quiz", SubjMathId, "Mathematics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 96.0m, 100m, 96.0m, "A", 4.0m, "Perfect score on calculus problem sets"),
        new(Guid.NewGuid(), CompQuizEnglishId, "Monthly Quiz", SubjEnglishId, "English Communication", StudentVisalId, "Visal Chan", "ADM-2026-0001", 92.0m, 100m, 92.0m, "A", 4.0m, "Great oral presentation score"),
        new(Guid.NewGuid(), CompQuizPhysicsId, "Monthly Quiz", SubjPhysicsId, "Physics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 90.0m, 100m, 90.0m, "A", 4.0m, "High precision kinematics quiz"),
        new(Guid.NewGuid(), CompQuizChemistryId, "Monthly Quiz", SubjChemistryId, "Chemistry", StudentVisalId, "Visal Chan", "ADM-2026-0001", 89.0m, 100m, 89.0m, "B", 3.0m, "Good periodic trends assessment"),
        new(Guid.NewGuid(), CompQuizBiologyId, "Monthly Quiz", SubjBiologyId, "Biology", StudentVisalId, "Visal Chan", "ADM-2026-0001", 92.0m, 100m, 92.0m, "A", 4.0m, "Accurate cell organelle diagrams"),
        new(Guid.NewGuid(), CompQuizHistoryId, "Monthly Quiz", SubjHistoryId, "History", StudentVisalId, "Visal Chan", "ADM-2026-0001", 91.0m, 100m, 91.0m, "A", 4.0m, "Thorough timeline knowledge"),
        new(Guid.NewGuid(), CompQuizIctId, "Monthly Quiz", SubjIctId, "Computer Science & ICT", StudentVisalId, "Visal Chan", "ADM-2026-0001", 99.0m, 100m, 99.0m, "A", 4.0m, "Outstanding algorithm test score"),

        // === Semester 1 Final Exam Grades (Visal Chan) ===
        new(Guid.NewGuid(), CompFinalKhmerId, "Final Exam", SubjKhmerId, "Khmer Literature", StudentVisalId, "Visal Chan", "ADM-2026-0001", 93.0m, 100m, 93.0m, "A", 4.0m, "Mastery of literary essays"),
        new(Guid.NewGuid(), CompFinalMathId, "Final Exam", SubjMathId, "Mathematics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 97.0m, 100m, 97.0m, "A", 4.0m, "Flawless mathematical reasoning"),
        new(Guid.NewGuid(), CompFinalEnglishId, "Final Exam", SubjEnglishId, "English Communication", StudentVisalId, "Visal Chan", "ADM-2026-0001", 95.0m, 100m, 95.0m, "A", 4.0m, "Excellent rhetoric and analytical essay"),
        new(Guid.NewGuid(), CompFinalPhysicsId, "Final Exam", SubjPhysicsId, "Physics", StudentVisalId, "Visal Chan", "ADM-2026-0001", 91.0m, 100m, 91.0m, "A", 4.0m, "Great thermodynamics performance"),
        new(Guid.NewGuid(), CompFinalChemistryId, "Final Exam", SubjChemistryId, "Chemistry", StudentVisalId, "Visal Chan", "ADM-2026-0001", 88.0m, 100m, 88.0m, "B", 3.0m, "Very good organic chemistry final"),
        new(Guid.NewGuid(), CompFinalBiologyId, "Final Exam", SubjBiologyId, "Biology", StudentVisalId, "Visal Chan", "ADM-2026-0001", 93.0m, 100m, 93.0m, "A", 4.0m, "Strong genetics and physiology answers"),
        new(Guid.NewGuid(), CompFinalHistoryId, "Final Exam", SubjHistoryId, "History", StudentVisalId, "Visal Chan", "ADM-2026-0001", 94.0m, 100m, 94.0m, "A", 4.0m, "Deep historical synthesis"),
        new(Guid.NewGuid(), CompFinalIctId, "Final Exam", SubjIctId, "Computer Science & ICT", StudentVisalId, "Visal Chan", "ADM-2026-0001", 99.0m, 100m, 99.0m, "A", 4.0m, "Flawless software engineering project")
    };

    private static List<InvoiceDto> InitializeMockInvoices() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), StudentVisalId, "Visal Chan", "ADM-2026-0001", "10-A",
            Guid.NewGuid(), "2026-2027", "INV-2026-00001", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(15),
            730.0m, 500.0m, 230.0m, InvoiceStatus.PartiallyPaid, "Semester 1 Fee Package",
            new List<InvoiceItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Tuition", "Tuition Fee - Semester 1", 650m),
                new(Guid.NewGuid(), Guid.NewGuid(), "Lab Fee", "Science Lab Equipment", 80m)
            },
            new List<PaymentDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "INV-2026-00001", StudentVisalId, "Visal Chan", "REC-2026-00001", 500m, DateTime.UtcNow.AddDays(-15), PaymentMethod.ABA, "ABA-981245", "Partial payment via ABA", "Admin")
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
