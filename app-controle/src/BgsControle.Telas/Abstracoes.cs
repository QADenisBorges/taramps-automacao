namespace BgsControle.Telas;

/// <summary>
/// Um valor que se escreve num campo (o controle de parâmetro, o equilíbrio): o texto só vale no ✓, fora da
/// faixa é recusado com o motivo, e fechar sem ✓ descarta. A mesma peça de tela serve para os dois.
/// </summary>
public interface ICampoEditavel : System.ComponentModel.INotifyPropertyChanged
{
    bool EditandoCampo { get; }

    string TextoCampo { get; set; }

    string? ErroCampo { get; }

    /// <summary>O valor de agora, com a unidade ("+2,6 dB"), para a frase "Até lá, o som continua em…".</summary>
    string TextoValor { get; }

    /// <summary>O canal mestre da saída dona (0 a 3, a cor do contorno), ou null.</summary>
    int? Canal { get; }

    Comando Confirmar { get; }

    Comando FecharCampo { get; }
}

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

/// <summary>
/// A navegação do app (história 10): as telas cheias com ‹ e as telas que sobem sozinhas por cima de
/// tudo. Os ViewModels pedem; o app (MAUI) mostra. Nos testes, um falso guarda os pedidos.
/// </summary>
public interface INavegacao
{
    /// <summary>O Ajuste da saída que tem o canal (0 a 3; o mestre dela), em tela cheia.</summary>
    void AbrirAjuste(int canal);

    /// <summary>Uma tela de função da grade Áudio (tela cheia).</summary>
    void AbrirFuncao(FuncaoAudio funcao);

    /// <summary>Configurações (a engrenagem).</summary>
    void AbrirConfiguracoes();

    /// <summary>Configurações › Setups.</summary>
    void AbrirSetups();

    /// <summary>Configurações › Setups › um Setup: a tela Setup, com a lista de equipamentos.</summary>
    void AbrirSetup(string idSetup);

    /// <summary>Configurações › Memórias.</summary>
    void AbrirMemorias();

    /// <summary>A tela de conexão (tocar na pílula de status): Conectar, a senha, a leitura, o registro e Esquecer a senha.</summary>
    void AbrirConexao();

    /// <summary>
    /// As telas que sobem por cima de tudo, sem barra: a escolha do Setup (Setup em dúvida ou Trocar de
    /// Setup) e a folha "Aplicado pela metade". Mostra as que valem e tira as outras.
    /// </summary>
    void MostrarPorCima(bool escolhaSetup, bool pelaMetade);

    /// <summary>‹ numa tela cheia.</summary>
    void Voltar();

    /// <summary>Fecha as telas cheias e volta à casca (depois da escolha do Setup, a casca volta ao Início).</summary>
    void VoltarAoInicio();
}

/// <summary>
/// O seletor de arquivos de áudio do Android (história 11): escolhe vários de uma vez e devolve,
/// na ordem escolhida, a URI, o nome, a pasta e a duração de cada um. O app não copia a música.
/// </summary>
public interface ISeletorAudio
{
    /// <summary>Os arquivos escolhidos, na ordem; vazio se o Denis desistiu.</summary>
    Task<IReadOnlyList<BgsControle.Nucleo.Player.ArquivoAudio>> EscolherAsync();
}
