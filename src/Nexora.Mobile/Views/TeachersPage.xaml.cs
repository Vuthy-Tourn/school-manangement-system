using Nexora.Mobile.Services;
using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class TeachersPage : ContentPage
{
    private readonly TeachersViewModel _viewModel;
    private readonly IApiService _apiService;

    public TeachersPage(TeachersViewModel viewModel, IApiService apiService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _apiService = apiService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadTeachersCommand.ExecuteAsync(null);
    }

    private async void OnAddTeacherClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new TeacherFormPage(new TeacherFormViewModel(_apiService)));
    }
}
