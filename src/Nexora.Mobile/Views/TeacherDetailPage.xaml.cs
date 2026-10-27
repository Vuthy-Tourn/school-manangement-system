using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class TeacherDetailPage : ContentPage
{
    public TeacherDetailPage(TeacherDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
