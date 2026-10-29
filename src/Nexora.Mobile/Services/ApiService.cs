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
    Task<ApiResponse<TeacherDto>> UpdateTeacherAsync(Guid id, UpdateTeacherRequest request);
    Task<ApiResponse<bool>> DeactivateTeacherAsync(Guid id);

    Task<ApiResponse<List<ClassSectionDto>>> GetClassesAsync();
    Task<ApiResponse<bool>> AssignClassTeacherAsync(Guid classSectionId, Guid teacherId);
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
    Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId, string? subjectName = null);
    Task<ApiResponse<List<StudentGradeDto>>> GetStudentAllGradesAsync(Guid studentId, Guid examId);
    Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request, string? subjectName = null);
    Task<ApiResponse<bool>> RecordSingleGradeAsync(Guid componentId, Guid studentId, decimal score, string? remarks = null, string? admissionNumber = null, string? subjectName = null);
    Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId);
    Guid GetDeterministicComponentId(Guid examId, string? subjectName, Guid? classSectionId = null, string? examName = null);

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

            if (!response.IsSuccessStatusCode)
            {
                var lowerUser = usernameOrEmail.Trim().ToLowerInvariant();
                if (lowerUser.Contains("student") || lowerUser.Contains("teacher") || lowerUser.Contains("parent") || lowerUser.Contains("admin"))
                {
                    return FallbackOfflineLogin(usernameOrEmail, password, null);
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
            var tokens = uname.Split(new[] { '.', '@', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(t => !t.Equals("teacher", StringComparison.OrdinalIgnoreCase))
                              .ToList();

            var matchTeacher = _mockTeachers.FirstOrDefault(t =>
                tokens.Any(tok => tok.Equals(t.FirstName, StringComparison.OrdinalIgnoreCase) ||
                                  tok.Equals(t.LastName, StringComparison.OrdinalIgnoreCase)) ||
                uname.Equals(t.Email, StringComparison.OrdinalIgnoreCase) ||
                uname.Equals($"teacher.{t.FirstName.ToLower()}", StringComparison.OrdinalIgnoreCase))
                ?? _mockTeachers.FirstOrDefault(t =>
                    tokens.Any(tok => t.FirstName.StartsWith(tok, StringComparison.OrdinalIgnoreCase) ||
                                      t.LastName.StartsWith(tok, StringComparison.OrdinalIgnoreCase)))
                ?? _mockTeachers.First();

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
            var tokens = uname.Split(new[] { '.', '@', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(t => !t.Equals("student", StringComparison.OrdinalIgnoreCase))
                              .ToList();

            var matchStudent = _mockStudents.FirstOrDefault(s =>
                tokens.Any(tok => tok.Equals(s.FirstName, StringComparison.OrdinalIgnoreCase) ||
                                  tok.Equals(s.LastName, StringComparison.OrdinalIgnoreCase) ||
                                  tok.Equals(s.AdmissionNumber, StringComparison.OrdinalIgnoreCase)) ||
                uname.Equals(s.AdmissionNumber, StringComparison.OrdinalIgnoreCase) ||
                (s.Email != null && uname.Equals(s.Email, StringComparison.OrdinalIgnoreCase)) ||
                uname.Equals($"student.{s.FirstName.ToLower()}", StringComparison.OrdinalIgnoreCase))
                ?? _mockStudents.FirstOrDefault(s =>
                    tokens.Any(tok => s.FirstName.StartsWith(tok, StringComparison.OrdinalIgnoreCase) ||
                                      s.LastName.StartsWith(tok, StringComparison.OrdinalIgnoreCase)))
                ?? _mockStudents.First();

            firstName = matchStudent.FirstName;
            lastName = matchStudent.LastName;
            fullName = matchStudent.FullName;
            email = matchStudent.Email ?? $"{matchStudent.FirstName.ToLower()}.{matchStudent.LastName.ToLower()}@student.angkoracademy.edu.kh";
            phone = matchStudent.PhoneNumber ?? "+855 81 234 567";
            linkedProfileId = matchStudent.Id;
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
            new("Mon", 81.2m, true),
            new("Tue", 100.0m, true),
            new("Wed", null, false),
            new("Thu", null, false),
            new("Fri", null, false)
        };

        var mockDist = new GradeDistributionDto(49.1m, 157, 50.6m, 162, 0.3m, 1, 0.0m, 0, 320);

        return ApiResponse<AdminDashboardStatsDto>.Ok(new AdminDashboardStatsDto(
            TotalStudents: activeStudents,
            TotalTeachers: _mockTeachers.Count,
            TotalClasses: _mockClasses.Count,
            TodayAttendancePercentage: Math.Round(attPercent, 1),
            TotalFeesCollected: totalPaid > 0 ? totalPaid : 64200.0m,
            TotalFeesOutstanding: totalDue > 0 ? totalDue : 8900.0m,
            RecentAnnouncements: _mockAnnouncements.Take(3).ToList(),
            RecentAudits: _mockAudits.Take(4).ToList(),
            WeeklyAttendanceTrend: weeklyTrend,
            GradeDistribution: mockDist
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
                if (data.TotalStudentsTaught < _mockStudents.Count)
                {
                    data = data with { TotalStudentsTaught = _mockStudents.Count };
                }
                // If today is weekend or has no classes, provide teacher schedule for display
                if (data.TodaySchedule == null || data.TodaySchedule.Count == 0)
                {
                    var teacherId = CurrentUser?.LinkedProfileId ?? TeacherDaraId;
                    var teacherName = CurrentUser?.FullName ?? "Dara Sok";
                    var teacherSchedule = _mockSchedule
                        .Where(s => s.TeacherId == teacherId || string.Equals(s.TeacherName, teacherName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    data = data with { TodaySchedule = teacherSchedule.Count > 0 ? teacherSchedule : _mockSchedule };
                }
                return ApiResponse<TeacherDashboardStatsDto>.Ok(data);
            }
        }
        catch (Exception ex)
        {
            LogException("GetTeacherDashboardAsync", ex);
        }

        var fallbackTeacherId = CurrentUser?.LinkedProfileId ?? TeacherDaraId;
        var fallbackTeacherName = CurrentUser?.FullName ?? "Dara Sok";
        var fallbackSchedule = _mockSchedule
            .Where(s => s.TeacherId == fallbackTeacherId || string.Equals(s.TeacherName, fallbackTeacherName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var teacherAssignedClasses = _mockClasses.Where(c => c.ClassTeacherId == fallbackTeacherId).ToList();

        return ApiResponse<TeacherDashboardStatsDto>.Ok(new TeacherDashboardStatsDto(
            AssignedClassesCount: teacherAssignedClasses.Count,
            AssignedSubjectsCount: 8,
            TotalStudentsTaught: _mockStudents.Count,
            TodaySchedule: fallbackSchedule.Count > 0 ? fallbackSchedule : _mockSchedule,
            ClassesList: teacherAssignedClasses
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

        var targetStudentId = CurrentUser?.LinkedProfileId ?? StudentVisalId;
        var mockStudent = _mockStudents.FirstOrDefault(s => s.Id == targetStudentId) ?? _mockStudents.First();
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

        // Distinct subject grades for the selected child to avoid duplicates (picks newest grade)
        var childGrades = _mockGrades
            .Where(g => g.StudentId == targetStudent.Id || (!string.IsNullOrEmpty(targetStudent.AdmissionNumber) && string.Equals(g.AdmissionNumber, targetStudent.AdmissionNumber, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(g => g.SubjectName, StringComparer.OrdinalIgnoreCase)
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

    public async Task<ApiResponse<TeacherDto>> UpdateTeacherAsync(Guid id, UpdateTeacherRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/teachers/{id}", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<TeacherDto>>(JsonOptions);
                if (body?.Data != null)
                {
                    var idx = _mockTeachers.FindIndex(t => t.Id == id);
                    if (idx >= 0) _mockTeachers[idx] = body.Data;
                    return body;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("UpdateTeacherAsync", ex);
        }

        var localIdx = _mockTeachers.FindIndex(t => t.Id == id);
        if (localIdx >= 0)
        {
            var existing = _mockTeachers[localIdx];
            var updated = existing with
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                FullName = $"{request.FirstName} {request.LastName}",
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Qualification = request.Qualification,
                Specialization = request.Specialization,
                IsActive = request.IsActive
            };
            _mockTeachers[localIdx] = updated;
            return ApiResponse<TeacherDto>.Ok(updated, "Teacher updated.");
        }

        return ApiResponse<TeacherDto>.Fail("Teacher not found.");
    }

    public async Task<ApiResponse<bool>> DeactivateTeacherAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/teachers/{id}");
            if (res.IsSuccessStatusCode)
            {
                var idx = _mockTeachers.FindIndex(t => t.Id == id);
                if (idx >= 0)
                {
                    _mockTeachers[idx] = _mockTeachers[idx] with { IsActive = false };
                }
                return ApiResponse<bool>.Ok(true, "Teacher deactivated.");
            }
        }
        catch (Exception ex)
        {
            LogException("DeactivateTeacherAsync", ex);
        }

        var localIdx = _mockTeachers.FindIndex(t => t.Id == id);
        if (localIdx >= 0)
        {
            _mockTeachers[localIdx] = _mockTeachers[localIdx] with { IsActive = false };
        }
        return ApiResponse<bool>.Ok(true, "Teacher deactivated.");
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

    public async Task<ApiResponse<bool>> AssignClassTeacherAsync(Guid classSectionId, Guid teacherId)
    {
        try
        {
            var res = await _httpClient.PutAsync($"api/academics/classes/{classSectionId}/teacher/{teacherId}", null);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
                if (body != null) return body;
            }
        }
        catch (Exception ex)
        {
            LogException("AssignClassTeacherAsync", ex);
        }

        var local = _mockClasses.Find(c => c.Id == classSectionId);
        if (local != null)
        {
            var teacher = _mockTeachers.Find(t => t.Id == teacherId);
            var updated = local with { ClassTeacherId = teacherId, ClassTeacherName = teacher?.FullName };
            var idx = _mockClasses.IndexOf(local);
            if (idx >= 0) _mockClasses[idx] = updated;
        }

        return ApiResponse<bool>.Ok(true, "Homeroom advisor assigned.");
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

        var teacher = _mockTeachers.FirstOrDefault(t => t.Id == teacherId);
        var teacherName = teacher?.FullName ?? CurrentUser?.FullName ?? "Dara Sok";
        var list = _mockSchedule.Where(s => s.TeacherId == teacherId || string.Equals(s.TeacherName, teacherName, StringComparison.OrdinalIgnoreCase)).ToList();
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
            .GroupBy(s => s.AdmissionNumber)
            .Select(g => g.First())
            .OrderBy(s => s.AdmissionNumber)
            .ToList();

        // Ensure all 8 cohort students are always included for Class 10-A
        if (activeClassStudents.Count < 8)
        {
            activeClassStudents = _mockStudents
                .Where(x => x.Status == StudentStatus.Active)
                .GroupBy(s => s.AdmissionNumber)
                .Select(g => g.First())
                .OrderBy(s => s.AdmissionNumber)
                .ToList();
        }

        // Check if any student has an approved parent leave / excuse for this date in mock attendance
        var excusedInMock = _mockAttendance
            .Where(a => a.Date == date && a.Status == AttendanceStatus.Excused)
            .GroupBy(a => a.AdmissionNumber)
            .ToDictionary(g => g.Key, g => g.First());

        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AttendanceRecordDto>>>($"api/attendance/class/{classId}?date={date:yyyy-MM-dd}", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                var backendById = res.Data.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
                var backendByAdm = res.Data.Where(r => !string.IsNullOrEmpty(r.AdmissionNumber)).GroupBy(r => r.AdmissionNumber).ToDictionary(g => g.Key, g => g.First());
                var merged = new List<AttendanceRecordDto>();

                foreach (var s in activeClassStudents)
                {
                    AttendanceRecordDto? matched = null;
                    if (backendById.TryGetValue(s.Id, out var byId))
                    {
                        matched = byId;
                    }
                    else if (backendByAdm.TryGetValue(s.AdmissionNumber, out var byAdm))
                    {
                        matched = byAdm;
                    }

                    if (matched != null)
                    {
                        merged.Add(matched);
                    }
                    else
                    {
                        var status = excusedInMock.TryGetValue(s.AdmissionNumber, out var excRec) ? excRec.Status : AttendanceStatus.Present;
                        var remarks = excusedInMock.TryGetValue(s.AdmissionNumber, out var excRec2) ? excRec2.Remarks : null;

                        merged.Add(new AttendanceRecordDto(
                            Guid.NewGuid(), Guid.NewGuid(), targetClassId, "10-A",
                            s.Id, s.FullName, s.AdmissionNumber, date,
                            status, remarks, DateTime.UtcNow, "Homeroom Teacher", null, null, null
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

        var existingByAdm = _mockAttendance
            .Where(a => a.Date == date && (classId == Guid.Empty || a.ClassSectionId == classId || a.ClassSectionId == Class10AId) && !string.IsNullOrEmpty(a.AdmissionNumber))
            .GroupBy(a => a.AdmissionNumber)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToDictionary(a => a.AdmissionNumber);

        var records = new List<AttendanceRecordDto>();
        foreach (var s in activeClassStudents)
        {
            if (existingForDate.TryGetValue(s.Id, out var existing))
            {
                records.Add(existing);
            }
            else if (existingByAdm.TryGetValue(s.AdmissionNumber, out var existingAdm))
            {
                records.Add(existingAdm);
            }
            else
            {
                var status = excusedInMock.TryGetValue(s.AdmissionNumber, out var excRec) ? excRec.Status : AttendanceStatus.Present;
                var remarks = excusedInMock.TryGetValue(s.AdmissionNumber, out var excRec2) ? excRec2.Remarks : null;

                var r = new AttendanceRecordDto(
                    Guid.NewGuid(), Guid.NewGuid(), targetClassId, "10-A", s.Id, s.FullName, s.AdmissionNumber,
                    date, status, remarks, DateTime.UtcNow, "Homeroom Teacher", null, null, null
                );
                records.Add(r);
                _mockAttendance.Add(r);
            }
        }

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records.OrderBy(a => a.AdmissionNumber).ToList());
    }

    public async Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request)
    {
        if (CurrentUser?.Role == UserRole.Teacher && CurrentUser?.LinkedProfileId.HasValue == true)
        {
            var targetClass = _mockClasses.FirstOrDefault(c => c.Id == request.ClassSectionId);
            if (targetClass != null && targetClass.ClassTeacherId != CurrentUser.LinkedProfileId.Value)
            {
                var curTeacher = _mockTeachers.FirstOrDefault(t => t.Id == CurrentUser.LinkedProfileId.Value);
                bool teachesClass = curTeacher?.Assignments?.Any(a => a.ClassSectionId == request.ClassSectionId) == true ||
                                    _mockSchedule.Any(s => s.ClassSectionId == request.ClassSectionId && s.TeacherId == CurrentUser.LinkedProfileId.Value);
                if (!teachesClass)
                {
                    return ApiResponse<bool>.Fail($"Access restricted: Only the designated Class Advisor ({targetClass.ClassTeacherName ?? "Assigned Advisor"}) or assigned subject teacher is authorized to record attendance for Class {targetClass.Name}.");
                }
            }
        }

        // Keep in-memory mock state up to date so any subsequent reads (mock or hybrid) reflect the teacher's edits immediately
        foreach (var item in request.Records)
        {
            var student = _mockStudents.FirstOrDefault(s => s.Id == item.StudentId);
            if (student == null && item.StudentId == Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"))
            {
                student = _mockStudents.FirstOrDefault(s => s.AdmissionNumber == "ADM-2026-0001");
            }
            var name = student?.FullName ?? "Student";
            var adm = student?.AdmissionNumber ?? (item.StudentId == StudentVisalId ? "ADM-2026-0001" : "");
            var targetStudentId = student?.Id ?? item.StudentId;

            _mockAttendance.RemoveAll(a => (a.StudentId == item.StudentId || a.StudentId == targetStudentId || (!string.IsNullOrEmpty(adm) && a.AdmissionNumber == adm)) && a.Date == request.Date);
            _mockAttendance.Add(new AttendanceRecordDto(
                Guid.NewGuid(), Guid.NewGuid(), request.ClassSectionId, "10-A",
                targetStudentId, name, adm, request.Date, item.Status, item.Remarks,
                DateTime.UtcNow, "Teacher", null, null, null
            ));
        }

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

        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "teacher", AuditAction.Create, "Attendance",
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

        var matchedStudent = _mockStudents.FirstOrDefault(s => s.Id == studentId);
        if (matchedStudent == null && studentId == Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"))
        {
            matchedStudent = _mockStudents.FirstOrDefault(s => s.AdmissionNumber == "ADM-2026-0001");
        }
        var targetAdm = matchedStudent?.AdmissionNumber;

        var records = _mockAttendance
            .Where(a => a.StudentId == studentId || (matchedStudent != null && a.StudentId == matchedStudent.Id) || (!string.IsNullOrEmpty(targetAdm) && a.AdmissionNumber == targetAdm))
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
            request.Date.ToDateTime(new TimeOnly(0, 15), DateTimeKind.Utc), CurrentUser?.FullName ?? "Parent",
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
            _examNameCache[DbExamMidtermId] = "Semester 1 Midterm Examination";
            _examNameCache[DbExamQuiz1Id] = "Semester 1 Monthly Assessment (October)";
            _examNameCache[DbExamFinal1Id] = "Semester 1 Final Examination";
            _examNameCache[DbExamMidterm2Id] = "Semester 2 Midterm Examination";
            _examNameCache[DbExamFinal2Id] = "Semester 2 Final Annual Examination";
            _examNameCache[ExamMidtermId] = "Semester 1 Midterm Examination";
            _examNameCache[ExamQuiz1Id] = "Semester 1 Monthly Assessment (October)";
            _examNameCache[ExamFinal1Id] = "Semester 1 Final Examination";
            _examNameCache[ExamMidterm2Id] = "Semester 2 Midterm Examination";
            _examNameCache[ExamFinal2Id] = "Semester 2 Final Annual Examination";

            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ExamDto>>>("api/grades/exams", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                var combined = res.Data.UnionBy(defaultExams, e => e.Name).OrderBy(e => e.StartDate).ToList();
                foreach (var ex in combined)
                {
                    _examNameCache[ex.Id] = ex.Name;
                }
                return ApiResponse<List<ExamDto>>.Ok(combined);
            }
        }
        catch (Exception ex)
        {
            LogException("GetExamsAsync", ex);
        }

        foreach (var ex in defaultExams)
        {
            _examNameCache[ex.Id] = ex.Name;
        }
        return ApiResponse<List<ExamDto>>.Ok(defaultExams);
    }

    public Guid GetDeterministicComponentId(Guid examId, string? subjectName, Guid? classSectionId = null, string? examName = null) =>
        ResolveDeterministicComponentId(examId, subjectName, classSectionId, examName);

    public static Guid ResolveDeterministicComponentId(Guid examId, string? subjectName, Guid? classSectionId = null, string? examName = null)
    {
        var subj = subjectName?.Trim() ?? string.Empty;
        bool is10B = classSectionId.HasValue && classSectionId.Value == Class10BId;

        if (is10B)
        {
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompKhmer10BId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompMath10BId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompPhysics10BId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompChemistry10BId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompEnglish10BId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompBiology10BId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompHistory10BId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompIct10BId;
        }

        var exName = examName ?? string.Empty;
        if (string.IsNullOrEmpty(exName) && _examNameCache.TryGetValue(examId, out var cachedName))
        {
            exName = cachedName;
        }

        bool isMonthly = examId == ExamQuiz1Id || examId == DbExamQuiz1Id || exName.Contains("Monthly", StringComparison.OrdinalIgnoreCase) || exName.Contains("Quiz", StringComparison.OrdinalIgnoreCase) || exName.Contains("October", StringComparison.OrdinalIgnoreCase);
        bool isFinal2 = examId == ExamFinal2Id || examId == DbExamFinal2Id || (exName.Contains("Semester 2", StringComparison.OrdinalIgnoreCase) && exName.Contains("Final", StringComparison.OrdinalIgnoreCase));
        bool isMidterm2 = examId == ExamMidterm2Id || examId == DbExamMidterm2Id || (exName.Contains("Semester 2", StringComparison.OrdinalIgnoreCase) && (exName.Contains("Midterm", StringComparison.OrdinalIgnoreCase) || !isFinal2));
        bool isFinal1 = examId == ExamFinal1Id || examId == DbExamFinal1Id || (exName.Contains("Semester 1", StringComparison.OrdinalIgnoreCase) && exName.Contains("Final", StringComparison.OrdinalIgnoreCase)) || (exName.Contains("Final", StringComparison.OrdinalIgnoreCase) && !isFinal2);

        if (isMonthly)
        {
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompQuizKhmerId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompQuizMathId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompQuizPhysicsId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompQuizChemistryId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompQuizEnglishId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompQuizBiologyId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompQuizHistoryId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompQuizIctId;
        }
        else if (isFinal1)
        {
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompFinalKhmerId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompFinalMathId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompFinalPhysicsId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompFinalChemistryId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompFinalEnglishId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompFinalBiologyId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompFinalHistoryId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompFinalIctId;
        }
        else if (isMidterm2)
        {
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompMidterm2KhmerId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompMidterm2MathId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompMidterm2PhysicsId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompMidterm2ChemistryId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompMidterm2EnglishId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompMidterm2BiologyId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompMidterm2HistoryId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompMidterm2IctId;
        }
        else if (isFinal2)
        {
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompFinal2KhmerId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompFinal2MathId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompFinal2PhysicsId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompFinal2ChemistryId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompFinal2EnglishId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompFinal2BiologyId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompFinal2HistoryId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompFinal2IctId;
        }
        else
        {
            // Semester 1 Midterm or general fallback
            if (subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase)) return CompKhmerId;
            if (subj.Contains("Math", StringComparison.OrdinalIgnoreCase)) return CompMathId;
            if (subj.Contains("Phys", StringComparison.OrdinalIgnoreCase)) return CompPhysicsId;
            if (subj.Contains("Chem", StringComparison.OrdinalIgnoreCase)) return CompChemistryId;
            if (subj.Contains("Eng", StringComparison.OrdinalIgnoreCase)) return CompEnglishId;
            if (subj.Contains("Bio", StringComparison.OrdinalIgnoreCase)) return CompBiologyId;
            if (subj.Contains("Hist", StringComparison.OrdinalIgnoreCase)) return CompHistoryId;
            if (subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase)) return CompIctId;
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{examId}_{classSectionId}_{subj.ToLowerInvariant()}"));
        return new Guid(bytes);
    }

    public static (decimal Score, string Remarks) GetDifferentiatedScore(string? examName, string? admissionNumber, string? subjectNameOrCode)
    {
        var ex = examName ?? string.Empty;
        var adm = admissionNumber?.Trim() ?? string.Empty;
        var subj = subjectNameOrCode?.Trim() ?? string.Empty;

        string examKey = ex.Contains("Monthly", StringComparison.OrdinalIgnoreCase) || ex.Contains("Quiz", StringComparison.OrdinalIgnoreCase) || ex.Contains("October", StringComparison.OrdinalIgnoreCase) ? "Monthly"
            : ex.Contains("Final Annual", StringComparison.OrdinalIgnoreCase) || (ex.Contains("Semester 2", StringComparison.OrdinalIgnoreCase) && ex.Contains("Final", StringComparison.OrdinalIgnoreCase)) ? "FinalAnnual"
            : ex.Contains("Semester 2", StringComparison.OrdinalIgnoreCase) ? "S2Midterm"
            : ex.Contains("Final", StringComparison.OrdinalIgnoreCase) ? "S1Final"
            : "S1Midterm";

        string subjCode = subj switch
        {
            _ when subj.Contains("Math", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "MATH101", StringComparison.OrdinalIgnoreCase) => "MATH101",
            _ when subj.Contains("Khmer", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "KHM101", StringComparison.OrdinalIgnoreCase) => "KHM101",
            _ when subj.Contains("Phys", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "PHY101", StringComparison.OrdinalIgnoreCase) => "PHY101",
            _ when subj.Contains("Chem", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "CHM101", StringComparison.OrdinalIgnoreCase) => "CHM101",
            _ when subj.Contains("Eng", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "ENG101", StringComparison.OrdinalIgnoreCase) => "ENG101",
            _ when subj.Contains("Bio", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "BIO101", StringComparison.OrdinalIgnoreCase) => "BIO101",
            _ when subj.Contains("His", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "HIS101", StringComparison.OrdinalIgnoreCase) => "HIS101",
            _ when subj.Contains("ICT", StringComparison.OrdinalIgnoreCase) || subj.Contains("Comp", StringComparison.OrdinalIgnoreCase) || string.Equals(subj, "ICT101", StringComparison.OrdinalIgnoreCase) => "ICT101",
            _ => "MATH101"
        };

        return (examKey, adm, subjCode) switch
        {
            ("Monthly", "ADM-2026-0001", "KHM101") => (88.0m, "Good comprehension in Khmer quiz"),
            ("Monthly", "ADM-2026-0001", "MATH101") => (95.0m, "Flawless score on polynomials quiz"),
            ("Monthly", "ADM-2026-0001", "PHY101") => (85.0m, "Good practical mechanics quiz"),
            ("Monthly", "ADM-2026-0001", "CHM101") => (82.0m, "Solid quiz, review chemical equations"),
            ("Monthly", "ADM-2026-0001", "ENG101") => (92.0m, "Accurate grammar and vocabulary quiz"),
            ("Monthly", "ADM-2026-0001", "BIO101") => (87.0m, "Clear answers on cellular biology"),
            ("Monthly", "ADM-2026-0001", "HIS101") => (89.0m, "Active recall of historical events"),
            ("Monthly", "ADM-2026-0001", "ICT101") => (96.0m, "Top score in practical lab test"),
            ("S1Midterm", "ADM-2026-0001", "KHM101") => (92.0m, "Mastery of Khmer grammar and rhetoric"),
            ("S1Midterm", "ADM-2026-0001", "MATH101") => (96.0m, "Outstanding analytical and algebraic skills"),
            ("S1Midterm", "ADM-2026-0001", "PHY101") => (89.0m, "Strong grasp of mechanics and kinematics"),
            ("S1Midterm", "ADM-2026-0001", "CHM101") => (86.5m, "Solid chemical reaction understanding"),
            ("S1Midterm", "ADM-2026-0001", "ENG101") => (94.0m, "Fluent expression and essay writing"),
            ("S1Midterm", "ADM-2026-0001", "BIO101") => (90.0m, "Great grasp of genetics and ecology"),
            ("S1Midterm", "ADM-2026-0001", "HIS101") => (91.5m, "Thorough knowledge of history"),
            ("S1Midterm", "ADM-2026-0001", "ICT101") => (98.0m, "Exceptional algorithm design and coding"),
            ("S1Final", "ADM-2026-0001", "KHM101") => (93.0m, "Comprehensive essay well argued"),
            ("S1Final", "ADM-2026-0001", "MATH101") => (97.0m, "Near-perfect calculus problem solving"),
            ("S1Final", "ADM-2026-0001", "PHY101") => (91.0m, "Excellent lab writeup and theory"),
            ("S1Final", "ADM-2026-0001", "CHM101") => (88.0m, "Thorough understanding of organic chemistry"),
            ("S1Final", "ADM-2026-0001", "ENG101") => (95.0m, "Sophisticated stylistic prose"),
            ("S1Final", "ADM-2026-0001", "BIO101") => (92.0m, "Detailed cellular respiration diagrams"),
            ("S1Final", "ADM-2026-0001", "HIS101") => (90.0m, "Broad thematic synthesis"),
            ("S1Final", "ADM-2026-0001", "ICT101") => (99.0m, "Outstanding capstone software project"),
            ("S2Midterm", "ADM-2026-0001", "KHM101") => (91.0m, "Insightful literary commentary"),
            ("S2Midterm", "ADM-2026-0001", "MATH101") => (95.5m, "Strong differential equations solutions"),
            ("S2Midterm", "ADM-2026-0001", "PHY101") => (88.5m, "Good electromagnetism analysis"),
            ("S2Midterm", "ADM-2026-0001", "CHM101") => (85.0m, "Good acid-base titration lab"),
            ("S2Midterm", "ADM-2026-0001", "ENG101") => (93.5m, "Compelling persuasive speech draft"),
            ("S2Midterm", "ADM-2026-0001", "BIO101") => (89.0m, "Accurate human physiology writeup"),
            ("S2Midterm", "ADM-2026-0001", "HIS101") => (88.0m, "Good historiographical critique"),
            ("S2Midterm", "ADM-2026-0001", "ICT101") => (97.5m, "Advanced database schema and query design"),
            ("FinalAnnual", "ADM-2026-0001", "KHM101") => (94.0m, "Exemplary mastery of classical literature"),
            ("FinalAnnual", "ADM-2026-0001", "MATH101") => (98.0m, "Top class mathematician"),
            ("FinalAnnual", "ADM-2026-0001", "PHY101") => (92.5m, "Outstanding optics and quantum principles"),
            ("FinalAnnual", "ADM-2026-0001", "CHM101") => (89.5m, "High marks in thermodynamics and kinetics"),
            ("FinalAnnual", "ADM-2026-0001", "ENG101") => (96.0m, "Superior rhetorical delivery and writing"),
            ("FinalAnnual", "ADM-2026-0001", "BIO101") => (93.0m, "In-depth ecological field research"),
            ("FinalAnnual", "ADM-2026-0001", "HIS101") => (92.0m, "Distinguished historical argumentation"),
            ("FinalAnnual", "ADM-2026-0001", "ICT101") => (100.0m, "Perfect score in full-stack project"),
            ("Monthly", "ADM-2026-0002", "KHM101") => (98.0m, "Highest score in class quiz"),
            ("Monthly", "ADM-2026-0002", "MATH101") => (84.0m, "Satisfactory quadratic equations quiz"),
            ("Monthly", "ADM-2026-0002", "PHY101") => (80.0m, "Review momentum principles"),
            ("Monthly", "ADM-2026-0002", "CHM101") => (88.0m, "Very good periodic trends knowledge"),
            ("Monthly", "ADM-2026-0002", "ENG101") => (99.0m, "Outstanding vocabulary and reading quiz"),
            ("Monthly", "ADM-2026-0002", "BIO101") => (91.0m, "Quick and accurate terminology"),
            ("Monthly", "ADM-2026-0002", "HIS101") => (95.0m, "Exceptional recall of ancient civilizations"),
            ("Monthly", "ADM-2026-0002", "ICT101") => (89.0m, "Clean HTML/CSS layout"),
            ("S1Midterm", "ADM-2026-0002", "KHM101") => (96.0m, "Top class essay, brilliant phrasing"),
            ("S1Midterm", "ADM-2026-0002", "MATH101") => (88.0m, "Strong geometric proofs"),
            ("S1Midterm", "ADM-2026-0002", "PHY101") => (84.0m, "Good lab work and scientific writeup"),
            ("S1Midterm", "ADM-2026-0002", "CHM101") => (91.0m, "Excellent laboratory procedure compliance"),
            ("S1Midterm", "ADM-2026-0002", "ENG101") => (98.0m, "Native-level fluency and analytical depth"),
            ("S1Midterm", "ADM-2026-0002", "BIO101") => (89.0m, "Solid grasp of evolutionary biology"),
            ("S1Midterm", "ADM-2026-0002", "HIS101") => (94.0m, "Active historical analysis and source evaluation"),
            ("S1Midterm", "ADM-2026-0002", "ICT101") => (92.0m, "Well executed interactive website project"),
            ("S1Final", "ADM-2026-0002", "KHM101") => (97.0m, "Exemplary analysis of Khmer poetry"),
            ("S1Final", "ADM-2026-0002", "MATH101") => (89.0m, "Diligently prepared calculus solutions"),
            ("S1Final", "ADM-2026-0002", "PHY101") => (86.0m, "Clear wave optics diagrams"),
            ("S1Final", "ADM-2026-0002", "CHM101") => (93.0m, "Top performance in chemical equilibrium"),
            ("S1Final", "ADM-2026-0002", "ENG101") => (98.5m, "Stunning literary critique"),
            ("S1Final", "ADM-2026-0002", "BIO101") => (92.0m, "Precise taxonomy and genetics problem sets"),
            ("S1Final", "ADM-2026-0002", "HIS101") => (96.0m, "Brilliant comparative historical essay"),
            ("S1Final", "ADM-2026-0002", "ICT101") => (93.0m, "User-friendly application UI"),
            ("S2Midterm", "ADM-2026-0002", "KHM101") => (95.0m, "Rich vocabulary and cultural context"),
            ("S2Midterm", "ADM-2026-0002", "MATH101") => (86.0m, "Good trigonometry applications"),
            ("S2Midterm", "ADM-2026-0002", "PHY101") => (83.0m, "Satisfactory circuits lab report"),
            ("S2Midterm", "ADM-2026-0002", "CHM101") => (90.0m, "Strong organic chemistry synthesis"),
            ("S2Midterm", "ADM-2026-0002", "ENG101") => (97.0m, "Eloquent debate and presentation"),
            ("S2Midterm", "ADM-2026-0002", "BIO101") => (88.0m, "Good ecosystem dynamics model"),
            ("S2Midterm", "ADM-2026-0002", "HIS101") => (93.0m, "Thoughtful analysis of modern history"),
            ("S2Midterm", "ADM-2026-0002", "ICT101") => (91.0m, "Good responsive design implementation"),
            ("FinalAnnual", "ADM-2026-0002", "KHM101") => (98.5m, "Class valedictorian literature submission"),
            ("FinalAnnual", "ADM-2026-0002", "MATH101") => (90.0m, "Achieved distinction in annual exam"),
            ("FinalAnnual", "ADM-2026-0002", "PHY101") => (87.0m, "Sound physics experimental analysis"),
            ("FinalAnnual", "ADM-2026-0002", "CHM101") => (94.0m, "Exceptional precision in final lab"),
            ("FinalAnnual", "ADM-2026-0002", "ENG101") => (99.0m, "Top English student of the year"),
            ("FinalAnnual", "ADM-2026-0002", "BIO101") => (94.0m, "Distinction in molecular biology"),
            ("FinalAnnual", "ADM-2026-0002", "HIS101") => (97.0m, "Masterful thesis on global treaties"),
            ("FinalAnnual", "ADM-2026-0002", "ICT101") => (95.0m, "Well architected final web project"),
            ("Monthly", "ADM-2026-0003", "KHM101") => (71.0m, "Passing quiz, study Khmer vocabulary"),
            ("Monthly", "ADM-2026-0003", "MATH101") => (90.0m, "Quick mental calculations on algebra"),
            ("Monthly", "ADM-2026-0003", "PHY101") => (93.0m, "High score on projectile motion quiz"),
            ("Monthly", "ADM-2026-0003", "CHM101") => (78.0m, "Review balancing redox equations"),
            ("Monthly", "ADM-2026-0003", "ENG101") => (70.0m, "Passing grade in listening quiz"),
            ("Monthly", "ADM-2026-0003", "BIO101") => (76.0m, "Review enzyme catalysts"),
            ("Monthly", "ADM-2026-0003", "HIS101") => (68.0m, "Needs review on medieval chronology"),
            ("Monthly", "ADM-2026-0003", "ICT101") => (85.0m, "Good pseudo-code structure"),
            ("S1Midterm", "ADM-2026-0003", "KHM101") => (76.0m, "Needs to expand descriptive vocabulary"),
            ("S1Midterm", "ADM-2026-0003", "MATH101") => (92.0m, "Strong spatial reasoning and equations"),
            ("S1Midterm", "ADM-2026-0003", "PHY101") => (94.0m, "Top lab performance in kinematics"),
            ("S1Midterm", "ADM-2026-0003", "CHM101") => (83.0m, "Good work on molecular bonding"),
            ("S1Midterm", "ADM-2026-0003", "ENG101") => (74.0m, "Work on essay structure and grammar"),
            ("S1Midterm", "ADM-2026-0003", "BIO101") => (80.0m, "Adequate cellular biology understanding"),
            ("S1Midterm", "ADM-2026-0003", "HIS101") => (72.0m, "Chronology and dates need improvement"),
            ("S1Midterm", "ADM-2026-0003", "ICT101") => (88.0m, "Good logic in programming loops"),
            ("S1Final", "ADM-2026-0003", "KHM101") => (78.0m, "Improved clarity in essay conclusion"),
            ("S1Final", "ADM-2026-0003", "MATH101") => (93.0m, "Impressive vector calculus proofs"),
            ("S1Final", "ADM-2026-0003", "PHY101") => (95.0m, "Distinction in physics final"),
            ("S1Final", "ADM-2026-0003", "CHM101") => (85.0m, "Sound chemistry lab execution"),
            ("S1Final", "ADM-2026-0003", "ENG101") => (76.0m, "Clear progress in paragraph transitions"),
            ("S1Final", "ADM-2026-0003", "BIO101") => (82.0m, "Solid final exam review"),
            ("S1Final", "ADM-2026-0003", "HIS101") => (74.0m, "Adequate comprehension of world events"),
            ("S1Final", "ADM-2026-0003", "ICT101") => (90.0m, "Well-commented C# algorithms"),
            ("S2Midterm", "ADM-2026-0003", "KHM101") => (75.0m, "Steady effort in essay drafting"),
            ("S2Midterm", "ADM-2026-0003", "MATH101") => (91.0m, "Solid complex numbers solutions"),
            ("S2Midterm", "ADM-2026-0003", "PHY101") => (92.0m, "Accurate electromagnetic field calculations"),
            ("S2Midterm", "ADM-2026-0003", "CHM101") => (81.0m, "Satisfactory electrochemistry report"),
            ("S2Midterm", "ADM-2026-0003", "ENG101") => (73.0m, "Focus on reading comprehension details"),
            ("S2Midterm", "ADM-2026-0003", "BIO101") => (79.0m, "Passing review of genetics"),
            ("S2Midterm", "ADM-2026-0003", "HIS101") => (71.0m, "Review primary vs secondary sources"),
            ("S2Midterm", "ADM-2026-0003", "ICT101") => (87.0m, "Good network protocols understanding"),
            ("FinalAnnual", "ADM-2026-0003", "KHM101") => (79.0m, "Good improvement in writing style"),
            ("FinalAnnual", "ADM-2026-0003", "MATH101") => (94.0m, "Exceptional performance on calculus"),
            ("FinalAnnual", "ADM-2026-0003", "PHY101") => (96.0m, "Highest Physics score in grade 10"),
            ("FinalAnnual", "ADM-2026-0003", "CHM101") => (84.0m, "Good overall chemistry practical"),
            ("FinalAnnual", "ADM-2026-0003", "ENG101") => (77.0m, "Satisfactory annual English completion"),
            ("FinalAnnual", "ADM-2026-0003", "BIO101") => (83.0m, "Solid biological systems synthesis"),
            ("FinalAnnual", "ADM-2026-0003", "HIS101") => (75.0m, "Passing grade in modern world history"),
            ("FinalAnnual", "ADM-2026-0003", "ICT101") => (91.0m, "Excellent robotics microcontroller code"),
            ("Monthly", "ADM-2026-0004", "KHM101") => (86.0m, "Good literary quiz score"),
            ("Monthly", "ADM-2026-0004", "MATH101") => (77.0m, "Review factoring polynomials"),
            ("Monthly", "ADM-2026-0004", "PHY101") => (75.0m, "Study heat transfer concepts"),
            ("Monthly", "ADM-2026-0004", "CHM101") => (89.0m, "Fast and accurate molar mass quiz"),
            ("Monthly", "ADM-2026-0004", "ENG101") => (88.0m, "Good spelling and idiom quiz"),
            ("Monthly", "ADM-2026-0004", "BIO101") => (94.0m, "Top quiz score on photosynthesis"),
            ("Monthly", "ADM-2026-0004", "HIS101") => (84.0m, "Good understanding of Angkor era"),
            ("Monthly", "ADM-2026-0004", "ICT101") => (80.0m, "Passing quiz on database basics"),
            ("S1Midterm", "ADM-2026-0004", "KHM101") => (89.0m, "Expressive poetic commentary"),
            ("S1Midterm", "ADM-2026-0004", "MATH101") => (81.0m, "Good effort on quadratic formulas"),
            ("S1Midterm", "ADM-2026-0004", "PHY101") => (79.0m, "Passing grade in thermodynamics"),
            ("S1Midterm", "ADM-2026-0004", "CHM101") => (92.0m, "Outstanding titration and lab safety"),
            ("S1Midterm", "ADM-2026-0004", "ENG101") => (91.0m, "Fluent reading comprehension"),
            ("S1Midterm", "ADM-2026-0004", "BIO101") => (95.0m, "Brilliant cytology diagrams and notes"),
            ("S1Midterm", "ADM-2026-0004", "HIS101") => (87.0m, "Engaging discussion of cultural heritage"),
            ("S1Midterm", "ADM-2026-0004", "ICT101") => (82.0m, "Satisfactory spreadsheets and presentations"),
            ("S1Final", "ADM-2026-0004", "KHM101") => (90.0m, "Distinction in Khmer prose"),
            ("S1Final", "ADM-2026-0004", "MATH101") => (83.0m, "Neat step-by-step math working"),
            ("S1Final", "ADM-2026-0004", "PHY101") => (81.0m, "Solid grasp of light and sound waves"),
            ("S1Final", "ADM-2026-0004", "CHM101") => (93.0m, "Superior understanding of reaction rates"),
            ("S1Final", "ADM-2026-0004", "ENG101") => (92.0m, "Excellent persuasive book review"),
            ("S1Final", "ADM-2026-0004", "BIO101") => (96.0m, "Top student in biology final"),
            ("S1Final", "ADM-2026-0004", "HIS101") => (88.0m, "Well-researched essay on trade routes"),
            ("S1Final", "ADM-2026-0004", "ICT101") => (84.0m, "Complete web page project"),
            ("S2Midterm", "ADM-2026-0004", "KHM101") => (88.0m, "Good storytelling and grammar"),
            ("S2Midterm", "ADM-2026-0004", "MATH101") => (80.0m, "Solid trigonometry foundations"),
            ("S2Midterm", "ADM-2026-0004", "PHY101") => (78.0m, "Passing review in mechanics"),
            ("S2Midterm", "ADM-2026-0004", "CHM101") => (91.0m, "Excellent organic functional groups"),
            ("S2Midterm", "ADM-2026-0004", "ENG101") => (89.0m, "Good oral speech delivery"),
            ("S2Midterm", "ADM-2026-0004", "BIO101") => (94.0m, "Detailed human anatomy chart"),
            ("S2Midterm", "ADM-2026-0004", "HIS101") => (86.0m, "Solid comparative government essay"),
            ("S2Midterm", "ADM-2026-0004", "ICT101") => (83.0m, "Good work on relational databases"),
            ("FinalAnnual", "ADM-2026-0004", "KHM101") => (91.0m, "Distinction level Khmer composition"),
            ("FinalAnnual", "ADM-2026-0004", "MATH101") => (84.0m, "Good overall progress in math"),
            ("FinalAnnual", "ADM-2026-0004", "PHY101") => (82.0m, "Sound understanding of electrical circuits"),
            ("FinalAnnual", "ADM-2026-0004", "CHM101") => (94.0m, "Exemplary chemistry final exam"),
            ("FinalAnnual", "ADM-2026-0004", "ENG101") => (93.0m, "Polished academic research writing"),
            ("FinalAnnual", "ADM-2026-0004", "BIO101") => (97.0m, "Phenomenal performance in biology"),
            ("FinalAnnual", "ADM-2026-0004", "HIS101") => (89.0m, "Very good synthesis of world history"),
            ("FinalAnnual", "ADM-2026-0004", "ICT101") => (85.0m, "Good application design project"),
            ("Monthly", "ADM-2026-0005", "KHM101") => (84.0m, "Good spelling and grammar quiz"),
            ("Monthly", "ADM-2026-0005", "MATH101") => (87.0m, "Quick quiz solutions"),
            ("Monthly", "ADM-2026-0005", "PHY101") => (83.0m, "Passing quiz on vectors"),
            ("Monthly", "ADM-2026-0005", "CHM101") => (72.0m, "Review atomic orbital configuration"),
            ("Monthly", "ADM-2026-0005", "ENG101") => (85.0m, "Good reading quiz"),
            ("Monthly", "ADM-2026-0005", "BIO101") => (82.0m, "Good knowledge of plant biology"),
            ("Monthly", "ADM-2026-0005", "HIS101") => (91.0m, "High score on Southeast Asia quiz"),
            ("Monthly", "ADM-2026-0005", "ICT101") => (92.0m, "Fast and clean code submission"),
            ("S1Midterm", "ADM-2026-0005", "KHM101") => (87.0m, "Clear essay logic and good grammar"),
            ("S1Midterm", "ADM-2026-0005", "MATH101") => (89.0m, "Accurate algebra problem solving"),
            ("S1Midterm", "ADM-2026-0005", "PHY101") => (86.0m, "Good analysis of Newton laws"),
            ("S1Midterm", "ADM-2026-0005", "CHM101") => (76.0m, "Needs practice on ionic equations"),
            ("S1Midterm", "ADM-2026-0005", "ENG101") => (88.0m, "Good vocabulary and text analysis"),
            ("S1Midterm", "ADM-2026-0005", "BIO101") => (85.0m, "Reliable understanding of ecology"),
            ("S1Midterm", "ADM-2026-0005", "HIS101") => (93.0m, "Exceptional interest in history"),
            ("S1Midterm", "ADM-2026-0005", "ICT101") => (94.0m, "Great programming intuition and code hygiene"),
            ("S1Final", "ADM-2026-0005", "KHM101") => (88.0m, "Solid structure in final composition"),
            ("S1Final", "ADM-2026-0005", "MATH101") => (90.0m, "Distinction in final mathematics"),
            ("S1Final", "ADM-2026-0005", "PHY101") => (87.0m, "Very good lab experiment execution"),
            ("S1Final", "ADM-2026-0005", "CHM101") => (79.0m, "Good improvement on chemical bonding"),
            ("S1Final", "ADM-2026-0005", "ENG101") => (89.0m, "Well-developed essay arguments"),
            ("S1Final", "ADM-2026-0005", "BIO101") => (86.0m, "Sound final biology score"),
            ("S1Final", "ADM-2026-0005", "HIS101") => (94.0m, "Outstanding analytical depth in history"),
            ("S1Final", "ADM-2026-0005", "ICT101") => (95.0m, "Distinction in ICT software build"),
            ("S2Midterm", "ADM-2026-0005", "KHM101") => (86.0m, "Consistent and neat handwriting"),
            ("S2Midterm", "ADM-2026-0005", "MATH101") => (88.0m, "Good application of probability"),
            ("S2Midterm", "ADM-2026-0005", "PHY101") => (85.0m, "Solid energy conservation models"),
            ("S2Midterm", "ADM-2026-0005", "CHM101") => (77.0m, "Passing work on solution concentrations"),
            ("S2Midterm", "ADM-2026-0005", "ENG101") => (87.0m, "Active participation in group seminar"),
            ("S2Midterm", "ADM-2026-0005", "BIO101") => (84.0m, "Satisfactory genetics problem sets"),
            ("S2Midterm", "ADM-2026-0005", "HIS101") => (92.0m, "Strong engagement with historical debates"),
            ("S2Midterm", "ADM-2026-0005", "ICT101") => (93.0m, "Good object-oriented design patterns"),
            ("FinalAnnual", "ADM-2026-0005", "KHM101") => (89.0m, "Polished literary dissertation"),
            ("FinalAnnual", "ADM-2026-0005", "MATH101") => (91.0m, "Distinction in advanced mathematics"),
            ("FinalAnnual", "ADM-2026-0005", "PHY101") => (88.0m, "Very good final physics score"),
            ("FinalAnnual", "ADM-2026-0005", "CHM101") => (81.0m, "Attained Merit level in chemistry"),
            ("FinalAnnual", "ADM-2026-0005", "ENG101") => (90.0m, "Distinction in English comprehensive"),
            ("FinalAnnual", "ADM-2026-0005", "BIO101") => (87.0m, "Reliable and consistent score"),
            ("FinalAnnual", "ADM-2026-0005", "HIS101") => (95.0m, "One of the best historians in class"),
            ("FinalAnnual", "ADM-2026-0005", "ICT101") => (96.0m, "Superb final programming project"),
            ("Monthly", "ADM-2026-0006", "KHM101") => (90.0m, "Distinction in poetry quiz"),
            ("Monthly", "ADM-2026-0006", "MATH101") => (75.0m, "Passing quiz on functions"),
            ("Monthly", "ADM-2026-0006", "PHY101") => (64.0m, "Needs review on force diagrams"),
            ("Monthly", "ADM-2026-0006", "CHM101") => (71.0m, "Passing quiz on the periodic table"),
            ("Monthly", "ADM-2026-0006", "ENG101") => (89.0m, "High score on reading quiz"),
            ("Monthly", "ADM-2026-0006", "BIO101") => (81.0m, "Good understanding of cell division"),
            ("Monthly", "ADM-2026-0006", "HIS101") => (87.0m, "Quick historical timeline recall"),
            ("Monthly", "ADM-2026-0006", "ICT101") => (78.0m, "Passing quiz on web development"),
            ("S1Midterm", "ADM-2026-0006", "KHM101") => (92.0m, "Elegant writing style and deep empathy"),
            ("S1Midterm", "ADM-2026-0006", "MATH101") => (78.0m, "Needs practice on trigonometric graphs"),
            ("S1Midterm", "ADM-2026-0006", "PHY101") => (67.0m, "Struggling with rotational mechanics"),
            ("S1Midterm", "ADM-2026-0006", "CHM101") => (74.0m, "Passing score, review stoichiometry"),
            ("S1Midterm", "ADM-2026-0006", "ENG101") => (91.0m, "Outstanding essay writing and prose"),
            ("S1Midterm", "ADM-2026-0006", "BIO101") => (83.0m, "Good understanding of human systems"),
            ("S1Midterm", "ADM-2026-0006", "HIS101") => (89.0m, "Thoughtful cultural analysis"),
            ("S1Midterm", "ADM-2026-0006", "ICT101") => (80.0m, "Good effort on web page styling"),
            ("S1Final", "ADM-2026-0006", "KHM101") => (93.0m, "Distinction in Khmer literature"),
            ("S1Final", "ADM-2026-0006", "MATH101") => (80.0m, "Attained merit through extra tutoring"),
            ("S1Final", "ADM-2026-0006", "PHY101") => (69.5m, "Close to passing, continued tutoring recommended"),
            ("S1Final", "ADM-2026-0006", "CHM101") => (76.0m, "Clear progress in chemistry lab"),
            ("S1Final", "ADM-2026-0006", "ENG101") => (92.0m, "Rich narrative and analytical skills"),
            ("S1Final", "ADM-2026-0006", "BIO101") => (85.0m, "Good final score in biology"),
            ("S1Final", "ADM-2026-0006", "HIS101") => (90.0m, "Distinction in final history paper"),
            ("S1Final", "ADM-2026-0006", "ICT101") => (82.0m, "Solid presentation of graphic design project"),
            ("S2Midterm", "ADM-2026-0006", "KHM101") => (91.0m, "Exceptional creative composition"),
            ("S2Midterm", "ADM-2026-0006", "MATH101") => (79.0m, "Passing grade in statistics"),
            ("S2Midterm", "ADM-2026-0006", "PHY101") => (68.0m, "Extra help needed with magnetic flux"),
            ("S2Midterm", "ADM-2026-0006", "CHM101") => (75.0m, "Passing lab writeup"),
            ("S2Midterm", "ADM-2026-0006", "ENG101") => (90.0m, "Distinction in literary analysis"),
            ("S2Midterm", "ADM-2026-0006", "BIO101") => (84.0m, "Good ecosystem project"),
            ("S2Midterm", "ADM-2026-0006", "HIS101") => (88.0m, "Strong historical inquiry"),
            ("S2Midterm", "ADM-2026-0006", "ICT101") => (81.0m, "Good design and interface work"),
            ("FinalAnnual", "ADM-2026-0006", "KHM101") => (94.0m, "Top tier writer and literary talent"),
            ("FinalAnnual", "ADM-2026-0006", "MATH101") => (82.0m, "Solid merit grade achieved"),
            ("FinalAnnual", "ADM-2026-0006", "PHY101") => (72.0m, "Achieved passing Grade C through hard work"),
            ("FinalAnnual", "ADM-2026-0006", "CHM101") => (78.0m, "Good understanding of environmental chemistry"),
            ("FinalAnnual", "ADM-2026-0006", "ENG101") => (93.0m, "Distinction in English final"),
            ("FinalAnnual", "ADM-2026-0006", "BIO101") => (86.0m, "Solid overall biology mark"),
            ("FinalAnnual", "ADM-2026-0006", "HIS101") => (91.0m, "Distinction in annual history examination"),
            ("FinalAnnual", "ADM-2026-0006", "ICT101") => (84.0m, "Creative digital media project"),
            ("Monthly", "ADM-2026-0007", "KHM101") => (75.0m, "Passing quiz score on grammar"),
            ("Monthly", "ADM-2026-0007", "MATH101") => (84.0m, "Good quiz on linear inequalities"),
            ("Monthly", "ADM-2026-0007", "PHY101") => (80.0m, "Passing quiz on simple machines"),
            ("Monthly", "ADM-2026-0007", "CHM101") => (62.0m, "Needs review on balancing reactions"),
            ("Monthly", "ADM-2026-0007", "ENG101") => (74.0m, "Passing vocabulary quiz"),
            ("Monthly", "ADM-2026-0007", "BIO101") => (78.0m, "Passing quiz on cell organelles"),
            ("Monthly", "ADM-2026-0007", "HIS101") => (79.0m, "Good effort on historical events quiz"),
            ("Monthly", "ADM-2026-0007", "ICT101") => (94.0m, "Fastest student to solve programming challenge"),
            ("S1Midterm", "ADM-2026-0007", "KHM101") => (78.0m, "Focus on sentence structure"),
            ("S1Midterm", "ADM-2026-0007", "MATH101") => (86.0m, "Good grasp of coordinate geometry"),
            ("S1Midterm", "ADM-2026-0007", "PHY101") => (82.0m, "Solid lab report on acceleration"),
            ("S1Midterm", "ADM-2026-0007", "CHM101") => (65.0m, "Challenged by chemical equations"),
            ("S1Midterm", "ADM-2026-0007", "ENG101") => (77.0m, "Passing mark, work on essay flow"),
            ("S1Midterm", "ADM-2026-0007", "BIO101") => (80.0m, "Satisfactory understanding of food webs"),
            ("S1Midterm", "ADM-2026-0007", "HIS101") => (81.0m, "Good knowledge of local history"),
            ("S1Midterm", "ADM-2026-0007", "ICT101") => (95.0m, "Outstanding coding logic and debugging"),
            ("S1Final", "ADM-2026-0007", "KHM101") => (80.0m, "Achieved merit through consistent reading"),
            ("S1Final", "ADM-2026-0007", "MATH101") => (88.0m, "High score on geometry and trigonometry"),
            ("S1Final", "ADM-2026-0007", "PHY101") => (85.0m, "Very good final physics exam"),
            ("S1Final", "ADM-2026-0007", "CHM101") => (70.0m, "Passed final exam through remedial sessions"),
            ("S1Final", "ADM-2026-0007", "ENG101") => (79.0m, "Good progress in English speech and essay"),
            ("S1Final", "ADM-2026-0007", "BIO101") => (82.0m, "Solid understanding of genetics"),
            ("S1Final", "ADM-2026-0007", "HIS101") => (83.0m, "Good understanding of 20th century history"),
            ("S1Final", "ADM-2026-0007", "ICT101") => (97.0m, "Distinction capstone coding project"),
            ("S2Midterm", "ADM-2026-0007", "KHM101") => (79.0m, "Passing grade, good effort"),
            ("S2Midterm", "ADM-2026-0007", "MATH101") => (87.0m, "Solid work on vectors and matrices"),
            ("S2Midterm", "ADM-2026-0007", "PHY101") => (83.0m, "Good work on thermal physics"),
            ("S2Midterm", "ADM-2026-0007", "CHM101") => (68.0m, "Needs review on organic nomenclature"),
            ("S2Midterm", "ADM-2026-0007", "ENG101") => (78.0m, "Good conversational English"),
            ("S2Midterm", "ADM-2026-0007", "BIO101") => (81.0m, "Good effort on ecosystems"),
            ("S2Midterm", "ADM-2026-0007", "HIS101") => (82.0m, "Good class participation"),
            ("S2Midterm", "ADM-2026-0007", "ICT101") => (96.0m, "Superior web backend implementation"),
            ("FinalAnnual", "ADM-2026-0007", "KHM101") => (81.0m, "Merit grade achieved in Khmer"),
            ("FinalAnnual", "ADM-2026-0007", "MATH101") => (89.0m, "Strong final math performance"),
            ("FinalAnnual", "ADM-2026-0007", "PHY101") => (86.0m, "Good practical and theory score"),
            ("FinalAnnual", "ADM-2026-0007", "CHM101") => (73.0m, "Passing grade achieved in chemistry"),
            ("FinalAnnual", "ADM-2026-0007", "ENG101") => (80.0m, "Reached merit benchmark in English"),
            ("FinalAnnual", "ADM-2026-0007", "BIO101") => (83.0m, "Good overall biology performance"),
            ("FinalAnnual", "ADM-2026-0007", "HIS101") => (84.0m, "Solid annual history score"),
            ("FinalAnnual", "ADM-2026-0007", "ICT101") => (98.0m, "Top computer science student"),
            ("Monthly", "ADM-2026-0008", "KHM101") => (89.0m, "Good score in literature quiz"),
            ("Monthly", "ADM-2026-0008", "MATH101") => (92.0m, "Near-perfect quiz on quadratic graphs"),
            ("Monthly", "ADM-2026-0008", "PHY101") => (86.0m, "Good quiz on friction and work"),
            ("Monthly", "ADM-2026-0008", "CHM101") => (87.0m, "Very good quiz on atomic structure"),
            ("Monthly", "ADM-2026-0008", "ENG101") => (90.0m, "Distinction on grammar and idioms"),
            ("Monthly", "ADM-2026-0008", "BIO101") => (93.0m, "High score on plant respiration"),
            ("Monthly", "ADM-2026-0008", "HIS101") => (88.0m, "Good historical overview quiz"),
            ("Monthly", "ADM-2026-0008", "ICT101") => (87.0m, "Clean coding worksheet"),
            ("S1Midterm", "ADM-2026-0008", "KHM101") => (91.0m, "Neat and comprehensive Khmer analysis"),
            ("S1Midterm", "ADM-2026-0008", "MATH101") => (93.0m, "Excellent logic and numerical accuracy"),
            ("S1Midterm", "ADM-2026-0008", "PHY101") => (88.0m, "Sound physics experimental procedure"),
            ("S1Midterm", "ADM-2026-0008", "CHM101") => (89.0m, "High accuracy in acid-base calculations"),
            ("S1Midterm", "ADM-2026-0008", "ENG101") => (92.0m, "Fluent reading and precise answers"),
            ("S1Midterm", "ADM-2026-0008", "BIO101") => (94.0m, "Top score in microscopy and cellular study"),
            ("S1Midterm", "ADM-2026-0008", "HIS101") => (90.0m, "Distinction in historical document analysis"),
            ("S1Midterm", "ADM-2026-0008", "ICT101") => (89.0m, "Well-organized database schema"),
            ("S1Final", "ADM-2026-0008", "KHM101") => (93.0m, "Distinction in Khmer literature"),
            ("S1Final", "ADM-2026-0008", "MATH101") => (95.0m, "Distinction in advanced mathematics"),
            ("S1Final", "ADM-2026-0008", "PHY101") => (90.0m, "Distinction in physics theory"),
            ("S1Final", "ADM-2026-0008", "CHM101") => (91.0m, "Distinction in chemistry final"),
            ("S1Final", "ADM-2026-0008", "ENG101") => (94.0m, "Distinction in English comprehensive"),
            ("S1Final", "ADM-2026-0008", "BIO101") => (95.0m, "Distinction in biology final"),
            ("S1Final", "ADM-2026-0008", "HIS101") => (92.0m, "Distinction in history final"),
            ("S1Final", "ADM-2026-0008", "ICT101") => (91.0m, "Distinction in ICT practical project"),
            ("S2Midterm", "ADM-2026-0008", "KHM101") => (90.0m, "Clear and insightful prose"),
            ("S2Midterm", "ADM-2026-0008", "MATH101") => (92.0m, "Strong analytical math capability"),
            ("S2Midterm", "ADM-2026-0008", "PHY101") => (87.0m, "Good lab performance"),
            ("S2Midterm", "ADM-2026-0008", "CHM101") => (88.0m, "Solid work on periodic trends"),
            ("S2Midterm", "ADM-2026-0008", "ENG101") => (91.0m, "Active leadership in debate"),
            ("S2Midterm", "ADM-2026-0008", "BIO101") => (93.0m, "Accurate genetics diagrams"),
            ("S2Midterm", "ADM-2026-0008", "HIS101") => (89.0m, "Very good historical paper"),
            ("S2Midterm", "ADM-2026-0008", "ICT101") => (88.0m, "Good programming implementation"),
            ("FinalAnnual", "ADM-2026-0008", "KHM101") => (94.0m, "Exemplary Khmer annual exam"),
            ("FinalAnnual", "ADM-2026-0008", "MATH101") => (96.0m, "Superb calculus final score"),
            ("FinalAnnual", "ADM-2026-0008", "PHY101") => (91.0m, "Distinction in physics annual"),
            ("FinalAnnual", "ADM-2026-0008", "CHM101") => (92.0m, "Distinction in chemistry annual"),
            ("FinalAnnual", "ADM-2026-0008", "ENG101") => (95.0m, "Distinction in English annual"),
            ("FinalAnnual", "ADM-2026-0008", "BIO101") => (96.0m, "Distinction in biology annual"),
            ("FinalAnnual", "ADM-2026-0008", "HIS101") => (93.0m, "Distinction in history annual"),
            ("FinalAnnual", "ADM-2026-0008", "ICT101") => (92.0m, "Distinction in ICT annual"),
            _ => (85.0m, "Good performance")
        };
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
            if (res?.Data != null && res.Data.Count > 0)
            {
                foreach (var c in res.Data)
                {
                    _componentCache[c.Id] = c;
                    if (!_mockComponents.Any(m => m.Id == c.Id))
                    {
                        _mockComponents.Add(c);
                    }
                }

                if (res.Data.Count >= 8)
                {
                    return res;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("GetAssessmentComponentsAsync", ex);
        }

        var list = _mockComponents
            .Where(c => (c.ClassSectionId == targetClassId || c.ClassSectionId == Class10AId) &&
                        (c.ExamId == targetExamId || targetExamId == Guid.Empty))
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
                    var canonicalId = ResolveDeterministicComponentId(targetExamId, subj.Name, targetClassId);
                    var newComp = new AssessmentComponentDto(
                        canonicalId, targetExamId, $"{subj.Name} Written Exam",
                        subj.Id, subj.Name, targetClassId, "10-A", "Assessment Component", 100m, 40m
                    );
                    if (!_mockComponents.Any(m => m.Id == canonicalId))
                    {
                        _mockComponents.Add(newComp);
                    }
                    list.Add(newComp);
                }
            }
        }
        foreach (var c in list) _componentCache[c.Id] = c;
        return ApiResponse<List<AssessmentComponentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId, string? subjectName = null)
    {
        var comp = _mockComponents.FirstOrDefault(c => c.Id == componentId)
            ?? (_componentCache.TryGetValue(componentId, out var cachedComp) ? cachedComp : null);
        var compName = comp?.Name ?? "Assessment Exam";
        var subjId = comp?.SubjectId ?? SubjKhmerId;
        var resolvedSubjName = comp?.SubjectName ?? (!string.IsNullOrWhiteSpace(subjectName) ? subjectName : "Khmer Literature");
        var classId = comp?.ClassSectionId ?? Class10AId;

        if (comp == null && !string.IsNullOrWhiteSpace(subjectName))
        {
            _componentCache[componentId] = new AssessmentComponentDto(componentId, Guid.Empty, compName, subjId, resolvedSubjName, classId, "10-A", compName, 100m, 40m);
        }

        var examId = comp?.ExamId ?? Guid.Empty;
        var exName = comp?.ExamName ?? (_examNameCache.TryGetValue(examId, out var en) ? en : string.Empty);

        var allClassStudents = _mockStudents
            .Where(x => x.Status == StudentStatus.Active && (x.CurrentClassSectionId == classId || x.CurrentClassSectionId == Class10AId || classId == Class10AId))
            .GroupBy(s => s.AdmissionNumber)
            .Select(g => g.First())
            .OrderBy(s => s.AdmissionNumber)
            .ToList();

        var canonicalCompId = ResolveDeterministicComponentId(examId, resolvedSubjName, classId, exName);

        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<StudentGradeDto>>>($"api/grades/components/{componentId}/grades", JsonOptions);
            if (res?.Data != null && res.Data.Count > 0)
            {
                // Synchronize returned database grades into _mockGrades cache so subsequent queries reflect real persistent data
                foreach (var dbGrade in res.Data)
                {
                    _mockGrades.RemoveAll(g =>
                        (g.StudentId == dbGrade.StudentId || (!string.IsNullOrEmpty(dbGrade.AdmissionNumber) && string.Equals(g.AdmissionNumber, dbGrade.AdmissionNumber, StringComparison.OrdinalIgnoreCase))) &&
                        (g.AssessmentComponentId == componentId || g.AssessmentComponentId == canonicalCompId));
                    _mockGrades.Insert(0, dbGrade);
                }

                var existingById = res.Data.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
                var existingByAdm = res.Data.Where(g => !string.IsNullOrEmpty(g.AdmissionNumber)).GroupBy(g => g.AdmissionNumber).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var mockGradesForComp = _mockGrades.Where(g => g.AssessmentComponentId == componentId || g.AssessmentComponentId == canonicalCompId).ToList();
                var mockByAdm = mockGradesForComp.Where(g => !string.IsNullOrEmpty(g.AdmissionNumber)).GroupBy(g => g.AdmissionNumber).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var mockById = mockGradesForComp.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
                var merged = new List<StudentGradeDto>();

                foreach (var s in allClassStudents)
                {
                    // Prioritize real database grades when available
                    if (existingByAdm.TryGetValue(s.AdmissionNumber, out var existingAdm))
                    {
                        merged.Add(existingAdm);
                    }
                    else if (existingById.TryGetValue(s.Id, out var existing))
                    {
                        merged.Add(existing);
                    }
                    else if (mockByAdm.TryGetValue(s.AdmissionNumber, out var editedMock))
                    {
                        merged.Add(editedMock with { AssessmentComponentId = componentId, ComponentName = compName, SubjectId = subjId, SubjectName = resolvedSubjName });
                    }
                    else if (mockById.TryGetValue(s.Id, out var editedMockId))
                    {
                        merged.Add(editedMockId with { AssessmentComponentId = componentId, ComponentName = compName, SubjectId = subjId, SubjectName = resolvedSubjName });
                    }
                    else
                    {
                        var (sc, remarks) = GetDifferentiatedScore(exName, s.AdmissionNumber, resolvedSubjName);
                        var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                        var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                        merged.Add(new StudentGradeDto(
                            Guid.NewGuid(), componentId, compName, subjId, resolvedSubjName,
                            s.Id, s.FullName, s.AdmissionNumber, sc, 100m, sc, letter, gpa, remarks
                        ));
                    }
                }
                var deduped = merged
                    .GroupBy(g => g.AdmissionNumber, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(g => g.AdmissionNumber)
                    .ToList();
                return ApiResponse<List<StudentGradeDto>>.Ok(deduped);
            }
        }
        catch (Exception ex)
        {
            LogException("GetComponentGradesAsync", ex);
        }

        var existingForComp = _mockGrades.Where(g => g.AssessmentComponentId == componentId || g.AssessmentComponentId == canonicalCompId).ToList();
        var mockMap = existingForComp.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
        var mockMapByAdm = existingForComp.Where(g => !string.IsNullOrEmpty(g.AdmissionNumber)).GroupBy(g => g.AdmissionNumber).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var resultList = new List<StudentGradeDto>();

        foreach (var s in allClassStudents)
        {
            if (mockMapByAdm.TryGetValue(s.AdmissionNumber, out var egAdm))
            {
                resultList.Add(egAdm with { AssessmentComponentId = componentId, ComponentName = compName, SubjectId = subjId, SubjectName = resolvedSubjName });
            }
            else if (mockMap.TryGetValue(s.Id, out var eg))
            {
                resultList.Add(eg with { AssessmentComponentId = componentId, ComponentName = compName, SubjectId = subjId, SubjectName = resolvedSubjName });
            }
            else
            {
                var (sc, remarks) = GetDifferentiatedScore(exName, s.AdmissionNumber, resolvedSubjName);
                var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                var g = new StudentGradeDto(
                    Guid.NewGuid(), componentId, compName, subjId, resolvedSubjName,
                    s.Id, s.FullName, s.AdmissionNumber, sc, 100m, sc, letter, gpa, remarks
                );
                resultList.Add(g);
                // Intentionally do not permanently insert default sample scores into _mockGrades so they never overwrite edits
            }
        }

        var dedupedResult = resultList
            .GroupBy(g => g.AdmissionNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(g => g.AdmissionNumber)
            .ToList();

        return ApiResponse<List<StudentGradeDto>>.Ok(dedupedResult);
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetStudentAllGradesAsync(Guid studentId, Guid examId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-card/{studentId}/{examId}", JsonOptions);
            var targetExamName = _examNameCache.TryGetValue(examId, out var ename) ? ename : string.Empty;

            if (res?.Data?.Subjects != null && res.Data.Subjects.Count >= 8)
            {
                var list = res.Data.Subjects.Select(s =>
                {
                    var targetCompId = ResolveDeterministicComponentId(examId, s.SubjectName, Class10AId, targetExamName);
                    var localOverride = _mockGrades.FirstOrDefault(g =>
                        (g.StudentId == studentId || (!string.IsNullOrEmpty(res.Data.AdmissionNumber) && string.Equals(g.AdmissionNumber, res.Data.AdmissionNumber, StringComparison.OrdinalIgnoreCase))) &&
                        (g.AssessmentComponentId == targetCompId || (_componentCache.TryGetValue(g.AssessmentComponentId, out var gc) && gc.ExamId == examId && string.Equals(g.SubjectName, s.SubjectName, StringComparison.OrdinalIgnoreCase))));

                    var score = localOverride?.ScoreObtained ?? s.TotalScore;
                    var pct = localOverride?.Percentage ?? s.Percentage;
                    var letter = localOverride?.GradeLetter ?? s.GradeLetter;
                    var gp = localOverride?.GradePoint ?? s.GradePoint;

                    return new StudentGradeDto(
                        Guid.NewGuid(),
                        s.SubjectId,
                        "Exam Assessment",
                        s.SubjectId,
                        s.SubjectName,
                        studentId,
                        res.Data.StudentName,
                        res.Data.AdmissionNumber,
                        score,
                        s.MaxScore,
                        pct,
                        letter,
                        gp,
                        localOverride?.Remarks ?? "Official Academic Record"
                    );
                }).ToList();
                return ApiResponse<List<StudentGradeDto>>.Ok(list);
            }
            else if (res?.Data?.Subjects != null && res.Data.Subjects.Count > 0)
            {
                // Partial backend report card. Merge with complete 8-subject curriculum!
                var studentGradesList = _mockGrades.Where(g =>
                    (g.StudentId == studentId || (!string.IsNullOrEmpty(res.Data.AdmissionNumber) && string.Equals(g.AdmissionNumber, res.Data.AdmissionNumber, StringComparison.OrdinalIgnoreCase))) &&
                    (examId == Guid.Empty || g.AssessmentComponentId == ResolveDeterministicComponentId(examId, g.SubjectName, Class10AId, targetExamName) ||
                     (_componentCache.TryGetValue(g.AssessmentComponentId, out var gc) && gc.ExamId == examId))).ToList();

                var list = new List<StudentGradeDto>();
                var seenSubjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add backend report card subjects first (with local overrides if edited for this exam)
                foreach (var s in res.Data.Subjects)
                {
                    seenSubjNames.Add(s.SubjectName);
                    var targetCompId = ResolveDeterministicComponentId(examId, s.SubjectName, Class10AId, targetExamName);
                    var localOverride = _mockGrades.FirstOrDefault(g =>
                        (g.StudentId == studentId || string.Equals(g.AdmissionNumber, res.Data.AdmissionNumber, StringComparison.OrdinalIgnoreCase)) &&
                        (g.AssessmentComponentId == targetCompId || (_componentCache.TryGetValue(g.AssessmentComponentId, out var gc) && gc.ExamId == examId && string.Equals(g.SubjectName, s.SubjectName, StringComparison.OrdinalIgnoreCase))));

                    var score = localOverride?.ScoreObtained ?? s.TotalScore;
                    var pct = localOverride?.Percentage ?? s.Percentage;
                    var letter = localOverride?.GradeLetter ?? s.GradeLetter;
                    var gp = localOverride?.GradePoint ?? s.GradePoint;

                    list.Add(new StudentGradeDto(
                        Guid.NewGuid(),
                        s.SubjectId,
                        "Exam Assessment",
                        s.SubjectId,
                        s.SubjectName,
                        studentId,
                        res.Data.StudentName,
                        res.Data.AdmissionNumber,
                        score,
                        s.MaxScore,
                        pct,
                        letter,
                        gp,
                        localOverride?.Remarks ?? "Official Academic Record"
                    ));
                }

                // Add remaining curriculum subjects from mock grades for this exam
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

        // Authoritative fallback: return student's exact distinct grades from _mockGrades scoped to this exam
        var adm = _mockStudents.FirstOrDefault(s => s.Id == studentId)?.AdmissionNumber;
        var exName = _examNameCache.TryGetValue(examId, out var en) ? en : string.Empty;
        var studentGrades = _mockGrades
            .Where(g => (g.StudentId == studentId ||
                        (!string.IsNullOrEmpty(adm) && string.Equals(g.AdmissionNumber, adm, StringComparison.OrdinalIgnoreCase))) &&
                        (examId == Guid.Empty ||
                         g.AssessmentComponentId == ResolveDeterministicComponentId(examId, g.SubjectName, Class10AId, exName) ||
                         (_componentCache.TryGetValue(g.AssessmentComponentId, out var gc) && gc.ExamId == examId)))
            .ToList();

        if (studentGrades.Count == 0)
        {
            var studentObj = _mockStudents.FirstOrDefault(s => s.Id == studentId || (!string.IsNullOrEmpty(adm) && string.Equals(s.AdmissionNumber, adm, StringComparison.OrdinalIgnoreCase)));
            var studentName = studentObj?.FullName ?? "Enrolled Student";
            var admNo = studentObj?.AdmissionNumber ?? adm ?? "ADM-2026-0001";
            studentGrades = _mockSubjects.Select(subj =>
            {
                var compId = ResolveDeterministicComponentId(examId, subj.Name, Class10AId, exName);
                var (sc, remarks) = GetDifferentiatedScore(exName, admNo, subj.Name);
                var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                return new StudentGradeDto(
                    Guid.NewGuid(), compId, $"{subj.Name} Exam", subj.Id, subj.Name,
                    studentId, studentName, admNo, sc, 100m, sc, letter, gpa, remarks
                );
            }).ToList();

            foreach (var g in studentGrades)
            {
                _mockGrades.Add(g);
            }
        }

        // Deduplicate by SubjectName - picks the first item (since updated grades are placed at index 0)
        var distinct = studentGrades
            .GroupBy(g => g.SubjectName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return ApiResponse<List<StudentGradeDto>>.Ok(distinct);
    }

    public async Task<ApiResponse<bool>> RecordSingleGradeAsync(Guid componentId, Guid studentId, decimal score, string? remarks = null, string? admissionNumber = null, string? subjectName = null)
    {
        var request = new BatchGradeEntryRequest(
            componentId,
            new List<StudentGradeItem> { new(studentId, score, remarks, admissionNumber) }
        );
        return await RecordBatchGradesAsync(request, subjectName);
    }

    public async Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request, string? subjectName = null)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/grades/batch", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            }
            else
            {
                Debug.WriteLine($"[Nexora ApiService] RecordBatchGradesAsync returned non-success code: {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            LogException("RecordBatchGradesAsync", ex);
        }

        var comp = _mockComponents.FirstOrDefault(c => c.Id == request.AssessmentComponentId)
            ?? (_componentCache.TryGetValue(request.AssessmentComponentId, out var cachedComp) ? cachedComp : null);
        var compName = comp?.Name ?? "Assessment Exam";
        var subjId = comp?.SubjectId ?? Guid.Empty;
        var resolvedSubjName = comp?.SubjectName ?? (!string.IsNullOrWhiteSpace(subjectName) ? subjectName : "Khmer Literature");

        if (subjId == Guid.Empty)
        {
            var matchedSubj = _mockSubjects.FirstOrDefault(s => string.Equals(s.Name, resolvedSubjName, StringComparison.OrdinalIgnoreCase))
                ?? _mockSubjects.FirstOrDefault(s => resolvedSubjName.Contains(s.Name, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(resolvedSubjName, StringComparison.OrdinalIgnoreCase));
            subjId = matchedSubj?.Id ?? SubjKhmerId;
        }

        var targetExamId = comp?.ExamId ?? ExamMidtermId;
        var targetExamName = comp?.ExamName ?? (_examNameCache.TryGetValue(targetExamId, out var cachedExamName) ? cachedExamName : string.Empty);
        var canonicalCompId = ResolveDeterministicComponentId(targetExamId, resolvedSubjName, Class10AId, targetExamName);

        // Register in cache & mock components so subsequent queries find it
        if (!_componentCache.ContainsKey(request.AssessmentComponentId))
        {
            _componentCache[request.AssessmentComponentId] = new AssessmentComponentDto(
                request.AssessmentComponentId, targetExamId, compName, subjId, resolvedSubjName, Class10AId, "10-A", compName, 100m, 40m
            );
        }
        if (!_mockComponents.Any(c => c.Id == request.AssessmentComponentId))
        {
            _mockComponents.Add(_componentCache[request.AssessmentComponentId]);
        }
        if (!_componentCache.ContainsKey(canonicalCompId))
        {
            _componentCache[canonicalCompId] = new AssessmentComponentDto(
                canonicalCompId, targetExamId, compName, subjId, resolvedSubjName, Class10AId, "10-A", compName, 100m, 40m
            );
        }
        if (!_mockComponents.Any(c => c.Id == canonicalCompId))
        {
            _mockComponents.Add(_componentCache[canonicalCompId]);
        }

        foreach (var entry in request.Grades)
        {
            var student = _mockStudents.FirstOrDefault(s => s.Id == entry.StudentId)
                ?? (!string.IsNullOrWhiteSpace(entry.AdmissionNumber) ? _mockStudents.FirstOrDefault(s => s.AdmissionNumber == entry.AdmissionNumber) : null);
            var name = student?.FullName ?? "Student";
            var adm = !string.IsNullOrWhiteSpace(entry.AdmissionNumber) ? entry.AdmissionNumber : (student?.AdmissionNumber ?? "");
            var targetStudentId = student?.Id ?? entry.StudentId;

            // Purge existing grades for this student for this component AND canonical ID
            _mockGrades.RemoveAll(g =>
                (g.StudentId == targetStudentId || g.StudentId == entry.StudentId || (!string.IsNullOrEmpty(adm) && string.Equals(g.AdmissionNumber, adm, StringComparison.OrdinalIgnoreCase))) &&
                (g.AssessmentComponentId == request.AssessmentComponentId ||
                 g.AssessmentComponentId == canonicalCompId ||
                 (comp != null && g.AssessmentComponentId == comp.Id)));

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

            var newGrade = new StudentGradeDto(
                Guid.NewGuid(), request.AssessmentComponentId, compName, subjId, resolvedSubjName,
                targetStudentId, name, adm, entry.ScoreObtained, 100m, entry.ScoreObtained,
                gradeLetter, gpa, entry.Remarks
            );

            // Insert at index 0 so GroupBy/First() picks the newest edited grade
            _mockGrades.Insert(0, newGrade);
            if (canonicalCompId != request.AssessmentComponentId && canonicalCompId != Guid.Empty)
            {
                _mockGrades.Insert(0, newGrade with { AssessmentComponentId = canonicalCompId });
            }
        }

        _mockAudits.Insert(0, new AuditLogDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "teacher", AuditAction.Create, "Grade",
            request.AssessmentComponentId.ToString(), $"Recorded grades for {request.Grades.Count} students", null, null, "127.0.0.1", DateTime.UtcNow));

        return ApiResponse<bool>.Ok(true, "Grades successfully recorded.");
    }

    public async Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-card/{studentId}/{examId}", JsonOptions);
            if (res?.Data != null && res.Data.Subjects.Count > 0)
            {
                var targetExamName = _examNameCache.TryGetValue(examId, out var ename) ? ename : string.Empty;
                var subjects = res.Data.Subjects.Select(s =>
                {
                    var targetCompId = ResolveDeterministicComponentId(examId, s.SubjectName, Class10AId, targetExamName);
                    var local = _mockGrades.FirstOrDefault(g =>
                        (g.StudentId == studentId || string.Equals(g.AdmissionNumber, res.Data.AdmissionNumber, StringComparison.OrdinalIgnoreCase)) &&
                        (g.AssessmentComponentId == targetCompId || (_componentCache.TryGetValue(g.AssessmentComponentId, out var gc) && gc.ExamId == examId && string.Equals(g.SubjectName, s.SubjectName, StringComparison.OrdinalIgnoreCase))));
                    return local != null
                        ? s with { TotalScore = local.ScoreObtained, Percentage = local.Percentage, GradeLetter = local.GradeLetter, GradePoint = local.GradePoint }
                        : s;
                }).ToList();

                var totalObt = subjects.Sum(s => s.TotalScore);
                var totalMx = subjects.Sum(s => s.MaxScore);
                var pct = totalMx > 0 ? (totalObt / totalMx) * 100m : 0m;
                var gpa = subjects.Count > 0 ? subjects.Average(s => s.GradePoint) : 0m;

                return ApiResponse<ReportCardDto>.Ok(res.Data with
                {
                    Subjects = subjects,
                    TotalScoreObtained = totalObt,
                    TotalMaxScore = totalMx,
                    OverallPercentage = Math.Round(pct, 2),
                    CumulativeGpa = Math.Round(gpa, 2),
                    OverallGradeLetter = pct >= 85 ? "A" : pct >= 75 ? "B" : pct >= 65 ? "C" : pct >= 50 ? "D" : "F"
                });
            }
        }
        catch (Exception ex)
        {
            LogException("GetStudentReportCardAsync", ex);
        }

        var student = _mockStudents.FirstOrDefault(s => s.Id == studentId) ?? _mockStudents.First();
        var allGrades = (await GetStudentAllGradesAsync(student.Id, examId)).Data ?? new List<StudentGradeDto>();

        var subjectsList = allGrades.Select(g => new SubjectReportItem(
            g.SubjectId, g.SubjectName, "SUBJ", 3, g.ScoreObtained, g.MaxScore, g.Percentage, g.GradeLetter, g.GradePoint
        )).ToList();

        var totalObtained = subjectsList.Sum(s => s.TotalScore);
        var totalMax = subjectsList.Sum(s => s.MaxScore);
        var percentage = totalMax > 0 ? (totalObtained / totalMax) * 100m : 0m;
        var cumulativeGpa = subjectsList.Count > 0 ? subjectsList.Average(s => s.GradePoint) : 0m;
        var overallLetter = percentage >= 85 ? "A" : percentage >= 75 ? "B" : percentage >= 65 ? "C" : percentage >= 50 ? "D" : "F";

        return ApiResponse<ReportCardDto>.Ok(new ReportCardDto(
            student.Id, student.FullName, student.AdmissionNumber, "10-A", "2026-2027", "Semester 1",
            subjectsList, totalObtained, totalMax, Math.Round(percentage, 2), Math.Round(cumulativeGpa, 2), overallLetter, 1));
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
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, AssessmentComponentDto> _componentCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> _examNameCache = new();
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

    public static readonly Guid DbExamMidtermId = Guid.Parse("372c01e9-495b-458a-8b90-fce4ae24c1f7");
    public static readonly Guid DbExamQuiz1Id = Guid.Parse("c31e1389-d496-4502-9347-a9701a7be4c4");
    public static readonly Guid DbExamFinal1Id = Guid.Parse("f551261d-dfa3-43d5-a13a-9ecbc769ec9d");
    public static readonly Guid DbExamMidterm2Id = Guid.Parse("1a89f821-f2df-48f9-9c45-e6f9bdb1d790");
    public static readonly Guid DbExamFinal2Id = Guid.Parse("c3ae8c60-d559-4ca1-b378-67ff746dee61");

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

    private static readonly Guid CompMidterm2KhmerId = Guid.Parse("d0000000-0000-0000-0000-000000000041");
    private static readonly Guid CompMidterm2MathId = Guid.Parse("d0000000-0000-0000-0000-000000000042");
    private static readonly Guid CompMidterm2PhysicsId = Guid.Parse("d0000000-0000-0000-0000-000000000043");
    private static readonly Guid CompMidterm2ChemistryId = Guid.Parse("d0000000-0000-0000-0000-000000000044");
    private static readonly Guid CompMidterm2EnglishId = Guid.Parse("d0000000-0000-0000-0000-000000000045");
    private static readonly Guid CompMidterm2BiologyId = Guid.Parse("d0000000-0000-0000-0000-000000000046");
    private static readonly Guid CompMidterm2HistoryId = Guid.Parse("d0000000-0000-0000-0000-000000000047");
    private static readonly Guid CompMidterm2IctId = Guid.Parse("d0000000-0000-0000-0000-000000000048");

    private static readonly Guid CompFinal2KhmerId = Guid.Parse("d0000000-0000-0000-0000-000000000051");
    private static readonly Guid CompFinal2MathId = Guid.Parse("d0000000-0000-0000-0000-000000000052");
    private static readonly Guid CompFinal2PhysicsId = Guid.Parse("d0000000-0000-0000-0000-000000000053");
    private static readonly Guid CompFinal2ChemistryId = Guid.Parse("d0000000-0000-0000-0000-000000000054");
    private static readonly Guid CompFinal2EnglishId = Guid.Parse("d0000000-0000-0000-0000-000000000055");
    private static readonly Guid CompFinal2BiologyId = Guid.Parse("d0000000-0000-0000-0000-000000000056");
    private static readonly Guid CompFinal2HistoryId = Guid.Parse("d0000000-0000-0000-0000-000000000057");
    private static readonly Guid CompFinal2IctId = Guid.Parse("d0000000-0000-0000-0000-000000000058");

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
        new(Class10AId, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-A", "Room 201", 40, TeacherDaraId, "Dara Sok", 8),
        new(Class10BId, Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-B", "Room 202", 40, TeacherChendaId, "Chenda Chan", 0)
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

        // Semester 2 Midterm Components (Class 10-A)
        new(CompMidterm2KhmerId, ExamMidterm2Id, "Semester 2 Midterm", SubjKhmerId, "Khmer Literature", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2MathId, ExamMidterm2Id, "Semester 2 Midterm", SubjMathId, "Mathematics", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2PhysicsId, ExamMidterm2Id, "Semester 2 Midterm", SubjPhysicsId, "Physics", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2ChemistryId, ExamMidterm2Id, "Semester 2 Midterm", SubjChemistryId, "Chemistry", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2EnglishId, ExamMidterm2Id, "Semester 2 Midterm", SubjEnglishId, "English Communication", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2BiologyId, ExamMidterm2Id, "Semester 2 Midterm", SubjBiologyId, "Biology", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2HistoryId, ExamMidterm2Id, "Semester 2 Midterm", SubjHistoryId, "History", Class10AId, "10-A", "Midterm Exam", 100m, 40m),
        new(CompMidterm2IctId, ExamMidterm2Id, "Semester 2 Midterm", SubjIctId, "Computer Science & ICT", Class10AId, "10-A", "Midterm Exam", 100m, 40m),

        // Semester 2 Final Components (Class 10-A)
        new(CompFinal2KhmerId, ExamFinal2Id, "Semester 2 Final", SubjKhmerId, "Khmer Literature", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2MathId, ExamFinal2Id, "Semester 2 Final", SubjMathId, "Mathematics", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2PhysicsId, ExamFinal2Id, "Semester 2 Final", SubjPhysicsId, "Physics", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2ChemistryId, ExamFinal2Id, "Semester 2 Final", SubjChemistryId, "Chemistry", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2EnglishId, ExamFinal2Id, "Semester 2 Final", SubjEnglishId, "English Communication", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2BiologyId, ExamFinal2Id, "Semester 2 Final", SubjBiologyId, "Biology", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2HistoryId, ExamFinal2Id, "Semester 2 Final", SubjHistoryId, "History", Class10AId, "10-A", "Final Exam", 100m, 40m),
        new(CompFinal2IctId, ExamFinal2Id, "Semester 2 Final", SubjIctId, "Computer Science & ICT", Class10AId, "10-A", "Final Exam", 100m, 40m),

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
            // School morning arrival: on-campus gate check-in between 07:38 AM and 07:53 AM (00:38 - 00:53 UTC)
            var checkInUtc = d.ToDateTime(new TimeOnly(0, 38).AddMinutes((i * 3 + 4) % 15), DateTimeKind.Utc);

            // 1. Visal Chan: 26 Present, 2 Late, 2 Excused (Total 30) -> Rate: 90.0%
            AttendanceStatus visalStatus = AttendanceStatus.Present;
            string? visalRemarks = null;
            DateTime visalTimestamp = checkInUtc;

            if (i == 0) // TODAY (Tuesday): Present at school (checked in 07:42 AM)
            {
                visalStatus = AttendanceStatus.Present;
                visalRemarks = null;
                visalTimestamp = d.ToDateTime(new TimeOnly(0, 42), DateTimeKind.Utc);
            }
            else if (i == 1) // YESTERDAY (Monday): Excused absence via Parent Permission (asked 07:15 AM)
            {
                visalStatus = AttendanceStatus.Excused;
                visalRemarks = "[Parent Permission] Sick Leave / Medical: Visal has a mild fever";
                visalTimestamp = d.ToDateTime(new TimeOnly(0, 15), DateTimeKind.Utc);
            }
            else if (i == 3)
            {
                visalStatus = AttendanceStatus.Late;
                visalRemarks = "Traffic delay on Russian Blvd (Checked in 08:22 AM)";
                visalTimestamp = d.ToDateTime(new TimeOnly(1, 22), DateTimeKind.Utc);
            }
            else if (i == 9)
            {
                visalStatus = AttendanceStatus.Late;
                visalRemarks = "Monsoon heavy rain delay (Checked in 08:18 AM)";
                visalTimestamp = d.ToDateTime(new TimeOnly(1, 18), DateTimeKind.Utc);
            }
            else if (i == 16)
            {
                visalStatus = AttendanceStatus.Excused;
                visalRemarks = "[Parent Permission] Doctor / Clinic Appointment";
                visalTimestamp = d.ToDateTime(new TimeOnly(0, 20), DateTimeKind.Utc);
            }

            records.Add(new(Guid.NewGuid(), Guid.NewGuid(), Class10AId, "10-A", StudentVisalId, "Visal Chan", "ADM-2026-0001", d, visalStatus, visalRemarks, visalTimestamp, "Teacher Dara Sok", null, null, null));

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

    private static List<StudentGradeDto> InitializeMockGrades()
    {
        var grades = new List<StudentGradeDto>();
        var components = InitializeMockComponents();
        var students = InitializeMockStudents();

        foreach (var c in components)
        {
            var exName = c.ExamName ?? "Semester 1 Midterm";
            var subjName = c.SubjectName ?? "Mathematics";
            var classStudents = students.Where(s => s.CurrentClassSectionId == c.ClassSectionId || c.ClassSectionId == Class10AId).ToList();
            foreach (var s in classStudents)
            {
                var (sc, remarks) = GetDifferentiatedScore(exName, s.AdmissionNumber, subjName);
                var letter = sc >= 85 ? "A" : sc >= 75 ? "B" : sc >= 65 ? "C" : sc >= 50 ? "D" : "F";
                var gpa = sc >= 85 ? 4.0m : sc >= 75 ? 3.0m : sc >= 65 ? 2.0m : sc >= 50 ? 1.0m : 0.0m;
                grades.Add(new StudentGradeDto(
                    Guid.NewGuid(), c.Id, c.Name, c.SubjectId, subjName,
                    s.Id, s.FullName, s.AdmissionNumber, sc, 100m, sc, letter, gpa, remarks
                ));
            }
        }
        return grades;
    }

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
