namespace BgsControle.App;

public partial class MainPage : ContentPage
{
    public MainPage(ConexaoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
