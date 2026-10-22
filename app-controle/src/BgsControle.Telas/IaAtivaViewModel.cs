using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas;

/// <summary>
/// A tela IA ativa (EXPERIENCE, v2 quadro 5): a faixa com o que a IA faz agora, os controles
/// travados ou a IA pausada, o PARAR IA (sempre à mão, sem confirmação), o registro "O que a IA
/// fez" (a mais nova em cima) e o aviso da última mudança da IA. O painel ao vivo são os
/// controles de parâmetro de sempre, que mostram cada mudança da IA pelo espelho.
/// </summary>
public sealed class IaAtivaViewModel : TelaBase, IDisposable
{
    public const string FaixaSemTarefa = "IA mexendo no som";
    public const string FaixaPausada = "IA pausada · retoma após 10 s sem toques";
    public const string MsgControlesTravados = "Controles travados enquanto a IA trabalha";
    public const string TextoPararIa = "PARAR IA";
    public const string PrefixoAgora = "Agora: ";

    private readonly UsoCompartilhado _uso;
    private readonly Historico _historico;

    private bool _ativa;
    private bool _pausada;
    private bool _travado;
    private string _faixa = FaixaSemTarefa;
    private IReadOnlyList<LinhaHistorico> _oQueAIaFez = [];
    private string? _ultimaMudanca;

    public IaAtivaViewModel(UsoCompartilhado uso, Historico historico, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _uso = uso;
        _historico = historico;
        PararIa = new Comando(_uso.PararIa); // o aviso do uso atualiza a tela
        _uso.Mudou += AoMudar;
        _historico.Registrou += AoRegistrar;
        Atualizar();
    }

    /// <summary>PARAR IA: revoga a IA na hora, sem confirmação; nunca fica travado.</summary>
    public Comando PararIa { get; }

    /// <summary>A IA está ativa ou pausada: a tela IA ativa fica aberta.</summary>
    public bool Ativa
    {
        get => _ativa;
        private set => Definir(ref _ativa, value);
    }

    /// <summary>No modo "O toque pausa a IA", o Denis mexeu e a IA espera.</summary>
    public bool Pausada
    {
        get => _pausada;
        private set => Definir(ref _pausada, value);
    }

    /// <summary>Os controles estão travados (modo padrão): só o PARAR IA destrava.</summary>
    public bool Travado
    {
        get => _travado;
        private set
        {
            if (Definir(ref _travado, value))
                Avisar(nameof(AvisoControles));
        }
    }

    /// <summary>"Controles travados enquanto a IA trabalha", ou null quando os controles estão livres.</summary>
    public string? AvisoControles => Travado ? MsgControlesTravados : null;

    /// <summary>A faixa em uma linha: "IA ajustando o EQ da FILME", ou a pausa.</summary>
    public string Faixa
    {
        get => _faixa;
        private set
        {
            if (_faixa == value)
                return;
            _faixa = value;
            Avisar();
        }
    }

    /// <summary>"O que a IA fez" desde que ela ficou ativa, da mais nova à mais antiga.</summary>
    public IReadOnlyList<LinhaHistorico> OQueAIaFez
    {
        get => _oQueAIaFez;
        private set
        {
            _oQueAIaFez = value;
            Avisar();
        }
    }

    /// <summary>O aviso da última mudança da IA ("Agora: Ganho · saída 3 · +2,0 dB"), ou null.</summary>
    public string? UltimaMudanca
    {
        get => _ultimaMudanca;
        private set
        {
            if (_ultimaMudanca == value)
                return;
            _ultimaMudanca = value;
            Avisar();
        }
    }

    private void AoMudar() => NaTela(Atualizar);

    private void AoRegistrar(EntradaHistorico e)
    {
        if (e.Origem == OrigemComando.Ia)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        var estado = _uso.Estado;
        Ativa = estado != EstadoIa.Inativa;
        Pausada = estado == EstadoIa.Pausada;
        Travado = _uso.Travado;
        Faixa = Pausada ? FaixaPausada : _uso.Tarefa ?? FaixaSemTarefa;

        var desde = _uso.AtivaDesde;
        var daIa = desde is { } d
            ? _historico.Entradas.Where(e => e.Origem == OrigemComando.Ia && e.Hora >= d).ToArray()
            : [];
        OQueAIaFez = daIa.Select(LinhaHistorico.De).ToArray();
        UltimaMudanca = daIa.FirstOrDefault(e => e.Enviado) is { } ultima ? PrefixoAgora + FraseComando.De(ultima) : null;
    }

    private bool Definir(ref bool campo, bool valor, [System.Runtime.CompilerServices.CallerMemberName] string? nome = null)
    {
        if (campo == valor)
            return false;
        campo = valor;
        Avisar(nome);
        return true;
    }

    public void Dispose()
    {
        _uso.Mudou -= AoMudar;
        _historico.Registrou -= AoRegistrar;
    }
}
