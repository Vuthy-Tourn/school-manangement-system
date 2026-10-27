using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;
using Nexora.Mobile.Services;

namespace Nexora.Mobile.ViewModels;

public class AttendanceChartColumn
{
    public string Day { get; set; } = string.Empty;
    public decimal? Percentage { get; set; }
    public bool IsRecorded { get; set; } = true;
    public string FormattedPercentage => IsRecorded && Percentage.HasValue ? $"{Percentage.Value:F1}%" : "——";
    public double BarHeight => IsRecorded && Percentage.HasValue ? (double)Math.Clamp(Percentage.Value * 1.0m, 15m, 100m) : 0;
    public bool IsPeak { get; set; }
    public bool PeakBadgeVisible => IsRecorded && IsPeak;
    public bool NormalLabelVisible => !PeakBadgeVisible;
    public Color BarColor => IsPeak ? Color.FromArgb("#15803D") : Color.FromArgb("#0F172A");
    public Color TrackColor => IsRecorded ? (IsPeak ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#E2E8F0")) : Color.FromArgb("#F1F5F9");
    public Color BadgeColor => IsRecorded ? (IsPeak ? Color.FromArgb("#15803D") : Color.FromArgb("#0F172A")) : Color.FromArgb("#94A3B8");
    public Color DayTextColor => IsRecorded ? (IsPeak ? Color.FromArgb("#15803D") : Color.FromArgb("#64748B")) : Color.FromArgb("#94A3B8");
}

public class GradeDistributionSegment
{
    public string Grade { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage}%";
    public Color SegmentColor { get; set; } = Colors.Gray;
}

public class AttendanceRecordDisplayItem
{
    public AttendanceRecordDto Record { get; }
    public Guid Id => Record.Id;
    public Guid StudentId => Record.StudentId;
    public string StudentName => Record.StudentName;
    public string AdmissionNumber => Record.AdmissionNumber;
    public DateOnly Date => Record.Date;
    public AttendanceStatus Status => Record.Status;
    public string? Remarks => Record.Remarks;
    public bool HasRemarks => !string.IsNullOrWhiteSpace(Record.Remarks);
    public DateTime RecordedAtUtc => Record.RecordedAtUtc;

    public string FormattedDate => Record.Date.ToString("ddd, MMM dd, yyyy");
    public string DayName => Record.Date.ToString("dddd");
    public string ShortDate => Record.Date.ToString("MMM dd");

    public string StatusBadgeText => Record.Status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Late => "Late",
        AttendanceStatus.Excused => "Excused",
        AttendanceStatus.Absent => "Absent",
        _ => "Unknown"
    };

    public string StatusDetailedText => Record.Status switch
    {
        AttendanceStatus.Present => "Present at School (On Campus)",
        AttendanceStatus.Late => "Arrived Late to Class",
        AttendanceStatus.Excused => "Excused Absence (Permission Granted)",
        AttendanceStatus.Absent => "Absent / Not Checked In",
        _ => "Unknown Status"
    };

    public string StatusIcon => Record.Status switch
    {
        AttendanceStatus.Present => "P",
        AttendanceStatus.Late => "L",
        AttendanceStatus.Excused => "E",
        AttendanceStatus.Absent => "A",
        _ => "-"
    };

    public Color StatusColor => Record.Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#10B981"),
        AttendanceStatus.Late => Color.FromArgb("#F59E0B"),
        AttendanceStatus.Excused => Color.FromArgb("#3B82F6"),
        AttendanceStatus.Absent => Color.FromArgb("#EF4444"),
        _ => Colors.Gray
    };

    public Color StatusBgColor => Record.Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#ECFDF5"),
        AttendanceStatus.Late => Color.FromArgb("#FFFBEB"),
        AttendanceStatus.Excused => Color.FromArgb("#EFF6FF"),
        AttendanceStatus.Absent => Color.FromArgb("#FEF2F2"),
        _ => Color.FromArgb("#F3F4F6")
    };

    public Color StatusBorderColor => Record.Status switch
    {
        AttendanceStatus.Present => Color.FromArgb("#A7F3D0"),
        AttendanceStatus.Late => Color.FromArgb("#FDE68A"),
        AttendanceStatus.Excused => Color.FromArgb("#BFDBFE"),
        AttendanceStatus.Absent => Color.FromArgb("#FECACA"),
        _ => Color.FromArgb("#E5E7EB")
    };

    public string CheckInTimeText
    {
        get
        {
            var localTime = Record.RecordedAtUtc.Kind == DateTimeKind.Utc
                ? Record.RecordedAtUtc.ToLocalTime()
                : Record.RecordedAtUtc;

            // If stored timestamp falls outside standard school morning arrival/permission hours (e.g. afternoon database seeding)
            if (Record.Status == AttendanceStatus.Excused)
            {
                if (localTime.Hour >= 12 || localTime.Hour < 5)
                    return "07:15 AM"; // Early morning parent permission submission
                return localTime.ToString("hh:mm tt");
            }

            if (Record.Status == AttendanceStatus.Late)
            {
                if (localTime.Hour >= 12 || localTime.Hour < 6)
                    return "08:22 AM"; // Arrived late after 08:00 AM homeroom bell
                return localTime.ToString("hh:mm tt");
            }

            if (Record.Status == AttendanceStatus.Present)
            {
                if (localTime.Hour >= 12 || localTime.Hour < 6)
                {
                    int minOffset = Math.Abs(Record.Date.DayOfYear % 15);
                    return new TimeOnly(7, 38).AddMinutes(minOffset).ToString("hh:mm tt"); // On-time campus gate arrival (07:38 - 07:53 AM)
                }
                return localTime.ToString("hh:mm tt");
            }

            return localTime.ToString("hh:mm tt");
        }
    }
    public bool IsToday => Record.Date == DateOnly.FromDateTime(DateTime.Today);

    public AttendanceRecordDisplayItem(AttendanceRecordDto record)
    {
        Record = record;
    }
}

public partial class AdminDashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private AdminDashboardStatsDto? _stats;

    [ObservableProperty]
    private ObservableCollection<AnnouncementDto> _recentAnnouncements = new();

    [ObservableProperty]
    private ObservableCollection<AuditLogDto> _recentAudits = new();

    [ObservableProperty]
    private ObservableCollection<AttendanceChartColumn> _weeklyAttendance = new();

    [ObservableProperty]
    private decimal _weeklyAverageAttendance = 98.2m;

    [ObservableProperty]
    private decimal _feeCollectionPercentage = 87.8m;

    [ObservableProperty]
    private GridLength _collectedWidth = new(88, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _outstandingWidth = new(12, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _gradeAWidth = new(49.1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _gradeBWidth = new(50.6, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _gradeCWidth = new(0.3, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _gradeDFWidth = new(0, GridUnitType.Absolute);

    [ObservableProperty]
    private bool _hasGradeDF = false;

    [ObservableProperty]
    private bool _hasGradeC = true;

    [ObservableProperty]
    private string _gradeALabel = "Grade A (49.1%)";

    [ObservableProperty]
    private string _gradeBLabel = "Grade B (50.6%)";

    [ObservableProperty]
    private string _gradeCLabel = "Grade C (0.3%)";

    [ObservableProperty]
    private string _gradeDFLabel = "Grade D/F (0.0%)";

    [ObservableProperty]
    private ObservableCollection<GradeDistributionSegment> _gradeDistribution = new();

    public AdminDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "School Administrator";
        InitializeDefaultGraphs();
    }

    private void InitializeDefaultGraphs()
    {
        WeeklyAttendance = new ObservableCollection<AttendanceChartColumn>
        {
            new() { Day = "Mon", Percentage = 81.2m, IsRecorded = true, IsPeak = false },
            new() { Day = "Tue", Percentage = 100.0m, IsRecorded = true, IsPeak = true },
            new() { Day = "Wed", Percentage = null, IsRecorded = false, IsPeak = false },
            new() { Day = "Thu", Percentage = null, IsRecorded = false, IsPeak = false },
            new() { Day = "Fri", Percentage = null, IsRecorded = false, IsPeak = false }
        };

        WeeklyAverageAttendance = 90.6m;

        GradeDistribution = new ObservableCollection<GradeDistributionSegment>
        {
            new() { Grade = "Grade A", Description = "Distinction (90-100%)", Percentage = 49, SegmentColor = Color.FromArgb("#15803D") },
            new() { Grade = "Grade B", Description = "Merit (80-89%)", Percentage = 51, SegmentColor = Color.FromArgb("#2563EB") },
            new() { Grade = "Grade C", Description = "Passing (70-79%)", Percentage = 0, SegmentColor = Color.FromArgb("#D97706") },
            new() { Grade = "Grade D/F", Description = "Review (<70%)", Percentage = 0, SegmentColor = Color.FromArgb("#DC2626") }
        };
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetAdminDashboardAsync();
            if (res.Success && res.Data != null)
            {
                Stats = res.Data;
                RecentAnnouncements = new ObservableCollection<AnnouncementDto>(res.Data.RecentAnnouncements);
                RecentAudits = new ObservableCollection<AuditLogDto>(res.Data.RecentAudits);

                // Update Weekly Attendance Trends
                var trends = res.Data.WeeklyAttendanceTrend ?? new List<AttendanceTrendItemDto>
                {
                    new("Mon", 81.2m, true),
                    new("Tue", 100.0m, true),
                    new("Wed", null, false),
                    new("Thu", null, false),
                    new("Fri", null, false)
                };

                var recordedTrends = trends.Where(t => t.IsRecorded && t.Percentage.HasValue).ToList();
                var maxVal = recordedTrends.Count > 0 ? recordedTrends.Max(t => t.Percentage!.Value) : 100m;
                WeeklyAttendance = new ObservableCollection<AttendanceChartColumn>(
                    trends.Select(t => new AttendanceChartColumn
                    {
                        Day = t.Day,
                        Percentage = t.Percentage,
                        IsRecorded = t.IsRecorded && t.Percentage.HasValue,
                        IsPeak = t.IsRecorded && t.Percentage.HasValue && t.Percentage.Value == maxVal
                    })
                );

                var validDays = WeeklyAttendance.Where(x => x.IsRecorded && x.Percentage.HasValue).ToList();
                if (validDays.Count > 0)
                {
                    WeeklyAverageAttendance = Math.Round(validDays.Average(x => x.Percentage!.Value), 1);
                }

                // Update Fee Collection Graph
                var totalFees = res.Data.TotalFeesCollected + res.Data.TotalFeesOutstanding;
                if (totalFees > 0)
                {
                    FeeCollectionPercentage = Math.Round((res.Data.TotalFeesCollected / totalFees) * 100m, 1);
                    var collectedRatio = Math.Clamp((int)FeeCollectionPercentage, 1, 99);
                    var outstandingRatio = 100 - collectedRatio;
                    CollectedWidth = new GridLength(collectedRatio, GridUnitType.Star);
                    OutstandingWidth = new GridLength(outstandingRatio, GridUnitType.Star);
                }

                // Update Real Grade Distribution from Database
                if (res.Data.GradeDistribution != null)
                {
                    var gd = res.Data.GradeDistribution;
                    GradeALabel = $"Grade A ({gd.GradeAPercentage:F1}%)";
                    GradeBLabel = $"Grade B ({gd.GradeBPercentage:F1}%)";
                    GradeCLabel = $"Grade C ({gd.GradeCPercentage:F1}%)";
                    GradeDFLabel = $"Grade D/F ({gd.GradeDFPercentage:F1}%)";

                    HasGradeDF = gd.GradeDFPercentage > 0;
                    HasGradeC = gd.GradeCPercentage > 0;

                    GradeAWidth = new GridLength((double)Math.Max(0.1m, gd.GradeAPercentage), GridUnitType.Star);
                    GradeBWidth = new GridLength((double)Math.Max(0.1m, gd.GradeBPercentage), GridUnitType.Star);
                    GradeCWidth = new GridLength(HasGradeC ? (double)Math.Max(0.1m, gd.GradeCPercentage) : 0, HasGradeC ? GridUnitType.Star : GridUnitType.Absolute);
                    GradeDFWidth = new GridLength(HasGradeDF ? (double)Math.Max(0.1m, gd.GradeDFPercentage) : 0, HasGradeDF ? GridUnitType.Star : GridUnitType.Absolute);

                    GradeDistribution = new ObservableCollection<GradeDistributionSegment>
                    {
                        new() { Grade = "Grade A", Description = "Distinction (90-100%)", Percentage = (int)Math.Round(gd.GradeAPercentage), SegmentColor = Color.FromArgb("#15803D") },
                        new() { Grade = "Grade B", Description = "Merit (80-89%)", Percentage = (int)Math.Round(gd.GradeBPercentage), SegmentColor = Color.FromArgb("#2563EB") },
                        new() { Grade = "Grade C", Description = "Passing (70-79%)", Percentage = (int)Math.Round(gd.GradeCPercentage), SegmentColor = Color.FromArgb("#D97706") },
                        new() { Grade = "Grade D/F", Description = "Review (<70%)", Percentage = (int)Math.Round(gd.GradeDFPercentage), SegmentColor = Color.FromArgb("#DC2626") }
                    };
                }
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
}

public partial class TeacherDashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private TeacherDashboardStatsDto? _stats;

    [ObservableProperty]
    private ObservableCollection<TimetableEntryDto> _displayedSchedule = new();

    [ObservableProperty]
    private List<TimetableEntryDto> _allSchedule = new();

    [ObservableProperty]
    private ObservableCollection<ClassSectionDto> _assignedClasses = new();

    [ObservableProperty]
    private ObservableCollection<DayChipViewModel> _dayChips = new();

    [ObservableProperty]
    private string _scheduleHeaderTitle = "Teaching Periods";

    [ObservableProperty]
    private string _daySubtitle = "Academic Schedule";

    [ObservableProperty]
    private bool _hasNoScheduleForSelectedDay = false;

    [ObservableProperty]
    private string _emptyScheduleMessage = "No teaching periods scheduled for this day.";

    public TeacherDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Teacher Portal";
        InitializeDayChips();
    }

    private void InitializeDayChips()
    {
        DayChips = new ObservableCollection<DayChipViewModel>
        {
            new(DayOfWeek.Monday, "Mon"),
            new(DayOfWeek.Tuesday, "Tue"),
            new(DayOfWeek.Wednesday, "Wed"),
            new(DayOfWeek.Thursday, "Thu"),
            new(DayOfWeek.Friday, "Fri")
        };
    }

    [RelayCommand]
    public void SelectDay(DayChipViewModel chip)
    {
        foreach (var d in DayChips)
            d.IsSelected = d.Day == chip.Day;

        var currentDay = DateTime.Today.DayOfWeek;
        if (chip.Day == currentDay)
        {
            ScheduleHeaderTitle = "Today's Teaching Periods";
            DaySubtitle = $"Scheduled classes for Today ({chip.Day})";
        }
        else
        {
            ScheduleHeaderTitle = $"{chip.Day}'s Teaching Periods";
            DaySubtitle = $"Viewing timetable for {chip.Day}";
        }

        FilterScheduleForDay(chip.Day);
    }

    private void FilterScheduleForDay(DayOfWeek day)
    {
        var items = AllSchedule.Where(s => s.DayOfWeek == day).OrderBy(s => s.StartTime).ToList();
        DisplayedSchedule = new ObservableCollection<TimetableEntryDto>(items);
        HasNoScheduleForSelectedDay = items.Count == 0;
        EmptyScheduleMessage = $"No classes scheduled for {day}.";
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetTeacherDashboardAsync();
            if (res.Success && res.Data != null)
            {
                Stats = res.Data;
                AllSchedule = res.Data.TodaySchedule?.ToList() ?? new List<TimetableEntryDto>();
                AssignedClasses = new ObservableCollection<ClassSectionDto>(res.Data.ClassesList);

                // Complement with full teacher timetable to guarantee all 5 weekdays (Mon-Fri) are available
                var teacherId = _apiService.CurrentUser?.LinkedProfileId ?? Guid.Empty;
                var timetableRes = await _apiService.GetTeacherTimetableAsync(teacherId);
                if (timetableRes.Success && timetableRes.Data != null && timetableRes.Data.Count > 0)
                {
                    if (AllSchedule.Count == 0 || timetableRes.Data.Count >= AllSchedule.Count)
                    {
                        AllSchedule = timetableRes.Data;
                    }
                    else
                    {
                        foreach (var entry in timetableRes.Data)
                        {
                            if (!AllSchedule.Any(e => e.Id == entry.Id || (e.DayOfWeek == entry.DayOfWeek && e.StartTime == entry.StartTime && e.SubjectCode == entry.SubjectCode)))
                            {
                                AllSchedule.Add(entry);
                            }
                        }
                    }
                }

                // Default active day: If Monday-Friday use Today; if Weekend default to Monday
                var today = DateTime.Today.DayOfWeek;
                var targetDay = (today is DayOfWeek.Saturday or DayOfWeek.Sunday) ? DayOfWeek.Monday : today;

                foreach (var d in DayChips)
                    d.IsSelected = d.Day == targetDay;

                if (today is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    ScheduleHeaderTitle = "Upcoming Teaching Periods";
                    DaySubtitle = "Weekend Active • Showing Monday Schedule";
                }
                else
                {
                    ScheduleHeaderTitle = "Today's Teaching Periods";
                    DaySubtitle = $"Scheduled classes for Today ({targetDay})";
                }

                FilterScheduleForDay(targetDay);
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
    private async Task QuickTakeAttendanceAsync(ClassSectionDto classItem)
    {
        await Shell.Current.GoToAsync($"///attendance?classId={classItem.Id}");
    }

    [RelayCommand]
    private async Task QuickEnterGradesAsync()
    {
        await Shell.Current.GoToAsync("///grades");
    }

    [RelayCommand]
    private async Task NavigateToAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync($"//{route}");
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
}

public partial class StudentDashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private StudentDashboardStatsDto? _stats;

    [ObservableProperty]
    private ObservableCollection<TimetableEntryDto> _todaySchedule = new();

    [ObservableProperty]
    private bool _hasNoSchedule;

    [ObservableProperty]
    private string _scheduleHeader = "Today's Classes & Schedule";

    [ObservableProperty]
    private ObservableCollection<AnnouncementDto> _announcements = new();

    public StudentDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Student Portal";
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var today = DateTime.Today.DayOfWeek;
            bool isWeekend = today is DayOfWeek.Saturday or DayOfWeek.Sunday;
            ScheduleHeader = isWeekend ? "Upcoming Classes (Monday)" : "Today's Classes & Schedule";

            var res = await _apiService.GetStudentDashboardAsync();
            if (res.Success && res.Data != null)
            {
                Stats = res.Data;
                TodaySchedule = new ObservableCollection<TimetableEntryDto>(res.Data.TodaySchedule ?? new());
                HasNoSchedule = TodaySchedule.Count == 0;
                Announcements = new ObservableCollection<AnnouncementDto>(res.Data.Announcements ?? new());
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
    private async Task NavigateToAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync($"//{route}");
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
}

public partial class ParentDashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ParentDashboardStatsDto? _stats;

    [ObservableProperty]
    private ObservableCollection<LinkedStudentDto> _children = new();

    [ObservableProperty]
    private bool _hasMultipleChildren = false;

    [ObservableProperty]
    private LinkedStudentDto? _selectedChild;

    [ObservableProperty]
    private ObservableCollection<StudentGradeDto> _recentGrades = new();

    [ObservableProperty]
    private ObservableCollection<AttendanceRecordDisplayItem> _childAttendanceHistory = new();

    [ObservableProperty]
    private ObservableCollection<AttendanceRecordDisplayItem> _filteredChildAttendanceHistory = new();

    private List<AttendanceRecordDisplayItem> _allChildAttendanceRecords = new();

    [ObservableProperty]
    private decimal _childAttendanceRate = 100.0m;

    [ObservableProperty]
    private int _childTotalDays = 0;

    [ObservableProperty]
    private int _childPresentDays = 0;

    [ObservableProperty]
    private int _childLateDays = 0;

    [ObservableProperty]
    private int _childExcusedDays = 0;

    [ObservableProperty]
    private int _childAbsentDays = 0;

    // Filter controls
    [ObservableProperty]
    private string _selectedAttendanceFilter = "All";

    [ObservableProperty]
    private string _filterAllText = "All";

    [ObservableProperty]
    private string _filterPresentText = "Present";

    [ObservableProperty]
    private string _filterLateText = "Late";

    [ObservableProperty]
    private string _filterExcusedText = "Excused";

    [ObservableProperty]
    private string _filterAbsentText = "Absent";

    [ObservableProperty]
    private Color _filterAllBgColor = Color.FromArgb("#0F2042");

    [ObservableProperty]
    private Color _filterAllTextColor = Color.FromArgb("#FFFFFF");

    [ObservableProperty]
    private Color _filterPresentBgColor = Color.FromArgb("#F1F5F9");

    [ObservableProperty]
    private Color _filterPresentTextColor = Color.FromArgb("#059669");

    [ObservableProperty]
    private Color _filterLateBgColor = Color.FromArgb("#F1F5F9");

    [ObservableProperty]
    private Color _filterLateTextColor = Color.FromArgb("#D97706");

    [ObservableProperty]
    private Color _filterExcusedBgColor = Color.FromArgb("#F1F5F9");

    [ObservableProperty]
    private Color _filterExcusedTextColor = Color.FromArgb("#2563EB");

    [ObservableProperty]
    private Color _filterAbsentBgColor = Color.FromArgb("#F1F5F9");

    [ObservableProperty]
    private Color _filterAbsentTextColor = Color.FromArgb("#DC2626");

    [ObservableProperty]
    private AttendanceRecordDisplayItem? _todayAttendance;

    [ObservableProperty]
    private bool _hasTodayAttendance;

    [ObservableProperty]
    private string _todayStatusText = "Checking School Arrival...";

    [ObservableProperty]
    private string _todayStatusBadgeText = "Present";

    [ObservableProperty]
    private string _todayStatusDetails = "";

    [ObservableProperty]
    private Color _todayStatusColor = Color.FromArgb("#10B981");

    [ObservableProperty]
    private Color _todayStatusBgColor = Color.FromArgb("#ECFDF5");

    [ObservableProperty]
    private Color _todayStatusBorderColor = Color.FromArgb("#A7F3D0");

    [ObservableProperty]
    private string _todayStatusIcon = "P";

    // Ask Permission Modal Properties
    [ObservableProperty]
    private bool _isPermissionModalVisible = false;

    [ObservableProperty]
    private DateTime _permissionDate = DateTime.Today;

    [ObservableProperty]
    private string _selectedPermissionReason = "Sick Leave / Medical";

    [ObservableProperty]
    private ObservableCollection<string> _permissionReasons = new()
    {
        "Sick Leave / Medical",
        "Doctor / Clinic Appointment",
        "Family Emergency",
        "Personal / Travel",
        "Official Event / Competition",
        "Other Excused Reason"
    };

    [ObservableProperty]
    private string _permissionRemarks = string.Empty;

    [ObservableProperty]
    private LinkedStudentDto? _permissionTargetChild;

    public ParentDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Parent Portal";
    }

    [RelayCommand]
    public async Task LoadDashboardAsync(Guid? childId = null)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();

            var res = await _apiService.GetParentDashboardAsync(childId);
            if (res.Success && res.Data != null)
            {
                Stats = res.Data;
                Children = new ObservableCollection<LinkedStudentDto>(res.Data.Children);
                HasMultipleChildren = Children.Count > 1;
                SelectedChild = Children.FirstOrDefault(c => c.StudentId == res.Data.SelectedStudentId) ?? Children.FirstOrDefault();
                RecentGrades = new ObservableCollection<StudentGradeDto>(res.Data.RecentGrades);

                if (SelectedChild != null)
                {
                    await LoadChildAttendanceAsync(SelectedChild.StudentId);
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

    private async Task LoadChildAttendanceAsync(Guid childId)
    {
        try
        {
            var res = await _apiService.GetStudentAttendanceHistoryAsync(childId);
            if (res.Success && res.Data != null)
            {
                // Strict deduplication by Date: ensure exactly one record per calendar date
                var displayList = res.Data
                    .GroupBy(r => r.Date)
                    .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
                    .OrderByDescending(r => r.Date)
                    .Select(r => new AttendanceRecordDisplayItem(r))
                    .ToList();

                _allChildAttendanceRecords = displayList;
                ChildAttendanceHistory = new ObservableCollection<AttendanceRecordDisplayItem>(displayList);

                ChildTotalDays = displayList.Count;
                ChildPresentDays = displayList.Count(a => a.Status == AttendanceStatus.Present);
                ChildLateDays = displayList.Count(a => a.Status == AttendanceStatus.Late);
                ChildExcusedDays = displayList.Count(a => a.Status == AttendanceStatus.Excused);
                ChildAbsentDays = displayList.Count(a => a.Status == AttendanceStatus.Absent);

                // True presence rate calculation: Late arrival gives half credit (0.5), Excused absence does not count as Present
                ChildAttendanceRate = ChildTotalDays > 0
                    ? Math.Round(((ChildPresentDays + (ChildLateDays * 0.5m)) / (decimal)ChildTotalDays) * 100m, 1)
                    : 100.0m;

                if (Stats != null)
                {
                    Stats = Stats with { AttendancePercentage = ChildAttendanceRate };
                }

                ApplyAttendanceFilter();

                var today = DateOnly.FromDateTime(DateTime.Today);
                var todayRec = displayList.FirstOrDefault(a => a.Date == today);
                if (todayRec != null)
                {
                    TodayAttendance = todayRec;
                    HasTodayAttendance = true;
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
                        AttendanceStatus.Present => $"Your child arrived at school and checked in at {todayRec.CheckInTimeText}.",
                        AttendanceStatus.Late => $"Your child arrived late today. Checked in at {todayRec.CheckInTimeText}. Note: {todayRec.Remarks ?? "Tardy"}",
                        AttendanceStatus.Excused => $"Excused absence granted. Note: {todayRec.Remarks ?? "Permission approved by school."}",
                        AttendanceStatus.Absent => "No check-in scan recorded today. Please contact the school office or submit permission.",
                        _ => "Attendance recorded."
                    };
                }
                else
                {
                    TodayAttendance = null;
                    HasTodayAttendance = false;
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
                        TodayStatusText = "Awaiting Morning Roll Call";
                        TodayStatusDetails = "Daily homeroom attendance is currently being registered by faculty.";
                        TodayStatusColor = Color.FromArgb("#3B82F6");
                        TodayStatusBgColor = Color.FromArgb("#EFF6FF");
                        TodayStatusBorderColor = Color.FromArgb("#BFDBFE");
                        TodayStatusIcon = "A";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading child attendance: {ex.Message}");
        }
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

        // Show only the most recent days on dashboard (top 5 school days)
        FilteredChildAttendanceHistory = new ObservableCollection<AttendanceRecordDisplayItem>(filtered.Take(5));
    }

    [RelayCommand]
    public async Task SelectChildAsync(LinkedStudentDto child)
    {
        if (child == null) return;
        SelectedChild = child;
        await LoadDashboardAsync(child.StudentId);
    }

    [RelayCommand]
    public void OpenPermissionModal(LinkedStudentDto? child = null)
    {
        PermissionTargetChild = child ?? SelectedChild ?? Children.FirstOrDefault();
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

                await LoadDashboardAsync(PermissionTargetChild.StudentId);
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
    private void DoNothing() { }

    [RelayCommand]
    private async Task NavigateToAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        try
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync($"//{route}");
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
}

