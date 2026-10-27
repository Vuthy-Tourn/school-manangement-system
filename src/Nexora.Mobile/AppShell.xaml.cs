using Nexora.Domain.Enums;
using Nexora.Mobile.Services;
using Nexora.Mobile.Views;

namespace Nexora.Mobile;

public partial class AppShell : Shell
{
    private readonly IApiService _apiService;

    public AppShell(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;

        // Register auxiliary / sub-routes
        Routing.RegisterRoute("student-form", typeof(StudentFormPage));
        Routing.RegisterRoute("teacher-form", typeof(TeacherFormPage));
        Routing.RegisterRoute("dashboard", typeof(ParentDashboardPage));
        Routing.RegisterRoute("attendance", typeof(AttendancePage));
        Routing.RegisterRoute("grades", typeof(GradesPage));
        Routing.RegisterRoute("timetables", typeof(TimetablesPage));
        Routing.RegisterRoute("fees", typeof(FeesPage));
        Routing.RegisterRoute("announcements", typeof(AnnouncementsPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("students", typeof(StudentsPage));
        Routing.RegisterRoute("teachers", typeof(TeachersPage));

        ConfigureRoleNavigation();
    }

    private void ConfigureRoleNavigation()
    {
        Items.Clear();

        var role = _apiService.CurrentUser?.Role ?? UserRole.Student;
        var tabBar = new TabBar();

        switch (role)
        {
            case UserRole.Teacher:
                tabBar.Items.Add(CreateTab("Dashboard", "icon_dashboard.png", "dashboard", typeof(TeacherDashboardPage)));
                tabBar.Items.Add(CreateTab("Attendance", "icon_attendance.png", "attendance", typeof(AttendancePage)));
                tabBar.Items.Add(CreateTab("Grades", "icon_grades.png", "grades", typeof(GradesPage)));
                tabBar.Items.Add(CreateTab("Timetable", "icon_timetable.png", "timetables", typeof(TimetablesPage)));
                tabBar.Items.Add(CreateTab("Updates", "icon_announcements.png", "announcements", typeof(AnnouncementsPage)));
                tabBar.Items.Add(CreateTab("Profile", "icon_profile.png", "profile", typeof(ProfilePage)));
                break;

            case UserRole.Student:
                tabBar.Items.Add(CreateTab("Dashboard", "icon_dashboard.png", "dashboard", typeof(StudentDashboardPage)));
                tabBar.Items.Add(CreateTab("Timetable", "icon_timetable.png", "timetables", typeof(TimetablesPage)));
                tabBar.Items.Add(CreateTab("Grades", "icon_grades.png", "grades", typeof(GradesPage)));
                tabBar.Items.Add(CreateTab("Fees", "icon_fees.png", "fees", typeof(FeesPage)));
                tabBar.Items.Add(CreateTab("Updates", "icon_announcements.png", "announcements", typeof(AnnouncementsPage)));
                tabBar.Items.Add(CreateTab("Profile", "icon_profile.png", "profile", typeof(ProfilePage)));
                break;

            case UserRole.Parent:
                tabBar.Items.Add(CreateTab("Dashboard", "icon_dashboard.png", "dashboard", typeof(ParentDashboardPage)));
                tabBar.Items.Add(CreateTab("Attendance", "icon_attendance.png", "attendance", typeof(AttendancePage)));
                tabBar.Items.Add(CreateTab("Grades", "icon_grades.png", "grades", typeof(GradesPage)));
                tabBar.Items.Add(CreateTab("Fees", "icon_fees.png", "fees", typeof(FeesPage)));
                tabBar.Items.Add(CreateTab("Timetable", "icon_timetable.png", "timetables", typeof(TimetablesPage)));
                tabBar.Items.Add(CreateTab("Updates", "icon_announcements.png", "announcements", typeof(AnnouncementsPage)));
                tabBar.Items.Add(CreateTab("Profile", "icon_profile.png", "profile", typeof(ProfilePage)));
                break;

            case UserRole.SuperAdmin:
            case UserRole.SchoolAdmin:
            default:
                tabBar.Items.Add(CreateTab("Dashboard", "icon_dashboard.png", "dashboard", typeof(AdminDashboardPage)));
                tabBar.Items.Add(CreateTab("Students", "icon_students.png", "students", typeof(StudentsPage)));
                tabBar.Items.Add(CreateTab("Teachers", "icon_faculty.png", "teachers", typeof(TeachersPage)));
                tabBar.Items.Add(CreateTab("Attendance", "icon_attendance.png", "attendance", typeof(AttendancePage)));
                tabBar.Items.Add(CreateTab("Grades", "icon_grades.png", "grades", typeof(GradesPage)));
                tabBar.Items.Add(CreateTab("Fees", "icon_fees.png", "fees", typeof(FeesPage)));
                tabBar.Items.Add(CreateTab("Timetable", "icon_timetable.png", "timetables", typeof(TimetablesPage)));
                tabBar.Items.Add(CreateTab("Updates", "icon_announcements.png", "announcements", typeof(AnnouncementsPage)));
                tabBar.Items.Add(CreateTab("Profile", "icon_profile.png", "profile", typeof(ProfilePage)));
                break;
        }

        Items.Add(tabBar);
    }

    private static Tab CreateTab(string title, string icon, string route, Type pageType)
    {
        var tab = new Tab
        {
            Title = title,
            Icon = icon,
            Route = route
        };
        tab.Items.Add(new ShellContent
        {
            Title = title,
            Route = route,
            ContentTemplate = new DataTemplate(pageType)
        });
        return tab;
    }
}