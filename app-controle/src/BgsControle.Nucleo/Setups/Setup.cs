using BgsControle.Nucleo.Escrita;

namespace BgsControle.Nucleo.Setups;

/// <summary>
/// Um Setup (CAP-16): uma instalação do mesmo PRO 2.4BT (o 2.1 de casa, o carro…), com os
/// equipamentos e a calibração dela. Criar, renomear e apagar (e mexer nos equipamentos) não mandam
/// nada ao aparelho.
/// </summary>
/// <param name="Id">Único e estável (o herdado tem o id fixo do código).</param>
/// <param name="Nome">De 1 a 24 caracteres (a regra dos presets), sem repetir entre os Setups.</param>
/// <param name="Quando">Quando a calibração foi guardada.</param>
/// <param name="Calibracao">Os valores da camada calibração (<see cref="Camada.Calibracao"/>), como o aparelho os devolve.</param>
/// <param name="Equilibrio">O equilíbrio por canal, somado ao ganho e ao limiter de cada canal.</param>
/// <param name="Link12">O link 1–2 (só do app).</param>
/// <param name="Link34">O link 3–4 (só do app).</param>
/// <param name="Herdado">Vem dos protótipos (CAP-6): não se apaga.</param>
/// <param name="Nota">A nota herdada, quando há.</param>
public sealed record Setup(
    string Id,
    string Nome,
    DateTimeOffset Quando,
    IReadOnlyList<ValorCopiado> Calibracao,
    Equilibrio Equilibrio,
    bool Link12,
    bool Link34,
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
/// Uma memória no celular (CAP-4): o caráter do som, dentro de um Setup, e o preset que ela lembra
/// (as 15 bandas, CAP-5), que volta junto quando ela é aplicada. O ganho e o limiter de cada canal
/// guardam a parte da memória, sem o equilíbrio do Setup.
/// </summary>
/// <param name="Id">Único e estável (a herdada tem o id fixo do código).</param>
/// <param name="IdSetup">O Setup dono.</param>
/// <param name="Nome">De 1 a 24 caracteres, sem repetir entre as memórias do mesmo Setup.</param>
/// <param name="Quando">Quando o caráter foi guardado.</param>
/// <param name="Carater">Os valores da camada caráter (<see cref="Camada.Carater"/>), sem o equilíbrio.</param>
/// <param name="Herdado">Vem dos protótipos (CAP-6).</param>
/// <param name="Nota">A nota herdada, quando há.</param>
/// <param name="IdPreset">O preset lembrado (o último usado com ela), ou null.</param>
public sealed record MemoriaCelular(
    string Id,
    string IdSetup,
    string Nome,
    DateTimeOffset Quando,
    IReadOnlyList<ValorCopiado> Carater,
    bool Herdado = false,
    string? Nota = null,
    string? IdPreset = null);

/// <summary>O que o app sabe que está tocando.</summary>
public enum TipoSomEmUso
{
    /// <summary>Nada: desconectado, ou um Setup sem memórias ("Nenhuma memória").</summary>
    Nenhum,

    /// <summary>Uma memória do celular, aplicada, criada ou confirmada pela conferência nesta conexão.</summary>
    MemoriaCelular,

    /// <summary>Uma memória do aparelho carregada por este app nesta conexão.</summary>
    MemoriaAparelho,

    /// <summary>O app não sabe: Seguir sem Setup, caráter diferente da memória ativa, ou reconexão depois de carregar.</summary>
    SomDoAparelho,
}

/// <summary>O som em uso: o tipo e, conforme o tipo, a memória do celular ou a memória do aparelho (0 a 2).</summary>
public sealed record SomEmUso(TipoSomEmUso Tipo, string? IdMemoria = null, int? MemoriaAparelho = null)
{
    public static SomEmUso Nenhum { get; } = new(TipoSomEmUso.Nenhum);

    public static SomEmUso SomDoAparelho { get; } = new(TipoSomEmUso.SomDoAparelho);

    public static SomEmUso DoCelular(string id) => new(TipoSomEmUso.MemoriaCelular, IdMemoria: id);

    public static SomEmUso DoAparelho(int n) => new(TipoSomEmUso.MemoriaAparelho, MemoriaAparelho: n);
}

/// <summary>Em que pé está a conferência do Setup (failure-modes, regra 5).</summary>
public enum EstadoConferencia
{
    /// <summary>Sem conexão, ainda sem conferir, ou seguindo sem Setup.</summary>
    Nenhuma,

    /// <summary>A calibração e o caráter batem (ou o Setup foi escolhido ou criado).</summary>
    Confere,

    /// <summary>A calibração bate e o caráter difere da memória ativa: sem mutar, com Reaplicar.</summary>
    SomDiferente,

    /// <summary>A calibração não bate: os 4 canais mutados até a escolha; a IA bloqueada.</summary>
    Duvida,

    /// <summary>O Denis tocou no Setup para trocar: mutado até escolher ou desistir; a IA bloqueada.</summary>
    Trocando,
}

/// <summary>Uma diferença de calibração entre o Setup e o aparelho, para a lista "O que não bate".</summary>
/// <param name="Rotulo">"Atraso · Sub", "Atraso · Sub · CH4", "Ganho do EQ de entrada" (pelo nome da saída do Setup).</param>
/// <param name="NoSetup">O valor guardado no Setup, para a tela ("80 cm").</param>
/// <param name="NoAparelho">O valor lido no aparelho, para a tela ("0 cm", "—").</param>
/// <param name="Canal">O canal mestre da saída dona (0 a 3, a cor dela), ou null quando não é de uma saída.</param>
public sealed record DiferencaCalibracao(string Codigo, int? Indice, string Rotulo, string NoSetup, string NoAparelho, int? Canal = null)
{
    /// <summary>"Atraso · Sub: 80 cm no Casa 2.1, 0 cm no aparelho".</summary>
    public string Linha(string nomeSetup) => $"{Rotulo}: {NoSetup} no {nomeSetup}, {NoAparelho} no aparelho";
}

/// <summary>A marca do mudo da dúvida, guardada no celular antes de mutar.</summary>
/// <param name="IdSetup">O Setup que era a referência (null se nenhum).</param>
/// <param name="MuteAnterior">O mute dos 4 canais antes de mutar, como o aparelho o devolveu.</param>
/// <param name="Quando">Quando mutou.</param>
public sealed record MarcaMudo(string? IdSetup, IReadOnlyList<string> MuteAnterior, DateTimeOffset Quando);

/// <summary>Como terminou uma operação de Setup ou de memória do celular. <see cref="Mensagem"/> é a frase para a tela.</summary>
/// <param name="PedeFolhaSalvar">Criar memória com uma memória do aparelho (ou o som do aparelho) em uso: o caminho é a folha do Salvar.</param>
public sealed record ResultadoSetup(bool Ok, string? Motivo, string? Mensagem = null, bool PedeFolhaSalvar = false)
{
    public static ResultadoSetup Falha(string motivo) => new(false, motivo, motivo);

    public static ResultadoSetup Feito(string? mensagem) => new(true, null, mensagem);
}

/// <summary>A resposta à pergunta da folha do Salvar quando a calibração do aparelho difere da do Setup.</summary>
public enum EscolhaCalibracao
{
    /// <summary>A folha não perguntou (ou ainda não houve escolha).</summary>
    Nenhuma,

    /// <summary>"Manter a calibração do Casa 2.1": o app reaplica a do Setup pelo BLE, com cópia antes.</summary>
    Manter,

    /// <summary>"Atualizar o Casa 2.1 com a do aparelho": vale para todas as memórias do Setup.</summary>
    Atualizar,
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

/// <summary>Uma linha por camada na folha do Salvar: "Calibração → Setup Casa 2.1", com as mudanças.</summary>
/// <param name="Herdado">A linha vai para um preset herdado (o selo HERDADO na linha das 15 bandas).</param>
public sealed record LinhaSalvar(Camada Camada, string Titulo, IReadOnlyList<MudancaSalvar> Mudancas, bool Herdado = false);

/// <summary>
/// A folha do Salvar por camadas (EXPERIENCE, "Salvar por camadas"). <see cref="Trava"/> diz por que
/// o Salvar está travado ("Nada para salvar.").
/// </summary>
/// <param name="Trava">Por que não há o que salvar, ou null.</param>
/// <param name="Linhas">Uma por camada que muda.</param>
/// <param name="PedeNome">O caráter vai para uma memória nova: pede o nome.</param>
/// <param name="PerguntaCalibracao">A calibração do aparelho difere da do Setup e a memória em uso não é do celular: Manter ou Atualizar.</param>
/// <param name="NomeSetup">O Setup em uso ("Casa 2.1"), ou null.</param>
/// <param name="Diferencas">As diferenças de calibração (para a pergunta).</param>
public sealed record FolhaSalvar(
    string? Trava,
    IReadOnlyList<LinhaSalvar> Linhas,
    bool PedeNome,
    bool PerguntaCalibracao,
    string? NomeSetup,
    IReadOnlyList<DiferencaCalibracao> Diferencas)
{
    public static FolhaSalvar Travada(string motivo, string? nomeSetup = null) => new(motivo, [], false, false, nomeSetup, []);

    /// <summary>
    /// As 15 bandas mexidas com uma memória do celular que lembra um preset: a escolha entre
    /// <see cref="EscolhaPreset.Atualizar"/> e <see cref="EscolhaPreset.Criar"/>.
    /// </summary>
    public bool PerguntaPreset { get; init; }

    /// <summary>O preset da linha das 15 bandas ("Rock", "Cinema"), ou null (preset novo).</summary>
    public string? NomePreset { get; init; }

    /// <summary>Por que "Atualizar o preset" está travado ("Preset herdado não é sobrescrito."), ou null.</summary>
    public string? MotivoAtualizarTravado { get; init; }

    /// <summary>A escolha que já vem marcada: <see cref="EscolhaPreset.Criar"/> com um preset herdado; senão nenhuma.</summary>
    public EscolhaPreset EscolhaPresetInicial { get; init; }

    /// <summary>As 15 bandas vão, sem pergunta, para um preset novo: pede o nome dele.</summary>
    public bool PedeNomePreset { get; init; }
}
