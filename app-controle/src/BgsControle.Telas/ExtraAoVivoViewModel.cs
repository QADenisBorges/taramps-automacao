using BgsControle.Nucleo.Rendimento;

namespace BgsControle.Telas;

/// <summary>
/// O "Extra ao vivo" (história 13f; <c>telas-rendimento.md</c>, no mini player e na Roda; sem XAML, que é da 13g): no Extra
/// em uso e tocando, o tempo que falta de Extra ("Extra: 13:52"); no descanso depois do corte, "Descansando: 38:04", com o
/// Bluetooth travado e o player sem tocar; e o aviso "Não consegui travar o Bluetooth. Desligue o módulo." enquanto o
/// travamento não confirma. Acompanha o modelo a cada segundo.
/// </summary>
public sealed class ExtraAoVivoViewModel : TelaBase, IDisposable
{
    private readonly TermicoExtra _termico;
    private bool _descartado;

    public ExtraAoVivoViewModel(TermicoExtra termico, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _termico = termico;
        _termico.Mudou += AoMudar;
    }

    /// <summary>Há algo a mostrar: o tempo de Extra, o motivo de não haver tempo, o descanso ou o aviso.</summary>
    public bool Visivel => TextoTempo is not null || MotivoSemModelo is not null || Descansando || Aviso is not null;

    /// <summary>
    /// "Extra: 13:52" no Extra em uso e tocando; "Extra: sem limite nesta faixa" quando nenhuma bobina chega à máxima com a
    /// faixa atual; ou null.
    /// </summary>
    public string? TextoTempo => _termico.TextoTempoExtra;

    /// <summary>No Extra, tocando, sem o modelo de algum alto-falante: "Falta o modelo térmico do Sub.", ou null.</summary>
    public string? MotivoSemModelo => _termico.MotivoSemModelo;

    /// <summary>O descanso anda (o player não toca).</summary>
    public bool Descansando => _termico.EmDescanso;

    /// <summary>"Descansando: 38:04", ou null.</summary>
    public string? TextoDescanso => _termico.TextoDescanso;

    /// <summary>No descanso, o aparelho tem os 4 canais mutados e o Bluetooth de áudio desligado.</summary>
    public bool BluetoothTravado => _termico.BluetoothTravado;

    /// <summary>"Bluetooth travado" com a trava confirmada, ou null.</summary>
    public string? TextoBluetooth => BluetoothTravado ? TermicoExtra.TextoBluetoothTravado : null;

    /// <summary>"Não consegui travar o Bluetooth. Desligue o módulo.", ou null.</summary>
    public string? Aviso => _termico.Aviso;

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        if (_descartado)
            return;
        Avisar(nameof(Visivel));
        Avisar(nameof(TextoTempo));
        Avisar(nameof(MotivoSemModelo));
        Avisar(nameof(Descansando));
        Avisar(nameof(TextoDescanso));
        Avisar(nameof(BluetoothTravado));
        Avisar(nameof(TextoBluetooth));
        Avisar(nameof(Aviso));
    }

    public void Dispose()
    {
        _descartado = true;
        _termico.Mudou -= AoMudar;
    }
}
