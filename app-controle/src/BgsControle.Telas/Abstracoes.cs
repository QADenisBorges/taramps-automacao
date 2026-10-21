namespace BgsControle.Telas;

/// <summary>Põe uma ação na thread da tela. No app é o MainThread do MAUI.</summary>
public interface IDespachante
{
    /// <summary>Enfileira a ação na thread da tela e volta logo, sem esperar.</summary>
    void Executar(Action acao);
}

/// <summary>Os arquivos privados do app (no Android, sem backup: allowBackup=false).</summary>
public interface IArquivosApp
{
    /// <summary>Grava o texto no arquivo, substituindo o que havia. O nome é só o nome, sem pasta.</summary>
    Task GravarTextoAsync(string nome, string conteudo);

    /// <summary>O texto do arquivo, ou null se ele não existe.</summary>
    Task<string?> LerTextoAsync(string nome);
}

/// <summary>Deixa o Bluetooth pronto para procurar o processador: permissão e Bluetooth ligado.</summary>
public interface IPreparoBluetooth
{
    /// <summary>
    /// Pede ao celular o que falta. Devolve null quando está tudo pronto, ou a mensagem do que
    /// falta para a tela (sem permissão, Bluetooth desligado).
    /// </summary>
    Task<string?> PrepararAsync();
}
