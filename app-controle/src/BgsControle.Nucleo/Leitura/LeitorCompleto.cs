using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Leitura;

/// <summary>Ritmo da leitura (MAPA §2). Os testes usam tempos curtos.</summary>
public sealed record TemposLeitura(TimeSpan Intervalo, TimeSpan Prazo)
{
    /// <summary>~30 ms entre envios e 5 s de prazo por consulta.</summary>
    public static TemposLeitura Padrao { get; } = new(TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(5));
}

/// <summary>
/// A leitura completa: pergunta cada consulta do catálogo, uma por vez, pela porta da sessão
/// (<see cref="Sessao.EnviarAsync"/>), e monta o <see cref="Retrato"/>. A resposta casa pelo
/// código e pelo canal ou banda; o eco do envio e o que não casa vão para os órfãos. Uma
/// consulta do Marco 1 que falha ganha uma nova tentativa; um extra é perguntado uma vez.
/// Se a conexão cair, a leitura para e o retrato fica incompleto.
/// </summary>
public sealed class LeitorCompleto
{
    public const string MotivoSemResposta = "sem resposta";
    public const string MsgDesconectado = "A leitura parou: o app desconectou.";
    public const string MsgSemConexao = "Sem conexão autenticada.";
    public const string MsgCancelada = "A leitura foi cancelada.";
    public const string MsgParouDeResponder = "O aparelho parou de responder.";

    /// <summary>
    /// Consultas do Marco 1 seguidas sem resposta (já com a nova tentativa) que param a
    /// leitura, como no protótipo. Os extras não contam: o aparelho pode nunca responder a eles.
    /// </summary>
    public const int MaximoMudasSeguidas = 8;

    private const int TentativasMarco1 = 2;
    private const int TentativasExtra = 1;

    private readonly Sessao _sessao;
    private readonly LogBle _log;
    private readonly TemposLeitura _tempos;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private int _lendo;
    private Pendente? _pendente;
    private List<string>? _orfaos;
    private TaskCompletionSource? _caiu;

    public LeitorCompleto(Sessao sessao, LogBle log, TemposLeitura? tempos = null, TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _log = log;
        _tempos = tempos ?? TemposLeitura.Padrao;
        _relogio = relogio ?? TimeProvider.System;
    }

    /// <summary>Um retrato parcial depois de cada consulta. Pode chegar em qualquer thread.</summary>
    public event Action<Retrato>? Avancou;

    public bool Lendo => Volatile.Read(ref _lendo) == 1;

    /// <summary>Lê o aparelho inteiro. Só uma leitura por vez.</summary>
    public async Task<Retrato> LerAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _lendo, 1) == 1)
            throw new InvalidOperationException("Já há uma leitura em andamento.");

        var inicio = _relogio.GetLocalNow();
        // Guardado já: depois de uma queda a sessão apaga o firmware.
        var firmware = _sessao.Firmware;
        var lidos = new List<ItemLido>();
        var falhas = new List<Falha>();
        DateTimeOffset? fimMarco1 = null;
        var caiu = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_trava)
        {
            _orfaos = [];
            _caiu = caiu;
            _pendente = null;
        }
        _sessao.Recebeu += AoReceber;
        _sessao.Mudou += AoMudar;

        Retrato Retratar(bool terminou, bool completo, string? motivo)
        {
            string[] orfaos;
            lock (_trava)
                orfaos = _orfaos!.ToArray();
            return new Retrato
            {
                Firmware = firmware,
                Inicio = inicio,
                FimMarco1 = fimMarco1,
                Fim = terminou ? _relogio.GetLocalNow() : null,
                Completo = completo,
                Motivo = motivo,
                Lidos = lidos.ToArray(),
                Falhas = falhas.ToArray(),
                Orfaos = orfaos,
            };
        }

        Retrato Parar(string motivo)
        {
            fimMarco1 ??= lidos.Count + falhas.Count >= Catalogo.TotalMarco1 ? _relogio.GetLocalNow() : null;
            var r = Retratar(terminou: true, completo: false, motivo);
            _log.Evento($"leitura incompleta: {motivo} (Marco 1 {r.PlacarMarco1.Texto}; extras {r.PlacarExtras.Texto})");
            return r;
        }

        var comPorta = false;
        try
        {
            // Espera a escrita em andamento terminar (eco ou releitura) antes de perguntar.
            await _sessao.ExclusaoLeituraEscrita.WaitAsync(ct);
            comPorta = true;
            if (!SessaoAberta)
                return Parar(_sessao.Mensagem ?? MsgSemConexao);
            _log.Evento($"leitura completa: {Catalogo.TotalMarco1} consultas do Marco 1 e {Catalogo.TotalExtras} extras");

            var primeira = true;
            var mudasSeguidas = 0;
            foreach (var consulta in Catalogo.Consultas)
            {
                if (consulta.Extra && fimMarco1 is null)
                    fimMarco1 = _relogio.GetLocalNow();

                var tentativas = consulta.Extra ? TentativasExtra : TentativasMarco1;
                Tentativa? falhou = null;
                for (var t = 1; t <= tentativas; t++)
                {
                    if (!primeira)
                        await Task.Delay(_tempos.Intervalo, ct);
                    primeira = false;

                    var r = await ConsultarAsync(consulta, caiu.Task, ct);
                    if (r.Caiu)
                        return Parar(_sessao.Mensagem ?? MsgDesconectado);
                    if (r.Valor is not null)
                    {
                        lidos.Add(new ItemLido(consulta.Parametro.Codigo, consulta.Parametro.Nome, consulta.Indice,
                            consulta.Onde, consulta.Extra, consulta.Texto, r.Cru!, r.Valor, t));
                        falhou = null;
                        break;
                    }
                    falhou = r;
                    _log.Evento(t < tentativas
                        ? $"{consulta.Texto}: {r.Motivo}; nova tentativa"
                        : $"{consulta.Texto}: {r.Motivo}");
                }
                if (falhou is not null)
                {
                    falhas.Add(new Falha(consulta.Parametro.Codigo, consulta.Parametro.Nome, consulta.Indice,
                        consulta.Onde, consulta.Extra, consulta.Texto, falhou.Motivo!, falhou.Cru, tentativas));
                }
                Avancou?.Invoke(Retratar(terminou: false, completo: false, null));

                if (!consulta.Extra)
                {
                    mudasSeguidas = falhou?.Motivo == MotivoSemResposta ? mudasSeguidas + 1 : 0;
                    if (mudasSeguidas >= MaximoMudasSeguidas)
                        return Parar(MsgParouDeResponder);
                }
            }

            fimMarco1 ??= _relogio.GetLocalNow();
            var final = Retratar(terminou: true, completo: true, null);
            _log.Evento($"leitura completa: Marco 1 {final.PlacarMarco1.Texto}; extras {final.PlacarExtras.Texto}; " +
                        $"{final.Falhas.Count} falhas; {final.Orfaos.Count} órfãos");
            return final;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Parar(MsgCancelada);
        }
        finally
        {
            if (comPorta)
                _sessao.ExclusaoLeituraEscrita.Release();
            _sessao.Recebeu -= AoReceber;
            _sessao.Mudou -= AoMudar;
            lock (_trava)
            {
                _pendente = null;
                _caiu = null;
                _orfaos = null;
            }
            Volatile.Write(ref _lendo, 0);
        }
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private async Task<Tentativa> ConsultarAsync(Consulta consulta, Task caiu, CancellationToken ct)
    {
        // A pendente existe antes do envio: a resposta pode chegar antes de a escrita voltar.
        var pendente = new Pendente(consulta);
        lock (_trava)
            _pendente = pendente;
        try
        {
            var envio = await _sessao.EnviarAsync(consulta.Texto);
            if (!envio.Enviado)
            {
                if (!SessaoAberta || caiu.IsCompleted)
                    return Tentativa.DeQueda;
                return new Tentativa(null, null, envio.Motivo ?? "não enviado");
            }

            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var prazo = Task.Delay(_tempos.Prazo, espera.Token);
            await Task.WhenAny(pendente.Resposta.Task, prazo, caiu);
            espera.Cancel();

            if (pendente.Resposta.Task.IsCompleted)
            {
                var (cru, noFio) = await pendente.Resposta.Task;
                var d = consulta.Parametro.Decodificar(noFio);
                return d.Valor is not null ? new Tentativa(d.Valor, cru, null) : new Tentativa(null, cru, d.Motivo);
            }
            if (caiu.IsCompleted)
                return Tentativa.DeQueda;
            ct.ThrowIfCancellationRequested();
            return new Tentativa(null, null, MotivoSemResposta);
        }
        finally
        {
            lock (_trava)
            {
                if (_pendente == pendente)
                    _pendente = null;
            }
        }
    }

    private void AoReceber(string texto)
    {
        lock (_trava)
        {
            if (_orfaos is null)
                return;
            var p = _pendente;
            if (p is not null && !p.Resposta.Task.IsCompleted && Casar(p.Consulta, texto) is { } noFio)
            {
                p.Resposta.TrySetResult((texto, noFio));
                return;
            }
            _orfaos.Add(texto);
        }
    }

    private void AoMudar()
    {
        if (SessaoAberta)
            return;
        TaskCompletionSource? caiu;
        lock (_trava)
            caiu = _caiu;
        caiu?.TrySetResult();
    }

    /// <summary>
    /// O texto do valor, se o quadro responde à consulta; senão null. Marco 1: o mesmo código
    /// e o número exato de segmentos do escopo (<c>#080#v#</c>, <c>#081#c#v#</c>,
    /// <c>020#b#v</c>), com o canal ou a banda iguais. Extra: o mesmo código e qualquer
    /// conteúdo, para guardar o cru seja o que vier, menos o eco do envio.
    /// </summary>
    internal static string? Casar(Consulta consulta, string texto)
    {
        var q = Quadros.Analisar(texto);
        var p = consulta.Parametro;
        if (q is null || q.Codigo != p.Codigo)
            return null;
        if (texto.Trim() == consulta.Texto)
            return null; // eco do envio

        if (p.Extra && p.Escopo == Escopo.Global)
            return string.Join("#", q.Segmentos); // os segmentos como vieram (#CCC## = vazio: "")

        if (p.Escopo == Escopo.Global)
            return q.Segmentos.Count == 1 ? q.Segmentos[0] : null;

        if (q.Segmentos.Count != 2 || !int.TryParse(q.Segmentos[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var indice) || indice != consulta.Indice)
            return null;
        return q.Segmentos[1];
    }

    private sealed class Pendente(Consulta consulta)
    {
        public Consulta Consulta { get; } = consulta;

        public TaskCompletionSource<(string Cru, string NoFio)> Resposta { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record Tentativa(Valor? Valor, string? Cru, string? Motivo, bool Caiu = false)
    {
        public static Tentativa DeQueda { get; } = new(null, null, null, Caiu: true);
    }
}
