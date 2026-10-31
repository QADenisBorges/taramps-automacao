using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha de ajuste na tela da criação: "Ganho da saída 3: +0,0 dB → +2,0 dB", a frase e o destaque.</summary>
/// <param name="Destaque">Mexido nesta rodada (no resumo do refino, em destaque).</param>
public sealed record LinhaCriacao(string Rotulo, string Antes, string Valor, string Frase, bool Destaque)
{
    public string Linha => $"{Rotulo}: {Antes} → {Valor}";
}

/// <summary>
/// A criação da memória do lado do usuário (CAP-18; <c>porta-de-comandos.md</c>, seção "Criação da memória"), sem
/// XAML (a tela é da história 10d). Uma tela toda, sem abas, uma etapa por vez:
/// <list type="bullet">
/// <item><b>Perguntas:</b> o texto, as respostas mandadas e "Outro…" (o texto do usuário; vazio não responde).</item>
/// <item><b>Ajustando:</b> os ajustes da rodada, com a frase de cada um.</item>
/// <item><b>Resumo:</b> "Pronto. Ouça um pouco.", quantos ajustes (no refino, só os da rodada, em destaque) e
/// "Avaliar o som".</item>
/// <item><b>Como ficou?:</b> "Ótimo, pode salvar" (do app), as respostas mandadas e "Outro…".</item>
/// <item><b>Salvar:</b> o nome e Salvar (só aqui a memória nasce), ou "Ajustar mais".</item>
/// <item><b>✕</b> (nas perguntas e no "Como ficou?"): a folha "Cancelar a nova memória?", com "Cancelar e
/// voltar" e "Continuar criando".</item>
/// </list>
/// Nenhum texto menciona quem manda as perguntas.
/// </summary>
public sealed class CriacaoMemoriaViewModel : TelaBase, IDisposable
{
    public const string TextoOutroBotao = "Outro…";
    public const string TituloResumoTexto = "Pronto. Ouça um pouco.";
    public const string TextoAvaliarBotao = "Avaliar o som";
    public const string TituloComoFicouTexto = "Como ficou?";
    public const string TextoPodeSalvarBotao = CriacaoMemoria.OtimoPodeSalvar;
    public const string TextoSalvarBotao = "Salvar";
    public const string TextoAjustarMaisBotao = "Ajustar mais";
    public const string TituloFolhaCancelarTexto = "Cancelar a nova memória?";
    public const string TextoCancelarEVoltarBotao = "Cancelar e voltar";
    public const string TextoContinuarCriandoBotao = "Continuar criando";
    public const string TextoAjustando = "Ajustando o som…";
    public const string TextoEsperandoPergunta = "Preparando a próxima pergunta…";

    /// <summary>"Nada é salvo e o aparelho volta para a FILME, como estava antes." (sem memória em uso: "o som do aparelho").</summary>
    public static string MsgFolhaCancelar(string? somAntes) => somAntes is null
        ? "Nada é salvo e o aparelho volta para o som do aparelho, como estava antes."
        : $"Nada é salvo e o aparelho volta para a {somAntes}, como estava antes.";

    /// <summary>A primeira rodada: "3 ajustes no aparelho, todos relidos." (1: "1 ajuste no aparelho, relido.").</summary>
    public static string MsgResumoPrimeira(int n) => n switch
    {
        0 => "Nenhum ajuste no aparelho.",
        1 => "1 ajuste no aparelho, relido.",
        _ => $"{n} ajustes no aparelho, todos relidos.",
    };

    /// <summary>O refino: "Mudou 2 ajustes, em destaque. O resto ficou igual." (1: "Mudou 1 ajuste, …").</summary>
    public static string MsgResumoRefino(int n) => n switch
    {
        0 => "Nada mudou nesta rodada.",
        1 => "Mudou 1 ajuste, em destaque. O resto ficou igual.",
        _ => $"Mudou {n} ajustes, em destaque. O resto ficou igual.",
    };

    private readonly CriacaoMemoria _criacao;

    private EtapaCriacao _etapa;
    private string? _nomeSetup;
    private PerguntaCriacao? _pergunta;
    private IReadOnlyList<LinhaCriacao> _ajustesDaRodada = [];
    private IReadOnlyList<LinhaCriacao> _linhasResumo = [];
    private string? _textoResumo;
    private IReadOnlyList<string> _respostasComoFicou = [];
    private string? _textoOutro;
    private string? _nomeMemoria;
    private string? _erroNome;
    private string? _aviso;
    private bool _folhaCancelarAberta;
    private string? _textoFolhaCancelar;
    private bool _descartado;

    public CriacaoMemoriaViewModel(CriacaoMemoria criacao, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _criacao = criacao;
        Responder = new ComandoCom<int>(i => Mostrar(EtapaAtual == EtapaCriacao.ComoFicou ? _criacao.ResponderComoFicou(i) : _criacao.Responder(i)));
        ResponderOutro = new Comando(AoResponderOutro);
        Avaliar = new Comando(() => Mostrar(_criacao.AvaliarSom()));
        PodeSalvar = new Comando(() => Mostrar(_criacao.PodeSalvar()));
        AjustarMais = new Comando(() => Mostrar(_criacao.AjustarMais()));
        Salvar = new Comando(SalvarAsync);
        Fechar = new Comando(AbrirFolhaCancelar);
        CancelarEVoltar = new Comando(CancelarEVoltarAsync);
        ContinuarCriando = new Comando(() => FolhaCancelarAberta = false);
        _criacao.Mudou += AoMudar;
        Atualizar();
    }

    // ------------------------------------------------------------------ o que a tela mostra

    /// <summary>Uma criação está aberta: a tela toda aparece.</summary>
    public bool Aberta => _etapa != EtapaCriacao.Fechada;

    public EtapaCriacao Etapa => _etapa;

    private EtapaCriacao EtapaAtual => _etapa;

    public bool NasPerguntas => _etapa == EtapaCriacao.Perguntas;
    public bool Ajustando => _etapa == EtapaCriacao.Ajustando;
    public bool NoResumo => _etapa == EtapaCriacao.Resumo;
    public bool NoComoFicou => _etapa == EtapaCriacao.ComoFicou;
    public bool NoSalvar => _etapa == EtapaCriacao.Salvar;

    /// <summary>O Setup da memória nova ("Casa 2.1").</summary>
    public string? NomeSetup => _nomeSetup;

    /// <summary>O ✕ aparece nas perguntas e no "Como ficou?".</summary>
    public bool PodeFechar => _etapa is EtapaCriacao.Perguntas or EtapaCriacao.ComoFicou;

    /// <summary>Há uma pergunta esperando o toque.</summary>
    public bool TemPergunta => _pergunta is not null;

    /// <summary>"Preparando a próxima pergunta…" entre uma pergunta e outra, ou null.</summary>
    public string? TextoEspera => NasPerguntas && _pergunta is null ? TextoEsperandoPergunta : Ajustando ? TextoAjustando : null;

    /// <summary>O número da pergunta (1 = a primeira), ou null.</summary>
    public int? NumeroPergunta => _pergunta?.Numero;

    /// <summary>O texto da pergunta, ou null.</summary>
    public string? TextoPergunta => _pergunta?.Texto;

    /// <summary>As respostas da pergunta (sem o "Outro…", que vem de <see cref="TextoOutro"/>).</summary>
    public IReadOnlyList<string> RespostasPergunta => _pergunta?.Respostas ?? [];

    /// <summary>"Outro…".</summary>
    public string TextoOutro => TextoOutroBotao;

    /// <summary>O texto do "Outro…" (nas perguntas e no "Como ficou?").</summary>
    public string? TextoDoOutro
    {
        get => _textoOutro;
        set
        {
            if (_textoOutro == value)
                return;
            _textoOutro = value;
            Avisar();
        }
    }

    /// <summary>Os ajustes da rodada enquanto ela anda.</summary>
    public IReadOnlyList<LinhaCriacao> AjustesDaRodada => _ajustesDaRodada;

    /// <summary>"Pronto. Ouça um pouco."</summary>
    public string TituloResumo => TituloResumoTexto;

    /// <summary>"3 ajustes no aparelho, todos relidos." ou "Mudou 2 ajustes, em destaque. O resto ficou igual.", ou null fora do resumo.</summary>
    public string? TextoResumo => _textoResumo;

    /// <summary>Cada endereço mexido, com o último ajuste; os da rodada em destaque.</summary>
    public IReadOnlyList<LinhaCriacao> LinhasResumo => _linhasResumo;

    /// <summary>"Avaliar o som".</summary>
    public string TextoAvaliar => TextoAvaliarBotao;

    /// <summary>"Como ficou?".</summary>
    public string TituloComoFicou => TituloComoFicouTexto;

    /// <summary>"Ótimo, pode salvar" (o primeiro botão do "Como ficou?").</summary>
    public string TextoPodeSalvar => TextoPodeSalvarBotao;

    /// <summary>As respostas do "Como ficou?" depois de "Ótimo, pode salvar" (sem o "Outro…").</summary>
    public IReadOnlyList<string> RespostasComoFicou => _respostasComoFicou;

    /// <summary>"Salvar".</summary>
    public string TextoSalvar => TextoSalvarBotao;

    /// <summary>"Ajustar mais".</summary>
    public string TextoAjustarMais => TextoAjustarMaisBotao;

    /// <summary>O nome da memória nova (no Salvar).</summary>
    public string? NomeMemoria
    {
        get => _nomeMemoria;
        set
        {
            if (_nomeMemoria == value)
                return;
            _nomeMemoria = value;
            Avisar();
            ErroNome = null;
        }
    }

    /// <summary>Por que o nome não vale ("Já existe uma memória com esse nome no Casa 2.1."), ou null.</summary>
    public string? ErroNome
    {
        get => _erroNome;
        private set
        {
            if (_erroNome == value)
                return;
            _erroNome = value;
            Avisar();
        }
    }

    /// <summary>A frase do último toque que não passou ("Escreva a resposta."), ou null.</summary>
    public string? Aviso
    {
        get => _aviso;
        private set
        {
            if (_aviso == value)
                return;
            _aviso = value;
            Avisar();
        }
    }

    /// <summary>A folha do ✕ está aberta.</summary>
    public bool FolhaCancelarAberta
    {
        get => _folhaCancelarAberta;
        private set
        {
            if (_folhaCancelarAberta == value)
                return;
            _folhaCancelarAberta = value;
            Avisar();
        }
    }

    /// <summary>"Cancelar a nova memória?".</summary>
    public string TituloFolhaCancelar => TituloFolhaCancelarTexto;

    /// <summary>"Nada é salvo e o aparelho volta para a FILME, como estava antes.", com a folha aberta.</summary>
    public string? TextoFolhaCancelar
    {
        get => _textoFolhaCancelar;
        private set
        {
            if (_textoFolhaCancelar == value)
                return;
            _textoFolhaCancelar = value;
            Avisar();
        }
    }

    /// <summary>"Cancelar e voltar".</summary>
    public string TextoCancelarEVoltar => TextoCancelarEVoltarBotao;

    /// <summary>"Continuar criando".</summary>
    public string TextoContinuarCriando => TextoContinuarCriandoBotao;

    // ------------------------------------------------------------------ os toques

    /// <summary>O toque numa resposta (0 = a primeira), da pergunta ou do "Como ficou?".</summary>
    public ComandoCom<int> Responder { get; }

    /// <summary>Responde com o texto do "Outro…" (vazio não responde).</summary>
    public Comando ResponderOutro { get; }

    /// <summary>"Avaliar o som".</summary>
    public Comando Avaliar { get; }

    /// <summary>"Ótimo, pode salvar".</summary>
    public Comando PodeSalvar { get; }

    /// <summary>"Ajustar mais" (do Salvar ao "Como ficou?").</summary>
    public Comando AjustarMais { get; }

    /// <summary>"Salvar", com o <see cref="NomeMemoria"/>.</summary>
    public Comando Salvar { get; }

    /// <summary>O ✕: abre a folha "Cancelar a nova memória?" (só nas perguntas e no "Como ficou?").</summary>
    public Comando Fechar { get; }

    /// <summary>"Cancelar e voltar": nada é salvo e o som volta ao de antes.</summary>
    public Comando CancelarEVoltar { get; }

    /// <summary>"Continuar criando": a folha fecha e nada muda.</summary>
    public Comando ContinuarCriando { get; }

    private void AoResponderOutro()
    {
        var r = EtapaAtual == EtapaCriacao.ComoFicou ? _criacao.ResponderComoFicouOutro(TextoDoOutro) : _criacao.ResponderOutro(TextoDoOutro);
        if (r.Ok)
            TextoDoOutro = null;
        Mostrar(r);
    }

    private void Mostrar(ResultadoCriacao r)
    {
        Aviso = r.Ok ? null : r.Mensagem ?? r.Motivo;
        Atualizar();
    }

    private async Task SalvarAsync()
    {
        var r = await _criacao.SalvarAsync(NomeMemoria);
        if (r.Ok)
        {
            NomeMemoria = null;
            ErroNome = null;
            Aviso = null;
        }
        else
        {
            ErroNome = r.Mensagem ?? r.Motivo;
        }
        Atualizar();
    }

    private void AbrirFolhaCancelar()
    {
        if (!PodeFechar)
            return;
        TextoFolhaCancelar = MsgFolhaCancelar(_criacao.SomAntes);
        FolhaCancelarAberta = true;
    }

    private async Task CancelarEVoltarAsync()
    {
        if (!FolhaCancelarAberta)
            return;
        FolhaCancelarAberta = false;
        var r = await _criacao.CancelarAsync(CriacaoMemoria.PorUsuario);
        Aviso = r.Ok ? null : r.Mensagem ?? r.Motivo;
        Atualizar();
    }

    // ------------------------------------------------------------------ o núcleo mudou

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        if (_descartado)
            return;
        var etapa = _criacao.Etapa;
        var mudouEtapa = etapa != _etapa;
        _etapa = etapa;
        _nomeSetup = _criacao.NomeSetup;
        var numeroAntes = _pergunta?.Numero;
        _pergunta = _criacao.PerguntaAtual;
        // Uma pergunta nova (na mesma etapa) nasce com o "Outro…" vazio.
        var mudouPergunta = _pergunta is not null && _pergunta.Numero != numeroAntes;
        var rodada = _criacao.Rodada;
        _ajustesDaRodada = _criacao.Ajustes
            .Where(a => a.Rodada == rodada)
            .Select(a => new LinhaCriacao(a.Rotulo, a.AntesExibicao, a.RelidoExibicao, a.Frase, true))
            .ToArray();
        if (etapa is EtapaCriacao.Resumo or EtapaCriacao.ComoFicou or EtapaCriacao.Salvar)
        {
            var resumo = _criacao.Resumo;
            _linhasResumo = resumo.Select(l => new LinhaCriacao(l.Rotulo, l.Antes, l.Valor, l.Frase, rodada > 1 && l.DaRodada)).ToArray();
            _textoResumo = rodada > 1 ? MsgResumoRefino(resumo.Count(l => l.DaRodada)) : MsgResumoPrimeira(resumo.Count);
        }
        else
        {
            _linhasResumo = [];
            _textoResumo = null;
        }
        _respostasComoFicou = etapa == EtapaCriacao.ComoFicou ? _criacao.ComoFicouAtual?.Respostas ?? [] : [];
        if (mudouPergunta && !mudouEtapa)
            TextoDoOutro = null;
        if (mudouEtapa)
        {
            TextoDoOutro = null;
            if (!PodeFechar)
                FolhaCancelarAberta = false;
        }
        if (etapa == EtapaCriacao.Fechada)
        {
            FolhaCancelarAberta = false;
            ErroNome = null;
        }

        Avisar(nameof(Aberta));
        Avisar(nameof(Etapa));
        Avisar(nameof(NasPerguntas));
        Avisar(nameof(Ajustando));
        Avisar(nameof(NoResumo));
        Avisar(nameof(NoComoFicou));
        Avisar(nameof(NoSalvar));
        Avisar(nameof(NomeSetup));
        Avisar(nameof(PodeFechar));
        Avisar(nameof(TemPergunta));
        Avisar(nameof(TextoEspera));
        Avisar(nameof(NumeroPergunta));
        Avisar(nameof(TextoPergunta));
        Avisar(nameof(RespostasPergunta));
        Avisar(nameof(AjustesDaRodada));
        Avisar(nameof(TextoResumo));
        Avisar(nameof(LinhasResumo));
        Avisar(nameof(RespostasComoFicou));
    }

    public void Dispose()
    {
        _descartado = true;
        _criacao.Mudou -= AoMudar;
    }
}
