using BgsControle.App.Telas;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Uso;

namespace BgsControle.App;

public partial class App : Application
{
    private readonly IServiceProvider _servicos;

    public App(IServiceProvider servicos)
    {
        InitializeComponent();
        _servicos = servicos;
        // Só tema escuro (DESIGN, Colors): o app não muda com o tema claro do Android.
        UserAppTheme = AppTheme.Dark;
    }

    // A casca é criada aqui, depois do InitializeComponent: os tokens do App.xaml já existem.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var raiz = new NavigationPage(_servicos.GetRequiredService<CascaPage>());
        NavigationPage.SetHasNavigationBar(raiz, false);
        var janela = new Window(raiz) { Title = "BGS Controle" };
        // Ao sair do app (segundo plano) ou fechá-lo, o gerador desliga (failure-modes, regra 1).
        janela.Stopped += (_, _) => AoSair();
        janela.Destroying += (_, _) => AoSair();
        return janela;
    }

    // O gerador desliga; o histórico grava já o que ainda esperava os 2 s (história 11).
    private void AoSair()
    {
        _ = _servicos.GetRequiredService<GeradorSinal>().DesligarAoSairAsync();
        _ = _servicos.GetRequiredService<Historico>().GravarAgoraAsync();
    }
}
