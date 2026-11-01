using BgsControle.Nucleo.Player;

namespace BgsControle.Simulador;

/// <summary>
/// O dublê do decodificador da análise da playlist (história 13d): faixas sintéticas de picos conhecidos, em estéreo a
/// 48 kHz, entregues em blocos. Senos, senos com L e R diferentes, rajadas sobre um fundo baixo e arquivos sumidos. Uma
/// faixa pode ser segurada depois do primeiro bloco (a análise fica "em curso") até o teste soltar. Conta as leituras.
/// </summary>
public sealed class DecodificadorSintetico : IDecodificadorAudio
{
    public const int Taxa = 48000;

    /// <summary>Quadros por bloco entregue.</summary>
    public const int Quadros = 4096;

    private readonly object _trava = new();
    private readonly Dictionary<string, Func<float[]>> _faixas = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ausentes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _seguradas = new(StringComparer.Ordinal);
    private readonly List<string> _lidas = [];

    /// <summary>As URIs lidas (do começo), na ordem.</summary>
    public IReadOnlyList<string> Lidas
    {
        get
        {
            lock (_trava)
                return _lidas.ToArray();
        }
    }

    /// <summary>Um seno em L e R com o pico em dBFS.</summary>
    public void Seno(string uri, double picoDbfs, double frequencia = 1000, double segundos = 2) =>
        Estereo(uri, picoDbfs, picoDbfs, frequencia, segundos);

    /// <summary>A subida e a descida dos senos, em segundos (sem o degrau, um passa-alta daria um repique acima do pico).</summary>
    public const double Rampa = 0.1;

    /// <summary>
    /// Um seno com o pico de L e o de R em dBFS (o mesmo seno, em fase), com a subida e a descida em cosseno de
    /// <see cref="Rampa"/> s.
    /// </summary>
    public void Estereo(string uri, double picoL, double picoR, double frequencia = 1000, double segundos = 2)
    {
        var al = Math.Pow(10, picoL / 20);
        var ar = Math.Pow(10, picoR / 20);
        Definir(uri, () =>
        {
            var n = (int)(segundos * Taxa);
            var pcm = new float[n * 2];
            var rampa = (int)(Rampa * Taxa);
            for (var i = 0; i < n; i++)
            {
                var borda = Math.Min(i, n - 1 - i);
                var envelope = borda >= rampa ? 1 : 0.5 - 0.5 * Math.Cos(Math.PI * borda / rampa);
                var s = envelope * Math.Sin(2 * Math.PI * frequencia * i / Taxa);
                pcm[2 * i] = (float)(al * s);
                pcm[2 * i + 1] = (float)(ar * s);
            }
            return pcm;
        });
    }

    /// <summary>
    /// A soma de senos (frequência, pico em dBFS), em L e R, com a subida e a descida de <see cref="Rampa"/> s: o pico da
    /// soma depende de como as fases se encontram, não de cada tom.
    /// </summary>
    public void Tons(string uri, IReadOnlyList<(double Frequencia, double PicoDbfs)> tons, double segundos = 2) =>
        Definir(uri, () =>
        {
            var n = (int)(segundos * Taxa);
            var rampa = (int)(Rampa * Taxa);
            var pcm = new float[n * 2];
            for (var i = 0; i < n; i++)
            {
                var borda = Math.Min(i, n - 1 - i);
                var envelope = borda >= rampa ? 1 : 0.5 - 0.5 * Math.Cos(Math.PI * borda / rampa);
                var s = tons.Sum(t => Math.Pow(10, t.PicoDbfs / 20) * Math.Sin(2 * Math.PI * t.Frequencia * i / Taxa));
                pcm[2 * i] = (float)(envelope * s);
                pcm[2 * i + 1] = (float)(envelope * s);
            }
            return pcm;
        });

    /// <summary>Um fundo de 1 kHz em <paramref name="fundoDbfs"/> com rajadas de 50 ms em <paramref name="picoDbfs"/> nos instantes (s).</summary>
    public void Rajadas(string uri, double segundos, IReadOnlyList<double> instantes, double picoDbfs = -1, double fundoDbfs = -40)
    {
        var fundo = Math.Pow(10, fundoDbfs / 20);
        var pico = Math.Pow(10, picoDbfs / 20);
        Definir(uri, () =>
        {
            var n = (int)(segundos * Taxa);
            var pcm = new float[n * 2];
            for (var i = 0; i < n; i++)
            {
                var t = i / (double)Taxa;
                var a = instantes.Any(x => t >= x && t < x + 0.05) ? pico : fundo;
                var s = (float)(a * Math.Sin(2 * Math.PI * 1000 * t));
                pcm[2 * i] = s;
                pcm[2 * i + 1] = s;
            }
            return pcm;
        });
    }

    /// <summary>O arquivo sumiu: a leitura falha com "o arquivo não foi encontrado".</summary>
    public void Ausente(string uri)
    {
        lock (_trava)
            _ausentes.Add(uri);
    }

    /// <summary>A leitura da URI para depois do primeiro bloco até <see cref="Soltar"/>.</summary>
    public void Segurar(string uri)
    {
        lock (_trava)
            _seguradas[uri] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Soltar(string uri)
    {
        TaskCompletionSource? s;
        lock (_trava)
        {
            if (!_seguradas.Remove(uri, out s))
                return;
        }
        s.TrySetResult();
    }

    private void Definir(string uri, Func<float[]> gerar)
    {
        lock (_trava)
            _faixas[uri] = gerar;
    }

    public async Task LerAsync(string uri, Action<float[], int, int> bloco, CancellationToken cancelar)
    {
        Func<float[]>? gerar;
        TaskCompletionSource? segurada;
        lock (_trava)
        {
            _lidas.Add(uri);
            if (_ausentes.Contains(uri))
                throw new ErroDecodificacao(ErroDecodificacao.MsgArquivo);
            _faixas.TryGetValue(uri, out gerar);
            _seguradas.TryGetValue(uri, out segurada);
        }
        if (gerar is null)
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        await Task.Yield();
        var pcm = gerar();
        var primeiro = true;
        for (var i = 0; i < pcm.Length; i += Quadros * 2)
        {
            cancelar.ThrowIfCancellationRequested();
            var parte = pcm.AsSpan(i, Math.Min(Quadros * 2, pcm.Length - i)).ToArray();
            bloco(parte, 2, Taxa);
            if (primeiro && segurada is not null)
                await segurada.Task.WaitAsync(cancelar);
            primeiro = false;
        }
    }
}
