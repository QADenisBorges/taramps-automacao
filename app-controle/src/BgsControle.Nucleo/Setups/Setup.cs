using BgsControle.Nucleo.Escrita;

namespace BgsControle.Nucleo.Setups;

/// <summary>
/// Um Setup (CAP-16): uma instalação do mesmo PRO 2.4BT (o 2.1 de casa, o carro…). É só o físico: o
/// nome e os equipamentos com as fichas (forja de 29/10/2026). A regulagem fica na memória, e o link
/// sai sozinho das ligações (<see cref="Ligacoes.Links"/>). Criar, renomear e apagar (e mexer nos
/// equipamentos) não mandam nada ao aparelho.
/// </summary>
/// <param name="Id">Único e estável (o herdado tem o id fixo do código).</param>
/// <param name="Nome">De 1 a 24 caracteres (a regra dos presets), sem repetir entre os Setups.</param>
/// <param name="Quando">Quando o Setup foi criado.</param>
/// <param name="Herdado">Vem dos protótipos (CAP-6): não se apaga.</param>
/// <param name="Nota">A nota herdada, quando há.</param>
public sealed record Setup(
    string Id,
    string Nome,
    DateTimeOffset Quando,
    bool Herdado = false,
    string? Nota = null)
{
    /// <summary>
    /// Os equipamentos e as ligações (o modelo de ligação de <see cref="Ligacoes"/>), na ordem de
    /// criação. A lista é sempre válida (<see cref="Ligacoes.Validar"/>).
    /// </summary>
    public IReadOnlyList<Equipamento> Equipamentos { get; init; } = [];
}

/// <summary>Como terminou uma operação de equipamento. <see cref="Mensagem"/> é a frase para a tela.</summary>
/// <param name="Id">O id do equipamento (o novo, ao adicionar), ou null.</param>
public sealed record ResultadoEquipamento(bool Ok, string? Motivo, string? Mensagem = null, string? Id = null)
{
    public static ResultadoEquipamento Falha(string motivo) => new(false, motivo, motivo);
}

/// <summary>
/// Uma memória no celular (CAP-4): toda a regulagem, com o valor final de cada parâmetro, dentro de um
/// Setup, e o preset que ela lembra (as 15 bandas, CAP-5), que volta junto quando ela é aplicada.
/// </summary>
/// <param name="Id">Único e estável.</param>
/// <param name="IdSetup">O Setup dono.</param>
/// <param name="Nome">De 1 a 24 caracteres, sem repetir entre as memórias do mesmo Setup.</param>
/// <param name="Quando">Quando a regulagem foi guardada.</param>
/// <param name="Regulagem">Os valores da camada regulagem (<see cref="Camada.Regulagem"/>), como o aparelho os devolve.</param>
/// <param name="IdPreset">O preset lembrado (o último usado com ela), ou null.</param>
public sealed record MemoriaCelular(
    string Id,
    string IdSetup,
    string Nome,
    DateTimeOffset Quando,
    IReadOnlyList<ValorCopiado> Regulagem,
    string? IdPreset = null);

/// <summary>O que o app sabe que está tocando.</summary>
public enum TipoSomEmUso
{
    /// <summary>Nada: desconectado, ou um Setup sem memórias ("Nenhuma memória").</summary>
    Nenhum,

    /// <summary>Uma memória do celular, aplicada, criada ou confirmada pela conferência nesta conexão.</summary>
    MemoriaCelular,

    /// <summary>
    /// O app não sabe: Seguir sem Setup, ou a regulagem diferente da memória ativa. Não é uma memória
    /// do processador: elas não aparecem no app (decisão do Denis, 29/10/2026).
    /// </summary>
    SomDoAparelho,
}

/// <summary>O som em uso: o tipo e, numa memória do celular, o id dela.</summary>
public sealed record SomEmUso(TipoSomEmUso Tipo, string? IdMemoria = null)
{
    public static SomEmUso Nenhum { get; } = new(TipoSomEmUso.Nenhum);

    public static SomEmUso SomDoAparelho { get; } = new(TipoSomEmUso.SomDoAparelho);

    public static SomEmUso DoCelular(string id) => new(TipoSomEmUso.MemoriaCelular, IdMemoria: id);
}

/// <summary>Em que pé está a conferência do Setup (failure-modes, regra 5).</summary>
public enum EstadoConferencia
{
    /// <summary>Sem conexão, ainda sem conferir, ou seguindo sem Setup.</summary>
    Nenhuma,

    /// <summary>A regulagem bate com a memória ativa (ou o Setup foi escolhido ou criado).</summary>
    Confere,

    /// <summary>Só o ganho, o limitador, o EQ ou o MASTER diferem da memória ativa: sem mutar, com Reaplicar.</summary>
    SomDiferente,

    /// <summary>A rota, o atraso, a fase ou um corte diferem (ou o Setup não tem memória ativa): os 4 canais mutados até a escolha; a IA bloqueada.</summary>
    Duvida,

    /// <summary>O Denis tocou no Setup para trocar: mutado até escolher ou desistir; a IA bloqueada.</summary>
    Trocando,
}

/// <summary>
/// Uma diferença da conferência ao conectar entre a memória ativa e o aparelho, para a lista "O que não
/// bate" (só a rota, o atraso, a fase e os cortes: <see cref="Camadas.MutaNaConferencia"/>).
/// </summary>
/// <param name="Rotulo">"Atraso · Sub", "Atraso · Sub · CH4", "Passa-alta · JBL L" (pelo nome da saída do Setup).</param>
/// <param name="NaMemoria">O valor guardado na memória ativa, para a tela ("80 cm").</param>
/// <param name="NoAparelho">O valor lido no aparelho, para a tela ("0 cm", "—").</param>
/// <param name="Canal">O canal mestre da saída dona (0 a 3, a cor dela), ou null quando não é de uma saída.</param>
public sealed record DiferencaConferencia(string Codigo, int? Indice, string Rotulo, string NaMemoria, string NoAparelho, int? Canal = null)
{
    /// <summary>"Atraso · Sub: 80 cm na FILME, 0 cm no aparelho".</summary>
    public string Linha(string nomeMemoria) => $"{Rotulo}: {NaMemoria} na {nomeMemoria}, {NoAparelho} no aparelho";
}

/// <summary>A marca do mudo da dúvida, guardada no celular antes de mutar.</summary>
/// <param name="IdSetup">O Setup que era a referência (null se nenhum).</param>
/// <param name="MuteAnterior">O mute dos 4 canais antes de mutar, como o aparelho o devolveu.</param>
/// <param name="Quando">Quando mutou.</param>
public sealed record MarcaMudo(string? IdSetup, IReadOnlyList<string> MuteAnterior, DateTimeOffset Quando);

/// <summary>Como terminou uma operação de Setup ou de memória do celular. <see cref="Mensagem"/> é a frase para a tela.</summary>
/// <param name="PedeFolhaSalvar">Criar memória com o som do aparelho em uso: o caminho é a folha do Salvar.</param>
/// <param name="ErroNomeMemoria">Criar Setup novo: quem errou foi o nome da primeira memória, não o do Setup (a tela põe o erro embaixo do campo dela).</param>
/// <param name="ErroNomeSetup">Criar Setup novo: quem errou foi o nome do Setup (inválido ou repetido).</param>
public sealed record ResultadoSetup(bool Ok, string? Motivo, string? Mensagem = null, bool PedeFolhaSalvar = false, bool ErroNomeMemoria = false,
    bool ErroNomeSetup = false)
{
    public static ResultadoSetup Falha(string motivo) => new(false, motivo, motivo);

    /// <summary>O nome do Setup novo é inválido ou repetido.</summary>
    public static ResultadoSetup FalhaNomeSetup(string motivo) => new(false, motivo, motivo, ErroNomeSetup: true);

    /// <summary>O nome da primeira memória do Setup novo é inválido ou repetido ("Nome da memória: …").</summary>
    public static ResultadoSetup FalhaNomeMemoria(string motivo) => new(false, motivo, motivo, ErroNomeMemoria: true);

    public static ResultadoSetup Feito(string? mensagem) => new(true, null, mensagem);
}

/// <summary>Uma mudança na folha do Salvar: "Atraso · Sub: 80 cm → 85 cm".</summary>
/// <param name="Canal">O canal mestre da saída dona (0 a 3: o valor novo vai na cor dela), ou null.</param>
public sealed record MudancaSalvar(string Rotulo, string Antes, string Depois, int? Canal = null)
{
    public string Linha => $"{Rotulo}: {Antes} → {Depois}";
}

/// <summary>A resposta à pergunta das 15 bandas na folha do Salvar, com uma memória do celular em uso.</summary>
public enum EscolhaPreset
{
    /// <summary>A folha não perguntou (ou ainda não houve escolha).</summary>
    Nenhuma,

    /// <summary>"Atualizar o preset Rock": vale para toda memória que usa ele.</summary>
    Atualizar,

    /// <summary>"Criar preset novo", com o nome: a memória passa a lembrar o novo.</summary>
    Criar,
}

/// <summary>Uma linha por camada na folha do Salvar: "Regulagem → memória FILME", com as mudanças.</summary>
public sealed record LinhaSalvar(Camada Camada, string Titulo, IReadOnlyList<MudancaSalvar> Mudancas);

/// <summary>
/// A folha do Salvar por camadas (EXPERIENCE, "Salvar por camadas"): a regulagem vai para a memória em
/// uso (ou uma nova) e as 15 bandas para o preset. <see cref="Trava"/> diz por que o Salvar está
/// travado ("Nada para salvar.").
/// </summary>
/// <param name="Trava">Por que não há o que salvar, ou null.</param>
/// <param name="Linhas">Uma por camada que muda.</param>
/// <param name="PedeNome">A regulagem vai para uma memória nova: pede o nome.</param>
/// <param name="NomeSetup">O Setup em uso ("Casa 2.1"), ou null.</param>
public sealed record FolhaSalvar(
    string? Trava,
    IReadOnlyList<LinhaSalvar> Linhas,
    bool PedeNome,
    string? NomeSetup)
{
    public static FolhaSalvar Travada(string motivo, string? nomeSetup = null) => new(motivo, [], false, nomeSetup);

    /// <summary>
    /// As 15 bandas mexidas com uma memória do celular que lembra um preset: a escolha entre
    /// <see cref="EscolhaPreset.Atualizar"/> e <see cref="EscolhaPreset.Criar"/>.
    /// </summary>
    public bool PerguntaPreset { get; init; }

    /// <summary>O preset da linha das 15 bandas ("Rock"), ou null (preset novo).</summary>
    public string? NomePreset { get; init; }

    /// <summary>Por que "Atualizar o preset" está travado, ou null.</summary>
    public string? MotivoAtualizarTravado { get; init; }

    /// <summary>A escolha que já vem marcada: <see cref="EscolhaPreset.Criar"/> quando atualizar está travado; senão nenhuma.</summary>
    public EscolhaPreset EscolhaPresetInicial { get; init; }

    /// <summary>As 15 bandas vão, sem pergunta, para um preset novo: pede o nome dele.</summary>
    public bool PedeNomePreset { get; init; }
}
