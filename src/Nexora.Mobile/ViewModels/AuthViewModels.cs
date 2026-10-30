using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;
using Nexora.Mobile.Services;

namespace Nexora.Mobile.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    protected void SetError(string? message)
    {
        ErrorMessage = message;
        HasError = !string.IsNullOrEmpty(message);
    }

    protected void ClearError()
    {
        ErrorMessage = null;
        HasError = false;
    }
}

public partial class LoginViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private string _username = "admin";

    [ObservableProperty]
    private string _password = "Admin@123!";

    [ObservableProperty]
    private string _selectedRoleLabel = "School Administrator";

    [ObservableProperty]
    private string _signInButtonText = "Sign In to Nexora";

    [ObservableProperty]
    private string _loadingStatusText = "Verifying security credentials...";

    [ObservableProperty]
    private double _loadingProgress = 0.35;

    public LoginViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Title = "Login to Nexora";
    }

    [RelayCommand]
    private void QuickSelectRole(string role)
    {
        switch (role.ToLower())
        {
            case "admin":
                Username = "admin";
                Password = "Admin@123!";
                SelectedRoleLabel = "School Administrator";
                break;
            case "teacher":
                Username = "teacher.dara";
                Password = "Teacher@123!";
                SelectedRoleLabel = "Teacher (Sok Dara)";
                break;
            case "student":
                Username = "student.visal";
                Password = "Student@123!";
                SelectedRoleLabel = "Student (Visal Chan)";
                break;
            case "parent":
                Username = "parent.bopha";
                Password = "Parent@123!";
                SelectedRoleLabel = "Parent (Keo Bopha)";
                break;
            case "superadmin":
                Username = "superadmin";
                Password = "Admin@123!";
                SelectedRoleLabel = "Super Administrator";
                break;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter your username and password.");
            return;
        }

        try
        {
            IsBusy = true;
            SignInButtonText = "Signing In...";
            LoadingProgress = 0.35;
            LoadingStatusText = "Verifying security credentials...";
            ClearError();

            var loginTask = _apiService.LoginAsync(Username, Password);

            await Task.Delay(180);
            LoadingProgress = 0.72;
            LoadingStatusText = $"Connecting to {SelectedRoleLabel} portal...";

            var result = await loginTask;
            if (!result.Success || result.Data == null)
            {
                SetError(result.Message ?? "Invalid credentials. Please verify your details.");
                return;
            }

            LoadingProgress = 1.0;
            LoadingStatusText = "Session authorized. Launching dashboard...";
            await Task.Delay(200);

            // Navigate to main AppShell or role dashboard
            if (Microsoft.Maui.Controls.Application.Current != null)
            {
                var appShell = new AppShell(_apiService);
                if (Microsoft.Maui.Controls.Application.Current.Windows.Count > 0)
                {
                    Microsoft.Maui.Controls.Application.Current.Windows[0].Page = appShell;
                }
                else
                {
                    Microsoft.Maui.Controls.Application.Current.MainPage = appShell;
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            SignInButtonText = "Sign In to Nexora";
            LoadingProgress = 0.35;
            LoadingStatusText = "Verifying security credentials...";
            IsBusy = false;
        }
    }
}
