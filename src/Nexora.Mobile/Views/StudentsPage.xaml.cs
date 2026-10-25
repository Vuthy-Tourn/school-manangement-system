using Nexora.Mobile.Services;
using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class StudentsPage : ContentPage
{
    private readonly StudentsViewModel _viewModel;
    private readonly IApiService _apiService;

    public StudentsPage(StudentsViewModel viewModel, IApiService apiService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _apiService = apiService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadStudentsCommand.ExecuteAsync(null);
    }

    private async void OnAddStudentClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new StudentFormPage(new StudentFormViewModel(_apiService)));
    }
}
