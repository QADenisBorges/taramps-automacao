using BgsControle.Nucleo.Leitura;

namespace BgsControle.Nucleo.Aparelho;

/// <summary>
/// A monitoração ao vivo (CAP-7): ouve o que o aparelho manda sozinho pela sessão e guarda o
/// voltímetro e o clip. Não pergunta nada (o <c>#005#</c> responde <c>ERRO 0</c>, medido): só
/// ouve, e a leitura completa semeia os três valores.
/// <list type="bullet">
/// <item><c>#005#v#</c>: o voltímetro em V × 10. Suposição VOLTIMETRO-ESPONTANEO: o aparelho
/// manda o 005 sozinho, neste formato.</item>
/// <item><c>#150#bits#</c>: o clip das saídas, um bit por saída (bit 0 = saída 1).</item>
/// <item><c>#111#v#</c>: o clip da entrada (≠0 = clip). Suposição CLIP-ENTRADA: o 111 chega
/// sozinho, no formato da leitura.</item>
/// </list>
/// Um clip fica aceso por <see cref="ClipAceso"/> (3 s) depois do último aviso com o seu bit.
/// Ao desconectar, o voltímetro volta a "sem valor" e os clips apagam. Crie cedo (antes da
/// primeira conexão): ele se pendura na sessão e na leitura automática.
/// </summary>
public sealed class MonitorAparelho
{
    /// <summary>Quanto um clip fica aceso depois do último aviso com o seu bit (EXPERIENCE, faixa de monitoração).</summary>
    public static readonly TimeSpan ClipAceso = TimeSpan.FromSeconds(3);

    /// <summary>As saídas do clip (1 a 4 na tela).</summary>
    public const int Saidas = Parametro.Canais;

    private static readonly Consulta Consulta005 = Catalogo.ConsultaDe("#005#")!;
    private static readonly Consulta Consulta150 = Catalogo.ConsultaDe("#150#")!;
    private static readonly Consulta Consulta111 = Catalogo.ConsultaDe("#111#")!;

    private readonly Sessao _sessao;
    private readonly TimeProvider _relogio;
    private readonly object _trava = new();

    // Índices 0 a 3: as saídas; 4: a entrada.
    private readonly DateTimeOffset?[] _acesoAte = new DateTimeOffset?[Saidas + 1];
    private decimal? _decivolts;
    private ITimer? _apagar;

    public MonitorAparelho(Sessao sessao, LeituraAutomatica leitura, TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _relogio = relogio ?? TimeProvider.System;
        _sessao.Recebeu += AoReceber;
        _sessao.Mudou += AoMudarSessao;
        leitura.Terminou += AoTerminarLeitura;
    }

    /// <summary>O voltímetro, o clip de uma saída ou o da entrada mudou (acendeu ou apagou). Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>O voltímetro em volts (12,4), ou null sem valor.</summary>
    public decimal? Volts
    {
        get
        {
            lock (_trava)
                return _decivolts is { } d ? d / 10m : null;
        }
    }

    /// <summary>O clip da saída <paramref name="saida"/> (1 a 4) está aceso.</summary>
    public bool ClipSaida(int saida)
    {
        if (saida is < 1 or > Saidas)
            throw new ArgumentOutOfRangeException(nameof(saida), "Saída de 1 a 4.");
        return Aceso(saida - 1);
    }

    /// <summary>O clip da entrada está aceso.</summary>
    public bool ClipEntrada => Aceso(Saidas);

    private bool Aceso(int i)
    {
        lock (_trava)
            return _acesoAte[i] is { } ate && ate > _relogio.GetUtcNow();
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    // ------------------------------------------------------------------ entrada

    private void AoReceber(string texto)
    {
        if (texto.TrimStart().StartsWith('*'))
            return; // a forma de escrita não é um aviso do aparelho
        if (LeitorCompleto.Casar(Consulta005, texto) is { } volts)
            Voltimetro(volts);
        else if (LeitorCompleto.Casar(Consulta150, texto) is { } bits)
            ClipDasSaidas(bits);
        else if (LeitorCompleto.Casar(Consulta111, texto) is { } entrada)
            ClipDaEntrada(entrada);
    }

    /// <summary>A leitura completa semeia os três valores (os que vieram legíveis).</summary>
    private void AoTerminarLeitura(Retrato retrato)
    {
        if (!SessaoAberta)
            return;
        if (retrato.Item(Consulta005.Parametro.Codigo)?.Valor.NoFio is { } volts)
            Voltimetro(volts);
        if (retrato.Item(Consulta150.Parametro.Codigo)?.Valor.NoFio is { } bits)
            ClipDasSaidas(bits);
        if (retrato.Item(Consulta111.Parametro.Codigo)?.Valor.NoFio is { } entrada)
            ClipDaEntrada(entrada);
    }

    private void Voltimetro(string noFio)
    {
        var v = Consulta005.Parametro.Decodificar(noFio).Valor;
        if (v?.Numero is not { } n || v.ForaDaFaixa || n < 0)
            return; // ilegível: fica o último valor
        lock (_trava)
        {
            if (!SessaoAberta || _decivolts == n)
                return;
            _decivolts = n;
        }
        Notificar();
    }

    private void ClipDasSaidas(string noFio)
    {
        var n = Consulta150.Parametro.Decodificar(noFio).Valor?.Numero;
        if (n is not { } bits || bits != decimal.Truncate(bits) || bits is < 0 or > 15)
            return;
        var acender = Enumerable.Range(0, Saidas).Where(c => ((int)bits & (1 << c)) != 0).ToArray();
        Acender(acender);
    }

    private void ClipDaEntrada(string noFio)
    {
        if (Consulta111.Parametro.Decodificar(noFio).Valor is { Ligado: true })
            Acender([Saidas]);
    }

    // ------------------------------------------------------------------ clip

    private void Acender(int[] indices)
    {
        if (indices.Length == 0)
            return;
        lock (_trava)
        {
            if (!SessaoAberta)
                return;
            var ate = _relogio.GetUtcNow() + ClipAceso;
            foreach (var i in indices)
                _acesoAte[i] = ate;
            ReprogramarApagar();
        }
        Notificar();
    }

    /// <summary>Um só relógio, no próximo clip a apagar. Chamar com a trava.</summary>
    private void ReprogramarApagar()
    {
        _apagar?.Dispose();
        _apagar = null;
        var agora = _relogio.GetUtcNow();
        var proximo = _acesoAte.Where(a => a is { } ate && ate > agora).Min();
        if (proximo is { } quando)
            _apagar = _relogio.CreateTimer(_ => AoApagar(), null, quando - agora, Timeout.InfiniteTimeSpan);
    }

    private void AoApagar()
    {
        lock (_trava)
        {
            var agora = _relogio.GetUtcNow();
            for (var i = 0; i < _acesoAte.Length; i++)
            {
                if (_acesoAte[i] is { } ate && ate <= agora)
                    _acesoAte[i] = null;
            }
            ReprogramarApagar();
        }
        Notificar();
    }

    // ------------------------------------------------------------------ queda

    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        lock (_trava)
        {
            if (_decivolts is null && _acesoAte.All(a => a is null))
                return;
            // Nunca mostrar valor que não veio do aparelho nesta conexão.
            _decivolts = null;
            Array.Clear(_acesoAte);
            _apagar?.Dispose();
            _apagar = null;
        }
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception)
        {
            // Quem ouve não pode parar as notificações da sessão nem o relógio do clip.
        }
    }
}
