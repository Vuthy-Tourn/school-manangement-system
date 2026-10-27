using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class TeacherFormPage : ContentPage
{
    public TeacherFormPage(TeacherFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
