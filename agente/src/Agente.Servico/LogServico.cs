using System.Globalization;

namespace Agente.Servico;

/// <summary>
/// Um arquivo de log que não cresce sem fim: passado <see cref="Maximo"/>, o atual vira <c>.1</c> (o <c>.1</c> anterior sai)
/// e um novo começa. Uma falha ao escrever (disco, permissão) nunca derruba quem escreve.
/// </summary>
public static class ArquivoLog
{
    public const long Maximo = 5 * 1024 * 1024;

    public static void Anexar(string caminho, string linha, long maximo = Maximo)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
            var info = new FileInfo(caminho);
            if (info.Exists && info.Length >= maximo)
                File.Move(caminho, caminho + ".1", overwrite: true);
            File.AppendAllText(caminho, linha + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // o log é só para consulta
        }
    }
}

/// <summary>
/// O log do serviço (<c>servico.log</c>, na pasta local): uma linha por acontecimento, com data e hora. Nunca recebe a
/// chave (nada do pareamento passa por aqui).
/// </summary>
public sealed class LogServico(string? arquivo, bool console = false, TimeProvider? relogio = null)
{
    public const int MaximoEmMemoria = 1000;

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly object _trava = new();
    private readonly List<string> _linhas = [];

    /// <summary>Uma linha foi escrita (a linha inteira, com a hora).</summary>
    public event Action<string>? Escreveu;

    /// <summary>As últimas linhas (até <see cref="MaximoEmMemoria"/>).</summary>
    public IReadOnlyList<string> Linhas
    {
        get
        {
            lock (_trava)
                return _linhas.ToArray();
        }
    }

    public void Escrever(string texto)
    {
        // Uma linha por acontecimento: uma quebra no texto (a mensagem de uma exceção) não abre outra linha sem hora.
        var limpo = texto.ReplaceLineEndings(" ");
        var linha = $"{_relogio.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {limpo}";
        lock (_trava)
        {
            _linhas.Add(linha);
            if (_linhas.Count > MaximoEmMemoria)
                _linhas.RemoveAt(0);
            if (arquivo is not null)
                ArquivoLog.Anexar(arquivo, linha);
            if (console)
                Console.WriteLine(linha);
        }
        Escreveu?.Invoke(linha);
    }
}
