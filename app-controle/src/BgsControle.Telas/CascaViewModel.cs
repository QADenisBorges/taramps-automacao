using System.ComponentModel;
using BgsControle.Nucleo;

namespace BgsControle.Telas;

/// <summary>Os destinos da barra inferior. O Player entra na história 11.</summary>
public enum Aba
{
    Inicio,
    Mesa,
    Audio,
}

/// <summary>O ponto da pílula de status.</summary>
public enum PontoStatus
{
    Desconectado,
    Esperando,
    Conectado,
}

/// <summary>
/// A casca do app (EXPERIENCE, Information Architecture): a barra inferior (Início · Mesa · Áudio), que
/// troca de destino sem perder o estado de cada um; a pílula de status ("Conectado · R003",
/// "Conectando · senha", "Desconectado", "Só leitura · firmware R004"); a engrenagem; e as telas que
/// sobem sozinhas por cima de tudo: a escolha do Setup quando o <c>GerenteSetups</c> entra em dúvida ou
/// troca, e a folha "Aplicado pela metade" ao reconectar com a marca.
/// </summary>
public sealed class CascaViewModel : TelaBase, IDisposable
{
    private readonly Sessao _sessao;
    private readonly SetupViewModel _setup;
    private readonly MemoriasViewModel _memorias;
    private readonly INavegacao _nav;
    private Aba _aba = Aba.Inicio;
    private (bool Setup, bool Metade)? _porCima;

    public CascaViewModel(
        Sessao sessao,
        ConexaoViewModel conexao,
        SetupViewModel setup,
        MemoriasViewModel memorias,
        InicioViewModel inicio,
        MesaViewModel mesa,
        AudioViewModel audio,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        Inicio = inicio;
        Mesa = mesa;
        Audio = audio;
        _sessao = sessao;
        _setup = setup;
        _memorias = memorias;
        _nav = nav;
        IrInicio = new Comando(() => Ir(Aba.Inicio));
        IrMesa = new Comando(() => Ir(Aba.Mesa));
        IrAudio = new Comando(() => Ir(Aba.Audio));
        AbrirConfiguracoes = new Comando(nav.AbrirConfiguracoes);
        TocarPilula = new Comando(nav.AbrirConexao);

        _sessao.Mudou += AoMudarSessao;
        _setup.PropertyChanged += AoMudarTelas;
        _memorias.PropertyChanged += AoMudarTelas;
    }

    /// <summary>Os três destinos da barra (cada um guarda o próprio estado).</summary>
    public InicioViewModel Inicio { get; }

    public MesaViewModel Mesa { get; }

    public AudioViewModel Audio { get; }

    /// <summary>Os botões de memória e a lista "…" (a folha sobe por cima da casca).</summary>
    public BotoesMemoriaViewModel Memorias => Inicio.Memorias;

    // ------------------------------------------------------------------ barra inferior

    public Aba Aba => _aba;

    public bool NoInicio => _aba == Aba.Inicio;
    public bool NaMesa => _aba == Aba.Mesa;
    public bool NoAudio => _aba == Aba.Audio;

    public Comando IrInicio { get; }
    public Comando IrMesa { get; }
    public Comando IrAudio { get; }

    public void Ir(Aba aba)
    {
        if (_aba == aba)
            return;
        _aba = aba;
        Avisar(nameof(Aba));
        Avisar(nameof(NoInicio));
        Avisar(nameof(NaMesa));
        Avisar(nameof(NoAudio));
    }

    // ------------------------------------------------------------------ topo

    /// <summary>A engrenagem: Configurações.</summary>
    public Comando AbrirConfiguracoes { get; }

    /// <summary>"Conectado · R003", "Conectando", "Conectando · senha", "Desconectado", "Só leitura · firmware R004".</summary>
    public string TextoStatus => _sessao.Estado switch
    {
        EstadoSessao.Conectado when _sessao.SoLeitura => $"Só leitura · firmware {_sessao.Firmware}",
        EstadoSessao.Conectado => $"Conectado · {_sessao.Firmware}",
        EstadoSessao.PedindoSenha => "Conectando · senha",
        EstadoSessao.Desconectado => "Desconectado",
        _ => "Conectando",
    };

    public PontoStatus Ponto => _sessao.Estado switch
    {
        EstadoSessao.Conectado => PontoStatus.Conectado,
        EstadoSessao.Desconectado => PontoStatus.Desconectado,
        _ => PontoStatus.Esperando,
    };

    public bool PontoConectado => Ponto == PontoStatus.Conectado;
    public bool PontoEsperando => Ponto == PontoStatus.Esperando;
    public bool PontoDesconectado => Ponto == PontoStatus.Desconectado;

    /// <summary>
    /// Abre a tela de conexão: desconectado, com Conectar (a senha aparece ali, e não no Início
    /// escondido), o registro e Esquecer a senha; conectado, os detalhes.
    /// </summary>
    public Comando TocarPilula { get; }

    // ------------------------------------------------------------------ por cima de tudo

    /// <summary>A escolha do Setup está por cima (Setup em dúvida ou Trocar de Setup).</summary>
    public bool EscolhaSetupPorCima => _porCima?.Setup == true;

    /// <summary>A folha "Aplicado pela metade" está por cima.</summary>
    public bool PelaMetadePorCima => _porCima?.Metade == true;

    /// <summary>
    /// Confere o que deve ficar por cima e pede à navegação quando muda (ou sempre, com
    /// <paramref name="forcar"/>: a casca apareceu e a navegação pode não ter conseguido antes). Quando a
    /// escolha do Setup sai de cima (escolheu, criou, seguiu sem Setup ou desistiu), a casca volta ao
    /// Início: as telas cheias de antes fecham, porque as saídas e a memória podem ter mudado.
    /// </summary>
    public void Sincronizar(bool forcar = false)
    {
        var agora = (_setup.Aberta, _memorias.PelaMetade);
        if (_porCima == agora && !forcar)
            return;
        var saiuDaEscolha = _porCima?.Setup == true && !agora.Item1;
        _porCima = agora;
        if (saiuDaEscolha)
        {
            _nav.VoltarAoInicio();
            Ir(Aba.Inicio);
        }
        _nav.MostrarPorCima(agora.Item1, agora.Item2);
        Avisar(nameof(EscolhaSetupPorCima));
        Avisar(nameof(PelaMetadePorCima));
    }

    private void AoMudarTelas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SetupViewModel.Aberta) or nameof(MemoriasViewModel.PelaMetade))
            Sincronizar();
    }

    private void AoMudarSessao() => NaTela(() =>
    {
        Avisar(nameof(TextoStatus));
        Avisar(nameof(Ponto));
        Avisar(nameof(PontoConectado));
        Avisar(nameof(PontoEsperando));
        Avisar(nameof(PontoDesconectado));
    });

    public void Dispose()
    {
        _sessao.Mudou -= AoMudarSessao;
        _setup.PropertyChanged -= AoMudarTelas;
        _memorias.PropertyChanged -= AoMudarTelas;
    }
}
