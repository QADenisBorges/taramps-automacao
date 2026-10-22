using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Presets;

/// <summary>
/// Os presets no celular (CAP-5): guardar o som atual, aplicar, renomear, apagar e exportar. Um
/// preset são os valores de áudio do Marco 1; ele fica em <c>presets.json</c> nos arquivos privados
/// (só os próprios). Os herdados (<see cref="ConhecimentoHerdado.Presets"/>) vêm sempre primeiro e
/// não mudam.
/// <list type="bullet">
/// <item><b>Guardar</b> tira os valores do espelho e não manda nada ao aparelho.</item>
/// <item><b>Aplicar</b> passa pela <see cref="CopiaAntes"/> (regra 3 de <c>failure-modes.md</c>): guarda
/// a cópia, escreve só os ajustes diferentes, uma de cada vez, relê tudo e confere. Se cair no meio,
/// ao reconectar o estado fica "aplicado pela metade", com Terminar (refaz, com a mesma cópia) e
/// Voltar à cópia (da <see cref="CopiaAntes"/>).</item>
/// <item>Renomear e apagar valem só para os próprios e não mandam nada ao aparelho.</item>
/// </list>
/// Todas as operações do gerente são exclusivas entre si.
/// </summary>
public sealed class GerentePresets
{
    public const int Formato = 1;

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
    public const string MsgCaiuAplicar = "A conexão caiu no meio do aplicar. Ao reconectar, o app mostra o que ficou.";

    public static string MsgIncompleta(int n) => n == 1
        ? "A leitura está incompleta (1 ajuste sem valor)."
        : $"A leitura está incompleta ({n} ajustes sem valor).";

    public static string MsgGuardado(string nome) => $"Preset {nome} guardado.";

    public static string MsgJaAplicado(string nome) => $"Preset {nome} já está aplicado.";

    public static string MsgAplicado(string nome) => $"Preset {nome} aplicado. Para guardar numa memória, use Salvar.";

    public static string MsgAplicadoDiferente(string nome, int n) => n == 1
        ? $"Preset {nome} aplicado, mas 1 ajuste ficou diferente."
        : $"Preset {nome} aplicado, mas {n} ajustes ficaram diferentes.";

    public static string MsgNaoEscritos(int n, string? motivo) => n == 1
        ? $"1 ajuste não foi escrito: {motivo}"
        : $"{n} ajustes não foram escritos: {motivo}";

    public static string MsgRenomeado(string nome) => $"Preset renomeado para {nome}.";

    public static string MsgApagado(string nome) => $"Preset {nome} apagado.";

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

    /// <summary>Uma operação do gerente está andando.</summary>
    public bool Ocupado => Volatile.Read(ref _ocupado) == 1;

    /// <summary>A frase da última operação ("Preset NOITE aplicado…"), ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>Os presets guardados pelo Denis, do mais antigo ao mais novo.</summary>
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
    public Preset? Obter(string id) => Presets.FirstOrDefault(p => p.Id == id);

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

    // ------------------------------------------------------------------ guardar

    /// <summary>
    /// Guarda o som atual (o espelho) como preset próprio. Exige a leitura completa. Não manda nada
    /// ao aparelho e não precisa de chave.
    /// </summary>
    public async Task<ResultadoPreset> GuardarAsync(string? nomeDigitado)
    {
        await Carregada;
        if (!Ocupar())
            return ResultadoPreset.Falha(MsgOcupado);
        try
        {
            if (_estado.Lendo)
                return Terminar(ResultadoPreset.Falha(Escritor.MsgLendo));
            if (!_estado.Pronto)
                return Terminar(ResultadoPreset.Falha(Escritor.MsgSemLeitura));

            var espelho = _estado.Copiar();
            var faltam = Catalogo.ConsultasMarco1.Count(c => !espelho.ContainsKey(new Endereco(c.Parametro.Codigo, c.Indice)));
            if (faltam > 0)
                return Terminar(ResultadoPreset.Falha(MsgIncompleta(faltam)));

            if (NomeValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoPreset.Falha(MsgNomeInvalido));
            if (NomeRepetido(nome, null))
                return Terminar(ResultadoPreset.Falha(MsgNomeRepetido));

            var valores = Catalogo.ConsultasMarco1
                .Select(c => new ValorCopiado(c.Parametro.Codigo, c.Indice, espelho[new Endereco(c.Parametro.Codigo, c.Indice)].NoFio))
                .ToArray();
            var preset = new Preset($"preset-{Guid.NewGuid():N}", nome, _relogio.GetLocalNow(), valores);

            if (!await GravarAsync([.. Proprios, preset]))
                return Terminar(ResultadoPreset.Falha(MsgNaoGuardou));
            _log.Evento($"preset {nome} guardado ({valores.Length} valores)");
            return Terminar(new ResultadoPreset(true, null, MsgGuardado(nome), preset));
        }
        finally
        {
            Desocupar();
        }
    }

    // ------------------------------------------------------------------ renomear e apagar

    /// <summary>Renomeia um preset próprio (as mesmas regras de nome do guardar). Não manda nada ao aparelho.</summary>
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

    /// <summary>Apaga um preset próprio. Não manda nada ao aparelho.</summary>
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
    /// O JSON de um preset, próprio ou herdado (formato 1, com o nome, a nota e os valores), ou null
    /// se o id não existe. O arquivo e o compartilhar são da história 11; nunca é <c>.trps</c>.
    /// </summary>
    public string? ExportarJson(string id) => Obter(id) is { } preset ? ParaJson(preset, exportando: true) : null;

    // ------------------------------------------------------------------ aplicar

    /// <summary>
    /// Aplica o preset: cópia antes, escreve só os ajustes diferentes, relê tudo e confere. Sem
    /// confirmação (decisão da UX). Se cair no meio, a <see cref="CopiaAntes"/> deixa "aplicado
    /// pela metade".
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
            ilegiveis += ValoresQueFaltam(preset);
            if (ilegiveis > 0)
            {
                // Aplicar só uma parte daria "aplicado" sem estar: nada sai.
                _log.Evento($"preset {preset.Nome}: {ilegiveis} valores ilegíveis ou ausentes");
                return Terminar(ResultadoPreset.Falha(CopiaAntes.MsgCopiaIlegivel(ilegiveis)));
            }

            if (escritas.Count == 0)
            {
                // Já está igual. Se era o Terminar, a marca sai.
                if (refazendo)
                {
                    await _copia.ConcluirAsync();
                    return Terminar(new ResultadoPreset(true, null, MsgAplicado(preset.Nome), preset));
                }
                return Terminar(new ResultadoPreset(true, null, MsgJaAplicado(preset.Nome), preset));
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

            // Suposição PRESET-APLICA-TUDO: escrever os ajustes diferentes em sequência e reler tudo confirma o preset.
            _log.Evento($"preset {preset.Nome}: {escritas.Count} ajustes diferentes");
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
                _log.Evento($"preset {preset.Nome}: {falhas} ajustes não foram escritos ({ultimoMotivo})");
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
                return Terminar(ResultadoPreset.Falha(GerenteMemorias.MsgLeituraParou));
            }

            var (restam, _) = CopiaAntes.Diferencas(alvo, _estado.Copiar());
            await _copia.ConcluirAsync();
            var frase = restam.Count == 0 ? MsgAplicado(preset.Nome) : MsgAplicadoDiferente(preset.Nome, restam.Count);
            return Terminar(new ResultadoPreset(true, null, frase, preset));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>Quantos parâmetros do Marco 1 o preset não traz (um arquivo editado à mão).</summary>
    private static int ValoresQueFaltam(Preset preset)
    {
        var tem = preset.Valores.Select(v => new Endereco(v.Codigo, v.Indice)).ToHashSet();
        return Catalogo.ConsultasMarco1.Count(c => !tem.Contains(new Endereco(c.Parametro.Codigo, c.Indice)));
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
        if (json is not null)
        {
            try
            {
                lidos = DeJson(json);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                // Arquivo estragado: nenhum preset próprio, sem erro.
            }
        }
        lock (_trava)
            _proprios = lidos;
        Notificar();
    }

    /// <summary>O JSON do <c>presets.json</c> (formato 1): só os presets próprios.</summary>
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
    /// Lê o <c>presets.json</c>. Fora do formato 1, ou com outra forma: lista vazia. Uma entrada sem id,
    /// com nome inválido, sem valores ou com id repetido é descartada; as outras ficam.
    /// </summary>
    public static IReadOnlyList<Preset> DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato != Formato
            || !raiz.TryGetProperty("presets", out var lista) || lista.ValueKind != JsonValueKind.Array)
            return [];

        var saida = new List<Preset>();
        var ids = new HashSet<string>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = Texto(item, "id");
            if (string.IsNullOrEmpty(id) || ConhecimentoHerdado.Presets.Any(h => h.Id == id) || !ids.Add(id))
                continue;
            if (NomeValido(Texto(item, "nome")) is not { } nome)
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
            if (valores.Count == 0)
            {
                ids.Remove(id);
                continue;
            }
            saida.Add(new Preset(id, nome, quando, valores));
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
