using System.Globalization;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Um dado da proposta na folha (história 14b): o rótulo, o valor com a unidade, a origem, a fonte, o aviso (o que não
/// fechou) e a escolha do usuário (usar, recusar ou ainda nada).
/// </summary>
public sealed class DadoPropostoViewModel : TelaBase
{
    private bool? _usar;

    internal DadoPropostoViewModel(DadoProposto dado, DadoTecnico? atual, Action mudou, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Dado = dado;
        var (rotulo, unidade) = EquipamentosViewModel.RotuloDoCampo(dado.Campo);
        Rotulo = rotulo;
        Valor = PropostaFichaViewModel.Texto(dado.Valor, unidade);
        Origem = DadoViewModel.TextoSelo(dado.Origem);
        DadoAtual = atual;
        ValorAtual = atual is null ? null : PropostaFichaViewModel.Texto(atual.Valor, unidade);
        OrigemAtual = atual is null ? null : DadoViewModel.TextoSelo(atual.Origem);
        TextoAtual = ValorAtual is null ? null : PropostaFichaViewModel.TextoAgoraDe(ValorAtual, OrigemAtual!);
        Usar = new Comando(() =>
        {
            Escolha = true;
            mudou();
        });
        Recusar = new Comando(() =>
        {
            Escolha = false;
            mudou();
        });
    }

    public DadoProposto Dado { get; }

    /// <summary>O nome do campo ("fs").</summary>
    public string Campo => Dado.Campo;

    /// <summary>"Fs".</summary>
    public string Rotulo { get; }

    /// <summary>"46 Hz".</summary>
    public string Valor { get; }

    /// <summary>"oficial ✓" ou "outra fonte".</summary>
    public string Origem { get; }

    /// <summary>O dado que a ficha tem agora no campo, ou null (em branco).</summary>
    public DadoTecnico? DadoAtual { get; }

    /// <summary>O valor que a ficha tem agora, ao lado do proposto ("44 Hz"), ou null (em branco).</summary>
    public string? ValorAtual { get; }

    /// <summary>A origem do valor de agora ("medido ✓", "digitado"), ou null.</summary>
    public string? OrigemAtual { get; }

    /// <summary>História 14c: "Agora: 44 Hz (medido ✓)" na folha, quando a ficha já tem valor; senão null.</summary>
    public string? TextoAtual { get; }

    /// <summary>
    /// O valor de agora foi medido na instalação: "Usar todos" não o troca (só o "Usar" deste dado, o toque do usuário).
    /// </summary>
    public bool AtualMedido => DadoAtual?.Origem == OrigemDado.Medido;

    /// <summary>O endereço ou a descrição de onde o dado veio.</summary>
    public string Fonte => Dado.Fonte;

    /// <summary>O que não fechou ("Qts não fecha com Qes e Qms"), ou null.</summary>
    public string? Aviso => Dado.Aviso;

    /// <summary>true: "Usar"; false: "Recusar"; null: ainda sem escolha (não entra no Salvar).</summary>
    public bool? Escolha
    {
        get => _usar;
        internal set
        {
            if (_usar == value)
                return;
            _usar = value;
            Avisar();
            Avisar(nameof(Usado));
            Avisar(nameof(Recusado));
        }
    }

    public bool Usado => _usar == true;

    public bool Recusado => _usar == false;

    /// <summary>"Usar".</summary>
    public Comando Usar { get; }

    /// <summary>"Recusar".</summary>
    public Comando Recusar { get; }
}

/// <summary>
/// A folha da proposta de ficha (história 14b; spec-agente-memoria CAP-6): a ficha encontrada para um modelo que a base não
/// tinha, cada dado com o rótulo, o valor, a origem, a fonte e o aviso, com "Usar" e "Recusar" por dado e "Usar todos" /
/// "Recusar todos". Cada dado mostra, ao lado, o valor que a ficha tem agora e a origem dele; "Usar todos" não troca um valor
/// medido (só o "Usar" daquele dado). Só o "Salvar" grava, e só os usados, na ficha do equipamento e na base de modelos; o
/// recusado fica em branco (o que a ficha já tem não muda). Fechar sem salvar não grava nada. A XAML (PropostaFichaPage,
/// uma folha global da casca) é da história 14c.
/// </summary>
public sealed class PropostaFichaViewModel : TelaBase, IDisposable
{
    public const string TextoUsarBotao = "Usar";
    public const string TextoRecusarBotao = "Recusar";
    public const string TextoUsarTodosBotao = "Usar todos";
    public const string TextoRecusarTodosBotao = "Recusar todos";
    public const string TextoSalvarBotao = "Salvar";
    public const string TextoFecharBotao = "Fechar";
    public const string TextoExplica = "Confira cada dado. Só o que você usar entra na ficha; o que recusar fica em branco.";

    /// <summary>História 14c: "Agora: 44 Hz (medido ✓)", o valor que a ficha tem agora, ao lado do proposto.</summary>
    public static string TextoAgoraDe(string valor, string origem) => $"Agora: {valor} ({origem})";

    /// <summary>"Ficha do ABC 123".</summary>
    public static string TituloDe(string modelo) => $"Ficha do {modelo}";

    /// <summary>"2 dados salvos na ficha do Sub da sala." (1: "1 dado salvo …").</summary>
    public static string MsgSalvos(int n, string equipamento) =>
        n == 1 ? $"1 dado salvo na ficha do {equipamento}." : $"{n} dados salvos na ficha do {equipamento}.";

    internal static readonly CultureInfo Tela = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"46 Hz", "0,000012 mm": o número com todos os algarismos dele (sem zeros sobrando) e a unidade.</summary>
    public static string Texto(decimal valor, string? unidade)
    {
        // Dividir por 1,000… tira os zeros à direita sem arredondar.
        var numero = (valor / 1.0000000000000000000000000000m).ToString(Tela);
        return unidade is null ? numero : $"{numero} {unidade}";
    }

    private readonly PropostaFicha _ficha;
    private Proposta? _proposta;
    private IReadOnlyList<DadoPropostoViewModel> _dados = [];
    private string? _erro;
    private string? _aviso;
    private bool _salvando;

    public PropostaFichaViewModel(PropostaFicha ficha, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _ficha = ficha;
        UsarTodos = new Comando(() => EscolherTodos(true));
        RecusarTodos = new Comando(() => EscolherTodos(false));
        Salvar = new Comando(SalvarAsync);
        Fechar = new Comando(AoFechar);
        _ficha.Mudou += AoMudar;
        Atualizar();
    }

    /// <summary>A folha aparece (há uma proposta esperando o toque).</summary>
    public bool Aberta => _proposta is not null;

    /// <summary>"Ficha do ABC 123", ou null.</summary>
    public string? Titulo => _proposta is { } p ? TituloDe(p.Pedido.Modelo) : null;

    /// <summary>O nome do equipamento ("Sub da sala"), ou null.</summary>
    public string? Equipamento => _proposta?.NomeEquipamento;

    public string Explica => TextoExplica;

    /// <summary>Os dados da proposta, na ordem em que chegaram.</summary>
    public IReadOnlyList<DadoPropostoViewModel> Dados => _dados;

    public string TextoUsar => TextoUsarBotao;
    public string TextoRecusar => TextoRecusarBotao;
    public string TextoUsarTodos => TextoUsarTodosBotao;
    public string TextoRecusarTodos => TextoRecusarTodosBotao;
    public string TextoSalvar => TextoSalvarBotao;
    public string TextoFechar => TextoFecharBotao;

    /// <summary>Quantos dados estão marcados para usar.</summary>
    public int Usados => _dados.Count(d => d.Usado);

    /// <summary>O Salvar está andando.</summary>
    public bool Salvando
    {
        get => _salvando;
        private set
        {
            if (_salvando == value)
                return;
            _salvando = value;
            Avisar();
        }
    }

    /// <summary>Por que o Salvar não gravou, ou null.</summary>
    public string? Erro
    {
        get => _erro;
        private set
        {
            if (_erro == value)
                return;
            _erro = value;
            Avisar();
        }
    }

    /// <summary>O aviso depois de salvar ("2 dados salvos na ficha do Sub da sala."). Avisa a cada vez.</summary>
    public string? AvisoFinal
    {
        get => _aviso;
        private set
        {
            _aviso = value;
            Avisar();
        }
    }

    /// <summary>"Usar todos".</summary>
    public Comando UsarTodos { get; }

    /// <summary>"Recusar todos".</summary>
    public Comando RecusarTodos { get; }

    /// <summary>"Salvar": só os usados, na ficha e na base de modelos.</summary>
    public Comando Salvar { get; }

    /// <summary>Fecha a folha sem gravar nada.</summary>
    public Comando Fechar { get; }

    /// <summary>"Usar todos" pula os dados cujo valor de agora foi medido (eles pedem o "Usar" de cada um); "Recusar todos" marca todos.</summary>
    private void EscolherTodos(bool usar)
    {
        foreach (var d in _dados)
        {
            if (usar && d.AtualMedido)
                continue;
            d.Escolha = usar;
        }
        AoEscolher();
    }

    private void AoEscolher() => Avisar(nameof(Usados));

    private async Task SalvarAsync()
    {
        if (_proposta is null || Salvando)
            return;
        Salvando = true;
        var usados = _dados.Where(d => d.Usado).Select(d => d.Campo).ToArray();
        var nome = _proposta.NomeEquipamento;
        ResultadoEquipamento r;
        try
        {
            r = await _ficha.SalvarAsync(usados);
        }
        finally
        {
            Salvando = false;
        }
        if (!r.Ok)
        {
            Erro = r.Mensagem ?? r.Motivo;
            return;
        }
        Erro = null;
        if (usados.Length > 0)
        {
            var aviso = MsgSalvos(usados.Length, nome);
            NaTela(() => AvisoFinal = aviso);
        }
        NaTela(Atualizar);
    }

    private void AoFechar()
    {
        if (Salvando)
            return;
        _ficha.Fechar();
        Erro = null;
        NaTela(Atualizar);
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        var atual = _ficha.Atual;
        if (ReferenceEquals(atual, _proposta))
            return;
        _proposta = atual;
        _dados = atual is null ? [] : atual.Dados.Select(d => new DadoPropostoViewModel(d, _ficha.DadoAtual(atual, d.Campo), AoEscolher, Despachante, Relogio)).ToArray();
        Erro = null;
        Avisar(nameof(Aberta));
        Avisar(nameof(Titulo));
        Avisar(nameof(Equipamento));
        Avisar(nameof(Dados));
        Avisar(nameof(Usados));
    }

    public void Dispose() => _ficha.Mudou -= AoMudar;
}
