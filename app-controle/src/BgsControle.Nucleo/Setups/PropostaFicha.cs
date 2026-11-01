using System.Globalization;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Setups;

/// <summary>
/// O pedido de ficha (história 14b): o equipamento salvo com um modelo sem ficha na base de modelos. Vai ao
/// <c>/eventos</c> como <c>ficha-pedida</c>.
/// </summary>
/// <param name="Grupo">O grupo do equipamento (no JSON, o nome da base de modelos: <c>ativo</c>, <c>alto-falante</c>…).</param>
public sealed record PedidoFicha(string IdSetup, string IdEquipamento, string Modelo, GrupoEquipamento Grupo, string Categoria);

/// <summary>Um dado da proposta: o campo (o nome de <see cref="DadosTecnicos.Campos"/>), o valor, a origem, a fonte e o aviso.</summary>
/// <param name="Aviso">O que não fechou ("Qts não fecha com Qes e Qms"), ou null.</param>
public sealed record DadoProposto(string Campo, decimal Valor, OrigemDado Origem, string Fonte, string? Aviso = null);

/// <summary>Uma proposta recebida, esperando o toque do usuário na folha.</summary>
/// <param name="NomeEquipamento">O nome do equipamento quando a proposta chegou ("Sub da sala").</param>
public sealed record Proposta(PedidoFicha Pedido, string NomeEquipamento, IReadOnlyList<DadoProposto> Dados);

/// <summary>Como terminou uma proposta mandada pela porta. <see cref="Invalido"/>: o corpo veio mal formado (400).</summary>
public sealed record ResultadoProposta(bool Ok, string? Motivo, bool Invalido = false)
{
    public static ResultadoProposta Feito { get; } = new(true, null);

    public static ResultadoProposta Falha(string motivo) => new(false, motivo);

    public static ResultadoProposta Mal(string motivo) => new(false, motivo, Invalido: true);
}

/// <summary>A unidade de um campo da ficha e a faixa plausível dele (larga: só pega a unidade errada, como o Xmax em metros).</summary>
public sealed record FaixaCampo(string Campo, string? Unidade, decimal Minimo, decimal Maximo);

/// <summary>
/// A proposta de ficha (história 14b; spec-agente-memoria CAP-6, <c>lado-do-app.md</c>): o app pede a ficha de um modelo
/// sem ficha na base, o cliente pareado devolve a proposta, e só o toque do usuário salva.
/// <list type="bullet">
/// <item><b>Pedido:</b> o <see cref="GerenteSetups.FichaPedida"/> (o equipamento salvo com o modelo sem ficha na base e a
/// chave "Controle pela rede" ligada) fica pendente por <see cref="Prazo"/> e vai à porta (<see cref="Pedida"/>). No fim do
/// prazo, o pedido sai, sem aviso.</item>
/// <item><b>Proposta</b> (<see cref="Receber"/>): só com o pedido pendente do equipamento, dentro do prazo e com o
/// equipamento ainda com o mesmo modelo e grupo (senão, <see cref="MsgPrazo"/>); cada dado com um campo do grupo
/// (<see cref="CamposDo"/>), o valor na faixa plausível do campo (<see cref="Faixas"/>, que também diz a unidade), a origem
/// oficial ou outra fonte e a fonte (até 120 caracteres).</item>
/// <item><b>Salvar</b> (<see cref="SalvarAsync"/>, só o toque do usuário): só os dados usados, na ficha do equipamento
/// (<see cref="GerenteSetups.EditarEquipamentoAsync"/>, que leva a ficha à base de modelos com o
/// <see cref="BaseModelos.GuardarAsync"/>). Um dado recusado não apaga nem troca o que a ficha já tem. Com o modelo ou o
/// grupo mudado depois do pedido, a proposta sai e nada é gravado. Fechar sem salvar não grava nada.</item>
/// </list>
/// A porta nunca escreve a ficha.
/// </summary>
public sealed class PropostaFicha : IDisposable
{
    /// <summary>O tipo do evento do pedido no <c>/eventos</c>.</summary>
    public const string EventoPedida = "ficha-pedida";

    public const string MsgPrazo = "O prazo da proposta acabou.";
    public const string MsgSemEquipamento = "Falta o campo \"idEquipamento\".";
    public const string MsgSemDados = "Mande ao menos um dado.";
    public const string MsgSemProposta = "Não há proposta de ficha esperando.";

    /// <summary>O pedido espera a proposta por este tempo.</summary>
    public static TimeSpan Prazo { get; } = TimeSpan.FromSeconds(60);

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    /// <summary>"O campo \"qts\" não é um dado deste equipamento."</summary>
    public static string MsgCampo(string campo) => $"O campo \"{campo}\" não é um dado deste equipamento.";

    /// <summary>"O campo \"fs\" aparece duas vezes."</summary>
    public static string MsgCampoRepetido(string campo) => $"O campo \"{campo}\" aparece duas vezes.";

    /// <summary>"O valor de \"xmax\" vai de 0.1 a 50 (mm)." (a unidade errada, como o Xmax em metros, cai fora).</summary>
    public static string MsgFaixa(FaixaCampo f) =>
        $"O valor de \"{f.Campo}\" vai de {f.Minimo.ToString(Fio)} a {f.Maximo.ToString(Fio)}{(f.Unidade is null ? "" : $" ({f.Unidade})")}.";

    /// <summary>"A origem de \"fs\" é \"oficial\" ou \"outra-fonte\"."</summary>
    public static string MsgOrigem(string campo) => $"A origem de \"{campo}\" é \"oficial\" ou \"outra-fonte\".";

    /// <summary>"A fonte de \"fs\" é obrigatória, até 120 caracteres, sem quebra de linha."</summary>
    public static string MsgFonte(string campo) =>
        $"A fonte de \"{campo}\" é obrigatória, até {Ligacoes.TamanhoMaximoTextoLongo} caracteres, sem quebra de linha.";

    /// <summary>"O aviso de \"fs\" vai até 120 caracteres, sem quebra de linha."</summary>
    public static string MsgAviso(string campo) => $"O aviso de \"{campo}\" vai até {Ligacoes.TamanhoMaximoTextoLongo} caracteres, sem quebra de linha.";

    /// <summary>
    /// A unidade e a faixa plausível de cada campo numérico (as unidades da ficha: <see cref="DadosTecnicos"/>). As faixas são
    /// largas de propósito: servem para recusar a unidade errada (Xmax em m, Sd em m², Le em H), não para julgar o dado.
    /// </summary>
    public static IReadOnlyList<FaixaCampo> Faixas { get; } =
    [
        new("diametro", "pol", 0.5m, 60m),
        new("impedancia", "Ω", 0.5m, 100m),
        new("potenciaRms", "W", 0.1m, 100000m),
        new("sensibilidade", "dB", 50m, 130m),
        new("faixaMinima", "Hz", 1m, 20000m),
        new("faixaMaxima", "Hz", 5m, 100000m),
        new("ganho", "×", 0.1m, 1000m),
        new("re", "Ω", 0.5m, 100m),
        new("fs", "Hz", 5m, 20000m),
        new("qes", null, 0.05m, 50m),
        new("qms", null, 0.05m, 50m),
        new("vas", "L", 0.01m, 2000m),
        new("bl", "T·m", 0.1m, 100m),
        new("sd", "cm²", 1m, 5000m),
        new("xmax", "mm", 0.1m, 50m),
        new("le", "mH", 0.001m, 20m),
        new("tMaxBobina", "°C", 50m, 500m),
        new("rthBobinaIma", "K/W", 0.01m, 100m),
        new("rthImaAr", "K/W", 0.01m, 100m),
        new("tauBobina", "s", 0.1m, 1000m),
        new("tauIma", "s", 1m, 100000m),
        new("volumeCaixa", "L", 0.1m, 5000m),
        new("fb", "Hz", 5m, 1000m),
        new("areaDuto", "cm²", 0.1m, 5000m),
        new("ganhoPonte", "×", 0.1m, 1000m),
        new("potenciaCanal", "W", 0.1m, 100000m),
        new("tensaoCeifamentoCanal", "V", 0.1m, 500m),
        new("potenciaPonte", "W", 0.1m, 100000m),
        new("tensaoCeifamentoPonte", "V", 0.1m, 500m),
        new("tensaoAlimentacao", "V", 0.1m, 500m),
        new("correnteAlimentacao", "A", 0.01m, 1000m),
    ];

    /// <summary>A faixa do campo, ou null (um campo que a proposta não aceita).</summary>
    public static FaixaCampo? FaixaDe(string campo) => Faixas.FirstOrDefault(f => f.Campo == campo);

    /// <summary>"fs (Hz, 5 a 20000), qes (0.05 a 50), …": as unidades e as faixas para a <c>/ajuda</c>.</summary>
    public static string TextoFaixas => string.Join(", ", Faixas.Select(f =>
        $"{f.Campo} ({(f.Unidade is null ? "" : f.Unidade + ", ")}{f.Minimo.ToString(Fio)} a {f.Maximo.ToString(Fio)})"));

    /// <summary>
    /// Os campos numéricos da ficha de cada grupo, os mesmos da folha de equipamento, menos os canais do módulo (eles vêm
    /// do "Onde está ligado"). O tipo da caixa é texto: fica de fora da proposta.
    /// </summary>
    public static IReadOnlyList<string> CamposDo(GrupoEquipamento grupo) => grupo switch
    {
        GrupoEquipamento.AltoFalante => [.. Gerais, .. AltoFalante, .. Termicos, .. Caixa],
        GrupoEquipamento.AmplificaETocam => [.. Gerais, .. AltoFalante, .. Caixa],
        GrupoEquipamento.Amplificador => ["ganho", "ganhoPonte", "potenciaCanal", "tensaoCeifamentoCanal", "potenciaPonte", "tensaoCeifamentoPonte",
            "tensaoAlimentacao", "correnteAlimentacao"],
        _ => ["potenciaRms"],
    };

    private static readonly string[] Gerais = ["diametro", "impedancia", "potenciaRms", "sensibilidade", "faixaMinima", "faixaMaxima"];
    private static readonly string[] AltoFalante = ["re", "fs", "qes", "qms", "vas", "bl", "sd", "xmax", "le"];
    private static readonly string[] Termicos = ["tMaxBobina", "rthBobinaIma", "rthImaAr", "tauBobina", "tauIma"];
    private static readonly string[] Caixa = ["volumeCaixa", "fb", "areaDuto"];

    private sealed record Pendente(PedidoFicha Pedido, DateTimeOffset Quando, ITimer Prazo);

    private readonly GerenteSetups _setups;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly object _trava = new();
    private readonly Dictionary<string, Pendente> _pendentes = new(StringComparer.Ordinal);
    private readonly List<Proposta> _fila = [];
    private bool _salvando;
    private bool _descartada;

    public PropostaFicha(GerenteSetups setups, LogBle log, TimeProvider? relogio = null)
    {
        _setups = setups;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _setups.FichaPedida += AoPedir;
    }

    /// <summary>Um pedido de ficha para a porta (<c>ficha-pedida</c>). Pode chegar em qualquer thread.</summary>
    public event Action<PedidoFicha>? Pedida;

    /// <summary>Chegou uma proposta, ou a da folha foi salva ou fechada. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A proposta da folha (a primeira que chegou e ainda não foi salva nem fechada), ou null.</summary>
    public Proposta? Atual
    {
        get
        {
            lock (_trava)
                return _fila.Count > 0 ? _fila[0] : null;
        }
    }

    /// <summary>Os pedidos esperando a proposta, dentro do prazo.</summary>
    public IReadOnlyList<PedidoFicha> Pendentes
    {
        get
        {
            var agora = _relogio.GetUtcNow();
            lock (_trava)
                return _pendentes.Values.Where(p => agora - p.Quando <= Prazo).Select(p => p.Pedido).ToArray();
        }
    }

    /// <summary>O dado que a ficha do equipamento da proposta tem agora no campo (para a folha mostrar ao lado), ou null.</summary>
    public DadoTecnico? DadoAtual(Proposta proposta, string campo) =>
        Equipamento(proposta.Pedido) is { } e && DadosTecnicos.Campos.FirstOrDefault(c => c.Nome == campo) is { } c ? c.Ler(e.Dados) : null;

    private void AoPedir(PedidoFicha pedido)
    {
        lock (_trava)
        {
            if (_descartada)
                return;
            if (_pendentes.Remove(pedido.IdEquipamento, out var antigo))
                antigo.Prazo.Dispose();
            Pendente? novo = null;
            var timer = _relogio.CreateTimer(_ => Vencer(pedido.IdEquipamento, novo), null, Prazo, Timeout.InfiniteTimeSpan);
            novo = new Pendente(pedido, _relogio.GetUtcNow(), timer);
            _pendentes[pedido.IdEquipamento] = novo;
        }
        try
        {
            Pedida?.Invoke(pedido);
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do pedido de ficha ({e.GetType().Name})");
        }
    }

    /// <summary>O prazo do pedido acabou: ele sai, sem aviso (a ficha fica como está).</summary>
    private void Vencer(string idEquipamento, Pendente? pendente)
    {
        lock (_trava)
        {
            if (pendente is null || !_pendentes.TryGetValue(idEquipamento, out var atual) || !ReferenceEquals(atual, pendente))
                return;
            _pendentes.Remove(idEquipamento);
            atual.Prazo.Dispose();
        }
        _log.Evento($"ficha: o pedido do modelo {pendente.Pedido.Modelo} venceu sem proposta");
    }

    /// <summary>O equipamento do pedido, se ainda tem o mesmo modelo e o mesmo grupo; senão null.</summary>
    private Equipamento? Equipamento(PedidoFicha pedido) =>
        _setups.Setup(pedido.IdSetup)?.Equipamentos.FirstOrDefault(e => e.Id == pedido.IdEquipamento) is { } e
        && e.Grupo == pedido.Grupo && BaseModelos.Chave(e.Modelo) == BaseModelos.Chave(pedido.Modelo)
            ? e
            : null;

    /// <summary>
    /// A proposta da porta para o equipamento: sem pedido pendente, depois do prazo, ou com o equipamento já com outro
    /// modelo ou grupo, <see cref="MsgPrazo"/>; um dado inválido, 400 com o motivo. Válida, vai para a folha; nada é gravado.
    /// </summary>
    public ResultadoProposta Receber(string? idEquipamento, IReadOnlyList<DadoProposto>? dados)
    {
        var id = idEquipamento?.Trim();
        if (string.IsNullOrEmpty(id))
            return ResultadoProposta.Mal(MsgSemEquipamento);
        PedidoFicha pedido;
        lock (_trava)
        {
            if (!_pendentes.TryGetValue(id, out var pendente))
                return ResultadoProposta.Falha(MsgPrazo);
            if (_relogio.GetUtcNow() - pendente.Quando > Prazo)
            {
                _pendentes.Remove(id);
                pendente.Prazo.Dispose();
                return ResultadoProposta.Falha(MsgPrazo);
            }
            pedido = pendente.Pedido;
        }
        if (Validar(pedido.Grupo, dados) is { } erro)
            return ResultadoProposta.Mal(erro);
        var equipamento = Equipamento(pedido);
        var limpos = dados!.Select(d => d with { Fonte = d.Fonte.Trim(), Aviso = string.IsNullOrWhiteSpace(d.Aviso) ? null : d.Aviso.Trim() }).ToArray();
        lock (_trava)
        {
            if (_pendentes.Remove(id, out var pendente))
                pendente.Prazo.Dispose();
            // O equipamento mudou de modelo ou de grupo (ou saiu) depois do pedido: os dados seriam de outro modelo.
            if (equipamento is null)
                return ResultadoProposta.Falha(MsgPrazo);
            _fila.Add(new Proposta(pedido, equipamento.Nome, limpos));
        }
        _log.Evento($"ficha: proposta para o modelo {pedido.Modelo} com {limpos.Length} dados");
        Notificar();
        return ResultadoProposta.Feito;
    }

    private static string? Validar(GrupoEquipamento grupo, IReadOnlyList<DadoProposto>? dados)
    {
        if (dados is null || dados.Count == 0)
            return MsgSemDados;
        var valem = CamposDo(grupo).ToHashSet(StringComparer.Ordinal);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in dados)
        {
            if (d is null)
                return MsgSemDados;
            if (!valem.Contains(d.Campo) || FaixaDe(d.Campo) is not { } faixa)
                return MsgCampo(d.Campo);
            if (!vistos.Add(d.Campo))
                return MsgCampoRepetido(d.Campo);
            if (d.Valor < faixa.Minimo || d.Valor > faixa.Maximo)
                return MsgFaixa(faixa);
            if (d.Origem is not (OrigemDado.Oficial or OrigemDado.OutraFonte))
                return MsgOrigem(d.Campo);
            if (string.IsNullOrWhiteSpace(d.Fonte) || Ligacoes.TextoLongoValido(d.Fonte) is null)
                return MsgFonte(d.Campo);
            if (!string.IsNullOrWhiteSpace(d.Aviso) && Ligacoes.TextoLongoValido(d.Aviso) is null)
                return MsgAviso(d.Campo);
        }
        return null;
    }

    /// <summary>
    /// O "Salvar" da folha (só o toque do usuário): os dados <paramref name="usados"/> (os campos) entram na ficha do
    /// equipamento com a origem e a fonte, e a ficha vai à base de modelos (pelo editar do equipamento). Os recusados não
    /// mexem na ficha. Sem nenhum usado, nada é gravado e a folha fecha. Com o equipamento já com outro modelo ou grupo (ou
    /// tirado), a proposta sai sem gravar nada (<see cref="MsgPrazo"/>). Com outra falha, a folha fica.
    /// </summary>
    public async Task<ResultadoEquipamento> SalvarAsync(IReadOnlyCollection<string> usados)
    {
        Proposta proposta;
        lock (_trava)
        {
            if (_fila.Count == 0 || _salvando)
                return ResultadoEquipamento.Falha(MsgSemProposta);
            proposta = _fila[0];
            _salvando = true;
        }
        try
        {
            var escolhidos = proposta.Dados.Where(d => usados.Contains(d.Campo)).ToArray();
            if (escolhidos.Length == 0)
            {
                Tirar(proposta);
                return new ResultadoEquipamento(true, null);
            }
            var pedido = proposta.Pedido;
            if (Equipamento(pedido) is not { } equipamento)
            {
                _log.Evento($"ficha: o equipamento da proposta do modelo {pedido.Modelo} mudou; nada foi gravado");
                Tirar(proposta);
                return ResultadoEquipamento.Falha(MsgPrazo);
            }
            var ficha = equipamento.Dados;
            foreach (var d in escolhidos)
            {
                var campo = DadosTecnicos.Campos.First(c => c.Nome == d.Campo);
                ficha = campo.Com(ficha, new DadoTecnico(d.Valor, d.Origem, d.Fonte));
            }
            var r = await _setups.EditarEquipamentoAsync(pedido.IdSetup, equipamento with { Dados = ficha });
            if (!r.Ok)
                return r;
            _log.Evento($"ficha: {escolhidos.Length} dados do modelo {pedido.Modelo} salvos pelo usuário");
            Tirar(proposta);
            return r;
        }
        finally
        {
            lock (_trava)
                _salvando = false;
        }
    }

    /// <summary>Fechar a folha sem salvar: nada é gravado.</summary>
    public void Fechar()
    {
        Proposta? proposta;
        lock (_trava)
        {
            if (_salvando)
                return;
            proposta = _fila.Count > 0 ? _fila[0] : null;
        }
        if (proposta is not null)
            Tirar(proposta);
    }

    private void Tirar(Proposta proposta)
    {
        lock (_trava)
            _fila.Remove(proposta);
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da proposta de ficha ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _setups.FichaPedida -= AoPedir;
        lock (_trava)
        {
            _descartada = true;
            foreach (var p in _pendentes.Values)
                p.Prazo.Dispose();
            _pendentes.Clear();
        }
    }
}
