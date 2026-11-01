using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// O teste a seco do Extra (história 13e; <c>telas-rendimento.md</c>, "Teste a seco"; sem XAML, que é da 13g): tela
/// toda, um passo por tela.
/// <list type="number">
/// <item>"O módulo vai ser desligado." com o Confirmar e o Cancelar;</item>
/// <item>os trechos tocando, com a faixa e o trecho ("Faixa 2 de 3: menos 6, trecho 1"), o progresso por trecho e o
/// Cancelar (o player para, o master volta ao de antes e o módulo fica desligado);</item>
/// <item>o resultado: "Teto validado: −4,7 dB." ou "Clip na …, saída …: o teto desceu para … dB.", ou a falha, com o
/// Concluir.</item>
/// </list>
/// </summary>
public sealed class TesteSecoViewModel : TelaBase, IDisposable
{
    public const string TituloTela = "Teste a seco";
    public const string TextoAbrir = "Fazer o teste a seco";
    public const string TextoConfirmar = "Confirmar";
    public const string TextoCancelar = "Cancelar";
    public const string TextoConcluir = "Concluir";
    public const string TextoAvisoDetalhe = "O app toca os trechos mais fortes da playlist e escuta o clip. O som não chega aos alto-falantes.";

    /// <summary>O TalkBack do anel duplo enquanto o teste anda (história 13g).</summary>
    public const string DescricaoAndando = "Teste a seco andando";

    /// <summary>O TalkBack do ‹ com o teste fechado.</summary>
    public const string DescricaoVoltar = "Voltar";

    /// <summary>"Fechar", com o teste fechado (sem poder começar).</summary>
    public const string TextoFechar = "Fechar";

    /// <summary>"Trecho 3 de 8".</summary>
    public static string TextoProgresso(int atual, int total) => $"Trecho {atual} de {total}";

    private readonly TesteSeco _teste;
    private readonly GerenteSetups _setups;
    private readonly AnalisePlaylist _analise;
    private readonly Sessao _sessao;
    private readonly CopiaAntes _copia;
    private readonly EstadoAparelho _estado;
    private string? _mensagem;
    private bool _agindo;
    private bool _descartado;

    public TesteSecoViewModel(
        TesteSeco teste,
        GerenteSetups setups,
        AnalisePlaylist analise,
        Sessao sessao,
        CopiaAntes copia,
        EstadoAparelho estado,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _teste = teste;
        _setups = setups;
        _analise = analise;
        _sessao = sessao;
        _copia = copia;
        _estado = estado;

        Abrir = new Comando(() => Mostrar(_teste.Abrir()));
        Confirmar = new Comando(ConfirmarAsync);
        Cancelar = new Comando(async () => Mostrar(await _teste.CancelarAsync()));
        Concluir = new Comando(() => Mostrar(_teste.Concluir()));

        _teste.Mudou += AoMudar;
        _setups.Mudou += AoMudar;
        _analise.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _copia.Mudou += AoMudar;
        _estado.Recarregou += AoMudar;
        Atualizar();
    }

    // ------------------------------------------------------------------ o que aparece

    /// <summary>"Teste a seco".</summary>
    public string Titulo => TituloTela;

    public EtapaTesteSeco Etapa => _teste.Etapa;

    /// <summary>O teste está aberto (tela toda).</summary>
    public bool Aberto => Etapa != EtapaTesteSeco.Fechado;

    /// <summary>Por que o teste não começa agora (sem análise pronta, sem conexão, chave…), ou null.</summary>
    public string? MotivoTrava => Etapa == EtapaTesteSeco.Fechado ? _teste.MotivoParaComecar() : null;

    /// <summary>"Fazer o teste a seco".</summary>
    public string TextoBotaoAbrir => TextoAbrir;

    /// <summary>A instrução do passo.</summary>
    public string? Instrucao => Etapa switch
    {
        EtapaTesteSeco.Aviso => TesteSeco.MsgAviso,
        EtapaTesteSeco.Preparar => TesteSeco.MsgPreparando,
        EtapaTesteSeco.Tocando => _teste.TrechoAtual?.Texto,
        EtapaTesteSeco.Resultado => _teste.Mensagem,
        _ => null,
    };

    /// <summary>A linha de apoio: o que o teste faz (no aviso) e o módulo desligado (no resultado de uma falha), ou null.</summary>
    public string? Detalhe => Etapa switch
    {
        EtapaTesteSeco.Aviso => TextoAvisoDetalhe,
        EtapaTesteSeco.Resultado when !_teste.Validado && _teste.ModuloDesligado => TesteSeco.MsgModuloDesligado,
        _ => null,
    };

    /// <summary>"Faixa 2 de 3: menos 6, trecho 1" enquanto toca, ou null.</summary>
    public string? TextoTrecho => Etapa == EtapaTesteSeco.Tocando ? _teste.TrechoAtual?.Texto : null;

    /// <summary>Quantos trechos já passaram (sem clip).</summary>
    public int TrechosFeitos => _teste.TrechosFeitos;

    /// <summary>Quantos trechos o teste toca.</summary>
    public int TotalTrechos => _teste.Plano.Count;

    /// <summary>De 0 a 1: os trechos feitos sobre o total (a barra do progresso).</summary>
    public double Progresso => TotalTrechos == 0 ? 0 : Math.Clamp((double)TrechosFeitos / TotalTrechos, 0, 1);

    /// <summary>"Trecho 3 de 8" enquanto toca, ou null.</summary>
    public string? TextoDoProgresso =>
        Etapa == EtapaTesteSeco.Tocando && TotalTrechos > 0 ? TextoProgresso(Math.Min(TrechosFeitos + 1, TotalTrechos), TotalTrechos) : null;

    /// <summary>O master do teste agora, "−5,7 dB", enquanto toca, ou null.</summary>
    public string? TextoMaster => Etapa is EtapaTesteSeco.Tocando && _teste.MasterAtual is { } m ? $"{LimiteFisico.Db(m)} dB" : null;

    /// <summary>Os clips do teste, um por linha ("Clip na menos 3, saída JBL L: o teto desceu para −5,7 dB.").</summary>
    public IReadOnlyList<string> LinhasClips => _teste.Clips.Select(c => TesteSeco.MsgClipNa(c.Faixa, c.Saida, c.Master)).ToArray();

    /// <summary>O teste terminou com o teto validado.</summary>
    public bool Validado => _teste.Validado;

    /// <summary>O preparo ou um trecho anda: o anel duplo.</summary>
    public bool Ocupado => Etapa is EtapaTesteSeco.Preparar || _agindo && Etapa != EtapaTesteSeco.Tocando;

    /// <summary>A frase do núcleo (uma recusa, o cancelar, a queda), ou null.</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
            Avisar(nameof(MensagemNaTela));
        }
    }

    /// <summary>A <see cref="Mensagem"/> sem repetir a instrução do passo (história 13g).</summary>
    public string? MensagemNaTela => Mensagem == Instrucao ? null : Mensagem;

    // ------------------------------------------------------------------ os botões

    /// <summary>Abre o teste da memória em uso (o passo 1).</summary>
    public Comando Abrir { get; }

    /// <summary>Passo 1: o teste anda até o resultado.</summary>
    public Comando Confirmar { get; }

    /// <summary>No aviso, fecha; andando, para (o master volta, o módulo fica desligado).</summary>
    public Comando Cancelar { get; }

    /// <summary>No resultado: fecha o teste.</summary>
    public Comando Concluir { get; }

    public bool PodeConfirmar => Etapa == EtapaTesteSeco.Aviso && !_agindo;

    public bool PodeCancelar => Etapa is EtapaTesteSeco.Aviso or EtapaTesteSeco.Preparar or EtapaTesteSeco.Tocando;

    public bool PodeConcluir => Etapa == EtapaTesteSeco.Resultado;

    /// <summary>O passo 1: o aviso, com o Confirmar e o Cancelar (história 13g).</summary>
    public bool NoAviso => Etapa == EtapaTesteSeco.Aviso;

    /// <summary>O passo 2: o preparo e os trechos tocando, com o anel e o Cancelar.</summary>
    public bool Andando => Etapa is EtapaTesteSeco.Preparar or EtapaTesteSeco.Tocando;

    /// <summary>O passo 3: o resultado, com o Concluir.</summary>
    public bool NoResultado => Etapa == EtapaTesteSeco.Resultado;

    /// <summary>O teste não abriu (o motivo em <see cref="MotivoTrava"/> ou na <see cref="Mensagem"/>), com o Fechar.</summary>
    public bool Fechado => Etapa == EtapaTesteSeco.Fechado;

    /// <summary>Há clips no resultado.</summary>
    public bool TemClips => _teste.Clips.Count > 0;

    // ------------------------------------------------------------------ ações

    private async Task ConfirmarAsync()
    {
        if (!PodeConfirmar)
            return;
        _agindo = true;
        Atualizar();
        ResultadoTesteSeco r;
        try
        {
            r = await _teste.ConfirmarAsync();
        }
        finally
        {
            _agindo = false;
        }
        Mostrar(r);
    }

    private void Mostrar(ResultadoTesteSeco r) => NaTela(() =>
    {
        Mensagem = r.Ok ? r.Mensagem : r.Mensagem ?? r.Motivo;
        Atualizar();
    });

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        if (_descartado)
            return;
        if (Etapa is EtapaTesteSeco.Fechado or EtapaTesteSeco.Resultado && _teste.Mensagem is { } frase)
            Mensagem = frase;
        else if (Etapa is EtapaTesteSeco.Aviso or EtapaTesteSeco.Preparar or EtapaTesteSeco.Tocando)
            Mensagem = _teste.Mensagem;
        foreach (var nome in new[]
                 {
                     nameof(Etapa), nameof(Aberto), nameof(MotivoTrava), nameof(Instrucao), nameof(Detalhe), nameof(TextoTrecho),
                     nameof(TrechosFeitos), nameof(TotalTrechos), nameof(Progresso), nameof(TextoDoProgresso), nameof(TextoMaster),
                     nameof(LinhasClips), nameof(Validado), nameof(Ocupado), nameof(PodeConfirmar), nameof(PodeCancelar), nameof(PodeConcluir),
                     nameof(NoAviso), nameof(Andando), nameof(NoResultado), nameof(Fechado), nameof(TemClips), nameof(MensagemNaTela),
                 })
            Avisar(nome);
    }

    public void Dispose()
    {
        _descartado = true;
        _teste.Mudou -= AoMudar;
        _setups.Mudou -= AoMudar;
        _analise.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _copia.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
    }
}
