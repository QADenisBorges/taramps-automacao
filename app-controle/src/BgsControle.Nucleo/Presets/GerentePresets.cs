using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Presets;

/// <summary>
/// Os presets no celular (CAP-5): só o EQ de 15 bandas (<c>020</c>). Criar (das bandas atuais),
/// atualizar as bandas de um próprio, aplicar, renomear, apagar e exportar. Os próprios ficam em
/// <c>presets.json</c> nos arquivos privados (formato 3). Não há preset herdado
/// (<see cref="ConhecimentoHerdado.Presets"/> é vazio, decisão do Denis de 29/10/2026); as travas de
/// herdado continuam valendo para qualquer preset marcado como herdado.
/// <list type="bullet">
/// <item><b>Criar</b> e <b>atualizar</b> não mandam nada ao aparelho. O preset novo nasce na folha do
/// Salvar (<see cref="Setups.GerenteSetups"/>).</item>
/// <item><b>Aplicar</b> passa pela <see cref="CopiaAntes"/> (regra 3 de <c>failure-modes.md</c>): guarda
/// a cópia, escreve só as bandas diferentes, uma de cada vez, relê tudo e confere. Se cair no meio,
/// ao reconectar o estado fica "aplicado pela metade", com Terminar (refaz, com a mesma cópia) e
/// Voltar à cópia (da <see cref="CopiaAntes"/>). Depois de aplicar bem, avisa quem ouve
/// (<see cref="AoAplicar"/>): a memória do celular em uso passa a lembrar o preset.</item>
/// <item>Renomear e apagar valem só para os próprios e não mandam nada ao aparelho. Um preset lembrado
/// por alguma memória não se apaga (<see cref="Lembrados"/>).</item>
/// </list>
/// Um <c>presets.json</c> nos formatos 1 ou 2 (de antes de 29/10/2026) começa do zero: os presets
/// próprios somem e o arquivo é regravado no formato 3 (decisão D2 do Denis). Todas as operações do
/// gerente são exclusivas entre si.
/// </summary>
public sealed class GerentePresets
{
    /// <summary>O formato do <c>presets.json</c>: o 3 é o do começo do zero (história 8f); os presets têm as 15 bandas.</summary>
    public const int Formato = 3;

    /// <summary>O formato 1 (os 84 valores do Marco 1), de antes de 29/10/2026: lido como nenhum preset e regravado no 3.</summary>
    public const int FormatoAntigo = 1;

    /// <summary>O formato 2 (as 15 bandas, com o Cinema herdado), de antes de 29/10/2026: lido como nenhum preset e regravado no 3.</summary>
    public const int Formato2 = 2;

    /// <summary>O código das 15 bandas do EQ gráfico.</summary>
    public const string CodigoBandas = "020";

    /// <summary>O tipo registrado na <see cref="CopiaAntes"/>; o alvo da marca é o id do preset.</summary>
    public const string TipoAplicar = "aplicar-preset";

    public const int TamanhoMaximoNome = 24;

    public const string MsgNomeInvalido = "Use de 1 a 24 letras, números, espaço, _ ou -";
    public const string MsgNomeRepetido = "Já existe um preset com esse nome.";
    public const string MsgNaoGuardou = "Não consegui guardar o preset no celular.";
    public const string MsgOcupado = "Uma operação de preset já está em andamento.";
    public const string MsgNaoExiste = "O preset não existe mais.";
    public const string MsgNaoExisteTerminar = "O preset não existe mais; use Voltar à cópia.";
    public const string MsgHerdado = "Preset herdado não muda.";
    public const string MsgHerdadoNaoSobrescrito = "Preset herdado não é sobrescrito.";
    public const string MsgCaiuAplicar = "A conexão caiu no meio do aplicar. Ao reconectar, o app mostra o que ficou.";

    public static string MsgIncompleta(int n) => n == 1
        ? "A leitura está incompleta (1 banda sem valor)."
        : $"A leitura está incompleta ({n} bandas sem valor).";

    public static string MsgCriado(string nome) => $"Preset {nome} criado.";

    public static string MsgAtualizado(string nome) => $"Preset {nome} atualizado.";

    public static string MsgJaAplicado(string nome) => $"Preset {nome} já está aplicado.";

    public static string MsgAplicado(string nome) => $"Preset {nome} aplicado.";

    public static string MsgAplicadoDiferente(string nome, int n) => n == 1
        ? $"Preset {nome} aplicado, mas 1 banda ficou diferente."
        : $"Preset {nome} aplicado, mas {n} bandas ficaram diferentes.";

    public static string MsgNaoEscritos(int n, string? motivo) => n == 1
        ? $"1 banda não foi escrita: {motivo}"
        : $"{n} bandas não foram escritas: {motivo}";

    public static string MsgRenomeado(string nome) => $"Preset renomeado para {nome}.";

    public static string MsgApagado(string nome) => $"Preset {nome} apagado.";

    /// <summary>"Lembrado por 1 memória." / "Lembrado por 2 memórias."</summary>
    public static string MsgLembrado(int n) => n == 1 ? "Lembrado por 1 memória." : $"Lembrado por {n} memórias.";

    /// <summary>As consultas das 15 bandas (<c>020</c>, banda 0 a 14), na ordem do catálogo.</summary>
    public static IReadOnlyList<Consulta> ConsultasBandas { get; } =
        Catalogo.ConsultasMarco1.Where(c => c.Parametro.Codigo == CodigoBandas).ToArray();

    /// <summary>O EQ plano: as 15 bandas em 0,0 dB (a referência do Salvar sem preset lembrado).</summary>
    public static IReadOnlyList<ValorCopiado> BandasPlanas { get; } =
        ConsultasBandas.Select(c => new ValorCopiado(CodigoBandas, c.Indice, "+0.0")).ToArray();

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly LeituraAutomatica _leitura;
    private readonly CopiaAntes _copia;
    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private IReadOnlyList<Preset> _proprios = [];
    private string? _aviso;
    private int _ocupado;

    public GerentePresets(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        IRepositorioTexto repositorio,
        LogBle log,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _leitura = leitura;
        _copia = copia;
        _repositorio = repositorio;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;

        // Ao reconectar com a marca de um aplicar: pela metade (Terminar ou Voltar à cópia).
        _copia.Registrar(TipoAplicar,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await AplicarInternoAsync(op.Alvo, refazendo: true)));

        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>presets.json</c> foi lido (ausente ou estragado = nenhum próprio).</summary>
    public Task Carregada { get; }

    /// <summary>A lista ou o andamento mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>
    /// Quantas memórias do celular lembram o preset (posto pelo <see cref="GerenteSetups"/>). Sem
    /// ninguém, nenhuma.
    /// </summary>
    internal Func<string, int>? Lembrados { get; set; }

    /// <summary>
    /// Chamado depois de um aplicar que terminou bem (também o "já está aplicado" e o Terminar), antes
    /// de o resultado voltar: o <see cref="GerenteSetups"/> faz a memória do celular em uso lembrar o preset.
    /// </summary>
    internal Func<Preset, Task>? AoAplicar { get; set; }

    /// <summary>Uma operação do gerente está andando.</summary>
    public bool Ocupado => Volatile.Read(ref _ocupado) == 1;

    /// <summary>A frase da última operação ("Preset Rock aplicado."), ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>Os presets criados pelo Denis, do mais antigo ao mais novo.</summary>
    public IReadOnlyList<Preset> Proprios
    {
        get
        {
            lock (_trava)
                return _proprios;
        }
    }

    /// <summary>A lista inteira: os herdados primeiro, depois os próprios.</summary>
    public IReadOnlyList<Preset> Presets => [.. ConhecimentoHerdado.Presets, .. Proprios];

    /// <summary>O preset (herdado ou próprio) pelo id, ou null.</summary>
    public Preset? Obter(string? id) => id is null ? null : Presets.FirstOrDefault(p => p.Id == id);

    /// <summary>Quantas memórias do celular lembram o preset.</summary>
    public int QuantasLembram(string id) => Lembrados?.Invoke(id) ?? 0;

    /// <summary>Por que o preset não se apaga ("Preset herdado não muda.", "Lembrado por 2 memórias."), ou null.</summary>
    public string? MotivoTravaApagar(string id)
    {
        if (Obter(id) is { Herdado: true })
            return MsgHerdado;
        var n = QuantasLembram(id);
        return n > 0 ? MsgLembrado(n) : null;
    }

    // ------------------------------------------------------------------ bandas

    /// <summary>
    /// As 15 bandas do espelho, na ordem do catálogo, e quantas não têm valor (uma leitura que falhou).
    /// </summary>
    public static (IReadOnlyList<ValorCopiado> Bandas, int Faltam) TirarBandas(IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        var bandas = new List<ValorCopiado>();
        var faltam = 0;
        foreach (var c in ConsultasBandas)
        {
            if (espelho.TryGetValue(new Endereco(CodigoBandas, c.Indice), out var v))
                bandas.Add(new ValorCopiado(CodigoBandas, c.Indice, v.NoFio));
            else
                faltam++;
        }
        return (bandas, faltam);
    }

    /// <summary>As bandas guardadas e as do espelho são as mesmas (as 15, banda por banda).</summary>
    public static bool MesmasBandas(IReadOnlyList<ValorCopiado> bandas, IReadOnlyDictionary<Endereco, Valor> espelho) =>
        FaltamEm(bandas) == 0 && Camadas.Diferentes(bandas, espelho).Count == 0;

    /// <summary>O primeiro preset (os herdados primeiro) com as mesmas 15 bandas do espelho, ou null.</summary>
    public Preset? ComAsBandas(IReadOnlyDictionary<Endereco, Valor> espelho) =>
        Presets.FirstOrDefault(p => MesmasBandas(p.Valores, espelho));

    /// <summary>Quantas das 15 bandas a lista não traz.</summary>
    private static int FaltamEm(IReadOnlyList<ValorCopiado> valores)
    {
        var tem = valores.Where(v => v.Codigo == CodigoBandas).Select(v => v.Indice).ToHashSet();
        return ConsultasBandas.Count(c => !tem.Contains(c.Indice));
    }

    /// <summary>Só as 15 bandas, uma vez cada, na ordem do catálogo (o que mais vier é descartado).</summary>
    private static IReadOnlyList<ValorCopiado> SoBandas(IEnumerable<ValorCopiado> valores)
    {
        var porIndice = new Dictionary<int, ValorCopiado>();
        foreach (var v in valores)
        {
            if (v.Codigo == CodigoBandas && v.Indice is { } i)
                porIndice.TryAdd(i, v);
        }
        return ConsultasBandas
            .Select(c => porIndice.GetValueOrDefault(c.Indice!.Value))
            .OfType<ValorCopiado>()
            .ToArray();
    }

    // ------------------------------------------------------------------ nome

    /// <summary>
    /// O nome do preset como foi digitado, sem os espaços das pontas, ou null se não vale: de 1 a 24
    /// caracteres entre letras (acentos valem), números, espaço, <c>_</c> e <c>-</c>. O nome guardado
    /// fica em FormC: um acento decomposto (letra + marca combinante, de texto colado) é composto antes.
    /// </summary>
    public static string? NomeValido(string? digitado)
    {
        var nome = digitado?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(nome) || nome.Length > TamanhoMaximoNome)
            return null;
        foreach (var c in nome)
        {
            if (!(char.IsLetterOrDigit(c) || c is ' ' or '_' or '-'))
                return null;
        }
        return nome;
    }

    private bool NomeRepetido(string nome, string? ignorarId) =>
        Presets.Any(p => p.Id != ignorarId && string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>O nome de um preset novo, ou o motivo de não valer (inválido ou repetido).</summary>
    public (string? Nome, string? Motivo) ConferirNomeNovo(string? digitado)
    {
        if (NomeValido(digitado) is not { } nome)
            return (null, MsgNomeInvalido);
        if (NomeRepetido(nome, null))
            return (null, MsgNomeRepetido);
        return (nome, null);
    }

    // ------------------------------------------------------------------ criar e atualizar

    /// <summary>
    /// Cria um preset próprio com as 15 bandas dadas, ou com as do espelho (exige a leitura das 15).
    /// Não manda nada ao aparelho e não precisa de chave. O caminho da tela é a folha do Salvar.
    /// </summary>
    public async Task<ResultadoPreset> CriarAsync(string? nomeDigitado, IReadOnlyList<ValorCopiado>? bandas = null)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            if (bandas is null)
            {
                var (lidas, motivo) = TirarDoEspelho();
                if (motivo is not null)
                    return Terminar(ResultadoPreset.Falha(motivo));
                bandas = lidas;
            }
            bandas = SoBandas(bandas);
            if (FaltamEm(bandas) is > 0 and var faltam)
                return Terminar(ResultadoPreset.Falha(MsgIncompleta(faltam)));

            var (nome, motivoNome) = ConferirNomeNovo(nomeDigitado);
            if (nome is null)
                return Terminar(ResultadoPreset.Falha(motivoNome!));

            var preset = new Preset($"preset-{Guid.NewGuid():N}", nome, _relogio.GetLocalNow(), bandas);
            if (!await GravarAsync([.. Proprios, preset]))
                return Terminar(ResultadoPreset.Falha(MsgNaoGuardou));
            _log.Evento($"preset {nome} criado");
            return Terminar(new ResultadoPreset(true, null, MsgCriado(nome), preset));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>
    /// Troca as 15 bandas de um preset próprio pelas dadas (ou pelas do espelho). Vale para toda
    /// memória que lembra ele. Um herdado não é sobrescrito. Não manda nada ao aparelho.
    /// </summary>
    public async Task<ResultadoPreset> AtualizarAsync(string id, IReadOnlyList<ValorCopiado>? bandas = null)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            if (Obter(id) is { Herdado: true })
                return Terminar(ResultadoPreset.Falha(MsgHerdadoNaoSobrescrito));
            var proprios = Proprios;
            if (proprios.FirstOrDefault(p => p.Id == id) is not { } preset)
                return Terminar(ResultadoPreset.Falha(MsgNaoExiste));
            if (bandas is null)
            {
                var (lidas, motivo) = TirarDoEspelho();
                if (motivo is not null)
                    return Terminar(ResultadoPreset.Falha(motivo));
                bandas = lidas;
            }
            bandas = SoBandas(bandas);
            if (FaltamEm(bandas) is > 0 and var faltam)
                return Terminar(ResultadoPreset.Falha(MsgIncompleta(faltam)));

            var novo = preset with { Valores = bandas, Quando = _relogio.GetLocalNow() };
            if (!await GravarAsync(proprios.Select(p => p.Id == id ? novo : p).ToArray()))
                return Terminar(ResultadoPreset.Falha(MsgNaoGuardou));
            _log.Evento($"preset {preset.Nome} atualizado");
            return Terminar(new ResultadoPreset(true, null, MsgAtualizado(preset.Nome), novo));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>As bandas do espelho, ou o motivo de não poder (sem leitura, lendo, incompleta).</summary>
    private (IReadOnlyList<ValorCopiado> Bandas, string? Motivo) TirarDoEspelho()
    {
        if (_estado.Lendo)
            return ([], Escritor.MsgLendo);
        if (!_estado.Pronto)
            return ([], Escritor.MsgSemLeitura);
        var (bandas, faltam) = TirarBandas(_estado.Copiar());
        return faltam > 0 ? ([], MsgIncompleta(faltam)) : (bandas, null);
    }

    // ------------------------------------------------------------------ renomear e apagar

    /// <summary>Renomeia um preset próprio (as mesmas regras de nome do criar). Não manda nada ao aparelho.</summary>
    public async Task<ResultadoPreset> RenomearAsync(string id, string? nomeDigitado)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            if (Obter(id) is { Herdado: true })
                return Terminar(ResultadoPreset.Falha(MsgHerdado));
            var proprios = Proprios;
            if (proprios.FirstOrDefault(p => p.Id == id) is not { } preset)
                return Terminar(ResultadoPreset.Falha(MsgNaoExiste));
            if (NomeValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoPreset.Falha(MsgNomeInvalido));
            if (NomeRepetido(nome, id))
                return Terminar(ResultadoPreset.Falha(MsgNomeRepetido));

            var novo = preset with { Nome = nome };
            if (!await GravarAsync(proprios.Select(p => p.Id == id ? novo : p).ToArray()))
                return Terminar(ResultadoPreset.Falha(MsgNaoGuardou));
            _log.Evento($"preset {preset.Nome} renomeado para {nome}");
            return Terminar(new ResultadoPreset(true, null, MsgRenomeado(nome), novo));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>Apaga um preset próprio que nenhuma memória lembra. Não manda nada ao aparelho.</summary>
    public async Task<ResultadoPreset> ApagarAsync(string id)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            if (Obter(id) is { Herdado: true })
                return Terminar(ResultadoPreset.Falha(MsgHerdado));
            var proprios = Proprios;
            if (proprios.FirstOrDefault(p => p.Id == id) is not { } preset)
                return Terminar(ResultadoPreset.Falha(MsgNaoExiste));
            if (QuantasLembram(id) is > 0 and var n)
                return Terminar(ResultadoPreset.Falha(MsgLembrado(n)));

            if (!await GravarAsync(proprios.Where(p => p.Id != id).ToArray()))
                return Terminar(ResultadoPreset.Falha(MsgNaoGuardou));
            _log.Evento($"preset {preset.Nome} apagado");
            return Terminar(new ResultadoPreset(true, null, MsgApagado(preset.Nome)));
        }
        finally
        {
            Desocupar();
        }
    }

    // ------------------------------------------------------------------ exportar

    /// <summary>
    /// O JSON de um preset, próprio ou herdado (formato 2, com o nome, a nota e as 15 bandas), ou null
    /// se o id não existe. O arquivo e o compartilhar são da história 11; nunca é <c>.trps</c>.
    /// </summary>
    public string? ExportarJson(string id) => Obter(id) is { } preset ? ParaJson(preset, exportando: true) : null;

    // ------------------------------------------------------------------ aplicar

    /// <summary>
    /// Aplica o preset por cima da memória: cópia antes, escreve só as bandas diferentes, relê tudo
    /// e confere. Sem confirmação (decisão da UX). A chave é "Ajustes de áudio". Se cair no meio, a
    /// <see cref="CopiaAntes"/> deixa "aplicado pela metade".
    /// </summary>
    public Task<ResultadoPreset> AplicarAsync(string id) => AplicarInternoAsync(id, refazendo: false);

    private async Task<ResultadoPreset> AplicarInternoAsync(string id, bool refazendo)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            var preset = Obter(id);
            // O Terminar sem o preset: continua pela metade (a CopiaAntes mantém a marca).
            if (refazendo && preset is null)
                return Terminar(ResultadoPreset.Falha(MsgNaoExisteTerminar));

            if (_estado.Lendo)
                return Terminar(ResultadoPreset.Falha(Escritor.MsgLendo));
            if (!_estado.Pronto)
                return Terminar(ResultadoPreset.Falha(Escritor.MsgSemLeitura));
            if (preset is null)
                return Terminar(ResultadoPreset.Falha(MsgNaoExiste));
            // Uma memória anda ou algo ficou pela metade: só o Terminar e o Voltar à cópia escrevem.
            if (!refazendo && _copia.MotivoBloqueio() is { } bloqueio)
                return Terminar(ResultadoPreset.Falha(bloqueio));

            var alvo = preset.ComoCopia();
            var (escritas, ilegiveis) = CopiaAntes.Diferencas(alvo, _estado.Copiar());
            ilegiveis += FaltamEm(preset.Valores);
            if (ilegiveis > 0)
            {
                // Aplicar só uma parte daria "aplicado" sem estar: nada sai.
                _log.Evento($"preset {preset.Nome}: {ilegiveis} bandas ilegíveis ou ausentes");
                return Terminar(ResultadoPreset.Falha(CopiaAntes.MsgCopiaIlegivel(ilegiveis)));
            }

            if (escritas.Count == 0)
            {
                // Já está igual. Se era o Terminar, a marca sai.
                if (refazendo)
                {
                    await _copia.ConcluirAsync();
                    return await AplicouAsync(new ResultadoPreset(true, null, MsgAplicado(preset.Nome), preset));
                }
                return await AplicouAsync(new ResultadoPreset(true, null, MsgJaAplicado(preset.Nome), preset));
            }

            // A trava do primeiro quadro: chave "Ajustes de áudio", sessão Conectada, fora do modo só leitura.
            if (_sessao.Conferir(escritas[0].Quadro) is { } motivo)
            {
                _log.Recusado(escritas[0].Quadro, motivo);
                return Terminar(ResultadoPreset.Falha(motivo));
            }

            var operacao = new OperacaoPendente(TipoAplicar, preset.Id, $"Aplicar o preset {preset.Nome}", preset.Nome);
            var copia = await _copia.IniciarAsync(operacao, manterCopia: refazendo);
            if (!copia.Ok)
                return Terminar(ResultadoPreset.Falha(copia.Motivo!));
            Avisar(null);

            // Suposição PRESET-APLICA-TUDO: escrever as bandas diferentes em sequência e reler tudo confirma o preset.
            _log.Evento($"preset {preset.Nome}: {escritas.Count} bandas diferentes");
            var falhas = 0;
            string? ultimoMotivo = null;
            foreach (var e in escritas)
            {
                var r = await _escritor.EscreverAsync(e.Parametro, e.Indice, e.Valor, espelhar: false, daCopia: true);
                if (!r.Ok || r.Diferente)
                {
                    falhas++;
                    ultimoMotivo = r.Motivo ?? $"o aparelho ficou em {r.Valor?.NoFio}";
                }
            }
            if (falhas > 0)
            {
                _log.Evento($"preset {preset.Nome}: {falhas} bandas não foram escritas ({ultimoMotivo})");
                // Com a sessão aberta, fica pela metade já; caída, a marca espera a próxima conexão.
                if (SessaoAberta)
                    _copia.MarcarPelaMetade();
                else
                    _copia.Interromper();
                return Terminar(ResultadoPreset.Falha(MsgNaoEscritos(falhas, ultimoMotivo)));
            }

            // A releitura completa: o espelho passa a ser o que o aparelho tem de fato.
            var retrato = await _leitura.LerNovaAsync();
            if (retrato is not { Completo: true })
            {
                if (!SessaoAberta)
                {
                    _copia.Interromper();
                    return Terminar(ResultadoPreset.Falha(MsgCaiuAplicar));
                }
                _copia.MarcarPelaMetade();
                return Terminar(ResultadoPreset.Falha(CopiaAntes.MsgLeituraParou));
            }

            var (restam, _) = CopiaAntes.Diferencas(alvo, _estado.Copiar());
            await _copia.ConcluirAsync();
            var frase = restam.Count == 0 ? MsgAplicado(preset.Nome) : MsgAplicadoDiferente(preset.Nome, restam.Count);
            return await AplicouAsync(new ResultadoPreset(true, null, frase, preset));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>Um aplicar que terminou bem: avisa quem ouve (a memória em uso lembra o preset) e devolve o resultado.</summary>
    private async Task<ResultadoPreset> AplicouAsync(ResultadoPreset r)
    {
        if (AoAplicar is { } aviso && r.Preset is { } preset)
        {
            try
            {
                await aviso(preset);
            }
            catch (Exception e)
            {
                _log.Evento($"falha no aviso do preset aplicado ({e.GetType().Name})");
            }
        }
        return Terminar(r);
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private static ResultadoCopia ParaCopia(ResultadoPreset r) =>
        r.Ok ? ResultadoCopia.Sucesso : ResultadoCopia.Falha(r.Mensagem ?? r.Motivo ?? "falhou");

    // ------------------------------------------------------------------ guardado

    /// <summary>Grava a lista de próprios (com duas tentativas); só se gravou ela vira a lista.</summary>
    private async Task<bool> GravarAsync(IReadOnlyList<Preset> novos)
    {
        if (!await Gravacao.GravarAsync(_repositorio, ParaJson(novos)))
        {
            _log.Evento("não consegui guardar os presets no celular");
            return false;
        }
        lock (_trava)
            _proprios = novos;
        Notificar();
        return true;
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        IReadOnlyList<Preset> lidos = [];
        var migrar = false;
        if (json is not null)
        {
            try
            {
                lidos = DeJson(json);
                migrar = FormatoDe(json) is FormatoAntigo or Formato2;
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                // Arquivo estragado: nenhum preset próprio, sem erro.
            }
        }
        lock (_trava)
            _proprios = lidos;
        if (migrar)
        {
            // O começo do zero (decisão D2 do Denis, 29/10/2026): os presets de antes somem.
            _log.Evento("presets.json migrado ao formato 3: os presets do celular de antes saíram");
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson([])))
                _log.Evento("não consegui regravar o presets.json no formato 3");
        }
        Notificar();
    }

    /// <summary>O formato do <c>presets.json</c>, ou null fora da forma.</summary>
    internal static int? FormatoDe(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
               && doc.RootElement.TryGetProperty("formato", out var f) && f.TryGetInt32(out var formato)
            ? formato
            : null;
    }

    /// <summary>O JSON do <c>presets.json</c> (formato 3): só os presets próprios, cada um com as 15 bandas.</summary>
    public static string ParaJson(IEnumerable<Preset> proprios)
    {
        using var fluxo = new MemoryStream();
        using (var w = NovoEscritor(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartArray("presets");
            foreach (var p in proprios)
                EscreverPreset(w, p, exportando: false);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>O JSON de um preset sozinho (a exportação, com a bandeira de herdado e a nota).</summary>
    public static string ParaJson(Preset preset, bool exportando)
    {
        using var fluxo = new MemoryStream();
        using (var w = NovoEscritor(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            EscreverCampos(w, preset, exportando);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>presets.json</c>. Formato 3: cada preset com as bandas que tem (só o <c>020</c>; sem
    /// nenhuma banda, descartado). Os formatos 1 e 2 (de antes de 29/10/2026), outro formato ou outra
    /// forma: lista vazia. Uma entrada sem id, com nome inválido, com id ou nome repetido (ou o id ou o
    /// nome de um herdado) é descartada; as outras ficam.
    /// </summary>
    public static IReadOnlyList<Preset> DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato)
            || formato != Formato
            || !raiz.TryGetProperty("presets", out var lista) || lista.ValueKind != JsonValueKind.Array)
            return [];

        var saida = new List<Preset>();
        var ids = new HashSet<string>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = Texto(item, "id");
            if (string.IsNullOrEmpty(id) || ConhecimentoHerdado.Presets.Any(h => h.Id == id) || ids.Contains(id))
                continue;
            if (NomeValido(Texto(item, "nome")) is not { } nome)
                continue;
            // Um nome repetido, entre os próprios ou com um herdado (em qualquer caixa), sai.
            if (saida.Concat(ConhecimentoHerdado.Presets).Any(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase)))
                continue;
            var quando = Texto(item, "quando") is { } q
                         && DateTimeOffset.TryParse(q, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                ? d
                : DateTimeOffset.MinValue;

            var valores = new List<ValorCopiado>();
            if (item.TryGetProperty("valores", out var vs) && vs.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vs.EnumerateArray())
                {
                    if (v.ValueKind != JsonValueKind.Object || Texto(v, "codigo") is not { } codigo || Texto(v, "noFio") is not { } noFio)
                        continue;
                    int? indice = v.TryGetProperty("indice", out var i) && i.TryGetInt32(out var n) ? n : null;
                    valores.Add(new ValorCopiado(codigo, indice, noFio));
                }
            }
            var bandas = SoBandas(valores);
            // Passa com alguma banda (o aplicar recusa as que faltam).
            if (bandas.Count == 0)
                continue;
            ids.Add(id);
            saida.Add(new Preset(id, nome, quando, bandas));
        }
        return saida;
    }

    private static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static Utf8JsonWriter NovoEscritor(Stream fluxo) => new(fluxo, new JsonWriterOptions
    {
        Indented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    private static void EscreverPreset(Utf8JsonWriter w, Preset preset, bool exportando)
    {
        w.WriteStartObject();
        EscreverCampos(w, preset, exportando);
        w.WriteEndObject();
    }

    private static void EscreverCampos(Utf8JsonWriter w, Preset preset, bool exportando)
    {
        w.WriteString("id", preset.Id);
        w.WriteString("nome", preset.Nome);
        if (exportando)
            w.WriteBoolean("herdado", preset.Herdado);
        if (exportando && preset.Nota is not null)
            w.WriteString("nota", preset.Nota);
        w.WriteString("quando", preset.Quando.ToString("O", CultureInfo.InvariantCulture));
        w.WriteStartArray("valores");
        foreach (var v in preset.Valores)
        {
            w.WriteStartObject();
            w.WriteString("codigo", v.Codigo);
            if (v.Indice is { } i)
                w.WriteNumber("indice", i);
            w.WriteString("noFio", v.NoFio);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    // ------------------------------------------------------------------ apoio

    private bool Ocupar()
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0)
            return false;
        Notificar();
        return true;
    }

    private void Desocupar()
    {
        Volatile.Write(ref _ocupado, 0);
        Notificar();
    }

    /// <summary>Guarda a frase do resultado como aviso e devolve o resultado.</summary>
    private ResultadoPreset Terminar(ResultadoPreset r)
    {
        Avisar(r.Mensagem);
        if (!r.Ok)
            _log.Evento($"preset: {r.Mensagem ?? r.Motivo}");
        return r;
    }

    private void Avisar(string? aviso)
    {
        lock (_trava)
            _aviso = aviso;
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso dos presets ({e.GetType().Name})");
        }
    }
}
