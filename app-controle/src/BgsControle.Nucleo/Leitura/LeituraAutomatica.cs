namespace BgsControle.Nucleo.Leitura;

/// <summary>
/// A regra "ler uma vez ao entrar em Conectado": a cada entrada da sessão em Conectado, dispara
/// <see cref="LeitorCompleto.LerAsync"/> uma vez. Não dispara de novo enquanto a sessão continua
/// em Conectado. Se a entrada chega enquanto a leitura anterior ainda não acabou, a leitura
/// automática fica pendente e começa assim que a anterior acaba (se a sessão ainda estiver
/// conectada). O "Ler de novo" da tela passa por
/// <see cref="LerAsync"/>, e as duas leituras avisam pelos mesmos eventos: quem mostra só
/// observa <see cref="Comecou"/>, <see cref="Avancou"/>, <see cref="Terminou"/> e
/// <see cref="Falhou"/>. Os eventos podem chegar em qualquer thread.
/// </summary>
public sealed class LeituraAutomatica
{
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;
    private readonly object _trava = new();
    private bool _conectadoAntes;
    private bool _lendo;
    private bool _pendente; // uma entrada em Conectado chegou durante outra leitura
    private int _iniciadas;

    public LeituraAutomatica(Sessao sessao, LeitorCompleto leitor)
    {
        _sessao = sessao;
        _leitor = leitor;
        _sessao.Mudou += AoMudar;
        _leitor.Avancou += AoAvancar;
    }

    /// <summary>Uma leitura começou (automática ou pedida).</summary>
    public event Action? Comecou;

    /// <summary>Um retrato parcial da leitura em andamento.</summary>
    public event Action<Retrato>? Avancou;

    /// <summary>A leitura terminou (completa ou não) com este retrato.</summary>
    public event Action<Retrato>? Terminou;

    /// <summary>A leitura lançou uma exceção inesperada.</summary>
    public event Action<Exception>? Falhou;

    public bool Lendo
    {
        get
        {
            lock (_trava)
                return _lendo;
        }
    }

    /// <summary>Quantas leituras começaram desde a criação (automáticas e pedidas).</summary>
    public int Iniciadas
    {
        get
        {
            lock (_trava)
                return _iniciadas;
        }
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    /// <summary>
    /// Lê o aparelho agora ("Ler de novo"). Devolve null sem ler se já há uma leitura ou se a
    /// sessão não está conectada; senão, o retrato (também entregue por <see cref="Terminou"/>).
    /// </summary>
    public Task<Retrato?> LerAsync()
    {
        lock (_trava)
        {
            if (_lendo || _leitor.Lendo || !SessaoAberta)
                return Task.FromResult<Retrato?>(null);
            _lendo = true;
            _iniciadas++;
        }
        Comecou?.Invoke();
        // Fora da thread de quem chamou: as respostas chegam pelas notificações.
        return Task.Run(ExecutarAsync);
    }

    private async Task<Retrato?> ExecutarAsync()
    {
        Retrato retrato;
        try
        {
            retrato = await _leitor.LerAsync();
        }
        catch (Exception e)
        {
            // O fim é avisado antes de soltar: o Comecou de uma leitura nova vem sempre depois.
            try
            {
                Falhou?.Invoke(e);
            }
            finally
            {
                Soltar();
            }
            return null;
        }
        try
        {
            Terminou?.Invoke(retrato);
        }
        finally
        {
            Soltar();
        }
        return retrato;
    }

    /// <summary>Acaba a leitura e começa a pendente, se a sessão ainda está conectada.</summary>
    private void Soltar()
    {
        bool iniciar;
        lock (_trava)
        {
            _lendo = false;
            iniciar = _pendente && SessaoAberta;
            _pendente = false;
        }
        if (iniciar)
            _ = LerAsync();
    }

    private void AoMudar()
    {
        bool disparar;
        lock (_trava)
        {
            var conectado = SessaoAberta;
            disparar = conectado && !_conectadoAntes;
            _conectadoAntes = conectado;
            if (!conectado)
                _pendente = false;
            else if (disparar && _lendo)
            {
                // A leitura anterior ainda não acabou: esta entrada lê quando ela acabar.
                _pendente = true;
                disparar = false;
            }
        }
        if (disparar)
            _ = LerAsync();
    }

    private void AoAvancar(Retrato r)
    {
        bool minha;
        lock (_trava)
            minha = _lendo;
        if (minha)
            Avancou?.Invoke(r);
    }
}
