using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;
using Nexora.Mobile.Services;

namespace Nexora.Mobile.ViewModels;

public partial class AdminDashboardViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private AdminDashboardStatsDto? _stats;

    [ObservableProperty]
    private ObservableCollection<AnnouncementDto> _recentAnnouncements = new();

    [ObservableProperty]
    private ObservableCollection<AuditLogDto> _recentAudits = new();

    public AdminDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "School Administrator";
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

            var res = await _apiService.GetStudentDashboardAsync();
            if (res.Success && res.Data != null)
            {
                Stats = res.Data;
                TodaySchedule = new ObservableCollection<TimetableEntryDto>(res.Data.TodaySchedule);
                Announcements = new ObservableCollection<AnnouncementDto>(res.Data.Announcements);
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
