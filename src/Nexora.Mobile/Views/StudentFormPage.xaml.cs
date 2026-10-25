using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class StudentFormPage : ContentPage
{
    public StudentFormPage(StudentFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
