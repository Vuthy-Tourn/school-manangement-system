using Nexora.Application.DTOs;
using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class GradesPage : ContentPage
{
    private readonly GradesViewModel _viewModel;

    public GradesPage(GradesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnExamChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is ExamDto exam)
        {
            _viewModel.SelectedExam = exam;
        }

        if (_viewModel.SelectedExam != null && _viewModel.SelectedClass != null && !_viewModel.IsBusy)
        {
            await _viewModel.LoadComponentsCommand.ExecuteAsync(null);
        }
    }

    private async void OnClassSectionChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is ClassSectionDto cls)
        {
            _viewModel.SelectedClass = cls;
        }

        if (_viewModel.SelectedExam != null && _viewModel.SelectedClass != null && !_viewModel.IsBusy)
        {
            await _viewModel.LoadComponentsCommand.ExecuteAsync(null);
        }
    }

    private void OnStudentFilterPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is StudentFilterOption opt)
        {
            _viewModel.SelectedStudentFilter = opt;
        }
    }

    private async void OnFilterChanged(object sender, EventArgs e)
    {
        if (_viewModel.SelectedExam != null && _viewModel.SelectedClass != null && !_viewModel.IsBusy)
        {
            await _viewModel.LoadComponentsCommand.ExecuteAsync(null);
        }
    }

    private async void OnComponentChanged(object sender, EventArgs e)
    {
        await _viewModel.LoadGradesCommand.ExecuteAsync(null);
    }
}
