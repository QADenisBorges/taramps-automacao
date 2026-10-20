using System.Text;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// Processador simulado. Responde à senha com <see cref="RespostaCerta"/> (em fragmentos,
/// se quiser) quando ela bate com <see cref="SenhaDoAparelho"/>, e com <c>#000#ERRO#</c>
/// quando não bate.
/// </summary>
internal sealed class TransporteFalso : ITransporte
{
    public string? Dispositivo { get; set; } = "AA:BB:CC:DD:EE:FF";
    public Exception? ErroAoProcurar { get; set; }
    public int FalhasDeConexao { get; set; }
    public string SenhaDoAparelho { get; set; } = "AB12";
    public string[] RespostaCerta { get; set; } = ["#000#PRO2.4MIR003#\r\n"];
    public string[] RespostaErrada { get; set; } = ["#000#ERRO#\r\n"];
    /// <summary>Fragmentos enviados logo depois de a notificação ser ligada (antes da senha).</summary>
    public string[] AoConectar { get; set; } = [];
    /// <summary>Fragmentos enviados logo depois da escrita da senha, antes da resposta.</summary>
    public string[] AntesDaResposta { get; set; } = [];
    public bool Mudo { get; set; }

    public List<string> Escritas { get; } = [];
    public int Procuras { get; private set; }
    public int TentativasDeConexao { get; private set; }
    public int Desconexoes { get; private set; }
    public bool NotificacaoLigada { get; private set; }
    public bool EscreveuSemNotificacao { get; private set; }

    public event Action<byte[]>? Notificacao;
    public event Action? Caiu;

    public Task<string?> ProcurarAsync(TimeSpan duracao, CancellationToken ct)
    {
        Procuras++;
        if (ErroAoProcurar is not null)
            throw ErroAoProcurar;
        return Task.FromResult(Dispositivo);
    }

    public Task ConectarAsync(string dispositivo, CancellationToken ct)
    {
        TentativasDeConexao++;
        if (TentativasDeConexao <= FalhasDeConexao)
            throw new FalhaConexaoException("status 133");
        NotificacaoLigada = true;
        foreach (var f in AoConectar)
            Emitir(f);
        return Task.CompletedTask;
    }

    public Task EscreverAsync(byte[] dados, CancellationToken ct)
    {
        if (!NotificacaoLigada)
            EscreveuSemNotificacao = true;
        var texto = Encoding.ASCII.GetString(dados);
        Escritas.Add(texto);
        if (Mudo || !texto.StartsWith("*000*"))
            return Task.CompletedTask;
        foreach (var f in AntesDaResposta)
            Emitir(f);
        var resposta = texto == $"*000*{SenhaDoAparelho}*" ? RespostaCerta : RespostaErrada;
        foreach (var f in resposta)
            Emitir(f);
        return Task.CompletedTask;
    }

    public Task DesconectarAsync()
    {
        Desconexoes++;
        NotificacaoLigada = false;
        return Task.CompletedTask;
    }

    public void Emitir(string fragmento) => Notificacao?.Invoke(Encoding.ASCII.GetBytes(fragmento));

    public void Derrubar()
    {
        NotificacaoLigada = false;
        Caiu?.Invoke();
    }
}

internal sealed class CofreFalso : ICofreSenha
{
    public string? Senha { get; set; }
    public bool FalharAoLer { get; set; }
    public List<string> Guardadas { get; } = [];
    public int Apagadas { get; private set; }

    public Task<string?> LerAsync()
    {
        if (FalharAoLer)
        {
            // Como o CofreSecureStorage: erro do Keystore = sem senha, e a entrada é apagada.
            Senha = null;
            Apagadas++;
            return Task.FromResult<string?>(null);
        }
        return Task.FromResult(Senha);
    }

    public Task GuardarAsync(string senha)
    {
        Senha = senha;
        Guardadas.Add(senha);
        return Task.CompletedTask;
    }

    public Task ApagarAsync()
    {
        Senha = null;
        Apagadas++;
        return Task.CompletedTask;
    }
}
