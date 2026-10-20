namespace BgsControle.Nucleo.Enlace;

/// <summary>
/// O enlace BLE com o processador. O app real usa a API do Android; os testes usam um
/// transporte falso. A sessão é a única que chama <see cref="EscreverAsync"/>, e só pela
/// porta de escrita com a trava.
/// </summary>
public interface ITransporte
{
    /// <summary>
    /// Procura o processador pelo anúncio <c>0x2055</c> (ou pelo serviço) por até
    /// <paramref name="duracao"/>. Devolve o endereço, ou null se não achar.
    /// Lança <see cref="BluetoothIndisponivelException"/> se o Bluetooth estiver desligado
    /// ou faltar permissão.
    /// </summary>
    Task<string?> ProcurarAsync(TimeSpan duracao, CancellationToken ct);

    /// <summary>
    /// Uma tentativa de conexão: conecta, descobre o serviço e liga a notificação da 1123.
    /// Só termina com sucesso depois de a notificação estar ligada. Em qualquer falha,
    /// libera o GATT e lança <see cref="FalhaConexaoException"/> (ou
    /// <see cref="OperationCanceledException"/> se <paramref name="ct"/> for cancelado).
    /// </summary>
    Task ConectarAsync(string dispositivo, CancellationToken ct);

    /// <summary>Escreve na 1122 com resposta. Uma operação GATT por vez.</summary>
    Task EscreverAsync(byte[] dados, CancellationToken ct);

    /// <summary>Desconecta e libera o GATT. Não dispara <see cref="Caiu"/>.</summary>
    Task DesconectarAsync();

    /// <summary>Um fragmento recebido pela notificação da 1123.</summary>
    event Action<byte[]>? Notificacao;

    /// <summary>A conexão caiu sem ter sido pedida (só depois de um <see cref="ConectarAsync"/> com sucesso).</summary>
    event Action? Caiu;
}

public enum MotivoBluetooth
{
    Desligado,
    SemPermissao,
    SemSuporte,
}

public sealed class BluetoothIndisponivelException(MotivoBluetooth motivo)
    : Exception($"Bluetooth indisponível: {motivo}")
{
    public MotivoBluetooth Motivo { get; } = motivo;
}

public sealed class FalhaConexaoException(string mensagem) : Exception(mensagem);
