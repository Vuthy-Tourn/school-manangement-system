using EduManage.Mobile.ViewModels;

namespace EduManage.Mobile.Views;

public partial class TimetablesPage : ContentPage
{
    private readonly TimetablesViewModel _viewModel;

    public TimetablesPage(TimetablesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadTimetableCommand.ExecuteAsync(null);
    }
}
