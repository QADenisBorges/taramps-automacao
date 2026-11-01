using System.Globalization;
using System.Text.Json;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// O fiscal do Extra (história 13e, spec-rendimento CAP-8 e "Revalidação"): só o player do app, no volume da conta,
/// alimenta o processador, e o master do Extra só baixa sozinho. Vale com a memória em uso no Extra e o player do app
/// tocando; durante o teste a seco, o fiscal não mexe em nada.
/// <list type="bullet">
/// <item><b>Clip não previsto</b> (o <c>150</c>): o master desce um passo (<see cref="Passo"/>) na hora, o validado
/// desce junto, e a faixa fica marcada na memória com cada saída do aviso e o momento. O 2º na mesma faixa põe a memória
/// em "revalidar". Se o master não desce, os 4 canais são mutados (estado seguro) e a tela avisa.</item>
/// <item><b>Outro player</b> (<see cref="IOutroPlayer.Comecou"/>): os 4 canais mutados (<c>100</c>), o player pausa e a
/// folha "Outro app começou a tocar. O som foi mutado.". O mudo fica guardado no celular e sai quando o usuário volta a
/// tocar no player do app (também depois de reabrir o app). Fechar o app não tira o mudo.</item>
/// <item><b>Volume do celular</b>: ao começar a tocar no Extra (e ao entrar no Extra tocando), o volume de mídia vai ao
/// máximo (o valor da conta); se ele mudar tocando, o app o devolve e avisa.</item>
/// <item><b>Revalidação</b>: a análise refeita com o candidato menor baixa o validado (e o master em uso) na hora; maior,
/// fica o validado até um teste a seco novo. O master da memória em uso no Extra nunca fica acima da conta do Extra
/// (<see cref="GerenteSetups.ContaEmUso"/>: o validado, ou min(validado, pior caso) fora de vigor).</item>
/// </list>
/// </summary>
public sealed class FiscalExtra : IDisposable
{
    /// <summary>Quanto o master desce a cada clip não previsto [ASSUMPTION: o passo de 1 dB, o mesmo do teste a seco].</summary>
    public const decimal Passo = TesteSeco.Passo;

    /// <summary>A menor diferença que faz o fiscal escrever o master (como a conta na hora da 13c).</summary>
    public const decimal Tolerancia = 0.05m;

    public const int Formato = 1;

    public const string MsgOutroPlayer = "Outro app começou a tocar. O som foi mutado.";
    public const string MsgVolume = "No Extra o volume fica no máximo; use o master.";
    public const string MsgVolumeBaixo = "O volume do celular não chega ao máximo. Confira o limite de volume e o Não perturbe.";
    public const string MsgMasterNaoDesceu = "Não consegui baixar o master depois do clip. O som foi mutado.";

    /// <summary>"Outro app começou a tocar, mas o som não foi mutado: …"</summary>
    public static string MsgNaoMutou(string motivo) => $"Outro app começou a tocar, mas o som não foi mutado: {motivo}";

    /// <summary>"Não consegui baixar o master depois do clip, nem mutar o som: …"</summary>
    public static string MsgNemMutou(string motivo) => $"Não consegui baixar o master depois do clip, nem mutar o som: {motivo}";

    /// <summary>Avisos de clip dentro deste tempo depois de uma reação são o mesmo clip (o 150 fica aceso por 3 s).</summary>
    public static readonly TimeSpan MesmoClip = MonitorAparelho.ClipAceso;

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;
    private static readonly Parametro Master = Catalogo.ParametroDe(Camadas.CodigoMaster)!;
    private static readonly Parametro Mute = Catalogo.ParametroDe(Camadas.CodigoMute)!;

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly AnalisePlaylist _analise;
    private readonly Player.Player _player;
    private readonly IReprodutor _reprodutor;
    private readonly MonitorAparelho _monitor;
    private readonly IOutroPlayer _outro;
    private readonly IVolumeCelular _volume;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly TesteSeco? _teste;
    private readonly IRepositorioTexto? _repositorio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _vez = new(1, 1);
    private DateTimeOffset? _ultimoClip;
    private decimal?[]? _muteAntes;
    private bool _pausando;
    private bool _folha;
    private string? _aviso;
    private bool _rodando;
    private bool _deNovo;
    private bool _descartado;

    public FiscalExtra(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        CopiaAntes copia,
        GerenteSetups setups,
        AnalisePlaylist analise,
        Player.Player player,
        IReprodutor reprodutor,
        MonitorAparelho monitor,
        IOutroPlayer outro,
        IVolumeCelular volume,
        LogBle log,
        TimeProvider? relogio = null,
        TesteSeco? teste = null,
        IRepositorioTexto? repositorio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _copia = copia;
        _setups = setups;
        _analise = analise;
        _player = player;
        _reprodutor = reprodutor;
        _monitor = monitor;
        _outro = outro;
        _volume = volume;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _teste = teste;
        _repositorio = repositorio;

        Carregado = Task.Run(CarregarAsync);
        _monitor.Clip += AoClip;
        _outro.Comecou += AoOutroPlayer;
        _volume.Mudou += AoMudarVolume;
        _analise.Mudou += Pedir;
        _setups.Mudou += Pedir;
        _copia.Mudou += Pedir;
        _estado.Recarregou += Pedir;
        _player.Mudou += Pedir;
        _player.AntesDeTocar = AntesDeTocarAsync;
    }

    /// <summary>Completa quando o mudo guardado (de antes de reabrir o app) foi lido.</summary>
    public Task Carregado { get; }

    /// <summary>A folha do outro player, um aviso ou uma marca mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A memória do celular em uso está no Extra.</summary>
    public bool ExtraEmUso => _setups.MemoriaEmUso is { Nivel: NivelRendimento.Extra };

    /// <summary>O fiscal vale agora: o Extra em uso, o player do app tocando e nenhum teste a seco andando.</summary>
    public bool Ativo => ExtraEmUso && _player.Estado == EstadoPlayer.Tocando && !EmTeste;

    /// <summary>O teste a seco anda: o fiscal não mexe em nada (o 150 é dele).</summary>
    private bool EmTeste => _teste?.EmOperacao == true;

    /// <summary>A folha "Outro app começou a tocar. O som foi mutado." está aberta.</summary>
    public bool FolhaOutroPlayer
    {
        get
        {
            lock (_trava)
                return _folha;
        }
    }

    /// <summary>O mudo posto pelo fiscal (o outro player, ou o master que não desceu) está no aparelho.</summary>
    public bool Mutado
    {
        get
        {
            lock (_trava)
                return _muteAntes is not null;
        }
    }

    /// <summary>
    /// O último aviso rápido ("No Extra o volume fica no máximo; use o master.", "Não consegui baixar o master…"), ou
    /// null. A tela o mostra uma vez (<see cref="LimparAviso"/>).
    /// </summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>Fecha a folha do outro player (o mudo continua até o usuário tocar no player do app).</summary>
    public void FecharFolha()
    {
        lock (_trava)
        {
            if (!_folha)
                return;
            _folha = false;
        }
        Notificar();
    }

    /// <summary>O aviso já foi mostrado.</summary>
    public void LimparAviso()
    {
        lock (_trava)
            _aviso = null;
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    // ------------------------------------------------------------------ o clip não previsto

    private void AoClip(IReadOnlyList<int> canais)
    {
        if (canais.Count == 0 || !Ativo || _player.Atual is not { } faixa || _setups.MemoriaEmUso is not { } memoria)
            return;
        var agora = _relogio.GetUtcNow();
        lock (_trava)
        {
            if (_descartado || (_ultimoClip is { } ultimo && agora - ultimo < MesmoClip))
                return;
            _ultimoClip = agora;
        }
        TimeSpan? posicao;
        try
        {
            posicao = _reprodutor.Posicao;
        }
        catch (Exception)
        {
            posicao = null;
        }
        _ = Task.Run(() => ReagirAoClipAsync(memoria.Id, faixa, canais.Distinct().Order().ToArray(), posicao, agora));
    }

    private async Task ReagirAoClipAsync(string idMemoria, Faixa faixa, int[] canais, TimeSpan? posicao, DateTimeOffset quando)
    {
        await _vez.WaitAsync();
        try
        {
            if (_setups.MemoriaEmUso is not { Nivel: NivelRendimento.Extra } memoria || memoria.Id != idMemoria || EmTeste)
                return;
            var chave = _setups.ChaveExtra(_setups.IdPresetEmUso);
            var atual = _estado.Valor(Camadas.CodigoMaster)?.Numero ?? memoria.ExtraValidado.GetValueOrDefault(chave)?.Master;
            var saidas = canais.Select(c => TesteSeco.NomeSaida(_setups.SaidasEmUso, c)).ToArray();
            double? segundos = posicao is { } p ? Math.Round(p.TotalSeconds, 1) : null;
            _log.Evento($"Extra: clip não previsto na {faixa.Nome}, saída {string.Join(", ", saidas)}"
                        + (segundos is { } s ? $", aos {s.ToString("0.0", Fio)} s" : ""));
            if (atual is { } m)
            {
                var novo = Math.Max(m - Passo, PiorCaso.MinimoMaster);
                if (await EscreverAsync(Master, null, novo) is { } falha)
                {
                    // O master não desceu: o estado seguro é o som mutado.
                    _log.Evento($"Extra: o master não desceu ({falha}); mutando os 4 canais");
                    var semMudo = await MutarSemVezAsync();
                    lock (_trava)
                    {
                        _aviso = semMudo is null ? MsgMasterNaoDesceu : MsgNemMutou(semMudo);
                        _pausando |= semMudo is null;
                    }
                }
                else
                {
                    _log.Evento($"Extra: master de {Texto(m)} para {Texto(novo)} dB");
                }
                await _setups.BaixarExtraAsync(memoria.Id, chave, novo);
            }
            var marcas = canais.Select((c, k) => new MarcaClip(faixa.Id, faixa.Nome, c, saidas[k], segundos, quando)).ToArray();
            if (await _setups.MarcarClipAsync(memoria.Id, marcas))
                _log.Evento($"Extra: a {memoria.Nome} foi para \"revalidar\" ({Niveis.MsgClipDuasVezes(faixa.Nome)})");
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao reagir ao clip ({e.GetType().Name})");
        }
        finally
        {
            _vez.Release();
        }
        await PausarSePedidoAsync();
        Notificar();
    }

    // ------------------------------------------------------------------ outro player

    private void AoOutroPlayer()
    {
        // Só com o Extra em uso e o player do app tocando (fora disso, nada).
        if (!Ativo)
            return;
        _ = Task.Run(MutarAsync);
    }

    /// <summary>Muta os 4 canais (guardando o mudo de antes), pausa o player e abre a folha.</summary>
    private async Task MutarAsync()
    {
        await _vez.WaitAsync();
        try
        {
            if (EmTeste)
                return;
            _log.Evento("Extra: outro app começou a tocar; mutando os 4 canais");
            var falha = await MutarSemVezAsync();
            lock (_trava)
            {
                // Até o player pausar, um "tocar" do usuário não tira o mudo (o outro app ainda toca).
                _pausando = true;
                _folha = true;
                if (falha is not null)
                    _aviso = MsgNaoMutou(falha);
            }
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao mutar ({e.GetType().Name})");
        }
        finally
        {
            _vez.Release();
        }
        await PausarSePedidoAsync();
        Notificar();
    }

    /// <summary>
    /// Pausa o player depois de mutar. Fora da vez: o pausar entra na fila do Player, que pode estar no antes de tocar,
    /// esperando a vez; até ele pausar, o antes de tocar não tira o mudo.
    /// </summary>
    private async Task PausarSePedidoAsync()
    {
        bool pausar;
        lock (_trava)
            pausar = _pausando;
        if (!pausar)
            return;
        try
        {
            await _player.PausarAsync("por outro app ou clip (Extra)");
        }
        finally
        {
            lock (_trava)
                _pausando = false;
        }
    }

    /// <summary>
    /// Muta os 4 canais, guardando (no celular também) o mudo de antes de cada um (null: desconhecido, não volta). Chamar
    /// com a vez. O motivo da primeira falha, ou null.
    /// </summary>
    private async Task<string?> MutarSemVezAsync()
    {
        decimal?[] antes;
        lock (_trava)
            antes = _muteAntes ?? Enumerable.Range(0, Parametro.Canais).Select(c => _estado.Valor(Camadas.CodigoMute, c)?.Numero).ToArray();
        string? falha = null;
        var algum = false;
        for (var c = 0; c < Parametro.Canais; c++)
        {
            if (_estado.Valor(Camadas.CodigoMute, c)?.Numero == 1m)
            {
                algum = true;
                continue;
            }
            if (await EscreverAsync(Mute, c, 1m) is { } motivo)
            {
                falha ??= motivo;
                _log.Evento($"Extra: o mute da saída {c + 1} não foi escrito ({motivo})");
            }
            else
            {
                algum = true;
            }
        }
        if (algum)
        {
            lock (_trava)
                _muteAntes = antes;
            await GuardarAsync(antes);
        }
        return falha;
    }

    /// <summary>
    /// Antes de o player do app tocar (um comando do usuário): o mudo do fiscal sai (o mudo de antes volta, menos o
    /// desconhecido) e, no Extra, o volume de mídia vai ao máximo.
    /// </summary>
    private async Task AntesDeTocarAsync()
    {
        await Carregado;
        await _vez.WaitAsync();
        try
        {
            decimal?[]? antes;
            bool pausando;
            lock (_trava)
            {
                antes = _muteAntes;
                pausando = _pausando;
            }
            if (antes is not null && !pausando)
            {
                var voltou = true;
                for (var c = 0; c < antes.Length; c++)
                {
                    if (antes[c] is not { } valor || _estado.Valor(Camadas.CodigoMute, c)?.Numero == valor)
                        continue;
                    if (await EscreverAsync(Mute, c, valor) is { } falha)
                    {
                        voltou = false;
                        _log.Evento($"Extra: o mute da saída {c + 1} não voltou ({falha})");
                    }
                }
                lock (_trava)
                {
                    if (voltou)
                        _muteAntes = null;
                    _folha = false;
                }
                if (voltou)
                {
                    await GuardarAsync(null);
                    _log.Evento("Extra: o player do app voltou a tocar; o mudo saiu");
                }
            }
            if (ExtraEmUso && !EmTeste)
                await VolumeNoMaximoAsync();
        }
        finally
        {
            _vez.Release();
        }
        Notificar();
    }

    // ------------------------------------------------------------------ o volume do celular

    private void AoMudarVolume()
    {
        if (!Ativo || _volume.Volume >= _volume.Maximo)
            return;
        lock (_trava)
            _aviso = MsgVolume;
        _log.Evento("Extra: o volume do celular mudou; de volta ao máximo");
        _ = Task.Run(VolumeNoMaximoAsync);
        Notificar();
    }

    /// <summary>Põe o volume de mídia no máximo; se ele não chega (o volume seguro, o "Não perturbe"), avisa.</summary>
    private async Task VolumeNoMaximoAsync()
    {
        if (_volume.Volume >= _volume.Maximo)
            return;
        bool chegou;
        try
        {
            chegou = await _volume.DefinirAsync(_volume.Maximo);
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: falha ao pôr o volume no máximo ({e.GetType().Name})");
            chegou = false;
        }
        if (chegou && _volume.Volume >= _volume.Maximo)
            return;
        lock (_trava)
            _aviso = MsgVolumeBaixo;
        _log.Evento("Extra: o volume do celular não chega ao máximo");
        Notificar();
    }

    // ------------------------------------------------------------------ a revalidação

    /// <summary>Pede a revisão (ela roda numa tarefa; com uma andando, ela roda de novo no fim).</summary>
    private void Pedir()
    {
        lock (_trava)
        {
            if (_descartado)
                return;
            if (_rodando)
            {
                _deNovo = true;
                return;
            }
            _rodando = true;
        }
        _ = Task.Run(RodarAsync);
    }

    private async Task RodarAsync()
    {
        while (true)
        {
            try
            {
                await ReverAsync();
            }
            catch (Exception e)
            {
                _log.Evento($"Extra: falha ao rever o validado ({e.GetType().Name})");
            }
            lock (_trava)
            {
                if (!_deNovo || _descartado)
                {
                    _rodando = false;
                    return;
                }
                _deNovo = false;
            }
        }
    }

    /// <summary>
    /// A análise refeita com o candidato abaixo do validado baixa o validado (o candidato maior não sobe nada); a memória
    /// em uso no Extra não fica com o master acima da conta do Extra; e, entrando no Extra com o player tocando, o
    /// volume vai ao máximo.
    /// </summary>
    private async Task ReverAsync()
    {
        if (EmTeste)
            return;
        foreach (var memoria in _setups.Dados.Memorias.Where(m => m.ExtraValidado.Count > 0).ToArray())
        {
            foreach (var (chave, validado) in memoria.ExtraValidado.ToArray())
            {
                var teto = _analise.Teto(memoria.Id, chave.Length == 0 ? null : chave);
                if (teto.Estado == EstadoExtra.Pronta && teto.Master is { } candidato && candidato < validado.Master)
                {
                    _log.Evento($"Extra: a análise da {memoria.Nome} baixou o teto de {Texto(validado.Master)} para {Texto(candidato)} dB");
                    await _setups.BaixarExtraAsync(memoria.Id, chave, candidato);
                }
            }
        }

        if (Ativo && _volume.Volume < _volume.Maximo)
        {
            await _vez.WaitAsync();
            try
            {
                await VolumeNoMaximoAsync();
            }
            finally
            {
                _vez.Release();
            }
        }

        if (!SessaoAberta || !_estado.Pronto || _estado.Lendo || _copia.MotivoBloqueio() is not null || _setups.Ocupado)
            return;
        if (!ExtraEmUso || _setups.ExtraValidadoEmUso is null || _setups.ContaEmUso()?.Master is not { } alvo)
            return;
        if (_estado.Valor(Camadas.CodigoMaster)?.Numero is not { } atual || atual <= alvo + Tolerancia)
            return;
        await _vez.WaitAsync();
        try
        {
            if (EmTeste)
                return;
            if (await EscreverAsync(Master, null, alvo) is { } falha)
                _log.Evento($"Extra: o master não desceu para a conta do Extra ({falha})");
            else
                _log.Evento($"Extra: master de {Texto(atual)} para a conta do Extra, {Texto(alvo)} dB");
        }
        finally
        {
            _vez.Release();
        }
    }

    // ------------------------------------------------------------------ o mudo guardado

    /// <summary>
    /// O mudo do fiscal guardado no celular: <c>{"formato":1,"mute":[0,null,0,0]}</c> (o mudo de antes de cada canal), ou
    /// <c>"mute": null</c> sem mudo. Ilegível = sem mudo.
    /// </summary>
    private async Task CarregarAsync()
    {
        if (_repositorio is null)
            return;
        try
        {
            if (await Gravacao.LerAsync(_repositorio) is not { } json)
                return;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato != Formato
                || !doc.RootElement.TryGetProperty("mute", out var mute) || mute.ValueKind != JsonValueKind.Array
                || mute.GetArrayLength() != Parametro.Canais)
                return;
            var antes = mute.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d is 0 or 1 ? (decimal?)d : null)
                .ToArray();
            lock (_trava)
                _muteAntes ??= antes;
            _log.Evento("Extra: o mudo do outro player continua no aparelho (de antes de reabrir o app)");
        }
        catch (Exception e)
        {
            _log.Evento($"Extra: o mudo guardado está ilegível ({e.GetType().Name})");
        }
    }

    private async Task GuardarAsync(decimal?[]? antes)
    {
        if (_repositorio is null)
            return;
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            if (antes is null)
            {
                w.WriteNull("mute");
            }
            else
            {
                w.WriteStartArray("mute");
                foreach (var v in antes)
                {
                    if (v is { } d)
                        w.WriteNumberValue(d);
                    else
                        w.WriteNullValue();
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
        if (!await Gravacao.GravarAsync(_repositorio, System.Text.Encoding.UTF8.GetString(fluxo.ToArray())))
            _log.Evento("Extra: não consegui guardar o mudo no celular");
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Escreve pelo escritor, com a origem App. O motivo da falha, ou null.</summary>
    private async Task<string?> EscreverAsync(Parametro parametro, int? indice, decimal valor)
    {
        ResultadoEscrita r;
        using (Origem.Como(OrigemComando.App))
            r = await _escritor.EscreverAsync(parametro, indice, valor, espelhar: false);
        if (!r.Ok)
            return r.Motivo ?? Sessao.MotivoFalhaEscrita;
        return r.Diferente ? $"o aparelho ficou em {r.Valor?.Exibicao ?? "outro valor"}." : null;
    }

    private static string Texto(decimal db) => db.ToString("0.0", Fio);

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do fiscal do Extra ({e.GetType().Name})");
        }
    }

    /// <summary>Fechar o app não tira o mudo que estiver no aparelho: só para de ouvir.</summary>
    public void Dispose()
    {
        lock (_trava)
            _descartado = true;
        _monitor.Clip -= AoClip;
        _outro.Comecou -= AoOutroPlayer;
        _volume.Mudou -= AoMudarVolume;
        _analise.Mudou -= Pedir;
        _setups.Mudou -= Pedir;
        _copia.Mudou -= Pedir;
        _estado.Recarregou -= Pedir;
        _player.Mudou -= Pedir;
        _player.AntesDeTocar = null;
    }
}
