using BgsControle.Nucleo.Leitura;

namespace BgsControle.Nucleo.Escrita;

/// <summary>Onde um valor mora: o código e, quando há, o canal ou a banda.</summary>
public readonly record struct Endereco(string Codigo, int? Indice);

/// <summary>
/// O espelho do estado do aparelho: o valor que as telas mostram. É semeado pelo
/// <see cref="Retrato"/> de cada leitura completa e atualizado a cada eco ou releitura do
/// <see cref="Escritor"/>. Só guarda o que veio do aparelho nesta conexão: ao desconectar,
/// esvazia. Os eventos podem chegar em qualquer thread.
/// </summary>
public sealed class EstadoAparelho
{
    private readonly Sessao _sessao;
    private readonly object _trava = new();
    private readonly Dictionary<Endereco, Valor> _valores = [];
    private bool _pronto;
    private bool _lendo;

    /// <param name="leitura">
    /// A leitura automática: o espelho fica "lendo" enquanto ela roda e é semeado quando ela
    /// termina. Sem ela, quem lê chama <see cref="Semear"/>.
    /// </param>
    public EstadoAparelho(Sessao sessao, LeituraAutomatica? leitura = null)
    {
        _sessao = sessao;
        _sessao.Mudou += AoMudarSessao;
        if (leitura is not null)
        {
            leitura.Comecou += () => DefinirLendo(true);
            leitura.Terminou += r =>
            {
                Semear(r);
                DefinirLendo(false);
            };
            leitura.Falhou += _ => DefinirLendo(false);
        }
    }

    /// <summary>Um valor mudou (eco ou releitura).</summary>
    public event Action<Endereco>? Mudou;

    /// <summary>Muitos valores de uma vez: semeado, esvaziado, começou ou acabou uma leitura.</summary>
    public event Action? Recarregou;

    /// <summary>Já houve uma leitura completa nesta conexão.</summary>
    public bool Pronto
    {
        get
        {
            lock (_trava)
                return _pronto;
        }
    }

    /// <summary>Uma leitura completa está em andamento.</summary>
    public bool Lendo
    {
        get
        {
            lock (_trava)
                return _lendo;
        }
    }

    public Valor? Valor(string codigo, int? indice = null) => Valor(new Endereco(codigo, indice));

    public Valor? Valor(Endereco endereco)
    {
        lock (_trava)
            return _valores.GetValueOrDefault(endereco);
    }

    /// <summary>Uma cópia de todos os valores do espelho agora (vazia antes da primeira leitura).</summary>
    public IReadOnlyDictionary<Endereco, Valor> Copiar()
    {
        lock (_trava)
            return new Dictionary<Endereco, Valor>(_valores);
    }

    /// <summary>Troca o espelho pelos valores lidos no retrato. Ignorado sem a sessão conectada.</summary>
    public void Semear(Retrato retrato)
    {
        lock (_trava)
        {
            if (!SessaoAberta)
                return;
            _valores.Clear();
            foreach (var l in retrato.Lidos)
                _valores[new Endereco(l.Codigo, l.Indice)] = l.Valor;
            _pronto = true;
        }
        Recarregou?.Invoke();
    }

    /// <summary>Um valor confirmado pelo aparelho (eco ou releitura).</summary>
    public void Atualizar(Endereco endereco, Valor valor)
    {
        lock (_trava)
        {
            if (!SessaoAberta)
                return;
            _valores[endereco] = valor;
        }
        Mudou?.Invoke(endereco);
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private void DefinirLendo(bool lendo)
    {
        lock (_trava)
        {
            if (_lendo == lendo)
                return;
            _lendo = lendo;
        }
        Recarregou?.Invoke();
    }

    private void AoMudarSessao()
    {
        lock (_trava)
        {
            if (SessaoAberta || (!_pronto && !_lendo && _valores.Count == 0))
                return;
            // Nunca mostrar valor que não veio do aparelho nesta conexão.
            _valores.Clear();
            _pronto = false;
            _lendo = false;
        }
        Recarregou?.Invoke();
    }
}
