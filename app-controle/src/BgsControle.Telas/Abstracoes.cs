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

    /// <summary>A tela Gerador (bloco Gerador da grade Áudio), em tela cheia (história 11).</summary>
    void AbrirGerador();

    /// <summary>Configurações › Presets.</summary>
    void AbrirPresets();

    /// <summary>Configurações › Aparelho › Senha: a troca de senha do processador.</summary>
    void AbrirSenha();

    /// <summary>Configurações › Aparelho › Voltímetro e clip (também ao tocar na faixa de monitoração da Mesa).</summary>
    void AbrirMonitoracao();

    /// <summary>Configurações › Chaves de acesso (também ao tocar num bloco travado pela chave).</summary>
    void AbrirChaves();

    /// <summary>
    /// A tela Aparelho, onde fica a chave "Controle pela rede" (o grupo APARELHO das Configurações; história 14b: o
    /// "+ Nova memória" com a chave desligada leva até ela).
    /// </summary>
    void AbrirAparelho();

    /// <summary>
    /// A calibração guiada em tela cheia (história 13g): pela linha "Calibrar saídas" das Configurações (sem saída: a
    /// escolha) ou pelo "Calibrar ›" de uma faixa de sem calibração (com o canal mestre da saída, 0 a 3: a calibração
    /// dela já abre no passo 1).
    /// </summary>
    void AbrirCalibracao(int? canal);

    /// <summary>O teste a seco do Extra em tela cheia (história 13g), pelo "Fazer o teste a seco ›" do Player.</summary>
    void AbrirTesteSeco();

    /// <summary>
    /// As telas que sobem por cima de tudo, sem barra: a escolha do Setup (Setup em dúvida ou Trocar de
    /// Setup) e a folha "Aplicado pela metade". Mostra as que valem e tira as outras.
    /// </summary>
    void MostrarPorCima(bool escolhaSetup, bool pelaMetade);

    /// <summary>
    /// As camadas que sobem sozinhas, também por cima de tudo, acima da escolha do Setup e da folha pela
    /// metade: a criação da memória em tela toda (história 10d), a folha de risco do app (por cima da criação),
    /// a tela toda "REAPLICANDO" e, no topo, a faixa "DESLIGUE O MÓDULO AGORA" do gerador. Mostra as que valem
    /// e tira as outras.
    /// </summary>
    void MostrarCamadas(bool criacao, bool folhaRisco, bool reaplicando, bool alarmeGerador);

    /// <summary>
    /// As folhas globais do Rendimento (história 13g), por cima de qualquer tela e abaixo da folha de risco do app: a folha
    /// "Nível da memória" (com a de risco do Máximo) e a folha "Outro app começou a tocar". História 14c: e a folha da
    /// proposta de ficha, por cima delas. Mostra as que valem e tira as outras.
    /// </summary>
    void MostrarFolhas(bool nivel, bool outroPlayer, bool fichaProposta);

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
    /// <summary>Os arquivos escolhidos, na ordem; vazio se o Denis desistiu ou se o seletor não abriu (<see cref="Motivo"/>).</summary>
    Task<IReadOnlyList<BgsControle.Nucleo.Player.ArquivoAudio>> EscolherAsync();

    /// <summary>Por que a última escolha voltou vazia sem o Denis desistir ("Este celular não tem um seletor de arquivos."), ou null.</summary>
    string? Motivo { get; }
}

/// <summary>
/// O som do celular (história 11, Player): por onde o Android manda o áudio, as músicas que perderam o
/// acesso (a permissão da URI dada pelo seletor) e o atalho para as configurações de Bluetooth. No app é o
/// Android; nos testes, um falso.
/// </summary>
public interface IAudioDoCelular
{
    /// <summary>True: o som sai por um aparelho Bluetooth de áudio; false: sai pelo celular; null: não se sabe.</summary>
    bool? SaindoPeloBluetooth { get; }

    /// <summary>
    /// O Android negou ler esta música (a permissão da URI se perdeu): ela fica indisponível até ser
    /// escolhida de novo no seletor. Uma permissão do sistema não a devolve.
    /// </summary>
    bool SemAcesso(string uri);

    /// <summary>As músicas voltaram a ter acesso (foram escolhidas de novo no seletor).</summary>
    void LimparSemAcesso();

    /// <summary>A saída do som ou o acesso mudou. Pode chegar em qualquer thread.</summary>
    event Action? Mudou;

    /// <summary>Abre as configurações de Bluetooth do Android.</summary>
    void AbrirBluetooth();
}

/// <summary>Entrega um arquivo para o Denis guardar ou mandar (o compartilhar do Android): o Exportar dos presets.</summary>
public interface IExportarArquivo
{
    /// <summary>Grava o texto num arquivo com o nome e abre o compartilhar. Devolve null se deu certo, ou o motivo.</summary>
    Task<string?> ExportarAsync(string nomeArquivo, string conteudo);
}
