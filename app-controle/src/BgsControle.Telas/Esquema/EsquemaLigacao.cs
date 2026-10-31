using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Esquema;

/// <summary>O estado de um canal no esquema do Aplicando (DESIGN, setup-schematic).</summary>
public enum EstadoCanal
{
    /// <summary>Ainda não gravado: cinza {colors.border-sheet}.</summary>
    AindaNao,

    /// <summary>Sendo gravado e relido agora: ciano, com brilho.</summary>
    Aceso,

    /// <summary>Já gravado e relido: luz fraca {colors.channel-done}.</summary>
    Gravado,
}

/// <summary>Uma saída CH1 a CH4 no esquema plano: a porta (14 × 12) na borda do processador e o rótulo.</summary>
/// <param name="Canal">0 a 3 (CH1 = 0).</param>
/// <param name="Y">O centro da porta, no viewBox.</param>
public sealed record PortaLigacao(int Canal, double Y)
{
    public string Rotulo => $"CH{Canal + 1}";
}

/// <summary>Um equipamento do esquema plano (84 × 20), com os canais que o alimentam.</summary>
/// <param name="Y">O topo da caixa, no viewBox.</param>
public sealed record EquipamentoLigacao(string Nome, IReadOnlyList<int> Canais, double Y)
{
    /// <summary>O meio da caixa, onde o fio chega.</summary>
    public double MeioY => Y + EsquemaLigacao.AlturaEquipamento / 2;
}

/// <summary>Um fio curvo da porta do canal até o equipamento que ele alimenta.</summary>
public sealed record FioLigacao(int Canal, double DeY, double AteY);

/// <summary>
/// O esquema de ligação plano do Aplicando (DESIGN, setup-schematic; EXPERIENCE, "Esquema de ligação no
/// Aplicando"; história 10d): o PRO 2.4BT com CH1 a CH4 à esquerda, os equipamentos à direita, um embaixo do
/// outro, e um fio de cada porta até o equipamento que ela alimenta. Montado só com os dados do Setup em uso
/// (equipamentos e ligações), para qualquer Setup. As posições são as do mock (viewBox 230 × 215); quem pinta é a
/// view, que escala.
/// <para>
/// Entram os equipamentos que tocam (os alto-falantes e o grupo 1), como no nome das saídas; um canal que não
/// alimenta nenhum que toca mostra os outros que ele alimenta (um amplificador sem alto-falante, por exemplo).
/// </para>
/// </summary>
public sealed class EsquemaLigacao
{
    public const double Largura = 230;
    public const double Altura = 215;

    /// <summary>O processador: x 10, y 12, 60 × 186, raio 8; rótulo embaixo.</summary>
    public const double ProcessadorX = 10;
    public const double ProcessadorY = 12;
    public const double ProcessadorLargura = 60;
    public const double ProcessadorAltura = 186;
    public const double RotuloProcessadorY = 210;
    public const string NomeProcessador = "PRO 2.4BT";

    /// <summary>As portas: x 58, 14 × 12, a primeira com o centro em y 34, de 46 em 46.</summary>
    public const double PortaX = 58;
    public const double PortaLargura = 14;
    public const double PortaAltura = 12;
    public const double PrimeiraPortaY = 34;
    public const double PassoPorta = 46;

    /// <summary>A coluna dos rótulos CHn, dentro do processador.</summary>
    public const double RotuloPortaX = 34;

    /// <summary>Os equipamentos: x 138, 84 × 20, raio 6, repartidos em 190 de altura a partir de y 14.</summary>
    public const double EquipamentoX = 138;
    public const double LarguraEquipamento = 84;
    public const double AlturaEquipamento = 20;
    public const double AreaEquipamentosY = 14;
    public const double AreaEquipamentosAltura = 190;

    /// <summary>O fio sai da borda direita do processador (x 70) e chega na esquerda do equipamento.</summary>
    public const double FioDeX = 70;

    private EsquemaLigacao(IReadOnlyList<EquipamentoLigacao> equipamentos, IReadOnlyList<FioLigacao> fios, bool link12, bool link34)
    {
        Equipamentos = equipamentos;
        Fios = fios;
        Link12 = link12;
        Link34 = link34;
        Portas = Enumerable.Range(0, Ligacoes.CanaisProcessador).Select(c => new PortaLigacao(c, PortaY(c))).ToArray();
    }

    /// <summary>CH1 a CH4, de cima para baixo.</summary>
    public IReadOnlyList<PortaLigacao> Portas { get; }

    /// <summary>Os equipamentos, na ordem do primeiro canal que os alimenta (e na de criação).</summary>
    public IReadOnlyList<EquipamentoLigacao> Equipamentos { get; }

    /// <summary>Um fio por canal e por equipamento que ele alimenta (o Sub em CH3 e CH4 recebe dois).</summary>
    public IReadOnlyList<FioLigacao> Fios { get; }

    /// <summary>O link 1–2 do Setup (o par escreve junto: os dois canais acendem juntos).</summary>
    public bool Link12 { get; }

    /// <summary>O link 3–4 do Setup.</summary>
    public bool Link34 { get; }

    /// <summary>Sem equipamento: só o processador e as portas.</summary>
    public bool Vazio => Equipamentos.Count == 0;

    /// <summary>O centro da porta do canal (0 a 3).</summary>
    public static double PortaY(int canal) => PrimeiraPortaY + canal * PassoPorta;

    /// <summary>Os canais que acendem juntos com o canal: ele e o vizinho do par com link.</summary>
    public IReadOnlyList<int> Juntos(int canal) => canal switch
    {
        0 or 1 when Link12 => [0, 1],
        2 or 3 when Link34 => [2, 3],
        _ => [canal],
    };

    /// <summary>"CH1 alimenta a JBL L." ou "CH4 não alimenta nenhum equipamento.", por canal (o TalkBack).</summary>
    public string DescreverCanal(int canal)
    {
        var nomes = Equipamentos.Where(e => e.Canais.Contains(canal)).Select(e => e.Nome).ToArray();
        return nomes.Length == 0
            ? $"CH{canal + 1} não alimenta nenhum equipamento."
            : $"CH{canal + 1} alimenta {Ligacoes.Lista(nomes)}.";
    }

    /// <summary>Todos os canais, um por frase.</summary>
    public string Descricao => string.Join(" ", Enumerable.Range(0, Ligacoes.CanaisProcessador).Select(DescreverCanal));

    /// <summary>Sem Setup: só o processador e as portas.</summary>
    public static EsquemaLigacao Nenhum { get; } = new([], [], false, false);

    /// <summary>Monta o esquema a partir dos equipamentos do Setup (só lê).</summary>
    public static EsquemaLigacao Montar(IReadOnlyList<Equipamento>? lista)
    {
        if (lista is null || lista.Count == 0)
            return Nenhum;
        var porCanal = Enumerable.Range(0, Ligacoes.CanaisProcessador)
            .Select(c => Ligacoes.Alimentados(lista, [c]))
            .ToArray();
        var escolhidos = new List<Equipamento>();
        for (var c = 0; c < porCanal.Length; c++)
        {
            var alimentados = porCanal[c];
            var tocam = alimentados.Where(e => e.Toca).ToArray();
            foreach (var e in tocam.Length > 0 ? tocam : alimentados)
            {
                if (!escolhidos.Any(x => x.Id == e.Id))
                    escolhidos.Add(e);
            }
        }
        var n = escolhidos.Count;
        var passo = n == 0 ? 0 : AreaEquipamentosAltura / n;
        var equipamentos = escolhidos.Select((e, i) => new EquipamentoLigacao(
                e.Nome,
                Enumerable.Range(0, Ligacoes.CanaisProcessador).Where(c => porCanal[c].Any(x => x.Id == e.Id)).ToArray(),
                AreaEquipamentosY + i * passo + passo / 2 - AlturaEquipamento / 2))
            .ToArray();
        var fios = equipamentos.SelectMany(e => e.Canais.Select(c => new FioLigacao(c, PortaY(c), e.MeioY))).ToArray();
        var (link12, link34) = Ligacoes.Links(lista);
        return new EsquemaLigacao(equipamentos, fios, link12, link34);
    }
}
