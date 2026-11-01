using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>Um alto-falante no <c>termico</c> do <c>/limites</c>: a temperatura estimada da bobina, a máxima (°C) e o selo; null sem o modelo.</summary>
public sealed record TermicoAltoFalante(string AltoFalante, double? Temperatura, double? Maxima, string? Selo);

/// <summary>
/// O modelo térmico ao vivo do Extra (história 13f, spec-rendimento CAP-7). A cada segundo, o
/// <see cref="ModeloTermico"/> dá um passo exato para cada alto-falante passivo (com o modelo completo) dos Setups:
/// <list type="bullet">
/// <item><b>Potência:</b> com o player do app tocando a memória em uso, a do RMS da faixa que toca (a análise da 13d, na
/// cadeia de agora) no master em uso; uma faixa sem análise usa o maior RMS da playlist. Sem nenhuma análise da cadeia de
/// agora, a de um tom contínuo de 0 dBFS pelo pior caso da cadeia (13c) no master em uso (o lado seguro). Uma potência
/// guardada só vale de novo para a mesma memória, o mesmo preset e o mesmo master. Parado ou pausado, P = 0 (esfria).
/// Outras fontes não entram (o app não as vigia).</item>
/// <item><b>Corte:</b> no Extra em uso, a bobina estimada de algum alto-falante chega à máxima, sem folga: o descanso é
/// gravado, o player pausa, os 4 canais são mutados (<c>100</c>) e o Bluetooth de áudio desliga (<c>251</c> = 0).</item>
/// <item><b>Descanso:</b> até toda bobina do Setup do corte voltar a ambiente + 1 °C (um alto-falante que perde um dado no
/// meio segue esfriando com os dados de antes); o player não toca ("Descansando: 12:34"), e tirar o mudo ou religar o
/// <c>251</c> pela tela ou pela porta é recusado. No fim, o app religa o <c>251</c>, tira o mudo que ele pôs e, se o player
/// tocava no corte, volta a tocar. Um travamento que não confirma tenta de novo (e avisa "Não consegui travar o Bluetooth.
/// Desligue o módulo."); sem conexão, o descanso começa igual, e o travamento (ou o religar) é escrito ao reconectar.</item>
/// <item><b>termico.json:</b> o estado de cada alto-falante (ΔTv, ΔTm, a hora e os dados), o descanso e o que o app pôs no
/// aparelho, a cada 10 s e ao fechar (<see cref="GravarAgoraAsync"/>). Ao abrir, o tempo fora entra como esfriamento, e o
/// descanso continua pelo relógio. Um arquivo ilegível conta como toda bobina na máxima: o Extra espera esfriar.</item>
/// </list>
/// </summary>
public sealed class TermicoExtra : IDisposable
{
    public const int Formato = 1;

    /// <summary>O passo do modelo.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1);

    /// <summary>De quanto em quanto tempo o estado vai para o <c>termico.json</c>.</summary>
    public static readonly TimeSpan IntervaloGravar = TimeSpan.FromSeconds(10);

    /// <summary>A espera entre duas tentativas de travar ou destravar que falharam [ASSUMPTION: 5 s, para não encher o registro].</summary>
    public static readonly TimeSpan EsperaTentativa = TimeSpan.FromSeconds(5);

    public const string MsgNaoTravou = "Não consegui travar o Bluetooth. Desligue o módulo.";
    public const string TextoBluetoothTravado = "Bluetooth travado";

    /// <summary>Antes de ler o <c>termico.json</c>, o descanso é desconhecido: o player espera.</summary>
    public const string MsgCarregando = "Carregando o modelo térmico do Extra.";

    /// <summary>No Extra, tocando, nenhuma bobina chega à máxima com a faixa atual.</summary>
    public const string MsgSemLimite = "Extra: sem limite nesta faixa";

    /// <summary>"13:52" (minutos e segundos, para cima); a partir de 1 h, "1:02:05".</summary>
    public static string Relogio(TimeSpan t)
    {
        var s = (long)Math.Ceiling(Math.Max(0, Math.Round(t.TotalSeconds, 6)));
        var dois = (long v) => v.ToString("00", CultureInfo.InvariantCulture);
        return s >= 3600
            ? $"{(s / 3600).ToString(CultureInfo.InvariantCulture)}:{dois(s / 60 % 60)}:{dois(s % 60)}"
            : $"{dois(s / 60)}:{dois(s % 60)}";
    }

    /// <summary>"Extra: 13:52".</summary>
    public static string MsgTempoExtra(TimeSpan t) => $"{Niveis.NomeExtra}: {Relogio(t)}";

    /// <summary>"Descansando: 38:04".</summary>
    public static string MsgDescansando(TimeSpan t) => $"Descansando: {Relogio(t)}";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;
    private static readonly Parametro Mute = Catalogo.ParametroDe(Camadas.CodigoMute)!;

    /// <summary>
    /// Um descanso: quem cortou (null no de precaução, o do arquivo ilegível), quando, o Setup (null: todos), as bobinas que
    /// precisam esfriar, se o player volta a tocar no fim e se o app trava o aparelho.
    /// </summary>
    private sealed record Repouso(string? AltoFalante, DateTimeOffset Quando, string? IdSetup, IReadOnlySet<string> Chaves, bool Retomar, bool Travar);

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly GerenteSetups _setups;
    private readonly AnalisePlaylist _analise;
    private readonly Player.Player _player;
    private readonly ComandosAparelho _comandos;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly IRepositorioTexto? _repositorio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _vez = new(1, 1);
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly Dictionary<string, EstadoTermico> _estados = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DadosTermicos> _dados = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (double P, string Contexto)> _potencias = new(StringComparer.Ordinal);
    private readonly SortedSet<int> _mutados = [];
    private bool _bluetoothDesligado;
    private Repouso? _descanso;
    private bool _retomar;
    private DateTimeOffset _ultimo;
    private DateTimeOffset _ultimaGravacao;
    private DateTimeOffset _proximaTentativa;
    private decimal? _ultimoMaster;
    private bool _carregado;
    private string? _aviso;
    private bool _descartado;
    private readonly ITimer _timer;

    public TermicoExtra(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        GerenteSetups setups,
        AnalisePlaylist analise,
        Player.Player player,
        ComandosAparelho comandos,
        LogBle log,
        TimeProvider? relogio = null,
        IRepositorioTexto? repositorio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _setups = setups;
        _analise = analise;
        _player = player;
        _comandos = comandos;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _repositorio = repositorio;
        _ultimo = _ultimaGravacao = _relogio.GetUtcNow();

        _player.Descanso = MotivoDescanso;
        _escritor.Descanso = RecusaEscrita;
        _comandos.TravaBluetooth = RecusaBluetooth;
        Carregado = Task.Run(CarregarAsync);
        _timer = _relogio.CreateTimer(_ => AoTique(), null, Intervalo, Intervalo);
    }

    /// <summary>Completa quando o <c>termico.json</c> foi lido (o tempo fora já entrou como esfriamento).</summary>
    public Task Carregado { get; }

    /// <summary>O modelo deu um passo, o descanso começou ou acabou, a trava ou o aviso mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>
    /// A memória do celular em uso está no Extra; depois de uma queda do BLE, a que o player segue tocando (a retida): a
    /// música continua pelo Bluetooth de áudio, e o corte vale do mesmo jeito.
    /// </summary>
    public bool ExtraEmUso => Contexto().Extra;

    /// <summary>A memória que o player toca: a em uso; sem ela, depois de uma queda, a retida.</summary>
    private MemoriaCelular? MemoriaTocando() =>
        _setups.MemoriaEmUso ?? (_player.Retida ? _setups.Memoria(_player.Memoria) : null);

    /// <summary>
    /// O Setup e o Extra de agora: os da memória em uso; sem ela, depois de uma queda, os da memória que o player retém;
    /// senão, o Setup em uso, fora do Extra.
    /// </summary>
    private (Setup? Setup, bool Extra) Contexto()
    {
        if (_setups.MemoriaEmUso is { } m)
            return (_setups.SetupEmUso ?? _setups.Setup(m.IdSetup), m.Nivel == NivelRendimento.Extra);
        if (_player.Retida && _setups.Memoria(_player.Memoria) is { } retida)
            return (_setups.Setup(retida.IdSetup), retida.Nivel == NivelRendimento.Extra);
        return (_setups.SetupEmUso, false);
    }

    /// <summary>O descanso anda.</summary>
    public bool EmDescanso
    {
        get
        {
            lock (_trava)
                return _descanso is not null;
        }
    }

    /// <summary>O alto-falante que cortou o Extra, no descanso, ou null (fora dele, ou no descanso de precaução).</summary>
    public string? CortadoPor
    {
        get
        {
            lock (_trava)
                return _descanso?.AltoFalante;
        }
    }

    /// <summary>Quando o descanso começou, ou null fora dele.</summary>
    public DateTimeOffset? DescansoDesde
    {
        get
        {
            lock (_trava)
                return _descanso?.Quando;
        }
    }

    /// <summary>O que falta de descanso (o maior tempo até ambiente + 1 °C entre as bobinas do descanso), ou null fora dele.</summary>
    public TimeSpan? Descanso
    {
        get
        {
            lock (_trava)
                return _descanso is { } d ? TimeSpan.FromSeconds(RestaSemTrava(d)) : null;
        }
    }

    /// <summary>O maior tempo até ambiente + 1 °C entre as bobinas do descanso. Chamar com a trava.</summary>
    private double RestaSemTrava(Repouso d) =>
        d.Chaves.Select(c => _estados.TryGetValue(c, out var x) && _dados.TryGetValue(c, out var dados) ? ModeloTermico.TempoDescanso(x, dados) : 0)
            .DefaultIfEmpty(0).Max();

    /// <summary>
    /// O tempo de Extra (no Extra em uso, tocando, fora do descanso e com o modelo de todo alto-falante): o menor tempo até a
    /// bobina de algum alto-falante do Setup chegar à máxima com a potência de agora; null fora disso ou se nenhuma chega
    /// (<see cref="SemLimite"/>).
    /// </summary>
    public TimeSpan? TempoExtra => Tempo().Tempo;

    /// <summary>No Extra, tocando, com o modelo: nenhuma bobina chega à máxima com a faixa atual ("Extra: sem limite nesta faixa").</summary>
    public bool SemLimite => Tempo().SemLimite;

    /// <summary>No Extra, tocando, sem o modelo de algum alto-falante: o motivo ("Falta o modelo térmico do Sub."), ou null.</summary>
    public string? MotivoSemModelo => Tempo().Motivo;

    private (TimeSpan? Tempo, bool SemLimite, string? Motivo) Tempo()
    {
        if (Contexto() is not { Extra: true, Setup: { } setup } || _player.Estado != EstadoPlayer.Tocando)
            return (null, false, null);
        var avaliacao = ModeloTermico.Avaliar(setup, Ligacoes.Saidas(setup.Equipamentos));
        if (!avaliacao.Pode)
            return (null, false, avaliacao.Motivo);
        var ambiente = ModeloTermico.Ambiente(setup);
        double? menor = null;
        lock (_trava)
        {
            // Antes do primeiro passo tocando, a potência ainda não se sabe: nem tempo, nem "sem limite".
            if (_descanso is not null || !avaliacao.AltoFalantes.Any(a => _potencias.ContainsKey(Chave(setup.Id, a.AltoFalante.Id))))
                return (null, false, null);
            foreach (var a in avaliacao.AltoFalantes)
            {
                var chave = Chave(setup.Id, a.AltoFalante.Id);
                if (!_estados.TryGetValue(chave, out var x))
                    continue;
                var p = _potencias.TryGetValue(chave, out var guardada) ? guardada.P : 0;
                if (ModeloTermico.TempoAteMaxima(x, a.Dados!, p, ambiente) is { } t && (menor is null || t < menor))
                    menor = t;
            }
        }
        return menor is { } m ? (TimeSpan.FromSeconds(m), false, null) : (null, true, null);
    }

    /// <summary>"Extra: 13:52", "Extra: sem limite nesta faixa", ou null.</summary>
    public string? TextoTempoExtra
    {
        get
        {
            var (tempo, semLimite, _) = Tempo();
            return tempo is { } t ? MsgTempoExtra(t) : semLimite ? MsgSemLimite : null;
        }
    }

    /// <summary>"Descansando: 38:04", ou null fora do descanso.</summary>
    public string? TextoDescanso => Descanso is { } t ? MsgDescansando(t) : null;

    /// <summary>No descanso, o espelho mostra os 4 canais mutados e o Bluetooth de áudio desligado.</summary>
    public bool BluetoothTravado => EmDescanso && Travado();

    /// <summary>"Não consegui travar o Bluetooth. Desligue o módulo." enquanto o travamento não confirma, ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>O app pôs o mudo ou desligou o Bluetooth e ainda não desfez (no descanso, ou esperando a conexão para religar).</summary>
    public bool TravaNoAparelho
    {
        get
        {
            lock (_trava)
                return _mutados.Count > 0 || _bluetoothDesligado;
        }
    }

    /// <summary>A avaliação do Extra pelo térmico no Setup em uso (o "pode", o motivo e o selo).</summary>
    public AvaliacaoTermica Avaliacao => _setups.TermicoEmUso;

    /// <summary>O ΔT estimado da bobina de um alto-falante do Setup de agora (o id), ou null sem o modelo.</summary>
    public EstadoTermico? Estado(string idAltoFalante)
    {
        if (Contexto().Setup is not { } setup)
            return null;
        lock (_trava)
            return _estados.TryGetValue(Chave(setup.Id, idAltoFalante), out var x) ? x : null;
    }

    /// <summary>A potência na bobina usada no último passo (W), de um alto-falante do Setup de agora, ou null.</summary>
    public double? PotenciaAtual(string idAltoFalante)
    {
        if (Contexto().Setup is not { } setup)
            return null;
        lock (_trava)
            return _potencias.TryGetValue(Chave(setup.Id, idAltoFalante), out var p) ? p.P : null;
    }

    /// <summary>Por alto-falante passivo do Setup de agora: a temperatura estimada, a máxima e o selo (null sem o modelo).</summary>
    public IReadOnlyList<TermicoAltoFalante> AltoFalantes
    {
        get
        {
            if (Contexto().Setup is not { } setup)
                return [];
            var ambiente = ModeloTermico.Ambiente(setup);
            var avaliacao = ModeloTermico.Avaliar(setup, Ligacoes.Saidas(setup.Equipamentos));
            lock (_trava)
            {
                return avaliacao.AltoFalantes.Select(a =>
                {
                    var chave = Chave(setup.Id, a.AltoFalante.Id);
                    // No descanso, um alto-falante que perdeu um dado segue com os de antes.
                    if (!_dados.TryGetValue(chave, out var d) || !_estados.TryGetValue(chave, out var x))
                        return new TermicoAltoFalante(a.AltoFalante.Nome, null, null, null);
                    return new TermicoAltoFalante(a.AltoFalante.Nome, Math.Round(ambiente + x.Total, 1), d.TMax, a.Completo ? a.Selo : null);
                }).ToArray();
            }
        }
    }

    // ------------------------------------------------------------------ o passo

    private void AoTique()
    {
        try
        {
            Andar();
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha no modelo térmico ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// Um passo do modelo, do último até agora, com a potência de agora; depois o corte, o fim do descanso, a trava e a
    /// gravação a cada 10 s.
    /// </summary>
    private void Andar()
    {
        if (!Volatile.Read(ref _carregado))
            return;
        var agora = _relogio.GetUtcNow();
        var (emUso, extra) = Contexto();
        var memoria = MemoriaTocando();
        var idPreset = _setups.MemoriaEmUso is not null ? _setups.IdPresetEmUso : memoria?.IdPreset;
        var tocando = _player.Estado == EstadoPlayer.Tocando;
        var faixa = _player.Atual;
        var masterLido = _estado.Valor(Camadas.CodigoMaster)?.Numero;
        var setups = _setups.Dados.Setups
            .Select(s => (Setup: s, Avaliacao: ModeloTermico.Avaliar(s, Ligacoes.Saidas(s.Equipamentos))))
            .ToArray();
        var avaliacaoEmUso = emUso is null ? null : setups.Where(s => s.Setup.Id == emUso.Id).Select(s => s.Avaliacao).FirstOrDefault();

        decimal? master;
        lock (_trava)
        {
            // Sem o espelho (a conexão caiu antes de um passo), o master guardado na memória que toca.
            _ultimoMaster = masterLido ?? _ultimoMaster ?? MasterGuardado(memoria);
            master = _ultimoMaster;
        }
        // A potência de agora, fora da trava (o pior caso faz a conta da cadeia inteira).
        var contexto = $"{memoria?.Id}|{idPreset}|{master?.ToString(Fio)}";
        var novas = new Dictionary<string, double>(StringComparer.Ordinal);
        if (tocando && emUso is not null && avaliacaoEmUso is not null && master is { } m)
        {
            var analises = Analises(memoria, idPreset);
            Func<Endereco, Valor?>? leitor = null;
            foreach (var a in avaliacaoEmUso.AltoFalantes.Where(a => a.Completo))
            {
                var p = PotenciaPelaAnalise(a, analises, faixa?.Id, m);
                if (p is null)
                {
                    leitor ??= Leitor(memoria);
                    p = PotenciaPeloPiorCaso(a, leitor, emUso.NivelBluetooth?.Valor, m);
                }
                if (p is { } valor)
                    novas[Chave(emUso.Id, a.AltoFalante.Id)] = valor;
            }
        }

        string? corte = null;
        var acabou = false;
        bool gravar;
        bool garantir;
        lock (_trava)
        {
            if (_descartado)
                return;
            var dt = (agora - _ultimo).TotalSeconds;
            _ultimo = agora;
            var vivos = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (setup, avaliacao) in setups)
            {
                foreach (var a in avaliacao.AltoFalantes.Where(a => a.Completo))
                {
                    var chave = Chave(setup.Id, a.AltoFalante.Id);
                    vivos.Add(chave);
                    _dados[chave] = a.Dados!;
                    var p = 0.0;
                    if (tocando && setup.Id == emUso?.Id)
                    {
                        if (novas.TryGetValue(chave, out var nova))
                            _potencias[chave] = (nova, contexto);
                        // Uma potência guardada só vale para a mesma memória, preset e master.
                        p = _potencias.TryGetValue(chave, out var guardada) && guardada.Contexto == contexto ? guardada.P : 0;
                    }
                    if (dt > 0)
                        _estados[chave] = ModeloTermico.Passo(_estados.GetValueOrDefault(chave), a.Dados!, p, dt);
                    else
                        _estados.TryAdd(chave, default);
                }
            }
            foreach (var chave in _estados.Keys.Where(k => !vivos.Contains(k)).ToArray())
            {
                // No descanso, uma bobina dele segue esfriando com os dados de antes (um dado tirado não acaba o descanso).
                if (_descanso?.Chaves.Contains(chave) == true && _dados.TryGetValue(chave, out var antes))
                {
                    if (dt > 0)
                        _estados[chave] = ModeloTermico.Passo(_estados[chave], antes, 0, dt);
                    _potencias.Remove(chave);
                    continue;
                }
                _estados.Remove(chave);
                _dados.Remove(chave);
                _potencias.Remove(chave);
            }

            if (_descanso is null && extra && emUso is not null && avaliacaoEmUso is not null)
            {
                // O corte: no Extra, a bobina na máxima, sem folga.
                var ambiente = ModeloTermico.Ambiente(emUso);
                var completos = avaliacaoEmUso.AltoFalantes.Where(a => a.Completo).ToArray();
                corte = completos.FirstOrDefault(a =>
                    ambiente + _estados.GetValueOrDefault(Chave(emUso.Id, a.AltoFalante.Id)).Total >= a.Dados!.TMax)?.AltoFalante.Nome;
                if (corte is not null)
                {
                    var chaves = completos.Select(a => Chave(emUso.Id, a.AltoFalante.Id)).ToHashSet(StringComparer.Ordinal);
                    _descanso = new Repouso(corte, agora, emUso.Id, chaves, tocando, Travar: true);
                    _retomar = false;
                    _proximaTentativa = agora;
                }
            }
            else if (_descanso is { } d && Acabou(d))
            {
                _descanso = null;
                _retomar = d.Retomar;
                _proximaTentativa = agora;
                acabou = true;
            }

            gravar = corte is null && (acabou || agora - _ultimaGravacao >= IntervaloGravar);
            garantir = corte is null && (_descanso is not null || _mutados.Count > 0 || _bluetoothDesligado || _retomar)
                       && agora >= _proximaTentativa;
        }

        if (corte is not null)
        {
            _log.Evento($"Extra: a bobina do {corte} chegou à temperatura máxima; descanso com o Bluetooth travado");
            _ = Task.Run(CortarAsync);
        }
        else if (garantir)
        {
            _ = Task.Run(GarantirAsync);
        }
        if (acabou)
            _log.Evento("Extra: o descanso acabou (as bobinas voltaram à temperatura ambiente)");
        if (gravar)
            _ = Task.Run(GravarAgoraAsync);
        Notificar();
    }

    /// <summary>O descanso acaba com toda bobina dele em ambiente + 1 °C; nunca com nenhuma. Chamar com a trava.</summary>
    private bool Acabou(Repouso d) =>
        d.Chaves.Count > 0 && d.Chaves.All(c => _estados.TryGetValue(c, out var x) && x.Total <= ModeloTermico.FolgaDescanso);

    /// <summary>As análises da memória que toca com o preset dela, na cadeia de agora. Uma falha conta como nenhuma.</summary>
    private IReadOnlyDictionary<string, AnaliseFaixa>? Analises(MemoriaCelular? memoria, string? idPreset)
    {
        try
        {
            return memoria is null ? null : _analise.Analises(memoria.Id, idPreset);
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao ler a análise para o térmico ({e.GetType().Name})");
            return null;
        }
    }

    /// <summary>
    /// A potência na bobina pela análise: a faixa que toca (sem análise, o maior RMS da playlist) no master, a maior entre
    /// os canais que alimentam o alto-falante. Null sem nenhuma análise da cadeia de agora.
    /// </summary>
    private static double? PotenciaPelaAnalise(AltoFalanteTermico a, IReadOnlyDictionary<string, AnaliseFaixa>? analises, string? idFaixa, decimal master)
    {
        if (analises is null || analises.Count == 0 || a.Impedancia is not { } z)
            return null;
        var daFaixa = idFaixa is not null && analises.TryGetValue(idFaixa, out var x) ? x : null;
        double Rms(AnaliseFaixa f, int canal) => f.Canais.FirstOrDefault(c => c.Canal == canal)?.Rms ?? MedidorFaixa.PisoDb;
        var maior = 0.0;
        foreach (var (canal, ganho) in a.Canais)
        {
            var rms = daFaixa is not null ? Rms(daFaixa, canal) : analises.Values.Max(f => Rms(f, canal));
            maior = Math.Max(maior, ModeloTermico.Potencia(rms, (double)master, ganho, z));
        }
        return maior;
    }

    /// <summary>
    /// A potência de um tom contínuo de 0 dBFS pelo pior caso da cadeia (13c), no master: o lado seguro sem análise. Null sem
    /// a cadeia ou sem o nível do Bluetooth.
    /// </summary>
    private static double? PotenciaPeloPiorCaso(AltoFalanteTermico a, Func<Endereco, Valor?>? leitor, decimal? nivelBluetooth, decimal master)
    {
        if (leitor is null || nivelBluetooth is not { } bt || a.Impedancia is not { } z)
            return null;
        var maior = 0.0;
        foreach (var (canal, ganho) in a.Canais)
        {
            if (PiorCaso.Pico(leitor, canal, bt).Db is not { } db)
                return null;
            maior = Math.Max(maior, ModeloTermico.Potencia(db, (double)master, ganho, z));
        }
        return maior;
    }

    /// <summary>A cadeia de agora: o espelho com a memória em uso; senão, a regulagem da memória que toca com o preset dela.</summary>
    private Func<Endereco, Valor?>? Leitor(MemoriaCelular? memoria)
    {
        if (_setups.MemoriaEmUso is not null && _estado.Pronto)
            return _estado.Valor;
        if (memoria is null)
            return null;
        var valores = new Dictionary<Endereco, Valor>();
        foreach (var v in memoria.Regulagem.Concat(_setups.PresetDe(memoria)?.Valores ?? GerentePresets.BandasPlanas))
        {
            if (Catalogo.ParametroDe(v.Codigo)?.Decodificar(v.NoFio).Valor is { } valor)
                valores[new Endereco(v.Codigo, v.Indice)] = valor;
        }
        return e => valores.GetValueOrDefault(e);
    }

    private static string Chave(string idSetup, string idAltoFalante) => $"{idSetup}/{idAltoFalante}";

    /// <summary>O MASTER guardado na regulagem da memória (o nível o grava lá), ou null.</summary>
    private static decimal? MasterGuardado(MemoriaCelular? memoria) =>
        memoria?.Regulagem.FirstOrDefault(v => v.Codigo == Camadas.CodigoMaster) is { } v
            ? Catalogo.ParametroDe(Camadas.CodigoMaster)?.Decodificar(v.NoFio).Valor?.Numero
            : null;

    // ------------------------------------------------------------------ o corte, a trava e o religar

    /// <summary>O descanso vai para o celular primeiro; depois o player pausa e a trava vai ao aparelho.</summary>
    private async Task CortarAsync()
    {
        try
        {
            await GravarAgoraAsync();
            await _player.PausarAsync("pelo descanso do Extra");
            await GarantirAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha no corte ({e.GetType().Name})");
        }
        Notificar();
    }

    /// <summary>
    /// No descanso, trava (o que falta; no de precaução, só adota a trava que já está no aparelho); fora dele, com a trava
    /// do app no aparelho, destrava; depois, se o player tocava no corte, volta a tocar. Uma vez por vez.
    /// </summary>
    private async Task GarantirAsync()
    {
        if (!await _vez.WaitAsync(0))
            return;
        var retomar = false;
        try
        {
            Repouso? d;
            lock (_trava)
                d = _descanso;
            if (d is { Travar: true })
                await TravarAsync();
            else if (d is not null)
                await AdotarAsync();
            else if (TravaNoAparelho)
                await DestravarAsync();
            if (d is null && !TravaNoAparelho)
            {
                lock (_trava)
                {
                    retomar = _retomar;
                    _retomar = false;
                }
            }
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao travar ou religar o Bluetooth ({e.GetType().Name})");
            Adiar();
        }
        finally
        {
            _vez.Release();
        }
        if (retomar && _player.Estado == EstadoPlayer.Pausado)
        {
            _log.Evento("Extra: o player volta a tocar depois do descanso");
            await _player.TocarPausarAsync();
        }
        Notificar();
    }

    private bool PodeEscrever =>
        _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao && _estado.Pronto && !_estado.Lendo;

    /// <summary>O espelho mostra os 4 canais mutados e o Bluetooth de áudio desligado.</summary>
    private bool Travado() =>
        Enumerable.Range(0, Parametro.Canais).All(c => _estado.Valor(Camadas.CodigoMute, c)?.Numero == 1m)
        && _estado.Valor(Catalogo.CodigoBluetoothAudio)?.Ligado == false;

    /// <summary>
    /// Muta os canais que o espelho não mostra mutados e desliga o <c>251</c> se ele não está desligado; o que o app pôs
    /// fica guardado para o fim. Sem conexão, fica para depois (sem aviso). Uma falha avisa e tenta de novo.
    /// </summary>
    private async Task TravarAsync()
    {
        if (!PodeEscrever)
            return;
        if (Travado())
        {
            LimparAviso(MsgNaoTravou);
            return;
        }
        string? falha = null;
        var mudou = false;
        for (var c = 0; c < Parametro.Canais; c++)
        {
            if (_estado.Valor(Camadas.CodigoMute, c)?.Numero == 1m)
                continue;
            if (await EscreverMudoAsync(c, 1m) is { } motivo)
            {
                falha ??= motivo;
                _log.Evento($"Extra: o mute da saída {c + 1} não foi escrito no descanso ({motivo})");
                continue;
            }
            lock (_trava)
                mudou |= _mutados.Add(c);
        }
        if (_estado.Valor(Catalogo.CodigoBluetoothAudio)?.Ligado != false)
        {
            ResultadoAparelho r;
            using (Origem.Como(OrigemComando.App))
                r = await _comandos.DefinirBluetoothAudioAsync(false);
            if (r.Ok)
            {
                lock (_trava)
                {
                    mudou |= !_bluetoothDesligado;
                    _bluetoothDesligado = true;
                }
            }
            else
            {
                falha ??= r.Motivo;
                _log.Evento($"Extra: o Bluetooth de áudio não desligou no descanso ({r.Motivo})");
            }
        }
        if (mudou)
            await GravarAgoraAsync();
        if (falha is null)
        {
            LimparAviso(MsgNaoTravou);
            _log.Evento("Extra: Bluetooth travado (os 4 canais mutados e o Bluetooth de áudio desligado)");
            return;
        }
        lock (_trava)
            _aviso = MsgNaoTravou;
        Adiar();
    }

    /// <summary>
    /// O descanso de precaução (o arquivo ilegível): o app não sabe o que ele pôs no aparelho. Se o espelho mostra a trava
    /// inteira (os 4 canais mutados e o <c>251</c> desligado), ela fica até o fim do descanso e sai nele.
    /// </summary>
    private async Task AdotarAsync()
    {
        if (!PodeEscrever || !Travado())
            return;
        lock (_trava)
        {
            if (_mutados.Count > 0 || _bluetoothDesligado)
                return;
            for (var c = 0; c < Parametro.Canais; c++)
                _mutados.Add(c);
            _bluetoothDesligado = true;
        }
        _log.Evento("Extra: a trava do Bluetooth está no aparelho; ela fica até o fim do descanso");
        await GravarAgoraAsync();
    }

    /// <summary>Fora do descanso: religa o <c>251</c> (se o app o desligou) e tira o mudo que o app pôs.</summary>
    private async Task DestravarAsync()
    {
        if (!PodeEscrever)
            return;
        int[] mutados;
        bool bluetooth;
        lock (_trava)
        {
            mutados = _mutados.ToArray();
            bluetooth = _bluetoothDesligado;
        }
        var ok = true;
        foreach (var c in mutados)
        {
            if (_estado.Valor(Camadas.CodigoMute, c)?.Numero != 0m && await EscreverMudoAsync(c, 0m) is { } motivo)
            {
                ok = false;
                _log.Evento($"Extra: o mute da saída {c + 1} não saiu no fim do descanso ({motivo})");
                continue;
            }
            lock (_trava)
                _mutados.Remove(c);
        }
        if (bluetooth)
        {
            var ligado = _estado.Valor(Catalogo.CodigoBluetoothAudio)?.Ligado == true;
            if (!ligado)
            {
                ResultadoAparelho r;
                using (Origem.Como(OrigemComando.App))
                    r = await _comandos.DefinirBluetoothAudioAsync(true);
                ligado = r.Ok;
                if (!r.Ok)
                    _log.Evento($"Extra: o Bluetooth de áudio não religou no fim do descanso ({r.Motivo})");
            }
            if (ligado)
            {
                lock (_trava)
                    _bluetoothDesligado = false;
            }
            else
            {
                ok = false;
            }
        }
        await GravarAgoraAsync();
        if (ok)
        {
            LimparAviso(MsgNaoTravou);
            _log.Evento("Extra: fim do descanso; o Bluetooth de áudio voltou e o mudo saiu");
        }
        else
        {
            Adiar();
        }
    }

    /// <summary>Escreve o mute de um canal pelo escritor, com a origem App. O motivo da falha, ou null.</summary>
    private async Task<string?> EscreverMudoAsync(int canal, decimal valor)
    {
        ResultadoEscrita r;
        using (Origem.Como(OrigemComando.App))
            r = await _escritor.EscreverAsync(Mute, canal, valor, espelhar: false);
        if (!r.Ok)
            return r.Motivo ?? Sessao.MotivoFalhaEscrita;
        return r.Diferente ? $"o aparelho ficou em {r.Valor?.Exibicao ?? "outro valor"}." : null;
    }

    private void Adiar()
    {
        lock (_trava)
            _proximaTentativa = _relogio.GetUtcNow() + EsperaTentativa;
    }

    private void LimparAviso(string aviso)
    {
        lock (_trava)
        {
            if (_aviso == aviso)
                _aviso = null;
        }
    }

    // ------------------------------------------------------------------ as recusas no descanso

    /// <summary>"Descansando: 38:04." (a recusa de quem quer desfazer a trava no descanso).</summary>
    private string MsgRecusa() => $"{MsgDescansando(Descanso ?? TimeSpan.Zero)}.";

    /// <summary>
    /// O player no descanso: "Descansando: 38:04" (no de precaução, só no Extra ou com a trava no aparelho). Antes de ler o
    /// <c>termico.json</c>, o descanso é desconhecido: "Carregando o modelo térmico do Extra.".
    /// </summary>
    private string? MotivoDescanso()
    {
        if (!Volatile.Read(ref _carregado))
            return MsgCarregando;
        Repouso? d;
        lock (_trava)
            d = _descanso;
        if (d is null || (!d.Travar && !TravaNoAparelho && !Contexto().Extra))
            return null;
        return MsgDescansando(Descanso ?? TimeSpan.Zero);
    }

    /// <summary>O descanso segura a trava: o do corte sempre; o de precaução, com a trava adotada do aparelho.</summary>
    private bool SeguraTrava()
    {
        lock (_trava)
            return _descanso is { } d && (d.Travar || _mutados.Count > 0 || _bluetoothDesligado);
    }

    /// <summary>No descanso, tirar o mudo (<c>100</c> = 0) é recusado.</summary>
    private string? RecusaEscrita(Parametro parametro, Valor? valor) =>
        parametro.Codigo == Camadas.CodigoMute && valor?.Numero == 0m && SeguraTrava() ? MsgRecusa() : null;

    /// <summary>No descanso, religar o Bluetooth de áudio é recusado.</summary>
    private string? RecusaBluetooth() => SeguraTrava() ? MsgRecusa() : null;

    // ------------------------------------------------------------------ termico.json

    /// <summary>
    /// Grava o <c>termico.json</c>: <c>{"formato":1,"altoFalantes":[{"chave","bobina","ima","quando","dados":[tMax,rv,rm,τv,τm]}],
    /// "descanso":{"altoFalante","quando","setup","chaves":[…],"travar"}|null,"trava":{"mutados":[0,1,2,3],"bluetooth":true}|null}</c>.
    /// False = não gravou.
    /// </summary>
    public async Task<bool> GravarAgoraAsync()
    {
        if (_repositorio is null || !Volatile.Read(ref _carregado))
            return true;
        await _gravacao.WaitAsync();
        try
        {
            string json;
            lock (_trava)
            {
                _ultimaGravacao = _relogio.GetUtcNow();
                json = ParaJson();
            }
            if (await Gravacao.GravarAsync(_repositorio, json))
                return true;
            _log.Evento("Extra: não consegui guardar o modelo térmico no celular");
            return false;
        }
        finally
        {
            _gravacao.Release();
        }
    }

    /// <summary>O JSON do estado agora. Chamar com a trava.</summary>
    private string ParaJson()
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartArray("altoFalantes");
            var quando = _ultimo.ToString("O", Fio);
            foreach (var (chave, x) in _estados.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("chave", chave);
                w.WriteNumber("bobina", Math.Round(x.Bobina, 6));
                w.WriteNumber("ima", Math.Round(x.Ima, 6));
                w.WriteString("quando", quando);
                if (_dados.TryGetValue(chave, out var d))
                {
                    w.WriteStartArray("dados");
                    foreach (var v in new[] { d.TMax, d.RthBobinaIma, d.RthImaAr, d.TauBobina, d.TauIma })
                        w.WriteNumberValue(v);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            if (_descanso is { } r)
            {
                w.WriteStartObject("descanso");
                w.WriteString("altoFalante", r.AltoFalante);
                w.WriteString("quando", r.Quando.ToString("O", Fio));
                w.WriteString("setup", r.IdSetup);
                w.WriteStartArray("chaves");
                foreach (var c in r.Chaves.Order(StringComparer.Ordinal))
                    w.WriteStringValue(c);
                w.WriteEndArray();
                w.WriteBoolean("travar", r.Travar);
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("descanso");
            }
            if (_mutados.Count > 0 || _bluetoothDesligado)
            {
                w.WriteStartObject("trava");
                w.WriteStartArray("mutados");
                foreach (var c in _mutados)
                    w.WriteNumberValue(c);
                w.WriteEndArray();
                w.WriteBoolean("bluetooth", _bluetoothDesligado);
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("trava");
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>termico.json</c>: cada bobina esfria o tempo que o app ficou fechado; o descanso e a trava voltam. Sem arquivo,
    /// tudo frio. Um arquivo ilegível (ou de outro formato) conta como toda bobina na máxima: o Extra espera esfriar.
    /// </summary>
    private async Task CarregarAsync()
    {
        try
        {
            await _setups.Carregada;
            if (_repositorio is not null && await Gravacao.LerAsync(_repositorio) is { } json)
            {
                bool lido;
                try
                {
                    lido = Ler(json);
                }
                catch (Exception e)
                {
                    _log.Evento($"Extra: o modelo térmico guardado está ilegível ({e.GetType().Name})");
                    lido = false;
                }
                if (!lido)
                    Precaucao();
            }
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao carregar o modelo térmico ({e.GetType().Name})");
            Precaucao();
        }
        finally
        {
            lock (_trava)
            {
                _ultimo = _ultimaGravacao = _relogio.GetUtcNow();
                Volatile.Write(ref _carregado, true);
            }
        }
        Notificar();
    }

    /// <summary>Os dados de cada bobina completa dos Setups agora, com o ambiente do Setup dela.</summary>
    private Dictionary<string, (DadosTermicos Dados, double Ambiente, string IdSetup)> DadosAtuais()
    {
        var dados = new Dictionary<string, (DadosTermicos, double, string)>(StringComparer.Ordinal);
        foreach (var s in _setups.Dados.Setups)
        {
            var ambiente = ModeloTermico.Ambiente(s);
            foreach (var a in ModeloTermico.Avaliar(s, Ligacoes.Saidas(s.Equipamentos)).AltoFalantes.Where(a => a.Completo))
                dados[Chave(s.Id, a.AltoFalante.Id)] = (a.Dados!, ambiente, s.Id);
        }
        return dados;
    }

    /// <summary>
    /// O arquivo ilegível: toda bobina completa começa na máxima, num descanso de precaução (o player espera no Extra); a
    /// trava que estiver no aparelho fica até ele acabar.
    /// </summary>
    private void Precaucao()
    {
        var atuais = DadosAtuais();
        lock (_trava)
        {
            _estados.Clear();
            _dados.Clear();
            foreach (var (chave, (d, ambiente, _)) in atuais)
            {
                _estados[chave] = ModeloTermico.NaMaxima(d, ambiente);
                _dados[chave] = d;
            }
            _descanso = atuais.Count > 0
                ? new Repouso(null, _relogio.GetUtcNow(), null, atuais.Keys.ToHashSet(StringComparer.Ordinal), Retomar: false, Travar: false)
                : null;
        }
        _log.Evento("Extra: o modelo térmico guardado está ilegível; as bobinas contam como na temperatura máxima até esfriar");
    }

    /// <summary>O <c>termico.json</c> lido; false se ele não é deste formato.</summary>
    private bool Ler(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato)
            || formato != Formato)
            return false;
        var agora = _relogio.GetUtcNow();
        var atuais = DadosAtuais();
        Repouso? lido = null;
        lock (_trava)
        {
            if (raiz.TryGetProperty("altoFalantes", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lista.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || Texto(item, "chave") is not { } chave
                        || Numero(item, "bobina") is not { } bobina || Numero(item, "ima") is not { } ima)
                        continue;
                    var d = DadosDe(item) ?? (atuais.TryGetValue(chave, out var atual) ? atual.Dados : null);
                    if (d is null)
                        continue;
                    var fora = Quando(item) is { } q ? Math.Max(0, (agora - q).TotalSeconds) : 0;
                    _estados[chave] = ModeloTermico.Passo(new EstadoTermico(Math.Max(0, bobina), Math.Max(0, ima)), d, 0, fora);
                    _dados[chave] = d;
                }
            }
            if (raiz.TryGetProperty("descanso", out var descanso) && descanso.ValueKind == JsonValueKind.Object)
            {
                var idSetup = Texto(descanso, "setup");
                var chaves = descanso.TryGetProperty("chaves", out var cs) && cs.ValueKind == JsonValueKind.Array
                    ? cs.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!).ToHashSet(StringComparer.Ordinal)
                    : _estados.Keys.Where(k => idSetup is null || k.StartsWith(idSetup + "/", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
                if (chaves.Count == 0)
                    chaves = atuais.Where(a => idSetup is null || a.Value.IdSetup == idSetup).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
                // Uma bobina do descanso sem o estado guardado conta como na máxima; sem os dados, não dá para esfriá-la.
                foreach (var c in chaves.ToArray())
                {
                    if (_estados.ContainsKey(c))
                        continue;
                    if (atuais.TryGetValue(c, out var atual))
                    {
                        _estados[c] = ModeloTermico.NaMaxima(atual.Dados, atual.Ambiente);
                        _dados[c] = atual.Dados;
                    }
                    else
                    {
                        chaves.Remove(c);
                    }
                }
                var travar = !descanso.TryGetProperty("travar", out var t) || t.ValueKind != JsonValueKind.False;
                lido = _descanso = new Repouso(Texto(descanso, "altoFalante"), Quando(descanso) ?? agora, idSetup, chaves, Retomar: false, travar);
            }
            if (raiz.TryGetProperty("trava", out var trava) && trava.ValueKind == JsonValueKind.Object)
            {
                if (trava.TryGetProperty("mutados", out var mutados) && mutados.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in mutados.EnumerateArray())
                    {
                        if (c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var canal) && canal is >= 0 and < Parametro.Canais)
                            _mutados.Add(canal);
                    }
                }
                _bluetoothDesligado = trava.TryGetProperty("bluetooth", out var bt) && bt.ValueKind == JsonValueKind.True;
            }
        }
        if (lido is { } r)
        {
            _log.Evento($"Extra: o descanso continua pelo relógio (cortado pelo {r.AltoFalante ?? "modelo"}); a trava segue no aparelho"
                        + (r.Chaves.Count == 0 ? ", sem bobina para esfriar" : ""));
        }
        return true;
    }

    /// <summary>Os cinco dados guardados com o estado (<c>[tMax, rv, rm, τv, τm]</c>), ou null.</summary>
    private static DadosTermicos? DadosDe(JsonElement item)
    {
        if (!item.TryGetProperty("dados", out var d) || d.ValueKind != JsonValueKind.Array || d.GetArrayLength() != 5)
            return null;
        var v = d.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var n) && double.IsFinite(n) && n > 0 ? n : (double?)null)
            .ToArray();
        return v.All(x => x is not null) ? new DadosTermicos(v[0]!.Value, v[1]!.Value, v[2]!.Value, v[3]!.Value, v[4]!.Value) : null;
    }

    private static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static double? Numero(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v) && double.IsFinite(v) ? v : null;

    private static DateTimeOffset? Quando(JsonElement o) =>
        Texto(o, "quando") is { } q && DateTimeOffset.TryParse(q, Fio, DateTimeStyles.RoundtripKind, out var d) ? d : null;

    // ------------------------------------------------------------------ apoio

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do térmico do Extra ({e.GetType().Name})");
        }
    }

    /// <summary>Fechar o app não tira a trava do aparelho (o mudo e o <c>251</c> desligado ficam): só para o relógio e solta os ganchos.</summary>
    public void Dispose()
    {
        lock (_trava)
            _descartado = true;
        _timer.Dispose();
        if (_player.Descanso == MotivoDescanso)
            _player.Descanso = null;
        if (_escritor.Descanso == RecusaEscrita)
            _escritor.Descanso = null;
        if (_comandos.TravaBluetooth == RecusaBluetooth)
            _comandos.TravaBluetooth = null;
    }
}
