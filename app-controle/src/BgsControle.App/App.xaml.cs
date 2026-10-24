using BgsControle.App.Telas;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Registro;
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
        janela.Stopped += (_, _) => AoSair(esperar: false);
        janela.Destroying += (_, _) => AoSair(esperar: true);
        return janela;
    }

    /// <summary>Quanto o fechar do app espera o histórico gravar.</summary>
    private static readonly TimeSpan EsperaHistorico = TimeSpan.FromSeconds(1);

    // O gerador desliga; o histórico grava já o que ainda esperava os 2 s (história 11). Ao fechar o app,
    // espera até 1 s pela gravação (fora da thread da tela) e deixa a falha no registro do BLE.
    private void AoSair(bool esperar)
    {
        _ = _servicos.GetRequiredService<GeradorSinal>().DesligarAoSairAsync();
        var log = _servicos.GetRequiredService<LogBle>();
        var gravacao = Task.Run(_servicos.GetRequiredService<Historico>().GravarAgoraAsync);
        _ = gravacao.ContinueWith(t =>
        {
            if (t.IsFaulted || !t.Result)
                log.Evento("histórico: não consegui gravar ao sair do app");
        }, TaskScheduler.Default);
        if (!esperar)
            return;
        try
        {
            if (!gravacao.Wait(EsperaHistorico))
                log.Evento("histórico: a gravação ao fechar o app passou de 1 s");
        }
        catch (AggregateException)
        {
            // A falha já vai para o registro pela continuação.
        }
    }
}
