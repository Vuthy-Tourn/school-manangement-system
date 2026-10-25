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
    public decimal Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage:F1}%";
    public double BarHeight => (double)Math.Clamp(Percentage * 1.15m, 20m, 120m);
    public bool IsPeak { get; set; }
    public Color BarColor => IsPeak ? Color.FromArgb("#16A34A") : Color.FromArgb("#1E40AF");
    public Color TrackColor => IsPeak ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#DBEAFE");
    public Color BadgeColor => IsPeak ? Color.FromArgb("#15803D") : Color.FromArgb("#1E40AF");
}

public class GradeDistributionSegment
{
    public string Grade { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage}%";
    public Color SegmentColor { get; set; } = Colors.Gray;
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
            new() { Day = "Mon", Percentage = 98.0m, IsPeak = false },
            new() { Day = "Tue", Percentage = 96.5m, IsPeak = false },
            new() { Day = "Wed", Percentage = 100.0m, IsPeak = true },
            new() { Day = "Thu", Percentage = 97.5m, IsPeak = false },
            new() { Day = "Fri", Percentage = 99.0m, IsPeak = false }
        };

        GradeDistribution = new ObservableCollection<GradeDistributionSegment>
        {
            new() { Grade = "Grade A", Description = "Distinction (90-100%)", Percentage = 48, SegmentColor = Color.FromArgb("#15803D") },
            new() { Grade = "Grade B", Description = "Merit (80-89%)", Percentage = 34, SegmentColor = Color.FromArgb("#2563EB") },
            new() { Grade = "Grade C", Description = "Satisfactory (70-79%)", Percentage = 14, SegmentColor = Color.FromArgb("#D97706") },
            new() { Grade = "Grade D/F", Description = "Review Needed (<70%)", Percentage = 4, SegmentColor = Color.FromArgb("#DC2626") }
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
                    new("Mon", 98.0m),
                    new("Tue", 96.5m),
                    new("Wed", 100.0m),
                    new("Thu", 97.5m),
                    new("Fri", 99.0m)
                };

                var maxVal = trends.Max(t => t.Percentage);
                WeeklyAttendance = new ObservableCollection<AttendanceChartColumn>(
                    trends.Select(t => new AttendanceChartColumn
                    {
                        Day = t.Day,
                        Percentage = t.Percentage,
                        IsPeak = t.Percentage == maxVal
                    })
                );

                if (WeeklyAttendance.Count > 0)
                {
                    WeeklyAverageAttendance = Math.Round(WeeklyAttendance.Average(x => x.Percentage), 1);
                }

                // Update Fee Collection Graph
                var totalFees = res.Data.TotalFeesCollected + res.Data.TotalFeesOutstanding;
                if (totalFees > 0)
                {
                    FeeCollectionPercentage = Math.Round((res.Data.TotalFeesCollected / totalFees) * 100m, 1);
                    var collectedRatio = Math.Clamp((int)FeeCollectionPercentage, 1, 99);
                    CollectedWidth = new GridLength(collectedRatio, GridUnitType.Star);
                    OutstandingWidth = new GridLength(100 - collectedRatio, GridUnitType.Star);
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
        await Shell.Current.GoToAsync($"///{route}");
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
        await Shell.Current.GoToAsync($"///{route}");
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
        await Shell.Current.GoToAsync($"///{route}");
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
    private LinkedStudentDto? _selectedChild;

    [ObservableProperty]
    private ObservableCollection<StudentGradeDto> _recentGrades = new();

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
                SelectedChild = Children.FirstOrDefault(c => c.StudentId == res.Data.SelectedStudentId) ?? Children.FirstOrDefault();
                RecentGrades = new ObservableCollection<StudentGradeDto>(res.Data.RecentGrades);
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
    public async Task SelectChildAsync(LinkedStudentDto child)
    {
        if (child == null) return;
        SelectedChild = child;
        await LoadDashboardAsync(child.StudentId);
    }

    [RelayCommand]
    private async Task NavigateToAsync(string route)
    {
        await Shell.Current.GoToAsync($"///{route}");
    }
}
