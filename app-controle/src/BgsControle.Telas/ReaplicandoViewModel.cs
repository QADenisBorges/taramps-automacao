using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// A tela toda "REAPLICANDO" (failure-modes, regra 5; decisão do Denis, 30/10/2026): ao conectar,
/// com só ganho, limitador, EQ ou MASTER diferentes da memória ativa, o app reaplica sozinho a
/// memória e o preset dela, sem mutar. Enquanto escreve, a casca mostra por cima de tudo o
/// "REAPLICANDO", o nome da memória, o do preset (se houver) e "Mantenha o celular perto do
/// processador."; a tela fecha ao terminar, com sucesso ou não.
/// </summary>
public sealed class ReaplicandoViewModel : TelaBase, IDisposable
{
    public const string TextoTitulo = "REAPLICANDO";
    public const string TextoInstrucao = "Mantenha o celular perto do processador.";

    private readonly GerenteSetups _setups;
    private Reaplicacao? _atual;

    public ReaplicandoViewModel(GerenteSetups setups, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _setups.Mudou += AoMudar;
        Atualizar();
    }

    /// <summary>A reaplicação anda: a tela toda está por cima.</summary>
    public bool Ativa => _atual is not null;

    /// <summary>"REAPLICANDO".</summary>
    public string Titulo => TextoTitulo;

    /// <summary>O nome da memória reaplicada ("FILME"), ou null.</summary>
    public string? Memoria => _atual?.Memoria;

    /// <summary>O nome do preset que ela lembra ("Cinema"), ou null (sem preset).</summary>
    public string? Preset => _atual?.Preset;

    /// <summary>"Mantenha o celular perto do processador."</summary>
    public string Instrucao => TextoInstrucao;

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        var atual = _setups.Reaplicando;
        if (Equals(atual, _atual))
            return;
        _atual = atual;
        Avisar(nameof(Ativa));
        Avisar(nameof(Memoria));
        Avisar(nameof(Preset));
    }

    public void Dispose() => _setups.Mudou -= AoMudar;
}
