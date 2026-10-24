using System.Globalization;
using System.Text;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Esquema;

/// <summary>
/// Um ponto da cena do esquema (história 10b), em unidades da cena: <see cref="U"/> vai da esquerda para
/// a direita (o processador à esquerda, os alto-falantes à direita), <see cref="V"/> é a fileira (de
/// trás para a frente) e <see cref="H"/> a altura acima do chão.
/// </summary>
public readonly record struct Ponto3(double U, double V, double H = 0)
{
    public static Ponto3 operator +(Ponto3 a, Ponto3 b) => new(a.U + b.U, a.V + b.V, a.H + b.H);

    public static Ponto3 operator -(Ponto3 a, Ponto3 b) => new(a.U - b.U, a.V - b.V, a.H - b.H);

    public static Ponto3 operator *(Ponto3 a, double k) => new(a.U * k, a.V * k, a.H * k);

    /// <summary>O mesmo ponto em outra altura.</summary>
    public Ponto3 NaAltura(double h) => this with { H = h };
}

/// <summary>A forma de uma peça, gerada pelo código (nunca uma imagem).</summary>
public enum FormaPeca
{
    /// <summary>O processador: bloco comprido com CH1 a CH4.</summary>
    Processador,

    /// <summary>Tweeter: domo pequeno.</summary>
    Domo,

    /// <summary>Driver: corneta.</summary>
    Corneta,

    /// <summary>Médio, médio-grave e woofer: cone, com o tamanho pelo diâmetro.</summary>
    Cone,

    /// <summary>Coaxial: cone com domo no centro.</summary>
    Coaxial,

    /// <summary>Subwoofer: caixa grande com cone.</summary>
    CaixaComCone,

    /// <summary>Caixa ativa e os outros do grupo 1: caixa com as vias de dentro.</summary>
    CaixaComVias,

    /// <summary>Uma caixa simples (função "outra").</summary>
    Caixa,

    /// <summary>Módulo e amplificador: bloco com os canais numerados e os bridges.</summary>
    BlocoComCanais,

    /// <summary>Grupos 3 e 4: bloco baixo com o nome da categoria.</summary>
    BlocoBaixo,
}

/// <summary>O tipo de uma porta: uma saída do processador (CHn), uma entrada de linha ou uma saída de equipamento.</summary>
public enum TipoPorta
{
    Processador,
    Entrada,
    Saida,
}

/// <summary>
/// Uma porta no chão, na face da peça: as saídas CH1 a CH4 do processador, as entradas (a borda na cor do
/// canal que chega, <see cref="Canal"/>; null sem sinal) e as saídas dos módulos e do grupo 3.
/// </summary>
/// <param name="Numero">CHn: o canal (0 a 3); entrada e saída: o número, a partir de 1.</param>
/// <param name="Face">A normal da face onde ela fica (no plano do chão), para saber se ela aparece.</param>
/// <param name="Canais">Os canais do processador que passam pela porta (para o destaque).</param>
public sealed record PortaCena(string IdPeca, TipoPorta Tipo, int Numero, Ponto3 Posicao, int? Canal, Ponto3 Face, IReadOnlySet<int> Canais);

/// <summary>Uma via de dentro de uma caixa ativa, desenhada no topo: o centro e o raio do cone.</summary>
public sealed record ViaCena(Ponto3 Centro, double Raio);

/// <summary>
/// Uma peça da cena: o processador ou um equipamento. As caixas e os blocos ficam com os lados a 45° das
/// fileiras (<see cref="CenaSetup.EixoA"/> e <see cref="CenaSetup.EixoB"/>), o que dá o isométrico padrão
/// no ângulo 0°; os alto-falantes soltos são cilindros (o cone, o domo, a corneta).
/// </summary>
/// <param name="Id">O id do equipamento, ou <see cref="CenaSetup.IdProcessador"/>.</param>
/// <param name="Nome">O nome no rótulo.</param>
/// <param name="Base">O centro da base, no chão.</param>
/// <param name="MeiaA">A meia medida ao longo do eixo A (caixas e blocos).</param>
/// <param name="MeiaB">A meia medida ao longo do eixo B (caixas e blocos).</param>
/// <param name="Altura">A altura do corpo (na corneta, até a boca).</param>
/// <param name="Raio">Cone, coaxial, domo, corneta (a boca) e o cone da caixa: o raio; 0 nos outros.</param>
public sealed record PecaCena(string Id, string Nome, FormaPeca Forma, Ponto3 Base, double MeiaA, double MeiaB, double Altura, double Raio)
{
    /// <summary>É um equipamento (toque abre a folha); o processador não é.</summary>
    public bool Equipamento => Id != CenaSetup.IdProcessador;

    /// <summary>Sem modelo: o rótulo traz "genérico" em apagado.</summary>
    public bool Generico { get; init; }

    /// <summary>Sem ligação: fica à parte, apagado, com "sem ligação".</summary>
    public bool SemLigacao { get; init; }

    /// <summary>O grupo do equipamento (null no processador).</summary>
    public GrupoEquipamento? Grupo { get; init; }

    /// <summary>Os canais do processador (0 a 3) que chegam até a peça, direto ou pela cadeia.</summary>
    public IReadOnlySet<int> Canais { get; init; } = new HashSet<int>();

    /// <summary>A coluna (0 = o processador).</summary>
    public int Coluna { get; init; }

    /// <summary>Grupos 3 e 4: o nome da categoria, escrito no bloco.</summary>
    public string? TextoBloco { get; init; }

    /// <summary>A caixa ativa: as vias de dentro, no topo.</summary>
    public IReadOnlyList<ViaCena> Vias { get; init; } = [];

    /// <summary>"genérico", "sem ligação" ou os dois ("genérico · sem ligação"); null sem nada.</summary>
    public string? Detalhe =>
        (Generico, SemLigacao) switch
        {
            (true, true) => $"{Ligacoes.TextoGenerico} · {Ligacoes.TextoSemLigacao}",
            (true, false) => Ligacoes.TextoGenerico,
            (false, true) => Ligacoes.TextoSemLigacao,
            _ => null,
        };

    /// <summary>É um cilindro (cone, coaxial, domo, corneta); senão, uma caixa ou um bloco.</summary>
    public bool Cilindro => Forma is FormaPeca.Cone or FormaPeca.Coaxial or FormaPeca.Domo or FormaPeca.Corneta;

    /// <summary>O centro do corpo (a meia altura), o ponto que o toque acerta.</summary>
    public Ponto3 Centro => Base.NaAltura(Altura / 2);

    /// <summary>A corneta: o raio do corpo do driver, embaixo da boca.</summary>
    public const double RaioCorpoCorneta = 7;

    /// <summary>Os cantos da caixa no chão (h = 0) ou no topo, na ordem em volta.</summary>
    public IReadOnlyList<Ponto3> Cantos(double h) =>
    [
        (Base + (CenaSetup.EixoA * -MeiaA) + (CenaSetup.EixoB * -MeiaB)).NaAltura(h),
        (Base + (CenaSetup.EixoA * MeiaA) + (CenaSetup.EixoB * -MeiaB)).NaAltura(h),
        (Base + (CenaSetup.EixoA * MeiaA) + (CenaSetup.EixoB * MeiaB)).NaAltura(h),
        (Base + (CenaSetup.EixoA * -MeiaA) + (CenaSetup.EixoB * MeiaB)).NaAltura(h),
    ];

    /// <summary>Os pontos que dão a silhueta (os cantos da caixa ou os círculos do cilindro, embaixo e em cima).</summary>
    public IEnumerable<Ponto3> Contorno()
    {
        if (!Cilindro)
            return Cantos(0).Concat(Cantos(Altura));
        var raioBase = Forma == FormaPeca.Corneta ? RaioCorpoCorneta : Raio;
        var contorno = Circulo(Base, raioBase, 12).Concat(Circulo(Base.NaAltura(Altura), Raio, 12));
        return AlturaDoDomo is { } domo ? contorno.Append(Base.NaAltura(domo)) : contorno;
    }

    /// <summary>O domo do tweeter e o do centro do coaxial: o raio dele (a meia esfera sobe isso).</summary>
    public double RaioDomo => Forma == FormaPeca.Domo ? Raio * 0.62 : Forma == FormaPeca.Coaxial ? Raio * 0.24 : 0;

    /// <summary>A base do domo: o topo, ou o centro afundado do coaxial.</summary>
    public double BaseDoDomo => Forma == FormaPeca.Coaxial ? Altura - Raio * 0.15 : Altura;

    /// <summary>A altura do alto do domo (tweeter e coaxial), que entra na silhueta; null sem domo.</summary>
    public double? AlturaDoDomo => RaioDomo > 0 ? BaseDoDomo + RaioDomo : null;

    private static IEnumerable<Ponto3> Circulo(Ponto3 centro, double raio, int pontos)
    {
        for (var i = 0; i < pontos; i++)
        {
            var a = 2 * Math.PI * i / pontos;
            yield return centro + new Ponto3(raio * Math.Cos(a), raio * Math.Sin(a));
        }
    }
}

/// <summary>
/// Um cabo no chão, de uma saída até uma entrada, na cor do canal que ele leva (<see cref="Canal"/>, o
/// menor dos <see cref="Canais"/>; null sem sinal).
/// </summary>
public sealed record CaboCena(string Id, IReadOnlyList<Ponto3> Pontos, int? Canal, IReadOnlySet<int> Canais, string IdOrigem, string IdDestino);

/// <summary>O ponto onde um cabo abre em Y (um canal ou uma saída que vai para mais de uma entrada).</summary>
public sealed record JuncaoCena(Ponto3 Posicao, int? Canal, IReadOnlySet<int> Canais, string IdOrigem);

/// <summary>
/// Um bridge de um módulo: a chave que junta as duas saídas (<see cref="Chave"/>, de A até B) e a saída do
/// bridge, no meio dela, de onde sai o cabo do alto-falante.
/// </summary>
public sealed record BridgeCena(string Id, string IdModulo, Bridge Bridge, IReadOnlyList<Ponto3> Chave, Ponto3 Saida, int? Canal, IReadOnlySet<int> Canais);

/// <summary>
/// A cena do esquema do Setup (CAP-17, história 10b): as peças, os cabos, os Y, os bridges e as portas,
/// montados a partir dos equipamentos do Setup. Da esquerda para a direita: o processador, as colunas do
/// grupo 3 (uma por nível da cadeia), os grupos 1, 2 e 4, e os alto-falantes passivos, junto da saída ou
/// do bridge do módulo deles. Os sem ligação ficam à parte, numa fileira embaixo. A montagem é pura
/// (sem MAUI) e linear no número de peças e de cabos; quem pinta é a view.
/// </summary>
public sealed class CenaSetup
{
    public const string IdProcessador = "processador";
    public const string NomeProcessador = "Processador";

    /// <summary>A distância entre as colunas.</summary>
    public const double PassoColuna = 130;

    /// <summary>A distância entre as fileiras.</summary>
    public const double PassoFileira = 120;

    /// <summary>A distância entre as saídas CH1 a CH4 na face do processador.</summary>
    public const double PassoCanalProcessador = 36;

    /// <summary>O cone: o raio por polegada de diâmetro, preso entre <see cref="RaioConeMinimo"/> e <see cref="RaioConeMaximo"/>.</summary>
    public const double RaioPorPolegada = 1.6;

    public const double RaioConeMinimo = 6;
    public const double RaioConeMaximo = 22;

    /// <summary>
    /// O eixo A das caixas, no chão: para a direita e para trás. Com o eixo B, faz as caixas ficarem a 45°
    /// das fileiras, que é o isométrico padrão no ângulo 0°.
    /// </summary>
    public static readonly Ponto3 EixoA = new(Math.Sqrt(0.5), -Math.Sqrt(0.5));

    /// <summary>O eixo B das caixas, no chão: para a direita e para a frente.</summary>
    public static readonly Ponto3 EixoB = new(Math.Sqrt(0.5), Math.Sqrt(0.5));

    /// <summary>Monta a cena com as partes já prontas (a montagem; nos testes, peças sobrepostas).</summary>
    internal CenaSetup(IReadOnlyList<PecaCena> pecas, IReadOnlyList<CaboCena> cabos, IReadOnlyList<JuncaoCena> juncoes,
        IReadOnlyList<BridgeCena> bridges, IReadOnlyList<PortaCena> portas)
    {
        Pecas = pecas;
        Cabos = cabos;
        Juncoes = juncoes;
        Bridges = bridges;
        Portas = portas;
        _portasDe = portas.ToLookup(p => p.IdPeca);
        var pontos = pecas.SelectMany(p => p.Contorno()).Concat(portas.Select(p => p.Posicao))
            .Concat(cabos.SelectMany(c => c.Pontos)).Concat(bridges.SelectMany(b => b.Chave)).ToArray();
        MinU = pontos.Min(p => p.U);
        MaxU = pontos.Max(p => p.U);
        MinV = pontos.Min(p => p.V);
        MaxV = pontos.Max(p => p.V);
        MaxH = pontos.Max(p => p.H);
        Centro = new Ponto3((MinU + MaxU) / 2, (MinV + MaxV) / 2);
    }

    private readonly ILookup<string, PortaCena> _portasDe;

    /// <summary>As peças: o processador primeiro, depois os equipamentos na ordem de criação.</summary>
    public IReadOnlyList<PecaCena> Pecas { get; }

    /// <summary>As portas da peça (separadas uma vez por cena).</summary>
    public IEnumerable<PortaCena> PortasDe(string idPeca) => _portasDe[idPeca];

    public IReadOnlyList<CaboCena> Cabos { get; }

    /// <summary>Os pontos dos Y.</summary>
    public IReadOnlyList<JuncaoCena> Juncoes { get; }

    public IReadOnlyList<BridgeCena> Bridges { get; }

    /// <summary>As portas: CH1 a CH4 do processador, as entradas e as saídas dos equipamentos.</summary>
    public IReadOnlyList<PortaCena> Portas { get; }

    /// <summary>O processador.</summary>
    public PecaCena Processador => Pecas[0];

    /// <summary>Só o processador: o Setup não tem equipamentos.</summary>
    public bool Vazia => Pecas.Count == 1;

    /// <summary>O meio da cena no chão (o eixo em volta do qual o ângulo gira).</summary>
    public Ponto3 Centro { get; }

    public double MinU { get; }
    public double MaxU { get; }
    public double MinV { get; }
    public double MaxV { get; }
    public double MaxH { get; }

    /// <summary>O id de um bridge na cena: "id-do-modulo:bridge:3+4".</summary>
    public static string IdBridge(string idModulo, Bridge b) => $"{idModulo}:bridge:{b.Rotulo}";

    /// <summary>A peça pelo id, ou null.</summary>
    public PecaCena? Peca(string? id) => Pecas.FirstOrDefault(p => p.Id == id);

    /// <summary>A peça pelo nome (sem distinguir maiúsculas), ou null.</summary>
    public PecaCena? PecaPorNome(string nome) =>
        Pecas.FirstOrDefault(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>A saída CHn do processador (canal 0 a 3).</summary>
    public PortaCena SaidaProcessador(int canal) => Portas.First(p => p.Tipo == TipoPorta.Processador && p.Numero == canal);

    /// <summary>Os cabos que chegam à peça.</summary>
    public IEnumerable<CaboCena> CabosAte(string idPeca) => Cabos.Where(c => c.IdDestino == idPeca);

    /// <summary>Os cabos que saem da peça (do processador, de um módulo ou de um equipamento do grupo 3).</summary>
    public IEnumerable<CaboCena> CabosDe(string idPeca) => Cabos.Where(c => c.IdOrigem == idPeca);

    // ------------------------------------------------------------------ a montagem

    /// <summary>
    /// Monta a cena a partir dos equipamentos do Setup (só lê; não muda nada).
    /// </summary>
    /// <param name="lista">Os equipamentos, na ordem de criação.</param>
    /// <param name="funcao">
    /// A função acústica que vale para um equipamento (a escolhida, ou a sugerida); só usada quando a
    /// categoria não tem forma própria. Sem ela, vale a escolhida pelo usuário.
    /// </param>
    public static CenaSetup Montar(IReadOnlyList<Equipamento> lista, Func<Equipamento, FuncaoAcustica?>? funcao = null) =>
        new Montagem(lista, funcao ?? (e => e.Funcao)).Cena();

    /// <summary>A forma de um equipamento pelas regras da CAP-17 (categoria; desconhecida, a função acústica).</summary>
    public static FormaPeca FormaDe(Equipamento e, FuncaoAcustica? funcao)
    {
        switch (e.Grupo)
        {
            case GrupoEquipamento.Amplificador:
                return FormaPeca.BlocoComCanais;
            case GrupoEquipamento.Repassa or GrupoEquipamento.SoRecebe:
                return FormaPeca.BlocoBaixo;
            case GrupoEquipamento.AmplificaETocam:
                return Normalizar(e.Categoria) is "sub ativo" or "subwoofer ativo" or "subwoofer" ? FormaPeca.CaixaComCone : FormaPeca.CaixaComVias;
        }
        return Normalizar(e.Categoria) switch
        {
            "tweeter" or "super tweeter" => FormaPeca.Domo,
            "driver" => FormaPeca.Corneta,
            "medio" or "medio-grave" or "medio grave" or "woofer" => FormaPeca.Cone,
            "coaxial" => FormaPeca.Coaxial,
            "subwoofer" => FormaPeca.CaixaComCone,
            _ => funcao switch
            {
                FuncaoAcustica.Sub => FormaPeca.CaixaComCone,
                FuncaoAcustica.Agudo or FuncaoAcustica.SuperAgudo => FormaPeca.Domo,
                FuncaoAcustica.Grave or FuncaoAcustica.MedioGrave or FuncaoAcustica.Medio => FormaPeca.Cone,
                FuncaoAcustica.FullRange => FormaPeca.Coaxial,
                _ => FormaPeca.Caixa,
            },
        };
    }

    /// <summary>
    /// O raio do cone: pelo diâmetro, proporcional e preso entre 6 e 22; sem diâmetro, um tamanho médio pela
    /// categoria (ou pela função): médio 5", médio-grave e coaxial 6,5", woofer e grave 10", sub 12".
    /// </summary>
    public static double RaioCone(Equipamento e, FuncaoAcustica? funcao)
    {
        var diametro = e.Dados.Diametro ?? Normalizar(e.Categoria) switch
        {
            "medio" => 5m,
            "medio-grave" or "medio grave" or "coaxial" => 6.5m,
            "woofer" => 10m,
            "subwoofer" or "sub ativo" or "subwoofer ativo" => 12m,
            _ => funcao switch
            {
                FuncaoAcustica.Medio => 5m,
                FuncaoAcustica.Grave => 10m,
                FuncaoAcustica.Sub => 12m,
                _ => 6.5m,
            },
        };
        return Math.Clamp((double)diametro * RaioPorPolegada, RaioConeMinimo, RaioConeMaximo);
    }

    /// <summary>A categoria de alto-falante tem forma própria (senão, vale a função acústica).</summary>
    public static bool CategoriaConhecida(string? categoria) =>
        Normalizar(categoria) is "tweeter" or "super tweeter" or "driver" or "medio" or "medio-grave" or "medio grave" or "woofer"
            or "coaxial" or "subwoofer";

    /// <summary>Minúsculas, sem acento, sem espaços repetidos.</summary>
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";
        var sem = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sem.Append(c);
        }
        return string.Join(' ', sem.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A montagem de uma cena: um objeto por chamada, para os passos dividirem o que já calcularam.</summary>
    private sealed class Montagem
    {
        private static readonly IReadOnlySet<int> Nenhum = new HashSet<int>();

        private readonly IReadOnlyList<Equipamento> _lista;
        private readonly Func<Equipamento, FuncaoAcustica?> _funcao;
        private readonly Dictionary<string, Equipamento> _porId = new();
        private readonly Dictionary<string, int> _ordem = new();
        private readonly Dictionary<string, HashSet<int>> _canais = new();
        private readonly Dictionary<string, PecaCena> _pecas = new();
        private readonly List<PortaCena> _portas = [];
        private readonly List<CaboCena> _cabos = [];
        private readonly List<JuncaoCena> _juncoes = [];
        private readonly List<BridgeCena> _bridges = [];
        private readonly Dictionary<string, int> _indiceNaColuna = new();

        public Montagem(IReadOnlyList<Equipamento> lista, Func<Equipamento, FuncaoAcustica?> funcao)
        {
            _funcao = funcao;
            // Fica o primeiro de cada id (a lista guardada já vem saneada; isto só protege a cena).
            _lista = lista.Where(e => _porId.TryAdd(e.Id, e)).ToArray();
            for (var i = 0; i < _lista.Count; i++)
                _ordem[_lista[i].Id] = i;
            // Os canais que chegam a cada equipamento: uma volta da cadeia por canal (4 voltas).
            for (var c = 0; c < Ligacoes.CanaisProcessador; c++)
            {
                foreach (var e in Ligacoes.Alimentados(_lista, [c]))
                {
                    if (!_canais.TryGetValue(e.Id, out var s))
                        _canais[e.Id] = s = [];
                    s.Add(c);
                }
            }
        }

        private IReadOnlySet<int> CanaisDe(string id) => _canais.TryGetValue(id, out var s) ? s : Nenhum;

        private IReadOnlySet<int> CanaisDaFonte(FonteSinal? f) =>
            f is null ? Nenhum : f.Canal is { } c ? new HashSet<int> { c } : f.IdEquipamento is { } id ? CanaisDe(id) : Nenhum;

        private static int? Menor(IReadOnlySet<int> canais) => canais.Count == 0 ? null : canais.Min();

        private int Chave(Equipamento e) => Menor(CanaisDe(e.Id)) ?? Ligacoes.CanaisProcessador;

        public CenaSetup Cena()
        {
            // A mesma regra da lista: sem ligação é quem nenhum canal do processador alcança
            // (Ligacoes.CanaisQueAlimentam vazio). Um alto-falante alcançado sempre tem canal ou bridge.
            bool SemLigacao(Equipamento e) => CanaisDe(e.Id).Count == 0;

            var soltos = _lista.Where(SemLigacao).ToArray();
            var ligados = _lista.Where(e => !SemLigacao(e)).ToArray();

            // As colunas: o processador, um nível do grupo 3 por coluna, os grupos 1, 2 e 4, os alto-falantes.
            var niveis = Niveis(ligados.Where(e => e.Grupo == GrupoEquipamento.Repassa));
            var colunasG3 = niveis.Values.Distinct().Order().ToArray();
            var principais = ligados
                .Where(e => e.Grupo is GrupoEquipamento.AmplificaETocam or GrupoEquipamento.Amplificador or GrupoEquipamento.SoRecebe)
                .OrderBy(Chave).ThenBy(e => _ordem[e.Id]).ToArray();
            var altoFalantes = ligados.Where(e => e.Grupo == GrupoEquipamento.AltoFalante).ToArray();
            var doModulo = altoFalantes
                .GroupBy(e => e.Ligado!.IdModulo)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Ligado!.Canal ?? e.Ligado.Bridge?.A ?? 0).ThenBy(e => _ordem[e.Id]).ToArray());

            var coluna = 1;
            var uG3 = colunasG3.ToDictionary(n => n, _ => coluna++ * PassoColuna);
            var uPrincipal = principais.Length > 0 ? coluna++ * PassoColuna : 0;
            var uAltoFalantes = altoFalantes.Length > 0 ? coluna * PassoColuna : 0;

            // As fileiras: cada coluna fica centrada na mais alta. Um módulo ocupa uma fileira por alto-falante.
            var blocos = principais.Select(e => Math.Max(1, doModulo.GetValueOrDefault(e.Id)?.Length ?? 0)).ToArray();
            var fileirasPrincipal = blocos.Sum();
            var fileiras = Math.Max(1, Math.Max(fileirasPrincipal,
                colunasG3.Length == 0 ? 0 : colunasG3.Max(n => niveis.Count(x => x.Value == n))));
            double Fileira(double f) => f * PassoFileira;

            var lista = new List<PecaCena> { Processador(Fileira((fileiras - 1) / 2.0)) };

            foreach (var n in colunasG3)
            {
                var daColuna = niveis.Where(x => x.Value == n).Select(x => _porId[x.Key])
                    .OrderBy(Chave).ThenBy(e => _ordem[e.Id]).ToArray();
                var inicio = (fileiras - daColuna.Length) / 2.0;
                for (var i = 0; i < daColuna.Length; i++)
                {
                    lista.Add(Equipamento(daColuna[i], new Ponto3(uG3[n], Fileira(inicio + i)), 1 + Array.IndexOf(colunasG3, n), false));
                    _indiceNaColuna[daColuna[i].Id] = i;
                }
            }

            var inicioPrincipal = (fileiras - fileirasPrincipal) / 2.0;
            var fileira = inicioPrincipal;
            var colunaPrincipal = 1 + colunasG3.Length;
            for (var i = 0; i < principais.Length; i++)
            {
                var e = principais[i];
                lista.Add(Equipamento(e, new Ponto3(uPrincipal, Fileira(fileira + (blocos[i] - 1) / 2.0)), colunaPrincipal, false));
                if (doModulo.TryGetValue(e.Id, out var deste))
                {
                    for (var j = 0; j < deste.Length; j++)
                        lista.Add(Equipamento(deste[j], new Ponto3(uAltoFalantes, Fileira(fileira + j)), colunaPrincipal + 1, false));
                }
                fileira += blocos[i];
            }

            // Os sem ligação: à parte, embaixo de todas, em fileiras da largura da cena (no mínimo 3 por fileira).
            var porFileira = Math.Max(3, coluna);
            for (var i = 0; i < soltos.Length; i++)
            {
                var (linha, posicao) = Math.DivRem(i, porFileira);
                lista.Add(Equipamento(soltos[i], new Ponto3((posicao + 1) * PassoColuna, Fileira(fileiras - 1 + 1.3 + linha)), posicao + 1, true));
            }

            foreach (var p in lista)
                _pecas[p.Id] = p;

            Portas();
            Cabos();

            // O processador primeiro, depois os equipamentos na ordem de criação.
            var pecas = lista.OrderBy(p => p.Equipamento ? _ordem[p.Id] + 1 : 0).ToArray();
            return new CenaSetup(pecas, _cabos, _juncoes, _bridges, _portas);
        }

        /// <summary>O nível de cada equipamento do grupo 3 na cadeia: 0 quando só os canais o alimentam.</summary>
        private Dictionary<string, int> Niveis(IEnumerable<Equipamento> grupo3)
        {
            var niveis = new Dictionary<string, int>();
            var ids = grupo3.Select(e => e.Id).ToHashSet();
            int Nivel(string id, int profundidade)
            {
                if (niveis.TryGetValue(id, out var n))
                    return n;
                // A cadeia não tem ciclo (a validação recusa); a profundidade só protege a cena.
                var fontes = _porId[id].Entradas.Select(x => x.Fonte?.IdEquipamento).OfType<string>().Where(ids.Contains).ToArray();
                n = fontes.Length == 0 || profundidade > ids.Count ? 0 : 1 + fontes.Max(f => Nivel(f, profundidade + 1));
                niveis[id] = n;
                return n;
            }
            foreach (var id in ids)
                Nivel(id, 0);
            return niveis;
        }

        private PecaCena Processador(double v)
        {
            const double meiaB = 16;
            var meiaA = 1.5 * PassoCanalProcessador + 14;
            return new PecaCena(IdProcessador, NomeProcessador, FormaPeca.Processador, new Ponto3(0, v), meiaA, meiaB, 20, 0);
        }

        private PecaCena Equipamento(Equipamento e, Ponto3 base_, int coluna, bool solto)
        {
            // A função (que pode custar a sugestão pelo corte) só entra quando a categoria não diz a forma.
            var funcao = e.Grupo == GrupoEquipamento.AltoFalante && !CategoriaConhecida(e.Categoria) ? _funcao(e) : e.Funcao;
            var forma = FormaDe(e, funcao);
            var peca = forma switch
            {
                FormaPeca.Domo => Cilindro(8, 5),
                FormaPeca.Corneta => Cilindro(15, 20),
                FormaPeca.Cone or FormaPeca.Coaxial => ConeSolto(RaioCone(e, funcao)),
                FormaPeca.CaixaComCone => CaixaComCone(RaioCone(e, funcao)),
                FormaPeca.CaixaComVias => CaixaComVias(e),
                FormaPeca.BlocoComCanais => Bloco(Math.Clamp(4 + e.Entradas.Count * 7, 22, 48), Math.Clamp(4 + e.Entradas.Count * 7, 22, 48), 14),
                FormaPeca.BlocoBaixo => Bloco(28, Math.Clamp(8 + e.Entradas.Count * 8, 20, 40), 12),
                _ => Bloco(14, 14, 30),
            };
            return peca with
            {
                Id = e.Id,
                Nome = e.Nome,
                Forma = forma,
                Base = base_,
                Generico = string.IsNullOrWhiteSpace(e.Modelo),
                SemLigacao = solto,
                Grupo = e.Grupo,
                Canais = CanaisDe(e.Id),
                Coluna = coluna,
                TextoBloco = forma == FormaPeca.BlocoBaixo ? e.Categoria : null,
                Vias = forma == FormaPeca.CaixaComVias ? ViasNoTopo(e, base_, peca.MeiaA, peca.Altura) : [],
            };

            static PecaCena Cilindro(double raio, double altura) => new("", "", FormaPeca.Cone, default, raio, raio, altura, raio);
            static PecaCena ConeSolto(double raio) => Cilindro(raio, Math.Max(4, raio * 0.4));
            static PecaCena Bloco(double meiaA, double meiaB, double altura) => new("", "", FormaPeca.Caixa, default, meiaA, meiaB, altura, 0);
            static PecaCena CaixaComCone(double raio)
            {
                var meia = Math.Max(20, raio + 5);
                return new("", "", FormaPeca.CaixaComCone, default, meia, meia, 32, raio);
            }
        }

        /// <summary>A caixa ativa: larga o bastante para as vias, enfileiradas no topo ao longo do eixo A.</summary>
        private const double MeiaACaixaMaxima = 48;

        private static PecaCena CaixaComVias(Equipamento e)
        {
            var raios = RaiosVias(e);
            var meiaA = Math.Clamp((raios.Sum() * 2 + 4 * (raios.Length + 1)) / 2, 18, MeiaACaixaMaxima);
            var meiaB = Math.Clamp(raios.Max() + 6, 16, 28);
            return new PecaCena("", "", FormaPeca.CaixaComVias, default, meiaA, meiaB, 40, 0);
        }

        /// <summary>Os raios das vias, encolhidos juntos quando não cabem na caixa mais comprida.</summary>
        private static double[] RaiosVias(Equipamento e)
        {
            var vias = e.Dados.Vias;
            if (vias.Count == 0)
                return [Math.Clamp((double)(e.Dados.Diametro ?? 8m) * RaioPorPolegada, 5, 16)];
            // Uma via sem diâmetro: um tamanho médio pela descrição (driver e tweeter menores).
            var raios = vias.Select(v => Math.Clamp((double)(v.Diametro ?? DiametroDaVia(v.Descricao)) * RaioPorPolegada, 5, 16)).ToArray();
            var cabe = 2 * MeiaACaixaMaxima - 4 * (raios.Length + 1);
            var precisa = raios.Sum() * 2;
            return precisa <= cabe ? raios : raios.Select(r => r * cabe / precisa).ToArray();
        }

        private static decimal DiametroDaVia(string descricao)
        {
            var d = Normalizar(descricao);
            return d.Contains("tweeter", StringComparison.Ordinal) || d.Contains("driver", StringComparison.Ordinal) ? 2m : 6.5m;
        }

        /// <summary>As vias no topo, de trás (eixo A positivo) para a frente, cada uma no seu pedaço.</summary>
        private static IReadOnlyList<ViaCena> ViasNoTopo(Equipamento e, Ponto3 base_, double meiaA, double altura)
        {
            var raios = RaiosVias(e);
            var total = raios.Sum() * 2;
            var folga = (2 * meiaA - total) / (raios.Length + 1);
            var vias = new List<ViaCena>();
            var a = meiaA - folga;
            foreach (var r in raios)
            {
                var centro = a - r;
                vias.Add(new ViaCena((base_ + (EixoA * centro)).NaAltura(altura), Math.Max(3, r - 1)));
                a -= 2 * r + folga;
            }
            return vias;
        }

        // ------------------------------------------------------------------ portas

        /// <summary>Uma entrada n de N na face da frente à esquerda (−A), a 1 em cima.</summary>
        private static Ponto3 Entrada(PecaCena p, int n, int total)
        {
            if (p.Cilindro)
                return p.Base + new Ponto3(-(p.Forma == FormaPeca.Corneta ? PecaCena.RaioCorpoCorneta : p.Raio), 0);
            var b = -p.MeiaB + (n - 0.5) * (2 * p.MeiaB / total);
            return p.Base + (EixoA * -p.MeiaA) + (EixoB * b);
        }

        /// <summary>Uma saída n de N na face da frente à direita (+B), a 1 em cima.</summary>
        private static Ponto3 Saida(PecaCena p, int n, int total)
        {
            var a = p.MeiaA - (n - 0.5) * (2 * p.MeiaA / total);
            return p.Base + (EixoB * p.MeiaB) + (EixoA * a);
        }

        private static readonly Ponto3 FaceEntrada = new(-Math.Sqrt(0.5), Math.Sqrt(0.5));
        private static readonly Ponto3 FaceSaida = EixoB;

        private void Portas()
        {
            var processador = _pecas[IdProcessador];
            for (var c = 0; c < Ligacoes.CanaisProcessador; c++)
                _portas.Add(new PortaCena(IdProcessador, TipoPorta.Processador, c, Saida(processador, c + 1, Ligacoes.CanaisProcessador), c, FaceSaida,
                    new HashSet<int> { c }));

            foreach (var e in _lista)
            {
                var p = _pecas[e.Id];
                if (e.Grupo == GrupoEquipamento.AltoFalante)
                {
                    var canais = CanaisDoAltoFalante(e);
                    _portas.Add(new PortaCena(e.Id, TipoPorta.Entrada, 1, Entrada(p, 1, 1), Menor(canais), FaceEntrada, canais));
                    continue;
                }
                for (var i = 0; i < e.Entradas.Count; i++)
                {
                    var canais = CanaisDaFonte(e.Entradas[i].Fonte);
                    _portas.Add(new PortaCena(e.Id, TipoPorta.Entrada, i + 1, Entrada(p, i + 1, e.Entradas.Count), Menor(canais), FaceEntrada, canais));
                }
                if (e.Grupo == GrupoEquipamento.Amplificador)
                {
                    for (var i = 0; i < e.Entradas.Count; i++)
                    {
                        var canais = CanaisDaFonte(e.Entradas[i].Fonte);
                        _portas.Add(new PortaCena(e.Id, TipoPorta.Saida, i + 1, Saida(p, i + 1, e.Entradas.Count), Menor(canais), FaceSaida, canais));
                    }
                    foreach (var b in e.Bridges.Where(b => b.B <= e.Entradas.Count))
                    {
                        var sa = Saida(p, b.A, e.Entradas.Count);
                        var sb = Saida(p, b.B, e.Entradas.Count);
                        var fora = EixoB * 7;
                        var canais = CanaisDoBridge(e, b);
                        _bridges.Add(new BridgeCena(IdBridge(e.Id, b), e.Id, b, [sa, sa + fora, sb + fora, sb], (sa + sb) * 0.5 + fora,
                            Menor(canais), canais));
                    }
                }
                else if (e.Grupo == GrupoEquipamento.Repassa)
                {
                    _portas.Add(new PortaCena(e.Id, TipoPorta.Saida, 1, Saida(p, 1, 1), Menor(CanaisDe(e.Id)), FaceSaida, CanaisDe(e.Id)));
                }
            }
        }

        private IReadOnlySet<int> CanaisDoBridge(Equipamento modulo, Bridge b)
        {
            var canais = new HashSet<int>();
            foreach (var n in new[] { b.A, b.B })
            {
                if (n >= 1 && n <= modulo.Entradas.Count)
                    canais.UnionWith(CanaisDaFonte(modulo.Entradas[n - 1].Fonte));
            }
            return canais;
        }

        private IReadOnlySet<int> CanaisDoAltoFalante(Equipamento e)
        {
            if (e.Ligado is not { } l || !_porId.TryGetValue(l.IdModulo, out var m) || m.Grupo != GrupoEquipamento.Amplificador)
                return Nenhum;
            if (l.Canal is { } c)
                return c >= 1 && c <= m.Entradas.Count ? CanaisDaFonte(m.Entradas[c - 1].Fonte) : Nenhum;
            return l.Bridge is { } b ? CanaisDoBridge(m, b) : Nenhum;
        }

        // ------------------------------------------------------------------ cabos

        /// <summary>Um destino de um cabo: a entrada (a porta) e a peça dela.</summary>
        private sealed record Destino(Ponto3 Porta, string IdPeca, int Numero);

        private void Cabos()
        {
            // As fontes, na ordem: CH1 a CH4, cada saída do grupo 3, cada saída e bridge de módulo.
            var processador = _pecas[IdProcessador];
            var maxU = Enumerable.Range(1, Ligacoes.CanaisProcessador).Max(n => Saida(processador, n, Ligacoes.CanaisProcessador).U);
            for (var c = 0; c < Ligacoes.CanaisProcessador; c++)
            {
                var destinos = new List<Destino>();
                foreach (var e in _lista.Where(e => e.Grupo != GrupoEquipamento.AltoFalante))
                {
                    for (var i = 0; i < e.Entradas.Count; i++)
                    {
                        if (e.Entradas[i].Fonte?.Canal == c)
                            destinos.Add(new Destino(Entrada(_pecas[e.Id], i + 1, e.Entradas.Count), e.Id, i + 1));
                    }
                }
                Ligar(IdProcessador, $"CH{c + 1}", SaidaDoProcessador(c), maxU + 14 + c * 9, destinos, new HashSet<int> { c });
            }

            foreach (var e in _lista.Where(e => e.Grupo == GrupoEquipamento.Repassa))
            {
                var destinos = new List<Destino>();
                foreach (var d in _lista.Where(d => d.Grupo != GrupoEquipamento.AltoFalante))
                {
                    for (var i = 0; i < d.Entradas.Count; i++)
                    {
                        if (d.Entradas[i].Fonte?.IdEquipamento == e.Id)
                            destinos.Add(new Destino(Entrada(_pecas[d.Id], i + 1, d.Entradas.Count), d.Id, i + 1));
                    }
                }
                var saida = Saida(_pecas[e.Id], 1, 1);
                // Cada equipamento da mesma coluna do grupo 3 com o seu barramento, para não se sobreporem.
                Ligar(e.Id, "saida", saida, saida.U + 14 + _indiceNaColuna.GetValueOrDefault(e.Id) * 8, destinos, CanaisDe(e.Id));
            }

            foreach (var m in _lista.Where(e => e.Grupo == GrupoEquipamento.Amplificador))
            {
                var p = _pecas[m.Id];
                var direita = p.Base.U + (p.MeiaA + p.MeiaB) * Math.Sqrt(0.5);
                var alto = _lista.Where(e => e.Grupo == GrupoEquipamento.AltoFalante && e.Ligado is { } l && l.IdModulo == m.Id
                                             && (l.Canal is not null || l.Bridge is not null)).ToArray();
                // Cada saída usada (canal ou bridge), de cima para baixo, com o seu barramento.
                var saidas = alto.Select(e => e.Ligado!).Select(l => l.Canal is { } c ? (Chave: (double)c, Canal: (int?)c, Bridge: (Bridge?)null)
                        : (Chave: l.Bridge!.A + 0.5, Canal: null, Bridge: l.Bridge))
                    .Distinct().OrderBy(s => s.Chave).ToArray();
                for (var i = 0; i < saidas.Length; i++)
                {
                    var s = saidas[i];
                    var destinos = alto
                        .Where(e => s.Canal is { } c ? e.Ligado!.Canal == c : e.Ligado!.Bridge == s.Bridge)
                        .Select(e => new Destino(Entrada(_pecas[e.Id], 1, 1), e.Id, 1)).ToList();
                    if (s.Canal is { } canal)
                    {
                        if (canal < 1 || canal > m.Entradas.Count)
                            continue;
                        Ligar(m.Id, $"saida-{canal}", Saida(p, canal, m.Entradas.Count), direita + 12 + i * 8, destinos,
                            CanaisDaFonte(m.Entradas[canal - 1].Fonte));
                    }
                    else if (_bridges.FirstOrDefault(b => b.IdModulo == m.Id && b.Bridge == s.Bridge) is { } bridge)
                    {
                        Ligar(m.Id, $"bridge-{bridge.Bridge.Rotulo}", bridge.Saida, direita + 12 + i * 8, destinos, bridge.Canais);
                    }
                }
            }
        }

        private Ponto3 SaidaDoProcessador(int canal) => _portas.First(p => p.Tipo == TipoPorta.Processador && p.Numero == canal).Posicao;

        /// <summary>
        /// Os cabos de uma fonte até cada destino: da saída para a direita até o barramento da fonte, pelo
        /// barramento até a fileira da entrada e para a direita até ela. Com mais de um destino, cada ponto
        /// em que o cabo se divide ganha o ponto do Y.
        /// </summary>
        private void Ligar(string idOrigem, string nomeSaida, Ponto3 saida, double barramento, IReadOnlyList<Destino> destinos, IReadOnlySet<int> canais)
        {
            // Sem cabo de ou para quem está à parte (sem ligação: nenhum canal chega).
            if (idOrigem != IdProcessador && _pecas[idOrigem].SemLigacao)
                return;
            destinos = destinos.Where(d => !_pecas[d.IdPeca].SemLigacao).ToArray();
            if (destinos.Count == 0)
                return;
            var canal = Menor(canais);
            foreach (var d in destinos)
            {
                var pontos = new List<Ponto3> { saida };
                if (Math.Abs(d.Porta.V - saida.V) > 0.01)
                {
                    pontos.Add(new Ponto3(barramento, saida.V));
                    pontos.Add(new Ponto3(barramento, d.Porta.V));
                }
                pontos.Add(d.Porta);
                _cabos.Add(new CaboCena($"{idOrigem}:{nomeSaida}>{d.IdPeca}:{d.Numero}", pontos, canal, canais, idOrigem, d.IdPeca));
            }
            if (destinos.Count < 2)
                return;

            // O Y: onde três trechos se encontram no barramento (o tronco, os ramos e o barramento acima e abaixo).
            static double Arredondar(double v) => Math.Round(v, 2);
            var fileiras = destinos.Select(d => d.Porta.V).Append(saida.V).DistinctBy(Arredondar).OrderBy(v => v).ToArray();
            foreach (var v in fileiras)
            {
                var trechos = (Arredondar(v) == Arredondar(saida.V) ? 1 : 0) + (destinos.Any(d => Arredondar(d.Porta.V) == Arredondar(v)) ? 1 : 0)
                              + (Arredondar(v) > Arredondar(fileiras[0]) ? 1 : 0) + (Arredondar(v) < Arredondar(fileiras[^1]) ? 1 : 0);
                if (trechos >= 3)
                    _juncoes.Add(new JuncaoCena(new Ponto3(barramento, v), canal, canais, idOrigem));
            }
        }
    }
}
