namespace BgsControle.Simulador;

/// <summary>
/// O que o simulador (e o núcleo, onde depende disso) faz sem ter sido medido no aparelho. Cada
/// id aparece no código onde a suposição vale, como <c>Suposição &lt;ID&gt;</c>, e numa linha de
/// <c>SUPOSICOES.md</c>, com o mesmo texto; o <c>SuposicoesTestes</c> confere que as listas
/// batem nos dois sentidos. A história 12 confere cada uma no PRO 2.4BT.
/// </summary>
public static class Suposicoes
{
    public const string Ack241 = "ACK-241";
    public const string ConteudoMemoria = "CONTEUDO-MEMORIA";
    public const string MemoriasIniciais = "MEMORIAS-INICIAIS";
    public const string Eco251252 = "ECO-251-252";
    public const string Ack001 = "ACK-001";
    public const string Reset004 = "RESET-004";
    public const string Reset242 = "RESET-242";
    public const string Fabrica = "FABRICA";
    public const string FormatoEco = "FORMATO-ECO";
    public const string ModoEstrito = "MODO-ESTRITO";
    public const string EscritaInvalida = "ESCRITA-INVALIDA";
    public const string MudoSemSenha = "MUDO-SEM-SENHA";
    public const string QuedaAposErro = "QUEDA-APOS-ERRO";
    public const string VoltimetroCrlf = "VOLTIMETRO-CRLF";
    public const string ClipEspontaneo = "CLIP-ESPONTANEO";
    public const string MemoriaNome = "MEMORIA-N-NOME";
    public const string RegraNomes = "REGRA-NOMES";
    public const string CarregarSemNomes = "CARREGAR-SEM-NOMES";
    public const string LinkParametros = "LINK-PARAMETROS";
    public const string SenhaErradaDesautentica = "SENHA-ERRADA-DESAUTENTICA";
    public const string FilaUnica = "FILA-UNICA";
    public const string QuedaDescartaFila = "QUEDA-DESCARTA-FILA";
    public const string AtrasoRespostas = "ATRASO-RESPOSTAS";
    public const string GeradorModo = "GERADOR-MODO";
    public const string EcoGerador = "ECO-GERADOR";
    public const string Ack002 = "ACK-002";
    public const string NomeProduto = "NOME-PRODUTO";
    public const string VoltimetroEspontaneo = "VOLTIMETRO-ESPONTANEO";
    public const string ClipEntrada = "CLIP-ENTRADA";
    public const string PresetAplicaTudo = "PRESET-APLICA-TUDO";

    /// <summary>Id → o que se supõe. A mesma lista, com o mesmo texto, está em <c>SUPOSICOES.md</c>.</summary>
    public static IReadOnlyDictionary<string, string> Todas { get; } = new Dictionary<string, string>
    {
        [Ack241] = "O ack de `*241*n*` (carregar) é `#241#n#`, no mesmo prazo do 240 (~1,17 s). O do 240 foi medido (acima).",
        [ConteudoMemoria] = "Uma memória guarda os 84 valores do Marco 1 e o ganho (200) e a frequência (201) do tom fixo (medido); não guarda o 202, o 210–213, o 251 e o 252 (não medido: eram iguais nas memórias carregadas).",
        [MemoriasIniciais] = "O conteúdo inicial das 3 memórias é igual ao estado semeado.",
        [Eco251252] = "O 251 e o 252 respondem com o eco no formato de leitura (`#252#v#`). (O nome `*23N*nome*` não ecoa: medido, acima.)",
        [Ack001] = "O ack de `*001*antiga*nova*` é `#001#OK#` (ou `#001#ERRO#` com a antiga errada).",
        [Reset004] = "O ack de `*004*` é `#004#`; o reset de fábrica volta o Marco 1 e as memórias ao estado de fábrica, apaga os nomes, volta a senha à de fábrica (`1234`) e a conexão continua.",
        [Reset242] = "O ack de `*242*` é `#242#`; o reset de áudio volta o Marco 1 ao estado de fábrica e mantém memórias, nomes, gerador, 251 e 252.",
        [Fabrica] = "O estado de fábrica do Marco 1 (ganhos 0 dB, roteamento a/b/a/b, filtros OFF, limiter OFF `+0.5`, attack 50 ms, release 200 ms, EQs planos em 1 kHz e Q 1,0, preset Flat).",
        [FormatoEco] = "Um valor escrito volta com a formatação das leituras: dB e Q com sinal (`+3.0`, `+0.7`), cm e attack sem sinal, o resto inteiro; os especiais (`0.5`, `0.01`, `0.05`) como foram.",
        [ModoEstrito] = "O aparelho só aceita o texto exato do codificador do app (`*081*0*-3.0*`, não `-3`); fora disso, não aplica nem responde.",
        [EscritaInvalida] = "Uma escrita inválida (canal 9, valor fora da faixa, código só leitura ou desconhecido) não é aplicada e não tem resposta.",
        [MudoSemSenha] = "Antes da senha aceita, uma escrita é ignorada, sem resposta. (Uma leitura recebe `ERRO 0`: medido.)",
        [QuedaAposErro] = "A senha certa dentro da janela cancela a queda de 29 s; um segundo `ERRO` não reinicia o prazo.",
        [VoltimetroCrlf] = "O órfão `ERRO 0` do voltímetro chega com `\\r\\n`.",
        [ClipEspontaneo] = "O clip espontâneo (`#150#bits#`) tem o formato da leitura; o período e o gatilho não foram medidos.",
        [MemoriaNome] = "O nome da memória é lido em `#23N#` (resposta `23N#NOME`), e a memória 1 é o `n` 0 do 241 e o nome 231 (a 2 é `n` 1 e 232; a 3 é `n` 2 e 233). O app não escreve o nome (história 8c).",
        [RegraNomes] = "A regra de nomes da Taramps (de 1 a 12 caracteres, A–Z, 0–9, espaço e `_`, em maiúsculas) vale para o nome do produto (`*002*NOME*`), como valia para as memórias no app da Taramps.",
        [CarregarSemNomes] = "Carregar (`*241*n*`) troca os 84 valores do Marco 1 e não mexe nos nomes 23N; o nome é do aparelho, não da memória.",
        [LinkParametros] = "O link copia e repete os parâmetros de canal do Marco 1 menos o roteamento, como o app da Taramps (`BVM:16189`); o comportamento na tela é conferido na história 10.",
        [SenhaErradaDesautentica] = "Com a sessão já autenticada, uma senha errada recebe a mesma resposta da certa e a sessão continua (medido; era a suposição contrária).",
        [FilaUnica] = "As respostas com atraso saem de uma fila única, na ordem em que foram geradas: cada uma espera o seu atraso e a anterior (um eco de 85 ms seguido de uma leitura de 25 ms chega antes dela).",
        [QuedaDescartaFila] = "Desconectar ou cair joga fora as respostas que ainda não saíram; elas não chegam numa conexão nova.",
        [AtrasoRespostas] = "No simulador, a senha responde ~23 ms e cada leitura ~25 ms depois do pedido, sempre com o mesmo atraso. No aparelho o atraso varia (medido em 24/10, na coluna ao lado); os prazos do app são de 2 s ou mais.",
        [GeradorModo] = "O tom fixo é o 213 em 0 e o sweep é o 213 de 1 a 3; o 202 liga e desliga os dois.",
        [EcoGerador] = "Os códigos 200–202 e 210–213 ecoam no formato de leitura (`#202#1#`), como o 081.",
        [Ack002] = "O ack de `*002*NOME*` (nome do produto) é `#002#NOME#`, com o nome aplicado; o app espera até 5 s, como nos outros comandos de aparelho inteiro.",
        [NomeProduto] = "O 002 não muda o começo `PRO2.4MI` da resposta 000. Se mudar, a próxima conexão entra em modo só leitura, que é o lado seguro.",
        [VoltimetroEspontaneo] = "O aparelho manda o voltímetro sozinho, como `#005#v#` (V × 10); a consulta `#005#` responde `ERRO 0` (medido). O período não foi medido.",
        [ClipEntrada] = "O clip da entrada (`#111#v#`, ≠0 = clip) chega sozinho, no formato da leitura; o período e o gatilho não foram medidos.",
        [PresetAplicaTudo] = "Escrever todos os ajustes de áudio diferentes em sequência e reler tudo confirma o preset no PRO 2.4BT; o aparelho não tem comando de preset.",
    };
}
