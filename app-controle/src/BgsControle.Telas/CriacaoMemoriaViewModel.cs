using System.ComponentModel;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;
using BgsControle.Telas.Esquema;

namespace BgsControle.Telas;

/// <summary>Uma linha de ajuste na tela da criação: "Ganho da saída 3: +0,0 dB → +2,0 dB", a frase, o motivo e o destaque.</summary>
/// <param name="Valor">O valor que o aparelho confirmou (o relido).</param>
/// <param name="Destaque">Mexido nesta rodada (no resumo do refino, em destaque).</param>
/// <param name="Motivo">Por que o ajuste foi feito (embaixo, no resumo).</param>
/// <param name="Pedido">O valor pedido; null quando não se sabe (no resumo, é o relido).</param>
/// <param name="Diferente">O aparelho ficou com um valor diferente do pedido.</param>
public sealed record LinhaCriacao(string Rotulo, string Antes, string Valor, string Frase, bool Destaque, string Motivo = "", string? Pedido = null,
    bool Diferente = false)
{
    public string Linha => $"{Rotulo}: {Antes} → {Valor}";
}

/// <summary>
/// As fases visuais da criação (história 10d). A tela anda atrás do núcleo: o núcleo escreve no ritmo da porta,
/// e a tela só mostra o resumo depois que o palco mostrou cada ajuste da rodada, o núcleo chegou ao resumo e o
/// Aplicando terminou.
/// </summary>
public enum FaseCriacao
{
    /// <summary>Nenhuma criação aberta: a camada não aparece.</summary>
    Fechada,

    /// <summary>Uma pergunta por tela, com ‹ e ✕.</summary>
    Perguntas,

    /// <summary>Cada ajuste como frase e valor, um por vez (só com a animação).</summary>
    Palco,

    /// <summary>Pião e cambalhota, do anel do chão até o anel do Aplicando (só com a animação).</summary>
    Passagem,

    /// <summary>APLICANDO, com o esquema de ligação acendendo canal por canal.</summary>
    Aplicando,

    /// <summary>"Pronto. Ouça um pouco." e "Avaliar o som".</summary>
    Resumo,

    /// <summary>"Como ficou?", com ✕.</summary>
    ComoFicou,

    /// <summary>"Salvar no Casa 2.1?", o nome, Salvar e "Ajustar mais".</summary>
    Salvar,

    /// <summary>
    /// A escuta "1 ou 2" (história 14b): o texto, os botões grandes "1" e "2" (que trocam o som ao vivo) e "Prefiro o 1",
    /// "Prefiro o 2" e "Tanto faz", com ✕.
    /// </summary>
    Escuta,
}

/// <summary>
/// O ajuste que o palco mostra agora: a frase (a palavra-chave em ciano é a última palavra), o valor de antes
/// riscado, a seta e o valor relido, e a barrinha (0 a 1, da posição antiga à nova).
/// </summary>
/// <param name="Inicio">A frase sem a última palavra ("Ajustando o"); vazio numa frase de uma palavra.</param>
/// <param name="Chave">A última palavra ("grave"), em ciano.</param>
/// <param name="OutroValor">O aparelho ficou com outro valor (diferente do pedido, ou confirmado só pela releitura).</param>
public sealed record AjustePalco(string Frase, string Inicio, string Chave, string Antes, string Valor, double PosicaoAntes, double PosicaoValor,
    bool OutroValor)
{
    /// <summary>"O aparelho ficou com outro valor." com <see cref="OutroValor"/>, ou null.</summary>
    public string? Nota => OutroValor ? CriacaoMemoriaViewModel.TextoOutroValor : null;

    /// <summary>O TalkBack, uma vez por ajuste: "Ajustando o grave: de +4,0 dB para +6,0 dB".</summary>
    public string Anuncio => $"{Frase}: de {Antes} para {Valor}";
}

/// <summary>Uma resposta da pergunta ou do "Como ficou?": o texto, se está marcada e o toque.</summary>
public sealed class OpcaoCriacao : TelaBase
{
    private readonly bool _marcadaAntes;
    private bool _marcada;

    /// <param name="tocar">Responde; devolve false se a resposta não passou (a pergunta mudou, ou o núcleo recusou).</param>
    internal OpcaoCriacao(string texto, bool marcada, Func<Task<bool>> tocar, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Texto = texto;
        _marcada = marcada;
        _marcadaAntes = marcada;
        Tocar = new Comando(async () =>
        {
            if (!await tocar())
                Apagar();
        });
    }

    public string Texto { get; }

    /// <summary>A resposta dada (ao voltar com ‹) ou a que acabou de ser tocada (acende antes de avançar).</summary>
    public bool Marcada
    {
        get => _marcada;
        private set
        {
            if (_marcada == value)
                return;
            _marcada = value;
            Avisar();
        }
    }

    /// <summary>A resposta acende (a página espera 0,18 s e então chama <see cref="Tocar"/>).</summary>
    public void Acender() => Marcada = true;

    /// <summary>A resposta não passou: volta a marca de antes (a resposta dada, ao voltar com ‹; senão, apagada).</summary>
    public void Apagar() => Marcada = _marcadaAntes;

    /// <summary>Responde e avança; se a resposta não passa, a marca volta.</summary>
    public Comando Tocar { get; }
}

/// <summary>
/// A criação da memória do lado do usuário (CAP-18; <c>porta-de-comandos.md</c>, seção "Criação da memória"; a
/// tela é a da história 10d). Uma camada em tela toda, sem abas, que segue <see cref="Fase"/>:
/// <list type="bullet">
/// <item><b>Perguntas:</b> uma por tela, as respostas mandadas e "Outro…" (o texto do usuário; vazio não
/// responde); ‹ volta à anterior com a resposta marcada.</item>
/// <item><b>Palco e passagem</b> (com a animação): cada ajuste da rodada, um por vez, e o anel que vira o do
/// Aplicando. A página chama <see cref="PalcoMostrou"/> e <see cref="PassagemTerminou"/>.</item>
/// <item><b>Aplicando:</b> o esquema de ligação do Setup, com os canais acendendo (<see cref="AvancarCanal"/>,
/// <see cref="AplicandoTerminou"/>). Sem a animação, ou com o movimento reduzido, entra direto e cada canal acende
/// quando um ajuste nele é relido.</item>
/// <item><b>Resumo:</b> "Pronto. Ouça um pouco.", quantos ajustes (no refino, os da rodada em destaque) e
/// "Avaliar o som".</item>
/// <item><b>Como ficou?:</b> "Ótimo, pode salvar" (do app), as respostas mandadas e "Outro…".</item>
/// <item><b>Salvar:</b> o nome e Salvar (só aqui a memória nasce), ou "Ajustar mais".</item>
/// <item><b>Escuta "1 ou 2"</b> (história 14b, só no refino): o texto, os botões grandes "1" e "2", que trocam o som ao
/// vivo (a versão marcada é a que toca), e "Prefiro o 1", "Prefiro o 2" e "Tanto faz". Uma escrita que não confere mostra
/// "O aparelho ficou com outro valor." (<see cref="AvisoEscuta"/>) e a escuta fica.</item>
/// <item><b>✕</b> (nas perguntas, no "Como ficou?" e na escuta): a folha "Cancelar a nova memória?", com "Cancelar e
/// voltar" e "Continuar criando".</item>
/// </list>
/// Nenhum texto menciona quem manda as perguntas.
/// </summary>
public sealed class CriacaoMemoriaViewModel : TelaBase, IDisposable
{
    public const string TextoOutroBotao = "Outro…";
    public const string TextoCampoOutro = "Escreva do seu jeito";
    public const string TextoOkBotao = "OK";
    public const string TituloResumoTexto = "Pronto. Ouça um pouco.";
    public const string TextoAvaliarBotao = "Avaliar o som";
    public const string TituloComoFicouTexto = "Como ficou?";
    public const string TextoPodeSalvarBotao = CriacaoMemoria.OtimoPodeSalvar;
    public const string TextoSalvarBotao = "Salvar";
    public const string TextoSalvandoBotao = "Salvando";
    public const string TextoAjustarMaisBotao = "Ajustar mais";
    public const string TituloFolhaCancelarTexto = "Cancelar a nova memória?";
    public const string TextoCancelarEVoltarBotao = "Cancelar e voltar";
    public const string TextoContinuarCriandoBotao = "Continuar criando";
    public const string TextoAjustando = "Ajustando o som…";
    public const string TextoEsperandoPergunta = "Preparando a próxima pergunta…";
    public const string TituloBarraTexto = "Nova memória";
    public const string TextoTocandoAgora = "tocando agora";
    public const string TextoAplicandoRotulo = "APLICANDO";
    public const string TextoOutroValor = "O aparelho ficou com outro valor.";
    public const string TextoExplicaSalvar = "Guarda toda a regulagem que está tocando agora.";
    public const string RotuloNomeMemoria = "Nome da memória";
    public const string DescricaoVoltar = "Voltar";
    public const string DescricaoCancelar = "Cancelar";
    public const string DescricaoAnel = "Aplicando";
    public const string TextoVersao1 = "1";
    public const string TextoVersao2 = "2";
    public const string TextoPrefiro1Botao = "Prefiro o 1";
    public const string TextoPrefiro2Botao = "Prefiro o 2";
    public const string TextoTantoFazBotao = "Tanto faz";

    /// <summary>O TalkBack de um botão da escuta: "Ouvir a versão 1, tocando agora" ou "Ouvir a versão 2".</summary>
    public static string DescricaoVersao(int versao, bool tocando) => tocando ? $"Ouvir a versão {versao}, tocando agora" : $"Ouvir a versão {versao}";

    /// <summary>O tamanho máximo do nome da memória (a regra do núcleo).</summary>
    public const int TamanhoMaximoNome = BgsControle.Nucleo.Presets.GerentePresets.TamanhoMaximoNome;

    /// <summary>"Salvar no Casa 2.1?" (sem Setup: "Salvar a memória?").</summary>
    public static string TituloSalvarDe(string? setup) => setup is null ? "Salvar a memória?" : $"Salvar no {setup}?";

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

    /// <summary>
    /// Onde o valor fica na barrinha do palco (0 a 1): pela faixa do parâmetro (em Hz, na escala logarítmica),
    /// pela posição da opção, ou 0 num valor especial ("OFF", "auto") e sem leitura.
    /// </summary>
    public static double PosicaoNaBarra(string codigo, string? noFio)
    {
        if (noFio is null || Catalogo.ParametroDe(codigo) is not { } p || p.Decodificar(noFio).Valor is not { } v)
            return 0;
        if (v.Rotulo is not null && p.Opcoes is { Count: > 1 } opcoes && v.Numero is { } escolhida)
        {
            var ordem = opcoes.Keys.Order().ToList();
            var i = ordem.IndexOf((int)escolhida);
            return i < 0 ? 0 : (double)i / (ordem.Count - 1);
        }
        if (v.Rotulo is not null && p.Tipo == TipoValor.Roteamento)
            return v.Texto switch { "a" => 0, "b" => 1, _ => 0.5 };
        if (v.Rotulo is not null || v.Numero is not { } n || p.Minimo is not { } min || p.Maximo is not { } max || max <= min)
            return 0;
        if (p.Tipo == TipoValor.Hertz && min > 0 && n > 0)
            return Math.Clamp(Math.Log((double)n / (double)min) / Math.Log((double)max / (double)min), 0, 1);
        return Math.Clamp((double)((n - min) / (max - min)), 0, 1);
    }

    private readonly CriacaoMemoria _criacao;
    private readonly object _trava = new();
    private readonly GerenteSetups? _setups;
    private readonly FolhaRiscoViewModel? _folhaRisco;
    private readonly PreferenciasApp? _preferencias;

    private EtapaCriacao _etapa;
    private FaseCriacao _fase;
    private string? _nomeSetup;
    private PerguntaCriacao? _pergunta;
    private int? _numeroMostrado;
    private bool _voltando;
    private IReadOnlyList<OpcaoCriacao> _opcoes = [];
    private string? _chaveOpcoes;
    private bool _campoOutroAberto;
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
    private bool _salvando;
    private string? _avisoFinal;
    private bool _movimentoReduzido;
    private bool _descartado;

    // A rodada que a tela mostra: o modo (com ou sem animação), a fila do palco e os canais do Aplicando.
    private int _rodadaVista;
    private bool _comAnimacao;
    private int _vistos;
    private readonly Queue<AjusteCriacao> _fila = new();
    private AjustePalco? _ajusteDoPalco;
    private EsquemaLigacao _esquema = EsquemaLigacao.Nenhum;
    private readonly EstadoCanal[] _canais = new EstadoCanal[4];
    private int _canalAtual = -1;

    // História 14b: a escuta "1 ou 2" que a tela mostra.
    private EscutaCriacao? _escuta;
    private int _escrevendo;

    public CriacaoMemoriaViewModel(CriacaoMemoria criacao, IDespachante despachante, TimeProvider relogio, GerenteSetups? setups = null,
        FolhaRiscoViewModel? folhaRisco = null, PreferenciasApp? preferencias = null)
        : base(despachante, relogio)
    {
        _criacao = criacao;
        _setups = setups;
        _folhaRisco = folhaRisco;
        _preferencias = preferencias;
        Responder = new ComandoCom<int>(i => Mostrar(ResponderNoNucleo(i)));
        ResponderOutro = new Comando(AoResponderOutro);
        AbrirOutro = new Comando(AoAbrirOutro);
        Avaliar = new Comando(() => Mostrar(_criacao.AvaliarSom()));
        PodeSalvar = new Comando(() => Mostrar(_criacao.PodeSalvar()));
        AjustarMais = new Comando(AoAjustarMais);
        Salvar = new Comando(SalvarAsync);
        Fechar = new Comando(AbrirFolhaCancelar);
        Anterior = new Comando(VoltarPergunta);
        CancelarEVoltar = new Comando(CancelarEVoltarAsync);
        ContinuarCriando = new Comando(() => FolhaCancelarAberta = false);
        Ouvir1 = new Comando(() => OuvirAsync(1));
        Ouvir2 = new Comando(() => OuvirAsync(2));
        Prefiro1 = new Comando(() => EscolherAsync(EscolhaEscuta.Um));
        Prefiro2 = new Comando(() => EscolherAsync(EscolhaEscuta.Dois));
        TantoFaz = new Comando(() => EscolherAsync(EscolhaEscuta.TantoFaz));
        _criacao.Mudou += AoMudar;
        _criacao.Aconteceu += AoAcontecer;
        if (_folhaRisco is not null)
            _folhaRisco.PropertyChanged += AoMudarFolhaRisco;
        if (_preferencias is not null)
            _preferencias.Mudou += AoMudarPreferencias;
        Atualizar();
    }

    // ------------------------------------------------------------------ o que a tela mostra

    /// <summary>Uma criação está aberta: a tela toda aparece.</summary>
    public bool Aberta => _fase != FaseCriacao.Fechada;

    /// <summary>A etapa do núcleo (a tela pode estar atrás dela: veja <see cref="Fase"/>).</summary>
    public EtapaCriacao Etapa => _etapa;

    /// <summary>A fase visual: o que a tela mostra agora.</summary>
    public FaseCriacao Fase => _fase;

    public bool NasPerguntas => _etapa == EtapaCriacao.Perguntas;
    public bool Ajustando => _etapa == EtapaCriacao.Ajustando;
    public bool NoResumo => _etapa == EtapaCriacao.Resumo;
    public bool NoComoFicou => _etapa == EtapaCriacao.ComoFicou;
    public bool NoSalvar => _etapa == EtapaCriacao.Salvar;

    public bool FasePerguntas => _fase == FaseCriacao.Perguntas;
    public bool FasePalco => _fase == FaseCriacao.Palco;
    public bool FasePassagem => _fase == FaseCriacao.Passagem;
    public bool FaseAplicando => _fase == FaseCriacao.Aplicando;
    public bool FaseResumo => _fase == FaseCriacao.Resumo;
    public bool FaseComoFicou => _fase == FaseCriacao.ComoFicou;
    public bool FaseSalvar => _fase == FaseCriacao.Salvar;
    public bool FaseEscuta => _fase == FaseCriacao.Escuta;

    /// <summary>A pergunta ou o "Como ficou?": a lista de respostas com "Outro…".</summary>
    public bool ComRespostas => _fase is FaseCriacao.Perguntas or FaseCriacao.ComoFicou;

    /// <summary>O Setup da memória nova ("Casa 2.1").</summary>
    public string? NomeSetup => _nomeSetup;

    /// <summary>"Nova memória", em todas as fases (o nome só existe no Salvar).</summary>
    public string TituloBarra => TituloBarraTexto;

    /// <summary>O nome do Setup; no "Como ficou?", "tocando agora".</summary>
    public string? SubtituloBarra => _fase == FaseCriacao.ComoFicou ? TextoTocandoAgora : _nomeSetup;

    /// <summary>O ✕ aparece nas perguntas, no "Como ficou?" e na escuta.</summary>
    public bool PodeFechar => _fase is FaseCriacao.Perguntas or FaseCriacao.ComoFicou or FaseCriacao.Escuta;

    /// <summary>O ‹ aparece da 2ª pergunta em diante.</summary>
    public bool PodeVoltar => _fase == FaseCriacao.Perguntas && NumeroNaTela > 1;

    /// <summary>A pergunta entrou pelo ‹ (vem da esquerda; senão, da direita).</summary>
    public bool Voltando => _voltando;

    /// <summary>Há uma pergunta na tela.</summary>
    public bool TemPergunta => _pergunta is not null;

    /// <summary>"Preparando a próxima pergunta…" entre uma pergunta e outra, "Ajustando o som…" nos ajustes, ou null.</summary>
    public string? TextoEspera => NasPerguntas && _pergunta is null ? TextoEsperandoPergunta : Ajustando ? TextoAjustando : null;

    /// <summary>O número da pergunta na tela (1 = a primeira), ou null.</summary>
    public int? NumeroPergunta => _pergunta?.Numero;

    /// <summary>O texto da pergunta, ou null.</summary>
    public string? TextoPergunta => _pergunta?.Texto;

    /// <summary>As respostas da pergunta (sem o "Outro…", que vem de <see cref="TextoOutro"/>).</summary>
    public IReadOnlyList<string> RespostasPergunta => _pergunta?.Respostas ?? [];

    /// <summary>As respostas para tocar: as da pergunta, ou "Ótimo, pode salvar" e as do "Como ficou?".</summary>
    public IReadOnlyList<OpcaoCriacao> Opcoes => _opcoes;

    /// <summary>"Outro…".</summary>
    public string TextoOutro => TextoOutroBotao;

    /// <summary>O botão do "Outro…": a resposta escrita (ao voltar com ‹), ou "Outro…".</summary>
    public string TextoBotaoOutro => _pergunta is { DoOutro: true, Resposta: { } escrita } && _fase == FaseCriacao.Perguntas ? escrita : TextoOutroBotao;

    /// <summary>A resposta dada foi a do "Outro…" (ao voltar com ‹).</summary>
    public bool OutroMarcado => _fase == FaseCriacao.Perguntas && _pergunta is { DoOutro: true };

    /// <summary>O "Outro…" virou o campo, com OK ao lado.</summary>
    public bool CampoOutroAberto
    {
        get => _campoOutroAberto;
        private set
        {
            if (_campoOutroAberto == value)
                return;
            _campoOutroAberto = value;
            Avisar();
        }
    }

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

    /// <summary>Cada endereço mexido, com o último ajuste e o motivo; no refino, os da rodada em destaque.</summary>
    public IReadOnlyList<LinhaCriacao> LinhasResumo => _linhasResumo;

    /// <summary>"Avaliar o som".</summary>
    public string TextoAvaliar => TextoAvaliarBotao;

    /// <summary>"Como ficou?".</summary>
    public string TituloComoFicou => TituloComoFicouTexto;

    /// <summary>"Ótimo, pode salvar" (o primeiro botão do "Como ficou?").</summary>
    public string TextoPodeSalvar => TextoPodeSalvarBotao;

    /// <summary>As respostas do "Como ficou?" depois de "Ótimo, pode salvar" (sem o "Outro…").</summary>
    public IReadOnlyList<string> RespostasComoFicou => _respostasComoFicou;

    /// <summary>"Salvar no Casa 2.1?".</summary>
    public string TituloSalvar => TituloSalvarDe(_nomeSetup);

    /// <summary>"Guarda toda a regulagem que está tocando agora."</summary>
    public string ExplicaSalvar => TextoExplicaSalvar;

    /// <summary>"Nome da memória".</summary>
    public string RotuloNome => RotuloNomeMemoria;

    /// <summary>"Salvar" ("Salvando" enquanto salva).</summary>
    public string TextoSalvar => _salvando ? TextoSalvandoBotao : TextoSalvarBotao;

    /// <summary>O Salvar está andando (o anel pequeno no botão).</summary>
    public bool Salvando
    {
        get => _salvando;
        private set
        {
            if (_salvando == value)
                return;
            _salvando = value;
            Avisar();
            Avisar(nameof(TextoSalvar));
        }
    }

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

    /// <summary>
    /// O aviso que a casca mostra por 2,6 s quando a criação fecha: "Memória MUSICA ORIG criada no Casa 2.1." ao
    /// salvar, ou "Nova memória cancelada. O som voltou ao de antes." ao cancelar. Avisa a cada vez, mesmo igual.
    /// </summary>
    public string? AvisoFinal
    {
        get => _avisoFinal;
        private set
        {
            _avisoFinal = value;
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

    // ------------------------------------------------------------------ a escuta "1 ou 2" (história 14b)

    /// <summary>O texto da escuta ("Qual grave você prefere?"), na fase da escuta; senão null.</summary>
    public string? TextoEscuta => _fase == FaseCriacao.Escuta ? _escuta?.Texto : null;

    /// <summary>A versão que toca agora (1 ou 2), na fase da escuta; senão null.</summary>
    public int? VersaoMarcada => _fase == FaseCriacao.Escuta ? _escuta?.Marcada : null;

    /// <summary>O botão "1" está marcado (a versão 1 toca).</summary>
    public bool Marcado1 => VersaoMarcada == 1;

    /// <summary>O botão "2" está marcado (a versão 2 toca).</summary>
    public bool Marcado2 => VersaoMarcada == 2;

    /// <summary>"1".</summary>
    public string TextoBotao1 => TextoVersao1;

    /// <summary>"2".</summary>
    public string TextoBotao2 => TextoVersao2;

    /// <summary>O TalkBack do botão "1".</summary>
    public string DescricaoBotao1 => DescricaoVersao(1, Marcado1);

    /// <summary>O TalkBack do botão "2".</summary>
    public string DescricaoBotao2 => DescricaoVersao(2, Marcado2);

    /// <summary>"Prefiro o 1".</summary>
    public string TextoPrefiro1 => TextoPrefiro1Botao;

    /// <summary>"Prefiro o 2".</summary>
    public string TextoPrefiro2 => TextoPrefiro2Botao;

    /// <summary>"Tanto faz".</summary>
    public string TextoTantoFaz => TextoTantoFazBotao;

    /// <summary>"O aparelho ficou com outro valor." quando a última troca não conferiu; senão null.</summary>
    public string? AvisoEscuta => _fase == FaseCriacao.Escuta ? _escuta?.Aviso : null;

    /// <summary>"1": o som troca ao vivo para a versão 1.</summary>
    public Comando Ouvir1 { get; }

    /// <summary>"2": o som troca ao vivo para a versão 2.</summary>
    public Comando Ouvir2 { get; }

    /// <summary>"Prefiro o 1": o som fica na versão 1 e a criação segue.</summary>
    public Comando Prefiro1 { get; }

    /// <summary>"Prefiro o 2": o som fica na versão 2 e a criação segue.</summary>
    public Comando Prefiro2 { get; }

    /// <summary>"Tanto faz": o som volta à versão 1 e a criação segue.</summary>
    public Comando TantoFaz { get; }

    /// <summary>A troca ou a escolha está escrevendo no aparelho: "1", "2", "Prefiro" e "Tanto faz" ficam travados.</summary>
    public bool Escrevendo => Volatile.Read(ref _escrevendo) > 0;

    /// <summary>Os botões da escuta respondem (na escuta e sem escrita andando).</summary>
    public bool PodeTocarEscuta => _fase == FaseCriacao.Escuta && !Escrevendo;

    private Task OuvirAsync(int versao) => NaEscutaAsync(() => _criacao.OuvirAsync(versao));

    private Task EscolherAsync(EscolhaEscuta escolha) => NaEscutaAsync(() => _criacao.EscolherAsync(escolha));

    /// <summary>Um toque da escuta: um por vez; um toque que chega com a escuta já fechada não mostra nada.</summary>
    private async Task NaEscutaAsync(Func<Task<ResultadoCriacao>> tocar)
    {
        if (_fase != FaseCriacao.Escuta || Interlocked.CompareExchange(ref _escrevendo, 1, 0) != 0)
            return;
        AvisarEscrevendo();
        ResultadoCriacao r;
        try
        {
            r = await tocar();
        }
        finally
        {
            Volatile.Write(ref _escrevendo, 0);
            AvisarEscrevendo();
        }
        if (!r.Ok && r.Motivo == CriacaoMemoria.MsgSemEscuta)
        {
            Atualizar();
            return;
        }
        Mostrar(r);
    }

    private void AvisarEscrevendo()
    {
        Avisar(nameof(Escrevendo));
        Avisar(nameof(PodeTocarEscuta));
    }

    /// <summary>"Continuar criando".</summary>
    public string TextoContinuarCriando => TextoContinuarCriandoBotao;

    // ------------------------------------------------------------------ palco, passagem e Aplicando

    /// <summary>"Animação ao criar o som" (Configurações do app); ligada sem preferências.</summary>
    public bool AnimacaoLigada => _preferencias?.AnimacaoCriacao ?? true;

    /// <summary>O movimento reduzido do Android (as animações do sistema em 0). A página lê ao aparecer.</summary>
    public bool MovimentoReduzido
    {
        get => _movimentoReduzido;
        set
        {
            if (_movimentoReduzido == value)
                return;
            _movimentoReduzido = value;
            Avisar();
        }
    }

    /// <summary>
    /// A rodada que a tela mostra tem palco e passagem (a animação ligada e sem o movimento reduzido, como
    /// estavam quando a rodada começou). Sem eles, o Aplicando aparece desde o começo da rodada.
    /// </summary>
    public bool ComAnimacao => _comAnimacao;

    /// <summary>A folha de risco está por cima: o palco pausa até a resposta.</summary>
    public bool Pausado => _folhaRisco?.FolhaAberta == true;

    /// <summary>O ajuste que o palco mostra agora, ou null (o anel gira e o palco espera).</summary>
    public AjustePalco? AjusteDoPalco => _ajusteDoPalco;

    /// <summary>O anúncio do TalkBack do ajuste do palco, ou null.</summary>
    public string? AnuncioPalco => _ajusteDoPalco?.Anuncio;

    /// <summary>"APLICANDO".</summary>
    public string TextoAplicando => TextoAplicandoRotulo;

    /// <summary>O nome embaixo do anel do Aplicando: "Nova memória" (a memória ainda não tem nome).</summary>
    public string NomeAplicando => TituloBarraTexto;

    /// <summary>"Aplicando" (o TalkBack do anel).</summary>
    public string TextoDescricaoAnel => DescricaoAnel;

    /// <summary>O esquema de ligação do Setup da criação.</summary>
    public EsquemaLigacao Esquema => _esquema;

    /// <summary>CH1 a CH4: aceso, gravado ou ainda não.</summary>
    public IReadOnlyList<EstadoCanal> EstadosCanais => _canais.ToArray();

    /// <summary>O TalkBack do esquema: cada canal e o que ele alimenta.</summary>
    public string DescricaoEsquema => _esquema.Descricao;

    /// <summary>
    /// O palco terminou o ajuste da vez (a entrada, a parada de 1,3 s, a saída e o intervalo): vem o próximo da
    /// fila; sem nenhum e com o núcleo no resumo, a passagem. Pausado, não anda (devolve false).
    /// </summary>
    public bool PalcoMostrou()
    {
        lock (_trava)
            return PalcoMostrouSemTrava();
    }

    private bool PalcoMostrouSemTrava()
    {
        if (_fase != FaseCriacao.Palco || _ajusteDoPalco is null || Pausado)
            return false;
        _ajusteDoPalco = null;
        Atualizar();
        return true;
    }

    /// <summary>A passagem terminou: entra o Aplicando, com todos os canais apagados.</summary>
    public bool PassagemTerminou()
    {
        lock (_trava)
            return PassagemTerminouSemTrava();
    }

    private bool PassagemTerminouSemTrava()
    {
        if (_fase != FaseCriacao.Passagem || Pausado)
            return false;
        ApagarCanais();
        MudarFase(FaseCriacao.Aplicando);
        AvisarCanais();
        return true;
    }

    /// <summary>
    /// Com a animação, o próximo canal acende (0,7 s cada, de CH1 a CH4) e o anterior fica gravado. Depois do
    /// CH4, todos ficam gravados e devolve false.
    /// </summary>
    public bool AvancarCanal()
    {
        lock (_trava)
            return AvancarCanalSemTrava();
    }

    private bool AvancarCanalSemTrava()
    {
        if (_fase != FaseCriacao.Aplicando || !_comAnimacao || Pausado || _canalAtual >= _canais.Length)
            return false;
        if (_canalAtual >= 0)
            _canais[_canalAtual] = EstadoCanal.Gravado;
        _canalAtual++;
        var mais = _canalAtual < _canais.Length;
        if (mais)
            _canais[_canalAtual] = EstadoCanal.Aceso;
        AvisarCanais();
        return mais;
    }

    /// <summary>O Aplicando terminou (tudo gravado, mais 1 s e a saída): entra o resumo, se o núcleo já está nele.</summary>
    public bool AplicandoTerminou()
    {
        lock (_trava)
            return AplicandoTerminouSemTrava();
    }

    private bool AplicandoTerminouSemTrava()
    {
        if (_fase != FaseCriacao.Aplicando || Pausado)
            return false;
        if (_etapa == EtapaCriacao.Ajustando)
            return false; // sem a animação, o resumo entra sozinho com a etapa
        for (var i = 0; i < _canais.Length; i++)
            _canais[i] = EstadoCanal.Gravado;
        _canalAtual = _canais.Length;
        AvisarCanais();
        MudarFase(FaseDaEtapa(_etapa));
        return true;
    }

    // ------------------------------------------------------------------ os toques

    /// <summary>O toque numa resposta (0 = a primeira), da pergunta ou do "Como ficou?".</summary>
    public ComandoCom<int> Responder { get; }

    /// <summary>Responde com o texto do "Outro…" (vazio não responde).</summary>
    public Comando ResponderOutro { get; }

    /// <summary>O "Outro…" vira o campo ("Escreva do seu jeito"), com a resposta escrita antes, se houver.</summary>
    public Comando AbrirOutro { get; }

    /// <summary>"Avaliar o som".</summary>
    public Comando Avaliar { get; }

    /// <summary>"Ótimo, pode salvar".</summary>
    public Comando PodeSalvar { get; }

    /// <summary>"Ajustar mais" (do Salvar ao "Como ficou?").</summary>
    public Comando AjustarMais { get; }

    /// <summary>"Salvar", com o <see cref="NomeMemoria"/>.</summary>
    public Comando Salvar { get; }

    /// <summary>O ✕: abre a folha "Cancelar a nova memória?" (só nas perguntas, no "Como ficou?" e na escuta).</summary>
    public Comando Fechar { get; }

    /// <summary>O ‹: a pergunta anterior, com a resposta dada marcada.</summary>
    public Comando Anterior { get; }

    /// <summary>"Cancelar e voltar": nada é salvo e o som volta ao de antes.</summary>
    public Comando CancelarEVoltar { get; }

    /// <summary>"Continuar criando": a folha fecha e nada muda.</summary>
    public Comando ContinuarCriando { get; }

    /// <summary>
    /// A tecla Voltar do Android. Devolve se tratou: com a folha de cancelar aberta, "Continuar criando"; nas
    /// perguntas, o ‹ (na 1ª, o ✕); no "Como ficou?" e na escuta, o ✕; no Salvar, "Ajustar mais"; no palco, na passagem,
    /// no Aplicando e no resumo, nada (mas trata). Fechada, false.
    /// </summary>
    public bool Voltar()
    {
        if (_fase == FaseCriacao.Fechada)
            return false;
        if (FolhaCancelarAberta)
        {
            FolhaCancelarAberta = false;
            return true;
        }
        if (CampoOutroAberto)
        {
            CampoOutroAberto = false;
            return true;
        }
        switch (_fase)
        {
            case FaseCriacao.Perguntas when PodeVoltar:
                VoltarPergunta();
                break;
            case FaseCriacao.Perguntas or FaseCriacao.ComoFicou or FaseCriacao.Escuta:
                AbrirFolhaCancelar();
                break;
            case FaseCriacao.Salvar:
                AoAjustarMais();
                break;
        }
        return true;
    }

    private int NumeroNaTela => _numeroMostrado ?? _pergunta?.Numero ?? _criacao.Perguntas.Count + 1;

    private void VoltarPergunta()
    {
        if (!PodeVoltar)
            return;
        var anterior = NumeroNaTela - 1;
        if (_criacao.Perguntas.FirstOrDefault(p => p.Numero == anterior) is not { Respondida: true })
            return;
        _numeroMostrado = anterior;
        _voltando = true;
        CampoOutroAberto = false;
        TextoDoOutro = null;
        Aviso = null;
        Atualizar();
    }

    /// <summary>A pergunta da tela foi respondida de novo com a mesma resposta: a tela avança, sem a porta.</summary>
    private void Avancar(int numero)
    {
        var proxima = numero + 1;
        _numeroMostrado = _criacao.Perguntas.Any(p => p.Numero == proxima && p.Respondida) ? proxima : null;
        _voltando = false;
    }

    /// <summary>"Ajustar mais": nada enquanto o Salvar anda (o núcleo não volta ao "Como ficou?" no meio do Salvar).</summary>
    private void AoAjustarMais()
    {
        if (Salvando)
            return;
        Mostrar(_criacao.AjustarMais());
    }

    /// <summary>
    /// O toque numa resposta da pergunta <paramref name="numero"/>: só vale se ela ainda está na tela (um ‹, a tecla
    /// Voltar ou uma pergunta nova no meio dos 0,18 s não fazem o toque responder outra pergunta).
    /// </summary>
    private bool TocarPergunta(int numero, int indice)
    {
        if (_fase != FaseCriacao.Perguntas || _pergunta?.Numero != numero)
            return false;
        var r = ResponderNoNucleo(indice);
        Mostrar(r);
        return r.Ok;
    }

    /// <summary>O toque no "Como ficou?" da rodada (null = "Ótimo, pode salvar"): só vale nessa rodada, ainda na tela.</summary>
    private bool TocarComoFicou(int rodada, int? indice)
    {
        if (_fase != FaseCriacao.ComoFicou || _criacao.Rodada != rodada)
            return false;
        var r = indice is { } i ? _criacao.ResponderComoFicou(i) : _criacao.PodeSalvar();
        Mostrar(r);
        return r.Ok;
    }

    private ResultadoCriacao ResponderNoNucleo(int indice)
    {
        if (_etapa == EtapaCriacao.ComoFicou)
            return _criacao.ResponderComoFicou(indice);
        if (_numeroMostrado is { } n)
        {
            var r = _criacao.ResponderDeNovo(n, indice);
            if (r.Ok)
                Avancar(n);
            return r;
        }
        _voltando = false;
        return _criacao.Responder(indice);
    }

    private void AoAbrirOutro()
    {
        if (!ComRespostas)
            return;
        TextoDoOutro = OutroMarcado ? _pergunta?.Resposta : null;
        CampoOutroAberto = true;
    }

    private void AoResponderOutro()
    {
        ResultadoCriacao r;
        if (_etapa == EtapaCriacao.ComoFicou)
        {
            r = _criacao.ResponderComoFicouOutro(TextoDoOutro);
        }
        else if (_numeroMostrado is { } n)
        {
            r = _criacao.ResponderDeNovo(n, TextoDoOutro);
            if (r.Ok)
                Avancar(n);
        }
        else
        {
            _voltando = false;
            r = _criacao.ResponderOutro(TextoDoOutro);
        }
        if (r.Ok)
        {
            TextoDoOutro = null;
            CampoOutroAberto = false;
        }
        Mostrar(r);
    }

    private void Mostrar(ResultadoCriacao r)
    {
        Aviso = r.Ok ? null : r.Mensagem ?? r.Motivo;
        Atualizar();
    }

    private async Task SalvarAsync()
    {
        if (Salvando)
            return;
        Salvando = true;
        ResultadoCriacao r;
        try
        {
            r = await _criacao.SalvarAsync(NomeMemoria);
        }
        finally
        {
            Salvando = false;
        }
        if (r.Ok)
        {
            NomeMemoria = null;
            ErroNome = null;
            Aviso = null;
            var aviso = r.Mensagem;
            NaTela(() => AvisoFinal = aviso);
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

    /// <summary>Cancelada (pelo ✕, pela folha de risco ou pela porta): o aviso da casca diz se o som voltou.</summary>
    private void AoAcontecer(EventoCriacao e)
    {
        if (e.Tipo != CriacaoMemoria.EventoCancelada)
            return;
        var aviso = e.Voltou == true ? CriacaoMemoria.MsgCancelada : CriacaoMemoria.MsgNaoVoltou(e.Motivo ?? "falhou");
        NaTela(() =>
        {
            if (!_descartado)
                AvisoFinal = aviso;
        });
    }

    private void AoMudarFolhaRisco(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolhaRiscoViewModel.FolhaAberta))
            Avisar(nameof(Pausado));
    }

    private void AoMudarPreferencias() => NaTela(() => Avisar(nameof(AnimacaoLigada)));

    private static FaseCriacao FaseDaEtapa(EtapaCriacao etapa) => etapa switch
    {
        EtapaCriacao.Perguntas => FaseCriacao.Perguntas,
        EtapaCriacao.Resumo => FaseCriacao.Resumo,
        EtapaCriacao.ComoFicou => FaseCriacao.ComoFicou,
        EtapaCriacao.Salvar => FaseCriacao.Salvar,
        EtapaCriacao.Ajustando => FaseCriacao.Aplicando,
        EtapaCriacao.Escuta => FaseCriacao.Escuta,
        _ => FaseCriacao.Fechada,
    };

    /// <summary>Uma atualização de cada vez (o aviso do núcleo e um toque ou um sinal podem chegar juntos).</summary>
    private void Atualizar()
    {
        lock (_trava)
            AtualizarSemTrava();
    }

    private void AtualizarSemTrava()
    {
        if (_descartado)
            return;
        var etapa = _criacao.Etapa;
        var mudouEtapa = etapa != _etapa;
        _etapa = etapa;
        _nomeSetup = _criacao.NomeSetup;
        var ajustes = _criacao.Ajustes;
        var rodada = _criacao.Rodada;

        // Uma criação nova (fechou e abriu no meio): tudo da tela recomeça.
        if (etapa is EtapaCriacao.Fechada or EtapaCriacao.Perguntas || ajustes.Count < _vistos)
            RecomecarRodadas(ajustes.Count);

        // A pergunta na tela: a da vez, ou a anterior (‹) enquanto ela segue respondida.
        if (etapa != EtapaCriacao.Perguntas
            || (_numeroMostrado is { } n && _criacao.Perguntas.FirstOrDefault(p => p.Numero == n) is not { Respondida: true }))
            _numeroMostrado = null;
        var numeroAntes = _pergunta?.Numero;
        _pergunta = _numeroMostrado is { } mostrado
            ? _criacao.Perguntas.FirstOrDefault(p => p.Numero == mostrado)
            : _criacao.PerguntaAtual;
        // Uma pergunta nova (na mesma etapa) nasce com o "Outro…" vazio.
        var mudouPergunta = _pergunta is not null && _pergunta.Numero != numeroAntes;

        // Uma rodada nova de ajustes (a primeira, ou o refino): o modo, a fila e os canais recomeçam.
        if (etapa is EtapaCriacao.Ajustando or EtapaCriacao.Resumo && rodada != _rodadaVista)
            IniciarRodada(rodada);

        // Os ajustes novos: para a fila do palco, ou (sem a animação) direto nos canais do Aplicando.
        for (var i = _vistos; i < ajustes.Count; i++)
        {
            var a = ajustes[i];
            if (a.Rodada != _rodadaVista)
                continue;
            if (_comAnimacao)
                _fila.Enqueue(a);
            else
                AcenderCanaisDo(a);
        }
        _vistos = ajustes.Count;

        _ajustesDaRodada = ajustes
            .Where(a => a.Rodada == rodada)
            .Select(a => new LinhaCriacao(a.Rotulo, a.AntesExibicao, a.RelidoExibicao, a.Frase, true, a.Motivo, a.NovoExibicao, a.Diferente))
            .ToArray();
        if (etapa is EtapaCriacao.Resumo or EtapaCriacao.ComoFicou or EtapaCriacao.Salvar)
        {
            var resumo = _criacao.Resumo;
            _linhasResumo = resumo.Select(l => new LinhaCriacao(l.Rotulo, l.Antes, l.Valor, l.Frase, rodada > 1 && l.DaRodada, l.Motivo, l.Valor)).ToArray();
            _textoResumo = rodada > 1 ? MsgResumoRefino(resumo.Count(l => l.DaRodada)) : MsgResumoPrimeira(resumo.Count);
        }
        else
        {
            _linhasResumo = [];
            _textoResumo = null;
        }
        _respostasComoFicou = etapa == EtapaCriacao.ComoFicou ? _criacao.ComoFicouAtual?.Respostas ?? [] : [];
        _escuta = etapa == EtapaCriacao.Escuta ? _criacao.Escuta : null;

        AtualizarFase(etapa);
        AtualizarOpcoes();

        if (mudouPergunta && !mudouEtapa)
        {
            TextoDoOutro = null;
            CampoOutroAberto = false;
        }
        if (mudouEtapa)
        {
            TextoDoOutro = null;
            CampoOutroAberto = false;
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
        Avisar(nameof(SubtituloBarra));
        Avisar(nameof(TituloSalvar));
        Avisar(nameof(PodeFechar));
        Avisar(nameof(PodeVoltar));
        Avisar(nameof(Voltando));
        Avisar(nameof(TemPergunta));
        Avisar(nameof(TextoEspera));
        Avisar(nameof(NumeroPergunta));
        Avisar(nameof(TextoPergunta));
        Avisar(nameof(RespostasPergunta));
        Avisar(nameof(TextoBotaoOutro));
        Avisar(nameof(OutroMarcado));
        Avisar(nameof(AjustesDaRodada));
        Avisar(nameof(TextoResumo));
        Avisar(nameof(LinhasResumo));
        Avisar(nameof(RespostasComoFicou));
        AvisarEscuta();
    }

    private void AvisarEscuta()
    {
        Avisar(nameof(TextoEscuta));
        Avisar(nameof(VersaoMarcada));
        Avisar(nameof(Marcado1));
        Avisar(nameof(Marcado2));
        Avisar(nameof(DescricaoBotao1));
        Avisar(nameof(DescricaoBotao2));
        Avisar(nameof(AvisoEscuta));
    }

    /// <summary>
    /// A fase anda atrás da etapa: o palco, a passagem e o Aplicando só saem pelos sinais da página. O "Como
    /// ficou?" e o Salvar só chegam por um toque do usuário (depois do resumo): neles, a tela segue a etapa. A escuta
    /// (história 14b) entra na hora; depois da escolha, a rodada segue no palco (com a animação) ou no Aplicando.
    /// </summary>
    private void AtualizarFase(EtapaCriacao etapa)
    {
        if (etapa == EtapaCriacao.Escuta)
        {
            // A escuta entra na hora, e o palco guarda o que ainda não mostrou da rodada (volta a ele depois da escolha).
            MudarFase(FaseCriacao.Escuta);
            return;
        }
        if (etapa is EtapaCriacao.Fechada or EtapaCriacao.Perguntas or EtapaCriacao.ComoFicou or EtapaCriacao.Salvar)
        {
            _fila.Clear();
            if (_ajusteDoPalco is not null)
            {
                _ajusteDoPalco = null;
                Avisar(nameof(AjusteDoPalco));
                Avisar(nameof(AnuncioPalco));
            }
            MudarFase(FaseDaEtapa(etapa));
            return;
        }
        switch (_fase)
        {
            case FaseCriacao.Palco:
                if (_ajusteDoPalco is null && _fila.Count > 0)
                {
                    var a = _fila.Dequeue();
                    _ajusteDoPalco = ParaPalco(a);
                    Avisar(nameof(AjusteDoPalco));
                    Avisar(nameof(AnuncioPalco));
                }
                else if (_ajusteDoPalco is null && etapa != EtapaCriacao.Ajustando)
                {
                    Avisar(nameof(AjusteDoPalco));
                    Avisar(nameof(AnuncioPalco));
                    MudarFase(FaseCriacao.Passagem);
                }
                else if (_ajusteDoPalco is null)
                {
                    Avisar(nameof(AjusteDoPalco));
                    Avisar(nameof(AnuncioPalco));
                }
                break;
            case FaseCriacao.Passagem:
                break;
            case FaseCriacao.Escuta:
                // A escolha voltou aos ajustes da mesma rodada: os da versão escolhida entram no palco (com a animação).
                if (etapa == EtapaCriacao.Ajustando && _comAnimacao)
                {
                    MudarFase(FaseCriacao.Palco);
                    AtualizarFase(etapa);
                }
                else
                {
                    MudarFase(FaseDaEtapa(etapa));
                }
                break;
            case FaseCriacao.Aplicando:
                // Com a animação, só o AplicandoTerminou sai daqui; sem, o resumo entra com a etapa.
                if (!_comAnimacao && etapa != EtapaCriacao.Ajustando)
                {
                    for (var i = 0; i < _canais.Length; i++)
                    {
                        if (_canais[i] == EstadoCanal.Aceso)
                            _canais[i] = EstadoCanal.Gravado;
                    }
                    AvisarCanais();
                    MudarFase(FaseDaEtapa(etapa));
                }
                break;
            default:
                MudarFase(FaseDaEtapa(etapa));
                break;
        }
    }

    private void MudarFase(FaseCriacao fase, bool forcar = false)
    {
        if (_fase == fase && !forcar)
            return;
        _fase = fase;
        if (fase != FaseCriacao.Perguntas)
            _voltando = false;
        Avisar(nameof(Fase));
        Avisar(nameof(Aberta));
        Avisar(nameof(FasePerguntas));
        Avisar(nameof(FasePalco));
        Avisar(nameof(FasePassagem));
        Avisar(nameof(FaseAplicando));
        Avisar(nameof(FaseResumo));
        Avisar(nameof(FaseComoFicou));
        Avisar(nameof(FaseSalvar));
        Avisar(nameof(FaseEscuta));
        Avisar(nameof(PodeTocarEscuta));
        Avisar(nameof(ComRespostas));
        Avisar(nameof(PodeFechar));
        Avisar(nameof(PodeVoltar));
        Avisar(nameof(SubtituloBarra));
        Avisar(nameof(TextoBotaoOutro));
        Avisar(nameof(OutroMarcado));
        if (!PodeFechar)
            FolhaCancelarAberta = false;
    }

    private void RecomecarRodadas(int vistos)
    {
        _rodadaVista = 0;
        _vistos = vistos;
        _fila.Clear();
        if (_ajusteDoPalco is not null)
        {
            _ajusteDoPalco = null;
            Avisar(nameof(AjusteDoPalco));
            Avisar(nameof(AnuncioPalco));
        }
        ApagarCanais();
        AvisarCanais();
    }

    private void IniciarRodada(int rodada)
    {
        _rodadaVista = rodada;
        _comAnimacao = AnimacaoLigada && !MovimentoReduzido;
        _fila.Clear();
        _ajusteDoPalco = null;
        if (rodada == 1 || ReferenceEquals(_esquema, EsquemaLigacao.Nenhum))
        {
            _esquema = EsquemaLigacao.Montar(_setups?.Setup(_criacao.IdSetup)?.Equipamentos);
            Avisar(nameof(Esquema));
            Avisar(nameof(DescricaoEsquema));
        }
        ApagarCanais();
        // Sem a animação, o Aplicando aparece desde o começo da rodada.
        MudarFase(_comAnimacao ? FaseCriacao.Palco : FaseCriacao.Aplicando, forcar: true);
        Avisar(nameof(ComAnimacao));
        Avisar(nameof(AjusteDoPalco));
        Avisar(nameof(AnuncioPalco));
        AvisarCanais();
    }

    private void ApagarCanais()
    {
        for (var i = 0; i < _canais.Length; i++)
            _canais[i] = EstadoCanal.AindaNao;
        _canalAtual = -1;
    }

    /// <summary>Sem a animação: o canal do ajuste relido acende (com o vizinho do link); um global acende todos. O aceso de antes fica gravado.</summary>
    private void AcenderCanaisDo(AjusteCriacao a)
    {
        IReadOnlyList<int> canais = a.Indice is { } i && Catalogo.ParametroDe(a.Codigo)?.Escopo == Escopo.Canal && i is >= 0 and < 4
            ? _esquema.Juntos(i)
            : [0, 1, 2, 3];
        for (var c = 0; c < _canais.Length; c++)
        {
            if (_canais[c] == EstadoCanal.Aceso)
                _canais[c] = EstadoCanal.Gravado;
        }
        foreach (var c in canais)
            _canais[c] = EstadoCanal.Aceso;
        AvisarCanais();
    }

    private void AvisarCanais()
    {
        Avisar(nameof(EstadosCanais));
    }

    private static AjustePalco ParaPalco(AjusteCriacao a)
    {
        // A pontuação final não entra na palavra-chave nem no anúncio ("Ajustando o grave." → "grave").
        var frase = a.Frase.Trim().TrimEnd('.', '!', '?', '…', ',', ';', ':').TrimEnd();
        if (frase.Length == 0)
            frase = a.Frase.Trim();
        var corte = frase.LastIndexOf(' ');
        var inicio = corte < 0 ? "" : frase[..corte];
        var chave = corte < 0 ? frase : frase[(corte + 1)..];
        return new AjustePalco(frase, inicio, chave, a.AntesExibicao, a.RelidoExibicao,
            PosicaoNaBarra(a.Codigo, a.Antes), PosicaoNaBarra(a.Codigo, a.Relido), a.Diferente || a.SemEco);
    }

    /// <summary>Refaz as respostas só quando a pergunta, a resposta dada ou o "Como ficou?" mudam.</summary>
    private void AtualizarOpcoes()
    {
        string chave;
        Func<IReadOnlyList<OpcaoCriacao>> montar;
        if (_fase == FaseCriacao.Perguntas && _pergunta is { } p)
        {
            chave = $"p|{p.Numero}|{p.Texto}|{string.Join("|", p.Respostas)}|{p.Resposta}|{p.DoOutro}";
            var numero = p.Numero;
            montar = () => p.Respostas
                .Select((r, i) => new OpcaoCriacao(r, !p.DoOutro && p.Resposta == r, () => Task.FromResult(TocarPergunta(numero, i)),
                    Despachante, Relogio))
                .ToArray();
        }
        else if (_fase == FaseCriacao.ComoFicou)
        {
            var respostas = _respostasComoFicou;
            var rodada = _criacao.Rodada;
            chave = $"c|{rodada}|{string.Join("|", respostas)}";
            montar = () => new[] { new OpcaoCriacao(TextoPodeSalvarBotao, false, () => Task.FromResult(TocarComoFicou(rodada, null)), Despachante, Relogio) }
                .Concat(respostas.Select((r, i) => new OpcaoCriacao(r, false, () => Task.FromResult(TocarComoFicou(rodada, i)), Despachante, Relogio)))
                .ToArray();
        }
        else
        {
            chave = "";
            montar = () => [];
        }
        if (chave == _chaveOpcoes)
            return;
        _chaveOpcoes = chave;
        _opcoes = montar();
        Avisar(nameof(Opcoes));
    }

    public void Dispose()
    {
        _descartado = true;
        _criacao.Mudou -= AoMudar;
        _criacao.Aconteceu -= AoAcontecer;
        if (_folhaRisco is not null)
            _folhaRisco.PropertyChanged -= AoMudarFolhaRisco;
        if (_preferencias is not null)
            _preferencias.Mudou -= AoMudarPreferencias;
    }
}
