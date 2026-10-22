using BgsControle.Nucleo.Gerador;

namespace BgsControle.App;

public partial class App : Application
{
    private readonly IServiceProvider _servicos;

    public App(IServiceProvider servicos)
    {
        InitializeComponent();
        _servicos = servicos;
    }

    // A página é criada aqui, depois do InitializeComponent: as cores do App.xaml já existem.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var janela = new Window(_servicos.GetRequiredService<MainPage>()) { Title = "BGS Controle" };
        // Ao sair do app (segundo plano) ou fechá-lo, o gerador desliga (failure-modes, regra 1).
        janela.Stopped += (_, _) => DesligarGerador();
        janela.Destroying += (_, _) => DesligarGerador();
        return janela;
    }

    private void DesligarGerador() => _ = _servicos.GetRequiredService<GeradorSinal>().DesligarAoSairAsync();
}
