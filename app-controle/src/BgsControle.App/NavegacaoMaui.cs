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
/// escolha a casca volta ao Início), e nos dois casos a linha vai para o registro. A única exceção é a
/// tela de conexão: ela sobe por cima da escolha do Setup (Conectar, o registro e Esquecer a senha
/// precisam estar ao alcance também ali).
/// </para>
/// <para>
/// História 11: as telas cheias novas (Gerador, Presets, Senha, Voltímetro e clip, Chaves de acesso) e as
/// camadas que sobem sozinhas, também modais, na ordem de baixo para cima: a escolha do Setup, a folha pela
/// metade, a folha de risco do app, a tela toda "REAPLICANDO" e a faixa "DESLIGUE O MÓDULO AGORA". As três
/// últimas ficam acima de tudo. A IA não aparece em nenhuma tela (decisão do Denis, 30/10/2026).
/// </para>
/// <para>
/// História 10d: a criação da memória em tela toda entra como camada, logo abaixo da folha de risco (que continua
/// por cima dela durante a criação).
/// </para>
/// <para>
/// História 13g: as telas cheias do Rendimento, a calibração guiada (com a saída, ela já abre no passo 1) e o teste a
/// seco (abre o passo 1, o aviso); e as folhas globais, logo abaixo da folha de risco: a do nível (uma só, qualquer que
/// seja o chip que a abriu) e a do outro player. Uma tela cheia já na pilha, mas não no topo, volta ao topo (as de cima
/// saem).
/// </para>
/// <para>
/// História 14c: a folha da proposta de ficha, também global, depois da do outro player e antes da folha de risco.
/// </para>
/// </summary>
public sealed class NavegacaoMaui(IServiceProvider servicos, LogBle log) : INavegacao
{
    private readonly Dictionary<int, AjusteSaidaPage> _ajustes = [];
    private readonly Dictionary<FuncaoAudio, FuncaoPage> _funcoes = [];
    private readonly SemaphoreSlim _porCima = new(1, 1);
    private (bool Setup, bool Metade) _pedido;
    private (bool Criacao, bool FolhaRisco, bool Reaplicando, bool Alarme) _camadas;
    private (bool Nivel, bool OutroPlayer, bool Ficha) _folhas;
    private (string Nome, Func<Page> Criar)? _esperando;

    private static INavigation? Navegacao => Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public void AbrirAjuste(int canal) => Empurrar($"ajuste do CH{canal + 1}", () =>
    {
        if (!_ajustes.TryGetValue(canal, out var pagina))
        {
            pagina = new AjusteSaidaPage(servicos.GetRequiredService<AjustesSaida>().Tela(canal), servicos.GetRequiredService<CascaViewModel>());
            _ajustes[canal] = pagina;
        }
        return pagina;
    });

    public void AbrirFuncao(FuncaoAudio funcao) => Empurrar($"função {funcao}", () =>
    {
        if (!_funcoes.TryGetValue(funcao, out var pagina))
        {
            pagina = new FuncaoPage(servicos.GetRequiredService<AudioViewModel>().Tela(funcao), servicos.GetRequiredService<CascaViewModel>());
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

    public void AbrirGerador() => Empurrar("Gerador", servicos.GetRequiredService<GeradorPage>);

    public void AbrirPresets() => Empurrar("Presets", servicos.GetRequiredService<PresetsPage>);

    public void AbrirSenha() => Empurrar("Senha", servicos.GetRequiredService<AparelhoPage>);

    public void AbrirMonitoracao() => Empurrar("Voltímetro e clip", servicos.GetRequiredService<MonitoracaoPage>);

    public void AbrirChaves() => Empurrar("Chaves de acesso", servicos.GetRequiredService<ChavesPage>);

    /// <summary>História 14b: a chave "Controle pela rede" fica no grupo APARELHO das Configurações.</summary>
    public void AbrirAparelho() => AbrirConfiguracoes();

    public void AbrirCalibracao(int? canal) => Empurrar("Calibração", () =>
    {
        // Com a saída (o "Calibrar ›" de uma faixa), a calibração dela abre no passo 1; a de outra saída aberta não troca (a
        // tela diz para terminar antes). A página decide e confere o resultado.
        var pagina = servicos.GetRequiredService<CalibracaoPage>();
        pagina.Abrir(canal);
        return pagina;
    });

    public void AbrirTesteSeco() => Empurrar("Teste a seco", () =>
    {
        // O passo 1 (o aviso); sem poder abrir, a tela mostra o motivo.
        var tela = servicos.GetRequiredService<TesteSecoViewModel>();
        if (!tela.Aberto)
            tela.Abrir.Execute(null);
        return servicos.GetRequiredService<TesteSecoPage>();
    });

    public void AbrirConexao() => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (Navegacao is not { } nav)
            return;
        var pagina = servicos.GetRequiredService<ConexaoPage>();
        if (nav.ModalStack.Contains(pagina) || nav.NavigationStack.Contains(pagina))
            return; // já aberta
        if (nav.ModalStack.Count > 0)
            await nav.PushModalAsync(pagina, false); // por cima da escolha do Setup ou da folha pela metade
        else
            await nav.PushAsync(pagina);
    });

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
        Reconciliar();
    }

    /// <summary>As camadas acima das outras: a criação da memória, a folha de risco, o "REAPLICANDO" e o alarme do gerador.</summary>
    public void MostrarCamadas(bool criacao, bool folhaRisco, bool reaplicando, bool alarmeGerador)
    {
        _camadas = (criacao, folhaRisco, reaplicando, alarmeGerador);
        Reconciliar();
    }

    /// <summary>As folhas globais: a do nível e a do outro player (história 13g) e a da proposta de ficha (história 14c).</summary>
    public void MostrarFolhas(bool nivel, bool outroPlayer, bool fichaProposta)
    {
        _folhas = (nivel, outroPlayer, fichaProposta);
        Reconciliar();
    }

    /// <summary>Deixa na pilha de modais só as telas que valem agora, na ordem de baixo para cima.</summary>
    private void Reconciliar()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _porCima.WaitAsync();
            try
            {
                if (Navegacao is not { } nav)
                    return;
                var (setup, metade) = _pedido;
                var (criacao, folhaRisco, reaplicando, alarme) = _camadas;
                var queremos = new List<Page>();
                if (setup)
                    queremos.Add(servicos.GetRequiredService<SetupEscolhaPage>());
                if (metade)
                    queremos.Add(servicos.GetRequiredService<PelaMetadePage>());
                if (criacao)
                    queremos.Add(servicos.GetRequiredService<CriacaoPage>());
                if (_folhas.Nivel)
                    queremos.Add(servicos.GetRequiredService<NivelFolhaPage>());
                if (_folhas.OutroPlayer)
                    queremos.Add(servicos.GetRequiredService<OutroPlayerPage>());
                if (_folhas.Ficha)
                    queremos.Add(servicos.GetRequiredService<PropostaFichaPage>());
                if (folhaRisco)
                    queremos.Add(servicos.GetRequiredService<FolhaRiscoPage>());
                if (reaplicando)
                    queremos.Add(servicos.GetRequiredService<ReaplicandoPage>());
                if (alarme)
                    queremos.Add(servicos.GetRequiredService<AlarmeGeradorPage>());
                // A tela de conexão aberta por cima da escolha não conta; se algo muda, ela sai junto.
                var conexao = servicos.GetRequiredService<ConexaoPage>();
                if (!nav.ModalStack.Where(p => p != conexao).SequenceEqual(queremos))
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
        {
            // Já aberta: se outras telas cheias estão por cima dela, elas saem até ela voltar ao topo.
            while (nav.NavigationStack.Count > 1 && nav.NavigationStack[^1] != pagina)
                await nav.PopAsync(false);
            return;
        }
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
