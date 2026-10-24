using BgsControle.App.Telas;
using BgsControle.Nucleo.Registro;
using BgsControle.Telas;

namespace BgsControle.App;

/// <summary>
/// A navegação do app (EXPERIENCE, Information Architecture): a casca numa NavigationPage sem barra; as telas
/// cheias com ‹ entram na pilha; a escolha do Setup e a folha "Aplicado pela metade" sobem como modais por
/// cima de tudo, sem barra. As páginas são criadas uma vez e reaproveitadas (os ViewModels são únicos).
/// <para>
/// Uma tela cheia pedida com um modal por cima não falha calada: com só a folha "Aplicado pela metade" por
/// cima, o pedido espera e entra quando ela fecha; com a escolha do Setup, ele é descartado (depois da
/// escolha a casca volta ao Início), e nos dois casos a linha vai para o registro.
/// </para>
/// </summary>
public sealed class NavegacaoMaui(IServiceProvider servicos, LogBle log) : INavegacao
{
    private readonly Dictionary<int, AjusteSaidaPage> _ajustes = [];
    private readonly Dictionary<FuncaoAudio, FuncaoPage> _funcoes = [];
    private readonly SemaphoreSlim _porCima = new(1, 1);
    private (bool Setup, bool Metade) _pedido;
    private (string Nome, Func<Page> Criar)? _esperando;

    private static INavigation? Navegacao => Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public void AbrirAjuste(int canal) => Empurrar($"ajuste do CH{canal + 1}", () =>
    {
        if (!_ajustes.TryGetValue(canal, out var pagina))
        {
            pagina = new AjusteSaidaPage(servicos.GetRequiredService<AjustesSaida>().Tela(canal));
            _ajustes[canal] = pagina;
        }
        return pagina;
    });

    public void AbrirFuncao(FuncaoAudio funcao) => Empurrar($"função {funcao}", () =>
    {
        if (!_funcoes.TryGetValue(funcao, out var pagina))
        {
            pagina = new FuncaoPage(servicos.GetRequiredService<AudioViewModel>().Tela(funcao));
            _funcoes[funcao] = pagina;
        }
        return pagina;
    });

    public void AbrirConfiguracoes() => Empurrar("Configurações", servicos.GetRequiredService<ConfiguracoesPage>);

    public void AbrirSetups() => Empurrar("Setups", servicos.GetRequiredService<SetupsPage>);

    public void AbrirSetup(string idSetup) => Empurrar("Setup", () =>
    {
        // A tela Setup mostra o Setup tocado (não o em uso).
        servicos.GetRequiredService<EquipamentosViewModel>().Mostrar(idSetup);
        return servicos.GetRequiredService<SetupPage>();
    });

    public void AbrirMemorias() => Empurrar("Memórias", servicos.GetRequiredService<MemoriasPage>);

    public void AbrirConexao() => Empurrar("Conexão", servicos.GetRequiredService<ConexaoPage>);

    public void Voltar() => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (Navegacao is { NavigationStack.Count: > 1 } nav)
            await nav.PopAsync();
    });

    public void VoltarAoInicio() => MainThread.BeginInvokeOnMainThread(async () =>
    {
        _esperando = null;
        if (Navegacao is { NavigationStack.Count: > 1 } nav)
            await nav.PopToRootAsync(false);
    });

    /// <summary>Mostra, por cima de tudo, as telas que valem agora, na ordem: a escolha do Setup e depois a folha pela metade.</summary>
    public void MostrarPorCima(bool escolhaSetup, bool pelaMetade)
    {
        _pedido = (escolhaSetup, pelaMetade);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _porCima.WaitAsync();
            try
            {
                if (Navegacao is not { } nav)
                    return;
                var (setup, metade) = _pedido;
                var queremos = new List<Page>();
                if (setup)
                    queremos.Add(servicos.GetRequiredService<SetupEscolhaPage>());
                if (metade)
                    queremos.Add(servicos.GetRequiredService<PelaMetadePage>());
                if (!nav.ModalStack.SequenceEqual(queremos))
                {
                    // Tira as que sobram (de cima para baixo) e põe as que faltam, na ordem.
                    while (nav.ModalStack.Count > 0 && !queremos.Take(nav.ModalStack.Count).SequenceEqual(nav.ModalStack))
                        await nav.PopModalAsync(false);
                    foreach (var pagina in queremos.Skip(nav.ModalStack.Count))
                        await nav.PushModalAsync(pagina, false);
                }
                if (nav.ModalStack.Count == 0 && _esperando is { } esperando)
                {
                    _esperando = null;
                    await PorNaPilha(nav, esperando.Criar());
                }
            }
            finally
            {
                _porCima.Release();
            }
        });
    }

    private void Empurrar(string nome, Func<Page> criar) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (Navegacao is not { } nav)
        {
            log.Evento($"tela: {nome} pedida antes da janela existir");
            return;
        }
        if (nav.ModalStack.Count > 0)
        {
            if (_pedido.Setup)
            {
                log.Evento($"tela: {nome} não abriu; a escolha do Setup está por cima");
                return;
            }
            log.Evento($"tela: {nome} espera a folha por cima fechar");
            _esperando = (nome, criar);
            return;
        }
        await PorNaPilha(nav, criar());
    });

    private static async Task PorNaPilha(INavigation nav, Page pagina)
    {
        if (nav.NavigationStack.Contains(pagina))
            return; // já aberta
        await nav.PushAsync(pagina);
    }
}

/// <summary>Os Ajustes da saída, um por canal pedido (cada um segue a saída do Setup que tem o canal).</summary>
public sealed class AjustesSaida(IServiceProvider servicos)
{
    private readonly Dictionary<int, AjusteSaidaViewModel> _telas = [];

    public AjusteSaidaViewModel Tela(int canal)
    {
        if (!_telas.TryGetValue(canal, out var tela))
        {
            tela = ActivatorUtilities.CreateInstance<AjusteSaidaViewModel>(servicos, canal);
            _telas[canal] = tela;
        }
        return tela;
    }
}
