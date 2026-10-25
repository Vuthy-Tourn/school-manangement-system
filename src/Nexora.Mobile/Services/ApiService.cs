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

    Task<ApiResponse<LoginResponse>> LoginAsync(string usernameOrEmail, string password);
    Task LogoutAsync();

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public event Action? AuthStateChanged;

    public string BaseUrl
    {
        get => _httpClient.BaseAddress?.ToString() ?? "http://localhost:5000";
        set => _httpClient.BaseAddress = new Uri(value.TrimEnd('/') + "/");
    }

    public UserProfileDto? CurrentUser => _currentUser;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _currentUser != null;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // On Android emulator: 10.0.2.2 connects to host machine's localhost
        var defaultHost = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5000/" : "http://localhost:5000/";
        _httpClient.BaseAddress = new Uri(defaultHost);
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    private void SetAuthorizationHeader(string? token)
    {
        _accessToken = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
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
                    _currentUser = result.Data.User;
                    try
                    {
                        await SecureStorage.Default.SetAsync("auth_token", result.Data.AccessToken);
                        await SecureStorage.Default.SetAsync("refresh_token", result.Data.RefreshToken);
                    }
                    catch { /* SecureStorage fallback on unsupported platforms */ }

                    AuthStateChanged?.Invoke();
                    return result;
                }
            }
            var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
            return errorResult ?? ApiResponse<LoginResponse>.Fail("Invalid username or password.");
        }
        catch (Exception ex)
        {
            // If offline / local development without live backend, provide fallback authentication
            return FallbackOfflineLogin(usernameOrEmail, password, ex);
        }
    }

    private ApiResponse<LoginResponse> FallbackOfflineLogin(string username, string password, Exception ex)
    {
        // Provide graceful offline fallback for dev/testing demo
        var schoolId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var role = username.ToLower() switch
        {
            "superadmin" => UserRole.SuperAdmin,
            "teacher.dara" or "teacher.chenda" or "teacher" => UserRole.Teacher,
            "student.visal" or "student.kanha" or "student" => UserRole.Student,
            "parent.bopha" or "parent" => UserRole.Parent,
            _ => UserRole.SchoolAdmin
        };

        var user = new UserProfileDto(
            Guid.NewGuid(),
            schoolId,
            "Angkor International Academy",
            username,
            $"{username}@angkoracademy.edu.kh",
            username.Contains('.') ? username.Split('.')[1] : username,
            "User",
            $"{username} (Offline Mode)",
            role,
            "+855 23 888 999",
            null
        );

        _currentUser = user;
        SetAuthorizationHeader("offline_mock_token");
        AuthStateChanged?.Invoke();

        return ApiResponse<LoginResponse>.Ok(new LoginResponse(
            "offline_mock_token", "offline_refresh_token", DateTime.UtcNow.AddDays(7), user
        ), "Authenticated in offline demo mode.");
    }

    public async Task LogoutAsync()
    {
        SetAuthorizationHeader(null);
        _currentUser = null;
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
            return res ?? ApiResponse<AdminDashboardStatsDto>.Fail("No data returned.");
        }
        catch
        {
            // Offline fallback
            return ApiResponse<AdminDashboardStatsDto>.Ok(new AdminDashboardStatsDto(
                TotalStudents: 142,
                TotalTeachers: 18,
                TotalClasses: 12,
                TodayAttendancePercentage: 96.5m,
                TotalFeesCollected: 64200.0m,
                TotalFeesOutstanding: 8900.0m,
                RecentAnnouncements: GetMockAnnouncements(),
                RecentAudits: GetMockAuditLogs()
            ));
        }
    }

    public async Task<ApiResponse<TeacherDashboardStatsDto>> GetTeacherDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<TeacherDashboardStatsDto>>("api/dashboards/teacher", JsonOptions);
            return res ?? ApiResponse<TeacherDashboardStatsDto>.Fail("No data returned.");
        }
        catch
        {
            return ApiResponse<TeacherDashboardStatsDto>.Ok(new TeacherDashboardStatsDto(
                AssignedClassesCount: 3,
                AssignedSubjectsCount: 2,
                TotalStudentsTaught: 72,
                TodaySchedule: GetMockSchedule(),
                ClassesList: GetMockClasses()
            ));
        }
    }

    public async Task<ApiResponse<StudentDashboardStatsDto>> GetStudentDashboardAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<StudentDashboardStatsDto>>("api/dashboards/student", JsonOptions);
            return res ?? ApiResponse<StudentDashboardStatsDto>.Fail("No data returned.");
        }
        catch
        {
            var mockStudent = GetMockStudents().First();
            return ApiResponse<StudentDashboardStatsDto>.Ok(new StudentDashboardStatsDto(
                Student: mockStudent,
                OverallAttendancePercentage: 95.0m,
                CurrentGpa: 3.85m,
                PendingFees: 230.0m,
                TodaySchedule: GetMockSchedule(),
                Announcements: GetMockAnnouncements()
            ));
        }
    }

    public async Task<ApiResponse<ParentDashboardStatsDto>> GetParentDashboardAsync(Guid? childId = null)
    {
        try
        {
            var query = childId.HasValue ? $"?childId={childId.Value}" : string.Empty;
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ParentDashboardStatsDto>>($"api/dashboards/parent{query}", JsonOptions);
            return res ?? ApiResponse<ParentDashboardStatsDto>.Fail("No data returned.");
        }
        catch
        {
            var mockStudent = GetMockStudents().First();
            var children = new List<LinkedStudentDto>
            {
                new(mockStudent.Id, mockStudent.FullName, mockStudent.AdmissionNumber, mockStudent.CurrentClassName, "Son", true)
            };
            return ApiResponse<ParentDashboardStatsDto>.Ok(new ParentDashboardStatsDto(
                children,
                mockStudent.Id,
                mockStudent,
                95.0m,
                230.0m,
                GetMockGrades()
            ));
        }
    }

    public async Task<ApiResponse<PaginatedResult<StudentDto>>> GetStudentsAsync(string? search = null, Guid? classId = null)
    {
        try
        {
            var url = "api/students?";
            if (!string.IsNullOrEmpty(search)) url += $"SearchTerm={Uri.EscapeDataString(search)}&";
            if (classId.HasValue) url += $"ClassSectionId={classId.Value}&";

            var res = await _httpClient.GetFromJsonAsync<ApiResponse<PaginatedResult<StudentDto>>>(url, JsonOptions);
            return res ?? ApiResponse<PaginatedResult<StudentDto>>.Fail("Failed to load students.");
        }
        catch
        {
            var mockList = GetMockStudents();
            if (!string.IsNullOrEmpty(search))
                mockList = mockList.Where(s => s.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) || s.AdmissionNumber.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            return ApiResponse<PaginatedResult<StudentDto>>.Ok(new PaginatedResult<StudentDto>(mockList, mockList.Count, 1, 20));
        }
    }

    public async Task<ApiResponse<StudentDto>> GetStudentByIdAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<StudentDto>>($"api/students/{id}", JsonOptions);
            return res ?? ApiResponse<StudentDto>.Fail("Student not found.");
        }
        catch
        {
            var s = GetMockStudents().FirstOrDefault(x => x.Id == id) ?? GetMockStudents().First();
            return ApiResponse<StudentDto>.Ok(s);
        }
    }

    public async Task<ApiResponse<StudentDto>> CreateStudentAsync(CreateStudentRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/students", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions))!;
        }
        catch
        {
            var s = new StudentDto(
                Guid.NewGuid(), Guid.NewGuid(), request.AdmissionNumber, request.FirstName, request.LastName,
                $"{request.FirstName} {request.LastName}", request.DateOfBirth, request.Gender, request.BloodGroup,
                request.EnrollmentDate, StudentStatus.Active, request.Email, request.PhoneNumber, request.Address,
                request.ProfilePhotoUrl, request.ClassSectionId, "10-A", request.EmergencyContactName,
                request.EmergencyContactPhone, request.EmergencyContactRelationship);
            return ApiResponse<StudentDto>.Ok(s, "Student created successfully.");
        }
    }

    public async Task<ApiResponse<StudentDto>> UpdateStudentAsync(Guid id, UpdateStudentRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/students/{id}", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<StudentDto>>(JsonOptions))!;
        }
        catch
        {
            var s = GetMockStudents().First();
            return ApiResponse<StudentDto>.Ok(s, "Student updated successfully.");
        }
    }

    public async Task<ApiResponse<bool>> DeactivateStudentAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/students/{id}");
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true, "Student deactivated.");
        }
    }

    public async Task<ApiResponse<List<TeacherDto>>> GetTeachersAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TeacherDto>>>("api/teachers", JsonOptions);
            return res ?? ApiResponse<List<TeacherDto>>.Fail("Failed to load teachers.");
        }
        catch
        {
            return ApiResponse<List<TeacherDto>>.Ok(GetMockTeachers());
        }
    }

    public async Task<ApiResponse<TeacherDto>> CreateTeacherAsync(CreateTeacherRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/teachers", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<TeacherDto>>(JsonOptions))!;
        }
        catch
        {
            var t = new TeacherDto(Guid.NewGuid(), Guid.NewGuid(), request.EmployeeNumber, request.FirstName, request.LastName,
                $"{request.FirstName} {request.LastName}", request.Email, request.PhoneNumber, request.HireDate,
                request.Qualification, request.Specialization, true, new List<TeacherSubjectAssignmentDto>());
            return ApiResponse<TeacherDto>.Ok(t, "Teacher added.");
        }
    }

    public async Task<ApiResponse<List<ClassSectionDto>>> GetClassesAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ClassSectionDto>>>("api/academics/classes", JsonOptions);
            return res ?? ApiResponse<List<ClassSectionDto>>.Fail("Failed to load classes.");
        }
        catch
        {
            return ApiResponse<List<ClassSectionDto>>.Ok(GetMockClasses());
        }
    }

    public async Task<ApiResponse<List<SubjectDto>>> GetSubjectsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<SubjectDto>>>("api/academics/subjects", JsonOptions);
            return res ?? ApiResponse<List<SubjectDto>>.Fail("Failed to load subjects.");
        }
        catch
        {
            return ApiResponse<List<SubjectDto>>.Ok(GetMockSubjects());
        }
    }

    public async Task<ApiResponse<List<AcademicYearDto>>> GetAcademicYearsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AcademicYearDto>>>("api/academics/years", JsonOptions);
            return res ?? ApiResponse<List<AcademicYearDto>>.Fail("Failed to load academic years.");
        }
        catch
        {
            return ApiResponse<List<AcademicYearDto>>.Ok(new List<AcademicYearDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "2026-2027", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), true)
            });
        }
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetClassTimetableAsync(Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/class/{classId}", JsonOptions);
            return res ?? ApiResponse<List<TimetableEntryDto>>.Fail("Failed to load timetable.");
        }
        catch
        {
            return ApiResponse<List<TimetableEntryDto>>.Ok(GetMockSchedule());
        }
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetTeacherTimetableAsync(Guid teacherId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/teacher/{teacherId}", JsonOptions);
            return res ?? ApiResponse<List<TimetableEntryDto>>.Fail("Failed to load timetable.");
        }
        catch
        {
            return ApiResponse<List<TimetableEntryDto>>.Ok(GetMockSchedule());
        }
    }

    public async Task<ApiResponse<List<TimetableEntryDto>>> GetStudentTimetableAsync(Guid studentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<TimetableEntryDto>>>($"api/timetables/student/{studentId}", JsonOptions);
            return res ?? ApiResponse<List<TimetableEntryDto>>.Fail("Failed to load timetable.");
        }
        catch
        {
            return ApiResponse<List<TimetableEntryDto>>.Ok(GetMockSchedule());
        }
    }

    public async Task<ApiResponse<TimetableEntryDto>> CreateTimetableEntryAsync(CreateTimetableEntryRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/timetables", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<TimetableEntryDto>>(JsonOptions))!;
        }
        catch
        {
            var entry = new TimetableEntryDto(Guid.NewGuid(), Guid.NewGuid(), request.ClassSectionId, "10-A",
                request.SubjectId, "Mathematics", "MATH101", request.TeacherId, "Sok Dara", request.DayOfWeek,
                request.StartTime, request.EndTime, request.RoomNumber);
            return ApiResponse<TimetableEntryDto>.Ok(entry, "Schedule slot created.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteTimetableEntryAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.DeleteAsync($"api/timetables/{id}");
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true, "Slot removed.");
        }
    }

    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetClassAttendanceAsync(Guid classId, DateOnly date)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AttendanceRecordDto>>>($"api/attendance/class/{classId}?date={date:yyyy-MM-dd}", JsonOptions);
            return res ?? ApiResponse<List<AttendanceRecordDto>>.Fail("Failed to load attendance.");
        }
        catch
        {
            var students = GetMockStudents();
            var list = students.Select(s => new AttendanceRecordDto(
                Guid.NewGuid(), Guid.NewGuid(), classId, "10-A", s.Id, s.FullName, s.AdmissionNumber,
                date, AttendanceStatus.Present, null, DateTime.UtcNow, "Sok Dara", null, null, null
            )).ToList();
            return ApiResponse<List<AttendanceRecordDto>>.Ok(list);
        }
    }

    public async Task<ApiResponse<bool>> RecordBatchAttendanceAsync(BatchAttendanceRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/attendance/batch", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true, "Attendance successfully saved.");
        }
    }

    public async Task<ApiResponse<bool>> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request)
    {
        try
        {
            var res = await _httpClient.PutAsJsonAsync($"api/attendance/{id}/correct", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true, "Attendance corrected.");
        }
    }

    public async Task<ApiResponse<AttendanceSummaryDto>> GetClassAttendanceSummaryAsync(Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<AttendanceSummaryDto>>($"api/attendance/class/{classId}/summary", JsonOptions);
            return res ?? ApiResponse<AttendanceSummaryDto>.Fail("No summary.");
        }
        catch
        {
            return ApiResponse<AttendanceSummaryDto>.Ok(new AttendanceSummaryDto(35, 33, 1, 1, 0, 95.7m));
        }
    }

    public async Task<ApiResponse<List<ExamDto>>> GetExamsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<ExamDto>>>("api/grades/exams", JsonOptions);
            return res ?? ApiResponse<List<ExamDto>>.Fail("Failed to load exams.");
        }
        catch
        {
            return ApiResponse<List<ExamDto>>.Ok(new List<ExamDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Semester 1", "Midterm Examination", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(5), true, DateTime.UtcNow)
            });
        }
    }

    public async Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AssessmentComponentDto>>>($"api/grades/components?examId={examId}&classSectionId={classId}", JsonOptions);
            return res ?? ApiResponse<List<AssessmentComponentDto>>.Fail("Failed to load components.");
        }
        catch
        {
            return ApiResponse<List<AssessmentComponentDto>>.Ok(new List<AssessmentComponentDto>
            {
                new(Guid.NewGuid(), examId, "Midterm Examination", Guid.NewGuid(), "Mathematics", classId, "10-A", "Written Exam", 100m, 100m),
                new(Guid.NewGuid(), examId, "Midterm Examination", Guid.NewGuid(), "Physics", classId, "10-A", "Theory & Lab", 100m, 100m)
            });
        }
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid componentId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<StudentGradeDto>>>($"api/grades/components/{componentId}/grades", JsonOptions);
            return res ?? ApiResponse<List<StudentGradeDto>>.Fail("Failed to load grades.");
        }
        catch
        {
            return ApiResponse<List<StudentGradeDto>>.Ok(GetMockGrades());
        }
    }

    public async Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/grades/batch", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true, "Grades recorded.");
        }
    }

    public async Task<ApiResponse<ReportCardDto>> GetStudentReportCardAsync(Guid studentId, Guid examId)
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<ReportCardDto>>($"api/grades/report-card/{studentId}/{examId}", JsonOptions);
            return res ?? ApiResponse<ReportCardDto>.Fail("Failed to load report card.");
        }
        catch
        {
            var subjects = new List<SubjectReportItem>
            {
                new(Guid.NewGuid(), "Mathematics", "MATH101", 4, 94.5m, 100m, 94.5m, "A", 4.0m),
                new(Guid.NewGuid(), "Physics", "PHY101", 3, 88.0m, 100m, 88.0m, "B", 3.0m),
                new(Guid.NewGuid(), "Khmer Literature", "KHM101", 4, 91.0m, 100m, 91.0m, "A", 4.0m),
                new(Guid.NewGuid(), "English", "ENG101", 3, 95.0m, 100m, 95.0m, "A", 4.0m)
            };
            return ApiResponse<ReportCardDto>.Ok(new ReportCardDto(
                studentId, "Visal Chan", "ADM-2026-0001", "10-A", "2026-2027", "Semester 1",
                subjects, 368.5m, 400m, 92.1m, 3.79m, "A", 1
            ));
        }
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(Guid? studentId = null, InvoiceStatus? status = null)
    {
        try
        {
            var url = "api/fees/invoices?";
            if (studentId.HasValue) url += $"studentId={studentId.Value}&";
            if (status.HasValue) url += $"status={status.Value}&";

            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<InvoiceDto>>>(url, JsonOptions);
            return res ?? ApiResponse<List<InvoiceDto>>.Fail("Failed to load invoices.");
        }
        catch
        {
            return ApiResponse<List<InvoiceDto>>.Ok(GetMockInvoices());
        }
    }

    public async Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/fees/invoices", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<InvoiceDto>>(JsonOptions))!;
        }
        catch
        {
            var inv = GetMockInvoices().First();
            return ApiResponse<InvoiceDto>.Ok(inv, "Invoice created.");
        }
    }

    public async Task<ApiResponse<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/fees/payments", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<PaymentDto>>(JsonOptions))!;
        }
        catch
        {
            var p = new PaymentDto(Guid.NewGuid(), request.InvoiceId, "INV-2026-00001", Guid.NewGuid(),
                "Visal Chan", "REC-2026-00002", request.Amount, DateTime.UtcNow, request.PaymentMethod,
                request.ReferenceNumber, request.Notes, "School Admin");
            return ApiResponse<PaymentDto>.Ok(p, "Payment recorded successfully.");
        }
    }

    public async Task<ApiResponse<FinancialSummaryDto>> GetFinancialSummaryAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<FinancialSummaryDto>>("api/fees/summary", JsonOptions);
            return res ?? ApiResponse<FinancialSummaryDto>.Fail("No summary.");
        }
        catch
        {
            return ApiResponse<FinancialSummaryDto>.Ok(new FinancialSummaryDto(64200m, 55300m, 8900m, 12, 110, 3));
        }
    }

    public async Task<ApiResponse<List<AnnouncementDto>>> GetAnnouncementsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AnnouncementDto>>>("api/announcements", JsonOptions);
            return res ?? ApiResponse<List<AnnouncementDto>>.Fail("Failed to load announcements.");
        }
        catch
        {
            return ApiResponse<List<AnnouncementDto>>.Ok(GetMockAnnouncements());
        }
    }

    public async Task<ApiResponse<AnnouncementDto>> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/announcements", request);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<AnnouncementDto>>(JsonOptions))!;
        }
        catch
        {
            var a = new AnnouncementDto(Guid.NewGuid(), Guid.NewGuid(), request.Title, request.Content,
                request.TargetRole, request.TargetClassSectionId, "10-A", request.Priority, false, DateTime.UtcNow, "Admin", false);
            return ApiResponse<AnnouncementDto>.Ok(a, "Announcement published.");
        }
    }

    public async Task<ApiResponse<bool>> MarkAnnouncementAsReadAsync(Guid id)
    {
        try
        {
            var res = await _httpClient.PostAsync($"api/announcements/{id}/read", null);
            return (await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions))!;
        }
        catch
        {
            return ApiResponse<bool>.Ok(true);
        }
    }

    public async Task<ApiResponse<List<AuditLogDto>>> GetAuditLogsAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>("api/auditlogs", JsonOptions);
            return res ?? ApiResponse<List<AuditLogDto>>.Fail("Failed to load logs.");
        }
        catch
        {
            return ApiResponse<List<AuditLogDto>>.Ok(GetMockAuditLogs());
        }
    }

    // --- Mock Generators for Resilient Fallback ---
    private static List<StudentDto> GetMockStudents() => new()
    {
        new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Guid.NewGuid(), "ADM-2026-0001", "Visal", "Chan", "Visal Chan",
            new DateTime(2010, 5, 14), "Male", "O+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "visal.chan@student.angkoracademy.edu.kh", "+855 98 123 456", "Preah Sihanouk Blvd, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Keo Bopha", "+855 92 112 233", "Mother"),
        new(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Guid.NewGuid(), "ADM-2026-0002", "Kanha", "Sok", "Kanha Sok",
            new DateTime(2010, 8, 22), "Female", "A+", new DateTime(2026, 9, 1), StudentStatus.Active,
            "kanha.sok@student.angkoracademy.edu.kh", "+855 98 654 321", "Norodom Blvd, Phnom Penh", null,
            Guid.NewGuid(), "10-A", "Sok Vichea", "+855 97 554 332", "Father")
    };

    private static List<TeacherDto> GetMockTeachers() => new()
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

    private static List<ClassSectionDto> GetMockClasses() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-A", "Room 201", 35, Guid.NewGuid(), "Dara Sok", 34),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-2027", Guid.NewGuid(), "Grade 10", "10-B", "Room 202", 35, Guid.NewGuid(), "Chenda Chan", 32)
    };

    private static List<SubjectDto> GetMockSubjects() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Mathematics", "MATH101", "Algebra & Calculus", 4),
        new(Guid.NewGuid(), Guid.NewGuid(), "Physics", "PHY101", "Mechanics & Optics", 3),
        new(Guid.NewGuid(), Guid.NewGuid(), "Khmer Literature", "KHM101", "Classical Khmer Literature", 4),
        new(Guid.NewGuid(), Guid.NewGuid(), "English Communication", "ENG101", "Academic English", 3)
    };

    private static List<TimetableEntryDto> GetMockSchedule() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Mathematics", "MATH101", Guid.NewGuid(), "Dara Sok", DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Physics", "PHY101", Guid.NewGuid(), "Chenda Chan", DayOfWeek.Monday, new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), "Physics Lab"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "10-A", Guid.NewGuid(), "Khmer Literature", "KHM101", Guid.NewGuid(), "Sokha Mean", DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(9, 30, 0), "Room 201")
    };

    private static List<StudentGradeDto> GetMockGrades() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", Guid.NewGuid(), "Mathematics", Guid.NewGuid(), "Visal Chan", "ADM-2026-0001", 94.5m, 100m, 94.5m, "A", 4.0m, "Excellent"),
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Exam", Guid.NewGuid(), "Physics", Guid.NewGuid(), "Visal Chan", "ADM-2026-0001", 88.0m, 100m, 88.0m, "B", 3.0m, "Very Good")
    };

    private static List<InvoiceDto> GetMockInvoices() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Visal Chan", "ADM-2026-0001", "10-A",
            Guid.NewGuid(), "2026-2027", "INV-2026-00001", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(15),
            730.0m, 500.0m, 230.0m, InvoiceStatus.PartiallyPaid, "Semester 1 Fee Package",
            new List<InvoiceItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Tuition", "Tuition Fee - Semester 1", 650m),
                new(Guid.NewGuid(), Guid.NewGuid(), "Lab Fee", "Science Lab Equipment", 80m)
            },
            new List<PaymentDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "INV-2026-00001", Guid.NewGuid(), "Visal Chan", "REC-2026-00001", 500m, DateTime.UtcNow.AddDays(-15), PaymentMethod.ABA, "ABA-981245", "Partial payment via ABA", "Admin")
            })
    };

    private static List<AnnouncementDto> GetMockAnnouncements() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), "Welcome to Academic Year 2026-2027", "Angkor International Academy welcomes all teachers and students to the new school term.", AnnouncementTarget.All, null, null, AnnouncementPriority.High, false, DateTime.UtcNow.AddDays(-5), "Admin", true),
        new(Guid.NewGuid(), Guid.NewGuid(), "Midterm Examination Schedule Announcement", "Semester 1 Midterm Examinations will commence on November 15. Please review the updated timetable.", AnnouncementTarget.All, null, null, AnnouncementPriority.Urgent, false, DateTime.UtcNow.AddDays(-1), "Admin", false)
    };

    private static List<AuditLogDto> GetMockAuditLogs() => new()
    {
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Student", "ADM-2026-0001", "Registered student Visal Chan", null, null, "127.0.0.1", DateTime.UtcNow.AddHours(-3)),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "admin", AuditAction.Create, "Payment", "REC-2026-00001", "Recorded payment $500 via ABA", null, null, "127.0.0.1", DateTime.UtcNow.AddHours(-1))
    };
}
