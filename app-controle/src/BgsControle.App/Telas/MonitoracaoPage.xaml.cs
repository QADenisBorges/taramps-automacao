using BgsControle.Telas;

namespace BgsControle.App.Telas;

public partial class MonitoracaoPage : ContentPage
{
    public MonitoracaoPage(MonitoracaoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();
}
