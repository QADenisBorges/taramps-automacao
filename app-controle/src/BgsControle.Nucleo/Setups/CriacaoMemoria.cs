using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Setups;

/// <summary>A etapa da criação da memória (CAP-18).</summary>
public enum EtapaCriacao
{
    /// <summary>Nenhuma criação aberta.</summary>
    Fechada,

    /// <summary>As perguntas, uma por tela, até a resposta à última.</summary>
    Perguntas,

    /// <summary>A rodada de ajustes: a porta escreve a regulagem, cada ajuste com a frase e o motivo.</summary>
    Ajustando,

    /// <summary>A rodada terminou: o resumo e "Avaliar o som".</summary>
    Resumo,

    /// <summary>"Como ficou?": "Ótimo, pode salvar", as respostas mandadas e "Outro…".</summary>
    ComoFicou,

    /// <summary>O Salvar com o nome (só o usuário) ou "Ajustar mais".</summary>
    Salvar,
}

/// <summary>Uma pergunta da criação, com as respostas mandadas e a do usuário (null = ainda sem resposta).</summary>
/// <param name="Numero">1 = a primeira.</param>
/// <param name="DoOutro">A resposta veio do "Outro…", com o texto do usuário.</param>
public sealed record PerguntaCriacao(int Numero, string Texto, IReadOnlyList<string> Respostas, bool Ultima, string? Resposta = null, bool DoOutro = false)
{
    public bool Respondida => Resposta is not null;
}

/// <summary>
/// Um ajuste da criação, como o aparelho o confirmou. Os valores vêm no fio, como o aparelho os devolve.
/// </summary>
/// <param name="Antes">O valor do espelho antes da escrita (null sem leitura).</param>
/// <param name="Novo">O valor pedido.</param>
/// <param name="Relido">O valor que o aparelho confirmou (eco ou releitura): vale este.</param>
/// <param name="Diferente">O aparelho ficou com um valor diferente do pedido.</param>
/// <param name="SemEco">Sem eco: o valor veio da releitura.</param>
/// <param name="Aviso">Com o link, a outra saída não acompanhou; ou null.</param>
public sealed record AjusteCriacao(int Rodada, string Codigo, int? Indice, string Frase, string Motivo, string? Antes, string? Novo, string? Relido,
    bool Diferente, bool SemEco, string? Aviso)
{
    public Endereco Endereco => new(Codigo, Indice);

    /// <summary>"Ganho da saída 3".</summary>
    public string Rotulo => Camadas.Rotulo(Codigo, Indice);

    public string AntesExibicao => Camadas.Exibir(Codigo, Antes);

    public string NovoExibicao => Camadas.Exibir(Codigo, Novo);

    public string RelidoExibicao => Camadas.Exibir(Codigo, Relido);
}

/// <summary>Uma linha do resumo: o endereço mexido, o valor de antes da criação e o do último ajuste.</summary>
/// <param name="Antes">Para a tela, o valor de antes da criação ("+0,0 dB").</param>
/// <param name="Valor">Para a tela, o valor relido do último ajuste ("+2,0 dB").</param>
/// <param name="DaRodada">O último ajuste é da rodada atual (em destaque).</param>
public sealed record LinhaResumoCriacao(string Codigo, int? Indice, string Rotulo, string Antes, string Valor, string Frase, string Motivo, bool DaRodada);

/// <summary>Um "Como ficou?": as respostas mandadas e a do usuário (null = ainda sem resposta).</summary>
/// <param name="Salvar">A resposta foi "Ótimo, pode salvar".</param>
public sealed record ComoFicouCriacao(int Rodada, IReadOnlyList<string> Respostas, string? Resposta = null, bool DoOutro = false, bool Salvar = false)
{
    public bool Respondida => Resposta is not null;
}

/// <summary>
/// Um evento da criação para a porta (<c>GET /eventos</c>): <c>criacao-pedida</c>, <c>criacao-resposta</c>,
/// <c>criacao-como-ficou</c>, <c>criacao-salva</c>, <c>criacao-cancelada</c> e <c>criacao-interrompida</c>. Só os
/// campos do tipo vêm preenchidos.
/// </summary>
public sealed record EventoCriacao(
    string Tipo,
    int? Numero = null,
    string? Texto = null,
    bool? DoOutro = null,
    bool? Salvar = null,
    int? Rodada = null,
    string? Por = null,
    bool? Voltou = null,
    string? Memoria = null,
    string? Motivo = null);

/// <summary>Como terminou um pedido à criação. <see cref="Invalido"/>: o pedido veio mal formado (a porta dá 400).</summary>
public sealed record ResultadoCriacao(bool Ok, string? Motivo, string? Mensagem = null, bool Invalido = false)
{
    public static ResultadoCriacao Feito(string? mensagem = null) => new(true, null, mensagem);

    public static ResultadoCriacao Falha(string motivo) => new(false, motivo, motivo);

    public static ResultadoCriacao Mal(string motivo) => new(false, motivo, motivo, Invalido: true);
}

/// <summary>Como terminou um ajuste da criação: o ajuste guardado, ou o motivo.</summary>
public sealed record ResultadoAjusteCriacao(bool Ok, string? Motivo, AjusteCriacao? Ajuste = null, bool Invalido = false)
{
    public static ResultadoAjusteCriacao Falha(string motivo) => new(false, motivo);
}

/// <summary>
/// A criação da memória (CAP-18, <c>porta-de-comandos.md</c>, seção "Criação da memória"): a porta vai nos
/// dois sentidos. O cliente pareado abre, manda as perguntas, os ajustes e o "Como ficou?"; o app mostra, devolve
/// cada resposta por evento (<see cref="Aconteceu"/>) e só aceita os ajustes depois da resposta à última pergunta.
/// <list type="bullet">
/// <item><b>Etapas:</b> <see cref="EtapaCriacao.Perguntas"/> → <see cref="EtapaCriacao.Ajustando"/> →
/// <see cref="EtapaCriacao.Resumo"/> → <see cref="EtapaCriacao.ComoFicou"/>; "Ótimo, pode salvar" leva ao
/// <see cref="EtapaCriacao.Salvar"/>, e qualquer outra resposta volta a ajustar, numa rodada nova (refino). Uma
/// criação por vez.</item>
/// <item><b>Cópia antes</b> (<c>failure-modes.md</c>, regra 3): ao abrir, a <see cref="CopiaAntes"/> guarda o
/// retrato e a marca <see cref="Tipo"/>; o primeiro ajuste anota a etapa <see cref="EtapaAjustes"/>. Enquanto a
/// marca existe, toda escrita de áudio de fora é recusada (a tela e o <c>/ajuste</c>); só os ajustes da criação
/// e a volta à cópia passam (<c>daCopia</c>).</item>
/// <item><b>Ajustes:</b> só a regulagem (<see cref="Camada.Regulagem"/>), como IA, pelo <see cref="Escritor"/>
/// (com o link repetindo), pelas mesmas chaves e o só leitura da <see cref="Sessao"/>. Não contam para o
/// Continuar: a resposta à última pergunta já é o toque.</item>
/// <item><b>Salvar</b> é só do usuário: a memória nova do Setup aberto com a regulagem relida, que vira a em
/// uso; a marca sai.</item>
/// <item><b>Cancelar</b> (o ✕, a porta ou o Cancelar de uma folha de risco) volta à cópia, como App, e fecha sem
/// salvar.</item>
/// <item><b>Regra 6:</b> a conexão que cai ou a tarefa da porta que acaba interrompe: nada é salvo, e com algum
/// ajuste escrito fica "aplicado pela metade" (já, com a sessão aberta; senão, na próxima conexão), cujo
/// Terminar diz "O app não sabe refazer esta operação.".</item>
/// </list>
/// </summary>
public sealed class CriacaoMemoria : IDisposable
{
    /// <summary>O tipo da marca na <see cref="CopiaAntes"/>; o alvo é o id do Setup.</summary>
    public const string Tipo = "criar-memoria";

    /// <summary>A etapa anotada na marca quando o primeiro ajuste vai sair.</summary>
    public const string EtapaAjustes = "ajustes";

    /// <summary>A resposta do app ao "Como ficou?" que leva ao Salvar (a porta não salva).</summary>
    public const string OtimoPodeSalvar = "Ótimo, pode salvar";

    public const string PorUsuario = "usuario";
    public const string PorFolha = "folha";
    public const string PorPorta = "porta";

    public const string EventoResposta = "criacao-resposta";
    public const string EventoComoFicou = "criacao-como-ficou";
    public const string EventoSalva = "criacao-salva";
    public const string EventoCancelada = "criacao-cancelada";
    public const string EventoInterrompida = "criacao-interrompida";

    /// <summary>O toque no "+ Nova memória" (história 10d): a criação está fechada e o usuário quer uma nova.</summary>
    public const string EventoPedida = "criacao-pedida";

    public const int MinimoRespostas = 3;
    public const int MaximoRespostas = 4;
    public const int MinimoComoFicou = 2;
    public const int MaximoComoFicou = 3;

    public const string FraseTarefa = "criando uma memória";

    public const string MsgAberta = "Criação da memória aberta.";
    public const string MsgJaAberta = "Já há uma nova memória sendo criada.";
    public const string MsgFechada = "Nenhuma memória nova está sendo criada.";
    public const string MsgSemSetup = "Sem Setup em uso, não há onde criar a memória. Escolha um Setup na tela.";
    public const string MsgSoNasPerguntas = "As perguntas só vêm antes dos ajustes.";
    public const string MsgPerguntaPendente = "A pergunta anterior ainda não foi respondida.";
    public const string MsgSemPergunta = "Não há pergunta esperando resposta.";
    public const string MsgOutroVazio = "Escreva a resposta.";
    public const string MsgAjusteCedo = "Os ajustes só vêm depois da resposta à última pergunta.";
    public const string MsgAjusteSemRodada = "Os ajustes só vêm depois da resposta ao \"Como ficou?\".";
    public const string MsgSoRegulagem = "Na criação da memória só entra a regulagem: as 15 bandas, o preset de EQ, o link e o mute ficam de fora (veja GET /catalogo).";
    public const string MsgSalvando = "A memória está sendo salva.";
    public const string MsgResumoFora = "O resumo só fecha uma rodada de ajustes.";
    public const string MsgComoFicouFora = "O \"Como ficou?\" só vem depois do resumo.";
    public const string MsgSoNoResumo = "Avaliar o som só depois do resumo.";
    public const string MsgSemComoFicou = "Não há \"Como ficou?\" esperando resposta.";
    public const string MsgSoNoSalvar = "Salvar só depois de \"Ótimo, pode salvar\".";
    public const string MsgInterrompida = "A criação da memória foi interrompida.";
    public const string MsgCancelada = "Nova memória cancelada. O som voltou ao de antes.";
    public const string MsgCaiu = "a conexão caiu";
    public const string MsgTarefaAcabou = "a tarefa da porta acabou";

    /// <summary>O motivo da porta quando o usuário toca em Cancelar numa folha de risco durante a criação.</summary>
    public const string MsgCanceladaNaFolha = "O usuário cancelou a nova memória.";

    public static string MsgRespostas(int minimo, int maximo) => $"Mande de {minimo} a {maximo} respostas, cada uma com texto.";

    public static string MsgNaoVoltou(string motivo) => $"O som não voltou todo ({motivo}). Ficou aplicado pela metade.";

    /// <summary>"O aparelho ficou diferente do pedido em Ganho da saída 3. Corrija antes do resumo."</summary>
    public static string MsgResumoDiferente(IReadOnlyList<string> rotulos) =>
        $"O aparelho ficou diferente do pedido em {Ligacoes.Lista(rotulos)}. Corrija antes do resumo.";

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly UsoCompartilhado _uso;
    private readonly LogBle _log;

    private readonly object _trava = new();

    /// <summary>Uma escrita, o salvar ou o cancelar de cada vez: o cancelar espera o ajuste que já saiu.</summary>
    private readonly SemaphoreSlim _vez = new(1, 1);

    private EtapaCriacao _etapa = EtapaCriacao.Fechada;
    private long _geracao;
    private string? _idSetup;
    private string? _nomeSetup;
    private string? _somAntes;
    private int _rodada;
    private bool _escreveu;

    /// <summary>O Salvar do usuário passou da etapa e anda: o cancelar e a interrupção esperam (o toque no Salvar ganha).</summary>
    private bool _salvando;
    private List<PerguntaCriacao> _perguntas = [];
    private List<AjusteCriacao> _ajustes = [];
    private List<ComoFicouCriacao> _comoFicou = [];
    private readonly HashSet<Endereco> _incertos = [];

    public CriacaoMemoria(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        CopiaAntes copia,
        GerenteSetups setups,
        UsoCompartilhado uso,
        LogBle log)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _copia = copia;
        _setups = setups;
        _uso = uso;
        _log = log;

        // Uma marca achada na conexão nova: com ajuste escrito, pela metade; sem, resolvida (nada mudou).
        // Terminar não existe: os ajustes vieram da porta.
        _copia.Registrar(Tipo,
            op => Task.FromResult(op.Etapa == EtapaAjustes ? Retomada.PelaMetade : Retomada.Resolvida),
            _ => Task.FromResult(ResultadoCopia.Falha(CopiaAntes.MsgNaoSeiRefazer)),
            deixaGerador: true);
        _sessao.Mudou += AoMudarSessao;
        _uso.Mudou += AoMudarUso;
    }

    /// <summary>A etapa, as perguntas, os ajustes ou o "Como ficou?" mudaram. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>Um evento para a porta (a resposta, o "Como ficou?", salva, cancelada, interrompida).</summary>
    public event Action<EventoCriacao>? Aconteceu;

    public EtapaCriacao Etapa
    {
        get
        {
            lock (_trava)
                return _etapa;
        }
    }

    /// <summary>Uma criação está aberta.</summary>
    public bool Aberta => Etapa != EtapaCriacao.Fechada;

    /// <summary>Os ajustes são aceitos agora (depois da resposta à última pergunta, ou ao "Como ficou?").</summary>
    public bool Autorizado => Etapa == EtapaCriacao.Ajustando;

    /// <summary>O id do Setup aberto, ou null.</summary>
    public string? IdSetup
    {
        get
        {
            lock (_trava)
                return _idSetup;
        }
    }

    /// <summary>O nome do Setup aberto ("Casa 2.1"), ou null.</summary>
    public string? NomeSetup
    {
        get
        {
            lock (_trava)
                return _nomeSetup;
        }
    }

    /// <summary>A memória do celular em uso ao abrir ("FILME"), para o texto do cancelar; null = o som do aparelho.</summary>
    public string? SomAntes
    {
        get
        {
            lock (_trava)
                return _somAntes;
        }
    }

    /// <summary>A rodada de ajustes (1 = a primeira; 0 antes da resposta à última pergunta).</summary>
    public int Rodada
    {
        get
        {
            lock (_trava)
                return _rodada;
        }
    }

    public IReadOnlyList<PerguntaCriacao> Perguntas
    {
        get
        {
            lock (_trava)
                return _perguntas.ToArray();
        }
    }

    /// <summary>A pergunta esperando a resposta do usuário, ou null.</summary>
    public PerguntaCriacao? PerguntaAtual
    {
        get
        {
            lock (_trava)
                return _etapa == EtapaCriacao.Perguntas && _perguntas.LastOrDefault() is { Respondida: false } p ? p : null;
        }
    }

    /// <summary>Todos os ajustes, de todas as rodadas, na ordem.</summary>
    public IReadOnlyList<AjusteCriacao> Ajustes
    {
        get
        {
            lock (_trava)
                return _ajustes.ToArray();
        }
    }

    public IReadOnlyList<ComoFicouCriacao> ComoFicou
    {
        get
        {
            lock (_trava)
                return _comoFicou.ToArray();
        }
    }

    /// <summary>O "Como ficou?" da rodada (o último dela), ou null.</summary>
    public ComoFicouCriacao? ComoFicouAtual
    {
        get
        {
            lock (_trava)
                return AtualComoFicou();
        }
    }

    /// <summary>
    /// O resumo: cada endereço mexido, com o valor de antes da criação e o último ajuste, na ordem do primeiro
    /// ajuste; os da rodada atual em destaque. Fora deles, nada.
    /// </summary>
    public IReadOnlyList<LinhaResumoCriacao> Resumo
    {
        get
        {
            lock (_trava)
                return MontarResumo();
        }
    }

    // ------------------------------------------------------------------ pedir

    /// <summary>
    /// O toque no "+ Nova memória" (história 10d): com a criação fechada, emite <see cref="EventoPedida"/> para a
    /// porta. A tela não abre a criação: quem abre é o cliente pareado (<c>POST /criacao/abrir</c>). A espera, o
    /// prazo e o aviso da chave desligada são das histórias 14b e 14c.
    /// </summary>
    public ResultadoCriacao Pedir()
    {
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.Fechada)
                return ResultadoCriacao.Falha(MsgJaAberta);
        }
        _log.Evento("criação: nova memória pedida na tela");
        Emitir(new EventoCriacao(EventoPedida));
        return ResultadoCriacao.Feito();
    }

    // ------------------------------------------------------------------ abrir

    /// <summary>
    /// Abre a criação no Setup em uso: exige o Setup sem dúvida, a conexão e a leitura pronta; guarda a cópia
    /// antes (a marca <see cref="Tipo"/>) e inicia a tarefa da porta (o vigia de 30 s passa a valer).
    /// </summary>
    public async Task<ResultadoCriacao> AbrirAsync()
    {
        await _setups.Carregada;
        await _vez.WaitAsync();
        try
        {
            lock (_trava)
            {
                if (_etapa != EtapaCriacao.Fechada)
                    return ResultadoCriacao.Falha(MsgJaAberta);
            }
            if (_setups.MotivoBloqueioIa is { } duvida)
                return ResultadoCriacao.Falha(duvida);
            if (!SessaoAberta)
                return ResultadoCriacao.Falha(GerenteSetups.MsgSemConexao);
            if (_setups.SetupEmUso is not { } setup)
                return ResultadoCriacao.Falha(MsgSemSetup);
            if (_estado.Lendo || !_estado.Pronto)
                return ResultadoCriacao.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura);
            if (_uso.Parada)
                return ResultadoCriacao.Falha(UsoCompartilhado.MsgParada);

            var somAntes = _setups.MemoriaEmUso?.Nome;
            var copia = await _copia.IniciarAsync(new OperacaoPendente(Tipo, setup.Id, $"Criar uma memória no {setup.Nome}"));
            if (!copia.Ok)
                return ResultadoCriacao.Falha(copia.Motivo!);
            var tarefa = _uso.IniciarTarefa(_uso.Tarefa ?? FraseTarefa);
            if (!tarefa.Ok)
            {
                await _copia.ConcluirAsync();
                return ResultadoCriacao.Falha(tarefa.Motivo!);
            }

            lock (_trava)
            {
                _geracao++;
                _etapa = EtapaCriacao.Perguntas;
                _idSetup = setup.Id;
                _nomeSetup = setup.Nome;
                _somAntes = somAntes;
                _rodada = 0;
                _escreveu = false;
                _perguntas = [];
                _ajustes = [];
                _comoFicou = [];
                _incertos.Clear();
            }
            _log.Evento($"criação da memória aberta no {setup.Nome}");
            Notificar();
            // Caiu ou a tarefa acabou entre a cópia e a etapa: interrompe como no meio.
            if (!SessaoAberta)
                Interromper(MsgCaiu);
            else if (!_uso.IaAtiva)
                Interromper(MsgTarefaAcabou);
            return Aberta ? ResultadoCriacao.Feito(MsgAberta) : ResultadoCriacao.Falha(MsgInterrompida);
        }
        finally
        {
            _vez.Release();
        }
    }

    // ------------------------------------------------------------------ perguntas

    /// <summary>Uma pergunta (3 ou 4 respostas; a tela acrescenta o "Outro…"). Só nas perguntas e depois da resposta à anterior.</summary>
    public ResultadoCriacao Perguntar(string? texto, IReadOnlyList<string>? respostas, bool ultima)
    {
        var limpo = texto?.Trim();
        if (string.IsNullOrEmpty(limpo))
            return ResultadoCriacao.Mal("A pergunta precisa de texto.");
        if (Limpar(respostas, MinimoRespostas, MaximoRespostas) is not { } lista)
            return ResultadoCriacao.Mal(MsgRespostas(MinimoRespostas, MaximoRespostas));
        PerguntaCriacao pergunta;
        lock (_trava)
        {
            if (_etapa == EtapaCriacao.Fechada)
                return ResultadoCriacao.Falha(MsgFechada);
            if (_etapa != EtapaCriacao.Perguntas)
                return ResultadoCriacao.Falha(MsgSoNasPerguntas);
            if (_perguntas.LastOrDefault() is { Respondida: false })
                return ResultadoCriacao.Falha(MsgPerguntaPendente);
            pergunta = new PerguntaCriacao(_perguntas.Count + 1, limpo, lista, ultima);
            _perguntas.Add(pergunta);
        }
        Notificar();
        return ResultadoCriacao.Feito($"Pergunta {pergunta.Numero} na tela.");
    }

    /// <summary>O toque numa resposta (0 = a primeira).</summary>
    public ResultadoCriacao Responder(int indice) => ResponderPergunta(p =>
        indice >= 0 && indice < p.Respostas.Count ? (p.Respostas[indice], false) : null);

    /// <summary>O "Outro…" com o texto do usuário; vazio não responde.</summary>
    public ResultadoCriacao ResponderOutro(string? texto)
    {
        var limpo = texto?.Trim();
        if (string.IsNullOrEmpty(limpo))
            return ResultadoCriacao.Falha(MsgOutroVazio);
        return ResponderPergunta(_ => (limpo, true));
    }

    private ResultadoCriacao ResponderPergunta(Func<PerguntaCriacao, (string Texto, bool DoOutro)?> escolher)
    {
        PerguntaCriacao respondida;
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.Perguntas || _perguntas.LastOrDefault() is not { Respondida: false } p)
                return ResultadoCriacao.Falha(MsgSemPergunta);
            if (escolher(p) is not { } escolha)
                return ResultadoCriacao.Falha(MsgSemPergunta);
            respondida = p with { Resposta = escolha.Texto, DoOutro = escolha.DoOutro };
            _perguntas[^1] = respondida;
            if (respondida.Ultima)
            {
                _etapa = EtapaCriacao.Ajustando;
                _rodada = 1;
            }
        }
        _log.Evento($"criação: resposta à pergunta {respondida.Numero}");
        Emitir(new EventoCriacao(EventoResposta, Numero: respondida.Numero, Texto: respondida.Resposta, DoOutro: respondida.DoOutro));
        Notificar();
        return ResultadoCriacao.Feito();
    }

    /// <summary>
    /// O ‹ (história 10d): responde de novo, com o toque numa resposta (0 = a primeira), a pergunta
    /// <paramref name="numero"/>, já respondida, ainda nas perguntas. A mesma resposta não muda nada (a tela só
    /// avança). Uma resposta diferente tira as perguntas depois dela e vai à porta como <c>criacao-resposta</c>
    /// com o número dela.
    /// </summary>
    public ResultadoCriacao ResponderDeNovo(int numero, int indice) => ResponderDeNovo(numero, p =>
        indice >= 0 && indice < p.Respostas.Count ? (p.Respostas[indice], false) : null);

    /// <summary>O ‹ com o "Outro…": responde de novo a pergunta <paramref name="numero"/> com o texto; vazio não responde.</summary>
    public ResultadoCriacao ResponderDeNovo(int numero, string? textoOutro)
    {
        var limpo = textoOutro?.Trim();
        if (string.IsNullOrEmpty(limpo))
            return ResultadoCriacao.Falha(MsgOutroVazio);
        return ResponderDeNovo(numero, _ => (limpo, true));
    }

    private ResultadoCriacao ResponderDeNovo(int numero, Func<PerguntaCriacao, (string Texto, bool DoOutro)?> escolher)
    {
        PerguntaCriacao respondida;
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.Perguntas)
                return ResultadoCriacao.Falha(_etapa == EtapaCriacao.Fechada ? MsgFechada : MsgSemPergunta);
            var i = _perguntas.FindIndex(p => p.Numero == numero);
            if (i < 0 || !_perguntas[i].Respondida)
                return ResultadoCriacao.Falha(MsgSemPergunta);
            var antes = _perguntas[i];
            if (escolher(antes) is not { } escolha)
                return ResultadoCriacao.Falha(MsgSemPergunta);
            if (escolha.Texto == antes.Resposta && escolha.DoOutro == antes.DoOutro)
                return ResultadoCriacao.Feito(); // a mesma resposta: nada muda
            respondida = antes with { Resposta = escolha.Texto, DoOutro = escolha.DoOutro };
            _perguntas = [.. _perguntas.Take(i), respondida];
            if (respondida.Ultima)
            {
                _etapa = EtapaCriacao.Ajustando;
                _rodada = 1;
            }
        }
        _log.Evento($"criação: resposta à pergunta {respondida.Numero} de novo");
        Emitir(new EventoCriacao(EventoResposta, Numero: respondida.Numero, Texto: respondida.Resposta, DoOutro: respondida.DoOutro));
        Notificar();
        return ResultadoCriacao.Feito();
    }

    // ------------------------------------------------------------------ ajustes

    /// <summary>Um ajuste numérico da regulagem, com a frase e o motivo.</summary>
    public Task<ResultadoAjusteCriacao> AjustarAsync(string codigo, int? indice, decimal valor, string frase, string motivo)
    {
        if (Conferir(codigo) is { } recusa)
            return Task.FromResult(recusa);
        var p = Catalogo.ParametroDe(codigo)!;
        return AjustarAsync(p, indice, () => Quadro(() => p.CodificarEscrita(indice, valor)),
            () => _escritor.EscreverAsync(p, indice, valor, espelhar: true, daCopia: true), frase, motivo);
    }

    /// <summary>Um ajuste de texto da regulagem (a rota: <c>a</c>, <c>b</c>, <c>a+b</c>), com a frase e o motivo.</summary>
    public Task<ResultadoAjusteCriacao> AjustarAsync(string codigo, int? indice, string valor, string frase, string motivo)
    {
        if (Conferir(codigo) is { } recusa)
            return Task.FromResult(recusa);
        var p = Catalogo.ParametroDe(codigo)!;
        return AjustarAsync(p, indice, () => Quadro(() => p.CodificarEscrita(indice, valor)),
            () => _escritor.EscreverAsync(p, indice, valor, espelhar: true, daCopia: true), frase, motivo);
    }

    /// <summary>O código entra na criação (só a regulagem), ou a recusa (400).</summary>
    public static bool EntraNaCriacao(string codigo) =>
        Catalogo.Marco1.Any(p => p.Codigo == codigo) && Camadas.De(codigo) == Camada.Regulagem;

    private static ResultadoAjusteCriacao? Conferir(string codigo) =>
        EntraNaCriacao(codigo) ? null : new ResultadoAjusteCriacao(false, MsgSoRegulagem, Invalido: true);

    private static string? Quadro(Func<string> codificar)
    {
        try
        {
            return codificar();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<ResultadoAjusteCriacao> AjustarAsync(Parametro p, int? indice, Func<string?> quadro,
        Func<Task<ResultadoEscrita>> escrever, string frase, string motivo)
    {
        if (quadro() is not { } q)
            return new ResultadoAjusteCriacao(false, $"Valor fora do {p.Codigo} (veja GET /catalogo).", Invalido: true);
        var novo = Catalogo.EscritaDeAudio(q)?.NoFio;
        var endereco = new Endereco(p.Codigo, indice);

        await _vez.WaitAsync();
        try
        {
            long geracao;
            bool anotar;
            int rodada;
            lock (_trava)
            {
                if (_etapa != EtapaCriacao.Ajustando)
                    return ResultadoAjusteCriacao.Falha(_etapa switch
                    {
                        EtapaCriacao.Fechada => MsgFechada,
                        EtapaCriacao.Perguntas => MsgAjusteCedo,
                        _ => MsgAjusteSemRodada,
                    });
                geracao = _geracao;
                rodada = _rodada;
                anotar = !_escreveu;
                _escreveu = true;
            }
            // A marca diz que algum ajuste pode ter saído antes de sair o primeiro (regra 6).
            if (anotar && !await _copia.AnotarEtapaAsync(EtapaAjustes))
            {
                lock (_trava)
                {
                    if (_geracao == geracao)
                        _escreveu = false;
                }
                return ResultadoAjusteCriacao.Falha(CopiaAntes.MsgNaoGuardou);
            }

            var antes = _estado.Valor(endereco)?.NoFio;
            ResultadoEscrita r;
            using (Origem.Como(OrigemComando.Ia))
                r = await escrever();

            AjusteCriacao? ajuste = null;
            lock (_trava)
            {
                if (_geracao != geracao)
                    return ResultadoAjusteCriacao.Falha(MsgInterrompida);
                if (r.Ok)
                {
                    ajuste = new AjusteCriacao(rodada, p.Codigo, indice, frase.Trim(), motivo.Trim(), antes, novo, r.Valor?.NoFio,
                        r.Diferente, r.Relido, r.Aviso);
                    _ajustes.Add(ajuste);
                    // Com o link, a outra saída que não acompanhou deixa o par partido: o resumo espera.
                    if (r.Aviso is null)
                        _incertos.Remove(endereco);
                    else
                        _incertos.Add(endereco);
                }
                else if (r.Motivo is Escritor.MsgSemConfirmacao or Escritor.MsgIlegivel or Escritor.MsgCaiu)
                {
                    // Saiu e o aparelho não confirmou: o resumo espera um ajuste relido igual aqui.
                    _incertos.Add(endereco);
                }
            }
            if (ajuste is null)
                return ResultadoAjusteCriacao.Falha(r.Motivo ?? Sessao.MotivoFalhaEscrita);
            _log.Evento($"criação: {ajuste.Rotulo} {ajuste.AntesExibicao} → {ajuste.RelidoExibicao} (rodada {rodada})");
            Notificar();
            return new ResultadoAjusteCriacao(true, null, ajuste);
        }
        finally
        {
            _vez.Release();
        }
    }

    // ------------------------------------------------------------------ resumo e "Como ficou?"

    /// <summary>
    /// Termina a rodada: só se todo endereço mexido foi relido igual ao pedido. Devolve as linhas do resumo
    /// (a mensagem diz quantas).
    /// </summary>
    public ResultadoCriacao FecharRodada()
    {
        lock (_trava)
        {
            if (_etapa == EtapaCriacao.Fechada)
                return ResultadoCriacao.Falha(MsgFechada);
            if (_etapa != EtapaCriacao.Ajustando)
                return ResultadoCriacao.Falha(MsgResumoFora);
            var ultimos = UltimosPorEndereco();
            var ruins = ultimos.Where(a => a.Diferente).Select(a => a.Endereco)
                .Concat(_incertos)
                .Distinct()
                .Select(e => Camadas.Rotulo(e.Codigo, e.Indice))
                .ToArray();
            if (ruins.Length > 0)
                return ResultadoCriacao.Falha(MsgResumoDiferente(ruins));
            _etapa = EtapaCriacao.Resumo;
        }
        _log.Evento("criação: resumo da rodada");
        Notificar();
        return ResultadoCriacao.Feito();
    }

    /// <summary>As respostas do "Como ficou?" (2 ou 3; a tela põe "Ótimo, pode salvar" antes e "Outro…" depois).</summary>
    public ResultadoCriacao PerguntarComoFicou(IReadOnlyList<string>? respostas)
    {
        if (Limpar(respostas, MinimoComoFicou, MaximoComoFicou) is not { } lista)
            return ResultadoCriacao.Mal(MsgRespostas(MinimoComoFicou, MaximoComoFicou));
        lock (_trava)
        {
            if (_etapa == EtapaCriacao.Fechada)
                return ResultadoCriacao.Falha(MsgFechada);
            if (_etapa is not (EtapaCriacao.Resumo or EtapaCriacao.ComoFicou))
                return ResultadoCriacao.Falha(MsgComoFicouFora);
            var novo = new ComoFicouCriacao(_rodada, lista);
            if (_comoFicou.LastOrDefault() is { Respondida: false } pendente && pendente.Rodada == _rodada)
                _comoFicou[^1] = novo;
            else
                _comoFicou.Add(novo);
        }
        Notificar();
        return ResultadoCriacao.Feito();
    }

    /// <summary>"Avaliar o som": do resumo ao "Como ficou?".</summary>
    public ResultadoCriacao AvaliarSom()
    {
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.Resumo)
                return ResultadoCriacao.Falha(MsgSoNoResumo);
            _etapa = EtapaCriacao.ComoFicou;
        }
        Notificar();
        return ResultadoCriacao.Feito();
    }

    /// <summary>"Ótimo, pode salvar": vai ao Salvar (a porta recebe <c>salvar:true</c>).</summary>
    public ResultadoCriacao PodeSalvar() => ResponderComoFicou(_ => (OtimoPodeSalvar, false, true));

    /// <summary>O toque numa das respostas mandadas (0 = a primeira): uma rodada nova de ajustes.</summary>
    public ResultadoCriacao ResponderComoFicou(int indice) => ResponderComoFicou(c =>
        indice >= 0 && indice < c.Respostas.Count ? (c.Respostas[indice], false, false) : null);

    /// <summary>O "Outro…" do "Como ficou?": uma rodada nova de ajustes; vazio não responde.</summary>
    public ResultadoCriacao ResponderComoFicouOutro(string? texto)
    {
        var limpo = texto?.Trim();
        if (string.IsNullOrEmpty(limpo))
            return ResultadoCriacao.Falha(MsgOutroVazio);
        return ResponderComoFicou(_ => (limpo, true, false));
    }

    private ResultadoCriacao ResponderComoFicou(Func<ComoFicouCriacao, (string Texto, bool DoOutro, bool Salvar)?> escolher)
    {
        ComoFicouCriacao respondido;
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.ComoFicou)
                return ResultadoCriacao.Falha(MsgSemComoFicou);
            var atual = AtualComoFicou() ?? new ComoFicouCriacao(_rodada, []);
            if (escolher(atual) is not { } escolha)
                return ResultadoCriacao.Falha(MsgSemComoFicou);
            respondido = atual with { Resposta = escolha.Texto, DoOutro = escolha.DoOutro, Salvar = escolha.Salvar };
            // Depois do "Ajustar mais", a resposta nova não apaga a de antes.
            if (_comoFicou.LastOrDefault() is { Respondida: false } pendente && pendente.Rodada == _rodada)
                _comoFicou[^1] = respondido;
            else
                _comoFicou.Add(respondido);
            if (escolha.Salvar)
            {
                _etapa = EtapaCriacao.Salvar;
            }
            else
            {
                _rodada++;
                _etapa = EtapaCriacao.Ajustando;
            }
        }
        _log.Evento(respondido.Salvar ? "criação: ótimo, pode salvar" : $"criação: refino (rodada {respondido.Rodada + 1})");
        Emitir(new EventoCriacao(EventoComoFicou, Texto: respondido.Resposta, DoOutro: respondido.DoOutro, Salvar: respondido.Salvar,
            Rodada: respondido.Rodada));
        Notificar();
        return ResultadoCriacao.Feito();
    }

    /// <summary>"Ajustar mais": do Salvar de volta ao "Como ficou?".</summary>
    public ResultadoCriacao AjustarMais()
    {
        lock (_trava)
        {
            if (_etapa != EtapaCriacao.Salvar)
                return ResultadoCriacao.Falha(MsgSoNoSalvar);
            _etapa = EtapaCriacao.ComoFicou;
        }
        Notificar();
        return ResultadoCriacao.Feito();
    }

    // ------------------------------------------------------------------ salvar e cancelar

    /// <summary>
    /// O Salvar do usuário, com o nome: a memória nova do Setup aberto com a regulagem relida, que vira a em
    /// uso e a ativa; a marca da cópia sai e a criação fecha. Um nome inválido ou repetido não fecha nada.
    /// </summary>
    public async Task<ResultadoCriacao> SalvarAsync(string? nome)
    {
        await _vez.WaitAsync();
        try
        {
            string idSetup;
            lock (_trava)
            {
                if (_etapa != EtapaCriacao.Salvar || _idSetup is null)
                    return ResultadoCriacao.Falha(_etapa == EtapaCriacao.Fechada ? MsgFechada : MsgSoNoSalvar);
                idSetup = _idSetup;
                _salvando = true;
            }
            ResultadoSetup? r = null;
            try
            {
                r = await _setups.SalvarCriacaoAsync(idSetup, nome);
            }
            finally
            {
                lock (_trava)
                {
                    _salvando = false;
                    if (r?.Ok == true)
                        Fechar(); // o Salvar fecha sempre: nada o interrompeu no meio
                }
            }
            if (!r!.Ok)
            {
                // O que chegou no meio do Salvar (a queda, o fim da tarefa) vale agora.
                if (!SessaoAberta)
                    Interromper(MsgCaiu);
                else if (!_uso.IaAtiva)
                    Interromper(MsgTarefaAcabou);
                return ResultadoCriacao.Falha(r.Mensagem ?? r.Motivo ?? GerenteSetups.MsgNaoGuardou);
            }
            await _copia.ConcluirAsync();
            var memoria = _setups.MemoriaEmUso?.Nome ?? GerenteSetups.NomeValido(nome);
            _log.Evento($"criação: memória {memoria} salva");
            Emitir(new EventoCriacao(EventoSalva, Memoria: memoria));
            Notificar();
            return ResultadoCriacao.Feito(r.Mensagem);
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>
    /// Cancelar (o ✕ do usuário, a porta, ou o Cancelar de uma folha de risco): fecha sem salvar e volta à
    /// cópia, como App, só no que difere. Se a volta falha no meio, fica "aplicado pela metade".
    /// </summary>
    /// <param name="por"><see cref="PorUsuario"/>, <see cref="PorFolha"/> ou <see cref="PorPorta"/>.</param>
    public async Task<ResultadoCriacao> CancelarAsync(string por)
    {
        lock (_trava)
        {
            if (_etapa == EtapaCriacao.Fechada)
                return ResultadoCriacao.Falha(MsgFechada);
            if (_salvando)
                return ResultadoCriacao.Falha(MsgSalvando);
            Fechar(); // nada mais entra; um ajuste que já saiu termina antes da volta
        }
        Notificar();

        ResultadoCopia volta;
        await _vez.WaitAsync();
        try
        {
            using (Origem.Como(OrigemComando.App))
                volta = await _copia.DesfazerAsync();
        }
        finally
        {
            _vez.Release();
        }
        _log.Evento(volta.Ok ? $"criação cancelada ({por}); o som voltou à cópia" : $"criação cancelada ({por}); a volta falhou: {volta.Motivo}");
        Emitir(new EventoCriacao(EventoCancelada, Por: por, Voltou: volta.Ok, Motivo: volta.Ok ? null : volta.Motivo));
        Notificar();
        return volta.Ok ? ResultadoCriacao.Feito(MsgCancelada) : ResultadoCriacao.Falha(MsgNaoVoltou(volta.Motivo ?? "falhou"));
    }

    // ------------------------------------------------------------------ interrupção (regra 6)

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private void AoMudarSessao()
    {
        if (!SessaoAberta)
            Interromper(MsgCaiu);
    }

    private void AoMudarUso()
    {
        if (!_uso.IaAtiva)
            Interromper(MsgTarefaAcabou);
    }

    /// <summary>
    /// A conexão caiu ou a tarefa da porta acabou: fecha sem salvar. Com ajuste escrito, "aplicado pela metade"
    /// (já, com a sessão aberta; senão, a marca fica para a próxima conexão); sem, a marca sai.
    /// </summary>
    private void Interromper(string causa)
    {
        bool escreveu;
        lock (_trava)
        {
            if (_etapa == EtapaCriacao.Fechada || _salvando)
                return; // durante o Salvar, o toque do usuário ganha (o Salvar é local, do espelho relido)
            escreveu = _escreveu;
            Fechar();
        }
        if (escreveu)
        {
            if (SessaoAberta)
                _copia.MarcarPelaMetade();
            else
                _copia.Interromper();
        }
        else
        {
            _ = ConcluirSeguroAsync();
        }
        _log.Evento($"criação da memória interrompida: {causa}{(escreveu ? "; aplicado pela metade" : "")}");
        Emitir(new EventoCriacao(EventoInterrompida, Motivo: causa));
        Notificar();
    }

    private async Task ConcluirSeguroAsync()
    {
        try
        {
            await _copia.ConcluirAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"criação: falha ao tirar a marca ({e.GetType().Name})");
        }
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Chamar com a trava.</summary>
    private void Fechar()
    {
        _etapa = EtapaCriacao.Fechada;
        _geracao++;
    }

    /// <summary>Chamar com a trava.</summary>
    private ComoFicouCriacao? AtualComoFicou() =>
        _comoFicou.LastOrDefault() is { } c && c.Rodada == _rodada ? c : null;

    /// <summary>O último ajuste de cada endereço, na ordem do primeiro. Chamar com a trava.</summary>
    private List<AjusteCriacao> UltimosPorEndereco()
    {
        var ordem = new List<Endereco>();
        var ultimo = new Dictionary<Endereco, AjusteCriacao>();
        foreach (var a in _ajustes)
        {
            if (!ultimo.ContainsKey(a.Endereco))
                ordem.Add(a.Endereco);
            ultimo[a.Endereco] = a;
        }
        return ordem.Select(e => ultimo[e]).ToList();
    }

    /// <summary>Chamar com a trava.</summary>
    private IReadOnlyList<LinhaResumoCriacao> MontarResumo()
    {
        var primeiro = new Dictionary<Endereco, AjusteCriacao>();
        foreach (var a in _ajustes)
            primeiro.TryAdd(a.Endereco, a);
        return UltimosPorEndereco()
            .Select(a => new LinhaResumoCriacao(a.Codigo, a.Indice, a.Rotulo, primeiro[a.Endereco].AntesExibicao, a.RelidoExibicao, a.Frase, a.Motivo,
                a.Rodada == _rodada))
            .ToArray();
    }

    private static IReadOnlyList<string>? Limpar(IReadOnlyList<string>? respostas, int minimo, int maximo)
    {
        if (respostas is null || respostas.Count < minimo || respostas.Count > maximo)
            return null;
        var lista = respostas.Select(r => r?.Trim() ?? "").ToArray();
        return lista.Any(string.IsNullOrEmpty) ? null : lista;
    }

    private void Emitir(EventoCriacao evento)
    {
        try
        {
            Aconteceu?.Invoke(evento);
        }
        catch (Exception e)
        {
            _log.Evento($"falha no evento da criação ({e.GetType().Name})");
        }
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da criação ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _sessao.Mudou -= AoMudarSessao;
        _uso.Mudou -= AoMudarUso;
    }
}
