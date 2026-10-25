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

        Routing.RegisterRoute("student-form", typeof(StudentFormPage));
    }
}