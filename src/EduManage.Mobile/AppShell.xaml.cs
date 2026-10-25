using EduManage.Mobile.Services;
using EduManage.Mobile.Views;

namespace EduManage.Mobile;

public partial class AppShell : Shell
{
    private readonly IApiService _apiService;

    public AppShell(IApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;

        Routing.RegisterRoute("student-form", typeof(StudentFormPage));
    }
}