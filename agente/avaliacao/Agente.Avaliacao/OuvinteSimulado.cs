using BgsControle.Nucleo.Setups;

namespace Agente.Avaliacao;

/// <summary>Uma pergunta que o Agente fez, com as respostas mandadas e a do ouvinte (null se ficou sem resposta).</summary>
public sealed record PerguntaFeita(int Numero, string Texto, IReadOnlyList<string> Respostas, string? Resposta, bool DoOutro);

/// <summary>Uma escuta "1 ou 2": o texto, a variável e a escolha do ouvinte ("1", "2" ou "tanto-faz").</summary>
public sealed record EscutaFeita(int Rodada, string Texto, string Variavel, string Escolha);

/// <summary>Uma resposta ao "Como ficou?": a rodada, o texto e se foi o "Ótimo, pode salvar".</summary>
public sealed record ComoFicouFeito(int Rodada, string Resposta, bool DoOutro, bool Salvar);

/// <summary>
/// O ouvinte simulado (spec-agente-memoria CAP-8): o lado do usuário de uma criação da memória, em código, sem modelo,
/// direto na <see cref="CriacaoMemoria"/> do núcleo (como os testes da 14a). Ele:
/// <list type="bullet">
/// <item><b>Pergunta:</b> toca na resposta com mais palavras em comum com os fatos do perfil (<see cref="Palavras"/>); sem
/// nenhuma em comum, usa o "Outro…" com o fato mais perto do texto da pergunta.</item>
/// <item><b>Resumo:</b> toca em "Avaliar o som".</item>
/// <item><b>"1 ou 2":</b> escolhe a versão de menor distância aos alvos; em distâncias iguais, "Tanto faz".</item>
/// <item><b>"Como ficou?":</b> salva quando todo alvo está a até 1,5 dB (os fora de dB, iguais); senão, responde a opção
/// mais perto da frase de refino (a queixa, depois o gosto, alternando), ou o "Outro…" com ela. Depois de 3 refinos,
/// salva assim mesmo.</item>
/// <item><b>Salvar:</b> com o nome <see cref="Perfil.NomeMemoria"/>.</item>
/// </list>
/// </summary>
/// <param name="nucleo">O núcleo da criação (null só para usar <see cref="Escolher"/> e <see cref="FatoMaisPerto"/> sozinhos).</param>
public sealed class OuvinteSimulado(Perfil perfil, NucleoSimulado? nucleo, TimeSpan? esperaComoFicou = null)
{
    public const int MaximoRefinos = 3;

    private static readonly TimeSpan Passo = TimeSpan.FromMilliseconds(40);

    private readonly TimeSpan _esperaComoFicou = esperaComoFicou ?? TimeSpan.FromSeconds(60);
    private readonly IReadOnlySet<string> _palavrasFatos = Palavras.De(string.Join(" ", perfil.Fatos.Values));
    private readonly object _trava = new();
    private readonly List<PerguntaFeita> _perguntas = [];
    private readonly List<EscutaFeita> _escutas = [];
    private readonly List<ComoFicouFeito> _comoFicou = [];
    private IReadOnlyList<AjusteCriacao> _ajustes = [];
    private DateTime? _comoFicouDesde;

    /// <summary>As perguntas feitas pelo Agente, na ordem (todas as que apareceram, mesmo as refeitas).</summary>
    public IReadOnlyList<PerguntaFeita> Perguntas
    {
        get
        {
            lock (_trava)
                return _perguntas.ToArray();
        }
    }

    public IReadOnlyList<EscutaFeita> Escutas
    {
        get
        {
            lock (_trava)
                return _escutas.ToArray();
        }
    }

    public IReadOnlyList<ComoFicouFeito> ComoFicou
    {
        get
        {
            lock (_trava)
                return _comoFicou.ToArray();
        }
    }

    /// <summary>Os ajustes da criação (o último retrato com ela aberta), na ordem, com a rodada.</summary>
    public IReadOnlyList<AjusteCriacao> Ajustes
    {
        get
        {
            lock (_trava)
                return _ajustes;
        }
    }

    /// <summary>Os refinos pedidos (respostas ao "Como ficou?" sem salvar).</summary>
    public int Refinos => ComoFicou.Count(c => !c.Salvar);

    /// <summary>A criação abriu em algum momento.</summary>
    public bool Abriu { get; private set; }

    /// <summary>O ouvinte salvou a memória.</summary>
    public bool Salvou { get; private set; }

    /// <summary>O motivo de o Salvar não ter dado certo, ou null.</summary>
    public string? FalhaSalvar { get; private set; }

    /// <summary>Faz o lado do usuário até a criação fechar (depois de ter aberto), ou até o cancelamento.</summary>
    public async Task RodarAsync(CancellationToken cancelar)
    {
        var c = (nucleo ?? throw new InvalidOperationException("O ouvinte precisa do núcleo.")).Criacao;
        while (!cancelar.IsCancellationRequested)
        {
            var etapa = c.Etapa;
            if (etapa != EtapaCriacao.Fechada)
            {
                Abriu = true;
                Retratar(c);
            }
            else if (Abriu)
            {
                return;
            }

            switch (etapa)
            {
                case EtapaCriacao.Perguntas when c.PerguntaAtual is { } p:
                    Responder(c, p);
                    break;
                case EtapaCriacao.Resumo:
                    c.AvaliarSom();
                    break;
                case EtapaCriacao.ComoFicou:
                    ResponderComoFicou(c);
                    break;
                case EtapaCriacao.Escuta when c.Escuta is { } e:
                    await EscolherAsync(c, e);
                    break;
                case EtapaCriacao.Salvar:
                    await SalvarAsync(c);
                    break;
            }
            if (etapa != EtapaCriacao.ComoFicou)
                _comoFicouDesde = null;
            try
            {
                await Task.Delay(Passo, cancelar);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Retratar(CriacaoMemoria c)
    {
        var ajustes = c.Ajustes;
        var perguntas = c.Perguntas;
        lock (_trava)
        {
            if (ajustes.Count >= _ajustes.Count)
                _ajustes = ajustes;
            foreach (var p in perguntas)
            {
                // Uma pergunta nova (ou refeita com outro texto depois do ‹) entra; a mesma só ganha a resposta.
                var i = _perguntas.FindIndex(x => x.Numero == p.Numero && x.Texto == p.Texto && x.Respostas.SequenceEqual(p.Respostas));
                if (i < 0)
                    _perguntas.Add(new PerguntaFeita(p.Numero, p.Texto, p.Respostas, p.Resposta, p.DoOutro));
                else if (p.Respondida && _perguntas[i].Resposta is null)
                    _perguntas[i] = _perguntas[i] with { Resposta = p.Resposta, DoOutro = p.DoOutro };
            }
        }
    }

    // ------------------------------------------------------------------ as perguntas

    private void Responder(CriacaoMemoria c, PerguntaCriacao p)
    {
        if (Palavras.MaisPerto(p.Respostas, _palavrasFatos) is { } i)
            c.Responder(i);
        else
            c.ResponderOutro(FatoMaisPerto(p.Texto));
        Retratar(c);
    }

    /// <summary>O fato (o valor) com mais palavras em comum com o texto, contando o nome do fato; o primeiro no empate.</summary>
    public string FatoMaisPerto(string texto)
    {
        var palavras = Palavras.De(texto);
        var melhor = perfil.Fatos.First();
        var maior = -1;
        foreach (var f in perfil.Fatos)
        {
            var n = Palavras.De(f.Key + " " + f.Value).Count(palavras.Contains);
            if (n > maior)
            {
                maior = n;
                melhor = f;
            }
        }
        return melhor.Value;
    }

    // ------------------------------------------------------------------ "1 ou 2"

    private async Task EscolherAsync(CriacaoMemoria c, EscutaCriacao e)
    {
        var escolha = Escolher(e.Versao1, e.Versao2);
        var r = await c.EscolherAsync(escolha);
        if (!r.Ok)
            return; // tenta de novo no próximo passo
        lock (_trava)
            _escutas.Add(new EscutaFeita(e.Rodada, e.Texto, e.Variavel, CriacaoMemoria.NomeEscolha(escolha)));
    }

    /// <summary>
    /// A versão mais perto do gosto: primeiro, a que erra menos alvos fora de dB (Hz, índices, a rota esperada); depois, a de
    /// menor distância em dB (a maior diferença). Iguais nos dois: "Tanto faz".
    /// </summary>
    public EscolhaEscuta Escolher(IReadOnlyList<AjusteEscuta> versao1, IReadOnlyList<AjusteEscuta> versao2)
    {
        var (erros1, db1) = DistanciaVersao(versao1, versao2);
        var (erros2, db2) = DistanciaVersao(versao2, versao1);
        if (erros1 != erros2)
            return erros1 < erros2 ? EscolhaEscuta.Um : EscolhaEscuta.Dois;
        if (db1 == db2)
            return EscolhaEscuta.TantoFaz;
        return db1 < db2 ? EscolhaEscuta.Um : EscolhaEscuta.Dois;
    }

    /// <summary>
    /// Nos alvos (e na rota) que alguma das duas versões mexe, com o valor da versão, ou, onde só a outra mexe, o valor de antes
    /// (o último ajuste da criação, ou o de partida): quantos alvos fora de dB ela erra e a maior diferença dos alvos em dB.
    /// </summary>
    private (int ErrosForaDeDb, decimal MaiorDb) DistanciaVersao(IReadOnlyList<AjusteEscuta> versao, IReadOnlyList<AjusteEscuta> outra)
    {
        var erros = 0;
        var maior = 0m;
        foreach (var alvo in perfil.Gosto.Alvos)
        {
            if (Valor(versao, outra, alvo.Codigo, alvo.Indice) is not { } v)
                continue;
            if (!Metricas.EmDb(alvo.Codigo))
                erros += Metricas.Numero(v) == alvo.Valor ? 0 : 1;
            else
                maior = Math.Max(maior, Metricas.Numero(v) is { } n ? Math.Abs(n - alvo.Valor) : Metricas.DistanciaSemValor);
        }
        foreach (var rota in perfil.Gosto.Rota)
        {
            if (Valor(versao, outra, Metricas.CodigoRota, rota.Canal) is { } v && v != rota.Rota)
                erros++;
        }
        return (erros, maior);
    }

    private string? Valor(IReadOnlyList<AjusteEscuta> versao, IReadOnlyList<AjusteEscuta> outra, string codigo, int? indice)
    {
        if (versao.FirstOrDefault(a => a.Codigo == codigo && a.Indice == indice) is { } meu)
            return meu.Texto ?? meu.Numero?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!outra.Any(a => a.Codigo == codigo && a.Indice == indice))
            return null;
        var antes = Ajustes.LastOrDefault(a => a.Codigo == codigo && a.Indice == indice)?.Relido;
        return antes ?? Perfis.EstadoPartida.GetValueOrDefault((codigo, indice));
    }

    // ------------------------------------------------------------------ "Como ficou?" e Salvar

    private void ResponderComoFicou(CriacaoMemoria c)
    {
        var atual = c.ComoFicouAtual;
        var comRespostas = atual is { Respondida: false, Respostas.Count: > 0 } && atual.Rodada == c.Rodada;
        _comoFicouDesde ??= DateTime.UtcNow;
        if (!comRespostas && DateTime.UtcNow - _comoFicouDesde < _esperaComoFicou)
            return; // o Agente ainda pode mandar as respostas
        var respostas = comRespostas ? atual!.Respostas : [];
        var rodada = c.Rodada;
        var refinos = Refinos;

        if (refinos >= MaximoRefinos || TodosAtingidos())
        {
            if (c.PodeSalvar().Ok)
            {
                lock (_trava)
                    _comoFicou.Add(new ComoFicouFeito(rodada, CriacaoMemoria.OtimoPodeSalvar, false, true));
            }
            return;
        }

        var frase = refinos % 2 == 0 ? perfil.Refino.Queixa : perfil.Refino.Gosto;
        var i = Palavras.MaisPerto(respostas, Palavras.De(frase));
        var r = i is { } indice ? c.ResponderComoFicou(indice) : c.ResponderComoFicouOutro(frase);
        if (r.Ok)
        {
            lock (_trava)
                _comoFicou.Add(new ComoFicouFeito(rodada, i is { } k ? respostas[k] : frase, i is null, false));
            _comoFicouDesde = null;
        }
    }

    /// <summary>Todo alvo atingido no aparelho agora (o valor relido).</summary>
    public bool TodosAtingidos() =>
        nucleo is not null && perfil.Gosto.Alvos.All(a => Metricas.Atingido(a, nucleo.Simulador.Valor(a.Codigo, a.Indice)));

    private async Task SalvarAsync(CriacaoMemoria c)
    {
        if (Salvou)
            return;
        Retratar(c);
        var r = await c.SalvarAsync(perfil.NomeMemoria);
        if (r.Ok)
            Salvou = true;
        else
            FalhaSalvar = r.Motivo;
    }
}
