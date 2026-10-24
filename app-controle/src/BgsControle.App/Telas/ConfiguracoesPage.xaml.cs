using BgsControle.Telas;

namespace BgsControle.App.Telas;

public partial class ConfiguracoesPage : ContentPage
{
    public ConfiguracoesPage(ConfiguracoesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
