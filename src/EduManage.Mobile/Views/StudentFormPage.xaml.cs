using EduManage.Mobile.ViewModels;

namespace EduManage.Mobile.Views;

public partial class StudentFormPage : ContentPage
{
    public StudentFormPage(StudentFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
