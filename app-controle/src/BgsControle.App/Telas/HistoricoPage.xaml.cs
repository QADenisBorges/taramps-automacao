using BgsControle.Telas;

namespace BgsControle.App.Telas;

public partial class HistoricoPage : ContentPage
{
    public HistoricoPage(HistoricoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();
}
