namespace BgsControle.Nucleo.Escrita;

/// <summary>
/// Envia pela porta da sessão (<see cref="Sessao.EnviarAsync"/>), no ritmo de
/// <see cref="TemposEscrita.Intervalo"/> entre os envios desta instância, e espera a resposta
/// que casa. A espera existe antes do envio: a resposta pode chegar antes de a escrita voltar.
/// Várias esperas podem estar abertas ao mesmo tempo (cada quadro recebido vai a todas); se a
/// sessão sai de Conectado, todas acabam sem resposta. Não pega a exclusão de leitura e escrita
/// da sessão: quem precisa dela (uma escrita com a sua confirmação) pega antes.
/// <para>
/// Em classe própria: o gerador usa agora, os comandos de aparelho inteiro (história 7b) depois.
/// </para>
/// </summary>
public sealed class EnvioComResposta
{
    private readonly Sessao _sessao;
    private readonly TimeProvider _relogio;
    private readonly object _trava = new();
    private readonly List<Espera> _esperas = [];
    private readonly SemaphoreSlim _ritmo = new(1, 1);
    private DateTimeOffset? _ultimoEnvio;

    public EnvioComResposta(Sessao sessao, TemposEscrita? tempos = null, TimeProvider? relogio = null)
    {
        _sessao = sessao;
        Tempos = tempos ?? TemposEscrita.Padrao;
        _relogio = relogio ?? TimeProvider.System;
        _sessao.Recebeu += AoReceber;
        _sessao.Mudou += AoMudarSessao;
    }

    public TemposEscrita Tempos { get; }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    /// <summary>
    /// Abre uma espera: a primeira resposta para a qual <paramref name="casar"/> devolve um texto
    /// não nulo. Abra antes de enviar; feche (Dispose) depois.
    /// </summary>
    public Espera Esperar(Func<string, string?> casar)
    {
        var espera = new Espera(this, casar);
        lock (_trava)
            _esperas.Add(espera);
        if (!SessaoAberta)
            espera.Acabar(null);
        return espera;
    }

    /// <summary>Envia pela sessão, no ritmo, sem esperar resposta.</summary>
    public async Task<ResultadoEnvio> EnviarAsync(string quadro)
    {
        // Um envio de cada vez (só o intervalo, o envio e a hora dele; nunca a espera da
        // resposta): dois envios ao mesmo tempo não leem o mesmo último envio e saem colados.
        await _ritmo.WaitAsync();
        try
        {
            var espera = TimeSpan.Zero;
            lock (_trava)
            {
                if (_ultimoEnvio is { } ultimo)
                    espera = ultimo + Tempos.Intervalo - _relogio.GetUtcNow();
            }
            if (espera > TimeSpan.Zero)
                await Task.Delay(espera, _relogio);

            var envio = await _sessao.EnviarAsync(quadro);
            if (envio.Enviado)
            {
                lock (_trava)
                    _ultimoEnvio = _relogio.GetUtcNow();
            }
            return envio;
        }
        finally
        {
            _ritmo.Release();
        }
    }

    /// <summary>
    /// Envia e espera a resposta que casa, até <paramref name="prazo"/>. Resposta null = não
    /// chegou no prazo, ou a sessão saiu de Conectado.
    /// </summary>
    public async Task<(ResultadoEnvio Envio, string? Resposta)> EnviarEEsperarAsync(string quadro, Func<string, string?> casar, TimeSpan prazo)
    {
        using var espera = Esperar(casar);
        var envio = await EnviarAsync(quadro);
        if (!envio.Enviado)
            return (envio, null);
        return (envio, await espera.AguardarAsync(prazo));
    }

    private void AoReceber(string texto)
    {
        Espera[] esperas;
        lock (_trava)
            esperas = _esperas.ToArray();
        foreach (var e in esperas)
        {
            if (e.Casar(texto) is { } resposta)
                e.Acabar(resposta);
        }
    }

    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        Espera[] esperas;
        lock (_trava)
            esperas = _esperas.ToArray();
        foreach (var e in esperas)
            e.Acabar(null);
    }

    private void Fechar(Espera espera)
    {
        lock (_trava)
            _esperas.Remove(espera);
    }

    /// <summary>Uma espera aberta por <see cref="Esperar"/>.</summary>
    public sealed class Espera : IDisposable
    {
        private readonly EnvioComResposta _dono;
        private readonly TaskCompletionSource<string?> _resposta = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Espera(EnvioComResposta dono, Func<string, string?> casar)
        {
            _dono = dono;
            Casar = casar;
        }

        internal Func<string, string?> Casar { get; }

        internal void Acabar(string? resposta) => _resposta.TrySetResult(resposta);

        /// <summary>A resposta que casou, ou null se não chegou no prazo ou se a sessão saiu de Conectado.</summary>
        public async Task<string?> AguardarAsync(TimeSpan prazo)
        {
            if (!_resposta.Task.IsCompleted)
            {
                using var cancelar = new CancellationTokenSource();
                var limite = Task.Delay(prazo, _dono._relogio, cancelar.Token);
                await Task.WhenAny(_resposta.Task, limite);
                cancelar.Cancel();
            }
            return _resposta.Task.IsCompleted ? await _resposta.Task : null;
        }

        public void Dispose()
        {
            _resposta.TrySetResult(null);
            _dono.Fechar(this);
        }
    }
}
