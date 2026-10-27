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

        // 1. HTTP Client & API Service (Registered as Singleton so all ViewModels and Pages share authenticated state)
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IApiService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient();
            return new ApiService(client);
        });

        // 2. ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<AdminDashboardViewModel>();
        builder.Services.AddTransient<TeacherDashboardViewModel>();
        builder.Services.AddTransient<StudentDashboardViewModel>();
        builder.Services.AddTransient<ParentDashboardViewModel>();
        builder.Services.AddTransient<StudentsViewModel>();
        builder.Services.AddTransient<StudentFormViewModel>();
        builder.Services.AddTransient<TeachersViewModel>();
        builder.Services.AddTransient<TeacherFormViewModel>();
        builder.Services.AddTransient<ClassesViewModel>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<GradesViewModel>();
        builder.Services.AddTransient<FeesViewModel>();
        builder.Services.AddTransient<TimetablesViewModel>();
        builder.Services.AddTransient<AnnouncementsViewModel>();
        builder.Services.AddTransient<AuditLogsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<StudentDetailViewModel>();
        builder.Services.AddTransient<TeacherDetailViewModel>();
        builder.Services.AddTransient<ReportCardViewModel>();

        // 3. Pages
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AdminDashboardPage>();
        builder.Services.AddTransient<TeacherDashboardPage>();
        builder.Services.AddTransient<StudentDashboardPage>();
        builder.Services.AddTransient<ParentDashboardPage>();
        builder.Services.AddTransient<StudentsPage>();
        builder.Services.AddTransient<StudentFormPage>();
        builder.Services.AddTransient<StudentDetailPage>();
        builder.Services.AddTransient<TeachersPage>();
        builder.Services.AddTransient<TeacherFormPage>();
        builder.Services.AddTransient<TeacherDetailPage>();
        builder.Services.AddTransient<ClassesPage>();
        builder.Services.AddTransient<ReportCardPage>();
        builder.Services.AddTransient<AuditLogsPage>();
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