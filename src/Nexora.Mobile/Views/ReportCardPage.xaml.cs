using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class ReportCardPage : ContentPage
{
    public ReportCardPage(ReportCardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
