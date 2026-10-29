using System.Globalization;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Uma linha da ficha na folha de equipamento (história 10c; DESIGN, folha-de-equipamento e
/// selo-de-origem): o rótulo com a unidade ("Fs (Hz)"), o campo e o selo de origem ("oficial ✓",
/// "outra fonte", "medido ✓", "estimado", "digitado"). Tocar no selo abre a escolha da origem (as 5) e
/// o campo Fonte. Um valor digitado ou mudado vira "digitado", a menos que o usuário escolha outra
/// origem; um valor que volta a ser o carregado volta à origem dele. Um campo vazio não tem selo (e
/// esvaziar o campo esquece a origem e a fonte escolhidas).
/// </summary>
public sealed class DadoViewModel : TelaBase
{
    /// <summary>A ordem das origens na escolha.</summary>
    public static IReadOnlyList<OrigemDado> OrdemOrigens { get; } =
        [OrigemDado.Oficial, OrigemDado.OutraFonte, OrigemDado.Medido, OrigemDado.Estimado, OrigemDado.Digitado];

    public const string DicaFonteOficial = "Endereço do manual ou da ficha";
    public const string DicaFonteOutra = "Endereço ou descrição";
    public const string RotuloFonte = "Fonte";
    public const string DicaSelo = "Toque para mudar a origem.";
    public const string DicaOrigem = "Toque para escolher esta origem.";

    /// <summary>"oficial", "outra fonte", "medido", "estimado", "digitado".</summary>
    public static string RotuloOrigem(OrigemDado origem) => origem switch
    {
        OrigemDado.Oficial => "oficial",
        OrigemDado.OutraFonte => "outra fonte",
        OrigemDado.Medido => "medido",
        OrigemDado.Estimado => "estimado",
        _ => "digitado",
    };

    /// <summary>O texto do selo: só oficial e medido levam o ✓ ("oficial ✓", "estimado").</summary>
    public static string TextoSelo(OrigemDado origem) =>
        origem is OrigemDado.Oficial or OrigemDado.Medido ? $"{RotuloOrigem(origem)} ✓" : RotuloOrigem(origem);

    /// <summary>
    /// O valor carregado na abertura da folha (do equipamento editado ou da base de modelos), com a
    /// origem. A origem só vale com o modelo de onde ele veio (<paramref name="Chave"/>); a do equipamento
    /// editado, também com o mesmo grupo.
    /// </summary>
    private sealed record Carregado(decimal? Numero, string? Texto, OrigemDado Origem, string? Fonte, bool DoEditado, string? Chave);

    private readonly EquipamentosViewModel _tela;
    private string _texto = "";
    private Carregado? _carregado;
    private OrigemDado? _escolhida;
    private string? _fonteDigitada;
    private bool _origemAberta;
    private IReadOnlyList<EscolhaViewModel>? _origens;

    internal DadoViewModel(string nome, string rotulo, string? unidade, bool numerico, bool soLeitura,
        EquipamentosViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _tela = tela;
        Nome = nome;
        Rotulo = rotulo;
        Unidade = unidade;
        Numerico = numerico;
        SoLeitura = soLeitura;
        AlternarOrigem = new Comando(() =>
        {
            if (Origem is null)
                return;
            if (_origemAberta)
                FecharOrigem();
            else
                _tela.AbrirOrigem(this);
        });
    }

    /// <summary>A chave do campo, a mesma do <c>setups.json</c> ("fs", "tipoCaixa").</summary>
    public string Nome { get; }

    /// <summary>"Fs", "Impedância" (o nome das mensagens de erro).</summary>
    public string Rotulo { get; }

    /// <summary>"Hz", "Ω", ou null (Qes, Qms, o tipo da caixa).</summary>
    public string? Unidade { get; }

    /// <summary>"Fs (Hz)", "Qes".</summary>
    public string RotuloTela => Unidade is null ? Rotulo : $"{Rotulo} ({Unidade})";

    /// <summary>Um número maior que zero (o tipo da caixa é texto).</summary>
    public bool Numerico { get; }

    /// <summary>Não se escreve aqui (os canais do módulo vêm do "Onde está ligado").</summary>
    public bool SoLeitura { get; }

    /// <summary>O campo, como texto (vírgula ou ponto). Vazio = não se sabe.</summary>
    public string Texto
    {
        get => _texto;
        set
        {
            if (SoLeitura)
                return;
            DefinirTexto(value);
        }
    }

    /// <summary>A origem que vale para o valor do campo, ou null com o campo vazio.</summary>
    public OrigemDado? Origem => Atual()?.Origem;

    /// <summary>A fonte (endereço ou descrição), ou null.</summary>
    public string? Fonte => Atual()?.Fonte is { } f && !string.IsNullOrWhiteSpace(f) ? f.Trim() : null;

    /// <summary>"oficial ✓", "digitado"… ou null com o campo vazio (um campo vazio não tem selo).</summary>
    public string? Selo => Origem is { } o ? TextoSelo(o) : null;

    /// <summary>Oficial ou medido.</summary>
    public bool Verificado => Origem is OrigemDado.Oficial or OrigemDado.Medido;

    /// <summary>Para o leitor de tela: "Origem de Fs: oficial, verificado." (ou null sem selo).</summary>
    public string? DescricaoSelo => Origem is { } o
        ? $"Origem de {Rotulo}: {RotuloOrigem(o)}{(Verificado ? ", verificado" : "")}."
        : null;

    /// <summary>"Toque para mudar a origem."</summary>
    public string DicaSeloTela => DicaSelo;

    /// <summary>Tocar no selo abre (ou fecha) a escolha da origem.</summary>
    public Comando AlternarOrigem { get; }

    /// <summary>A escolha da origem e o campo Fonte estão abertos (um por vez na folha).</summary>
    public bool OrigemAberta => _origemAberta;

    /// <summary>As 5 origens como pílulas, a que vale em branco (refeitas só quando a origem muda).</summary>
    public IReadOnlyList<EscolhaViewModel> Origens =>
        _origens ??= OrdemOrigens.Select(o => new EscolhaViewModel(RotuloOrigem(o), null, o == Origem, () => EscolherOrigem(o))).ToArray();

    /// <summary>"Fonte".</summary>
    public string TituloFonte => RotuloFonte;

    /// <summary>
    /// O campo Fonte (até 120 caracteres). Escrever nele não trava a origem: mudar o número depois
    /// ainda faz o dado virar "digitado", a menos que a origem tenha sido escolhida no selo.
    /// </summary>
    public string TextoFonte
    {
        get => Atual()?.Fonte ?? "";
        set
        {
            if (TextoFonte == value)
                return;
            _fonteDigitada = value;
            AvisarOrigem();
        }
    }

    /// <summary>"Endereço do manual ou da ficha" (oficial) ou "Endereço ou descrição".</summary>
    public string DicaFonte => Origem == OrigemDado.Oficial ? DicaFonteOficial : DicaFonteOutra;

    /// <summary>Escolhe a origem do valor do campo (a fonte que está no campo fica).</summary>
    public void EscolherOrigem(OrigemDado origem)
    {
        if (Atual() is not { } atual)
            return;
        _fonteDigitada = atual.Fonte;
        _escolhida = origem;
        AvisarOrigem();
    }

    // ------------------------------------------------------------------ a folha

    /// <summary>Esvazia o campo (equipamento novo).</summary>
    internal void Limpar()
    {
        _carregado = null;
        _escolhida = null;
        _fonteDigitada = null;
        _origemAberta = false;
        _texto = "";
        AvisarTudo();
    }

    /// <summary>O valor do equipamento editado, com a origem (que só vale com o mesmo grupo e modelo).</summary>
    internal void Carregar(DadoTecnico? dado) => Carregar(dado, doEditado: true, chave: null);

    /// <summary>O valor da base de modelos, com a origem dela (que só vale com o modelo dela).</summary>
    internal void CarregarDaBase(DadoTecnico dado, string? chave) => Carregar(dado, doEditado: false, chave);

    /// <summary>O tipo da caixa, do equipamento editado ou da base (com a chave do modelo).</summary>
    internal void Carregar(DadoTexto? dado, bool doEditado, string? chave)
    {
        _carregado = dado is null ? null : new Carregado(null, dado.Valor, dado.Origem, dado.Fonte, doEditado, chave);
        _escolhida = null;
        _fonteDigitada = null;
        _origemAberta = false;
        _texto = dado?.Valor ?? "";
        AvisarTudo();
    }

    private void Carregar(DadoTecnico? dado, bool doEditado, string? chave)
    {
        _carregado = dado is null ? null : new Carregado(dado.Valor, null, dado.Origem, dado.Fonte, doEditado, chave);
        _escolhida = null;
        _fonteDigitada = null;
        _origemAberta = false;
        _texto = dado is null ? "" : TextoNumero(dado.Valor);
        AvisarTudo();
    }

    /// <summary>Muda o campo (os canais do módulo vêm daqui, não do teclado). Esvaziar esquece a origem e a fonte escolhidas.</summary>
    internal void DefinirTexto(string valor)
    {
        if (_texto == valor)
            return;
        _texto = valor;
        if (string.IsNullOrWhiteSpace(valor))
        {
            _escolhida = null;
            _fonteDigitada = null;
            _origemAberta = false;
        }
        AvisarTudo();
    }

    internal void AbrirOrigemInterno()
    {
        if (_origemAberta)
            return;
        _origemAberta = true;
        Avisar(nameof(OrigemAberta));
        Avisar(nameof(Origens));
    }

    internal void FecharOrigem()
    {
        if (!_origemAberta)
            return;
        _origemAberta = false;
        Avisar(nameof(OrigemAberta));
    }

    /// <summary>
    /// A origem pode ter mudado (o grupo ou o modelo da folha, o campo): o selo acompanha. As pílulas
    /// das origens só são avisadas com a escolha aberta.
    /// </summary>
    internal void AvisarOrigem()
    {
        _origens = null;
        Avisar(nameof(Origem));
        Avisar(nameof(Fonte));
        Avisar(nameof(Selo));
        Avisar(nameof(Verificado));
        Avisar(nameof(DescricaoSelo));
        Avisar(nameof(TextoFonte));
        Avisar(nameof(DicaFonte));
        if (_origemAberta)
            Avisar(nameof(Origens));
    }

    /// <summary>
    /// O dado do campo numérico, com a origem e a fonte; null vazio. Um texto que não é número maior que
    /// zero, ou uma fonte inválida, dá o erro do campo.
    /// </summary>
    internal (DadoTecnico? Dado, string? Erro) MontarNumero()
    {
        if (string.IsNullOrWhiteSpace(_texto))
            return (null, null);
        if (Numero(_texto) is not { } v)
            return (null, EquipamentosViewModel.MsgNumero(Rotulo));
        var (origem, fonte, erro) = OrigemMontada();
        return erro is not null ? (null, erro) : (new DadoTecnico(v, origem, fonte), null);
    }

    /// <summary>O dado do campo de texto (o tipo da caixa), com a origem e a fonte; null vazio.</summary>
    internal (DadoTexto? Dado, string? Erro) MontarTexto()
    {
        if (string.IsNullOrWhiteSpace(_texto))
            return (null, null);
        if (Ligacoes.TextoValido(_texto) is not { } valor)
            return (null, Ligacoes.MsgTipoCaixaInvalido);
        var (origem, fonte, erro) = OrigemMontada();
        return erro is not null ? (null, erro) : (new DadoTexto(valor, origem, fonte), null);
    }

    private (OrigemDado Origem, string? Fonte, string? Erro) OrigemMontada()
    {
        var atual = Atual()!.Value;
        if (string.IsNullOrWhiteSpace(atual.Fonte))
            return (atual.Origem, null, null);
        return Ligacoes.TextoLongoValido(atual.Fonte) is { } limpa
            ? (atual.Origem, limpa, null)
            : (atual.Origem, null, EquipamentosViewModel.MsgFonte(Rotulo));
    }

    /// <summary>A origem e a fonte que valem agora; null com o campo vazio.</summary>
    private (OrigemDado Origem, string? Fonte)? Atual()
    {
        if (string.IsNullOrWhiteSpace(_texto))
            return null;
        if (_escolhida is { } escolhida)
            return (escolhida, _fonteDigitada);
        if (_carregado is { } c && Igual(c) && (c.DoEditado ? _tela.OrigemDoEditadoVale : _tela.ModeloE(c.Chave)))
            return (c.Origem, _fonteDigitada ?? c.Fonte);
        return (OrigemDado.Digitado, _fonteDigitada);
    }

    private bool Igual(Carregado c) => Numerico
        ? Numero(_texto) is { } v && v == c.Numero
        : string.Equals(_texto.Trim(), c.Texto, StringComparison.Ordinal);

    private void AvisarTudo()
    {
        Avisar(nameof(Texto));
        Avisar(nameof(OrigemAberta));
        AvisarOrigem();
    }

    /// <summary>Um número maior que zero (vírgula ou ponto), ou null.</summary>
    internal static decimal? Numero(string texto) =>
        decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) && v > 0m
            ? v
            : null;

    /// <summary>Um número para o campo, com vírgula e todas as casas que ele tem, sem arredondar ("0,0453").</summary>
    internal static string TextoNumero(decimal v) =>
        v.ToString("0.############################", CultureInfo.InvariantCulture).Replace('.', ',');
}

/// <summary>Um bloco da ficha na folha ("ALTO-FALANTE", "CAIXA", "MÓDULO"), com as linhas que valem para o grupo.</summary>
public sealed record SecaoDadosViewModel(string Titulo, IReadOnlyList<DadoViewModel> Dados);
