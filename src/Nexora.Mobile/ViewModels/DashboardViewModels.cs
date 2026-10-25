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
    private ObservableCollection<TimetableEntryDto> _todaySchedule = new();

    [ObservableProperty]
    private ObservableCollection<ClassSectionDto> _assignedClasses = new();

    public TeacherDashboardViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Teacher Portal";
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
                TodaySchedule = new ObservableCollection<TimetableEntryDto>(res.Data.TodaySchedule);
                AssignedClasses = new ObservableCollection<ClassSectionDto>(res.Data.ClassesList);
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
