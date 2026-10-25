using Nexora.Mobile.Services;
using Nexora.Mobile.ViewModels;
using Nexora.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace Nexora.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Poppins-Regular.ttf", "Poppins");
                fonts.AddFont("Poppins-Medium.ttf", "PoppinsMedium");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemiBold");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // 1. HTTP Client & API Service
        builder.Services.AddHttpClient<IApiService, ApiService>();

        // 2. ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<AdminDashboardViewModel>();
        builder.Services.AddTransient<TeacherDashboardViewModel>();
        builder.Services.AddTransient<StudentDashboardViewModel>();
        builder.Services.AddTransient<ParentDashboardViewModel>();
        builder.Services.AddTransient<StudentsViewModel>();
        builder.Services.AddTransient<StudentFormViewModel>();
        builder.Services.AddTransient<TeachersViewModel>();
        builder.Services.AddTransient<ClassesViewModel>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<GradesViewModel>();
        builder.Services.AddTransient<FeesViewModel>();
        builder.Services.AddTransient<TimetablesViewModel>();
        builder.Services.AddTransient<AnnouncementsViewModel>();
        builder.Services.AddTransient<AuditLogsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        // 3. Pages
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AdminDashboardPage>();
        builder.Services.AddTransient<StudentsPage>();
        builder.Services.AddTransient<StudentFormPage>();
        builder.Services.AddTransient<AttendancePage>();
        builder.Services.AddTransient<GradesPage>();
        builder.Services.AddTransient<FeesPage>();
        builder.Services.AddTransient<TimetablesPage>();
        builder.Services.AddTransient<AnnouncementsPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}