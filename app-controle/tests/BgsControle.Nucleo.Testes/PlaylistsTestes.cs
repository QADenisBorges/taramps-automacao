using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// As playlists do Player (histórias 8b e 8d) no núcleo: o <c>playlists.json</c> no formato 2, uma
/// playlist por memória do celular (pelo id), as pendentes do formato 1 (por posição), a adoção pelo
/// nome, o apagar de uma memória e as edições que só valem se gravaram. Nada aqui fala com o processador.
/// </summary>
public sealed class PlaylistsTestes
{
    private const string A = "memoria-a";
    private const string B = "memoria-b";
    private const string C = "memoria-c";

    private static ArquivoAudio Arquivo(int i) =>
        new($"content://musica/{i}", $"Faixa {i}", "Music", TimeSpan.FromSeconds(100 + i));

    private static async Task<GerentePlaylists> Novo(TextoMemoria repositorio)
    {
        var g = new GerentePlaylists(repositorio, new LogBle());
        await g.Carregada;
        return g;
    }

    private static string[] Uris(IEnumerable<Faixa> faixas) => faixas.Select(f => f.Uri).ToArray();

    /// <summary>Um <c>playlists.json</c> do formato 1: as 3 listas pela posição da memória do aparelho.</summary>
    private static string Formato1(params string[][] listas) =>
        "{\"formato\":1,\"playlists\":[" + string.Join(",", listas.Select(l =>
            "[" + string.Join(",", l.Select(id => $"{{\"id\":\"{id}\",\"uri\":\"content://{id}\",\"nome\":\"{id}\"}}")) + "]")) + "]}";

    [Fact]
    public async Task Sem_arquivo_nenhuma_playlist_nem_pendente()
    {
        var g = await Novo(new TextoMemoria());

        Assert.Empty(g.Playlist(A));
        Assert.Empty(g.Playlist(null));
        Assert.Empty(g.Playlist(""));
        Assert.Empty(g.Pendentes);
    }

    [Fact]
    public async Task Adicionar_varios_entram_no_fim_na_ordem_com_ids_unicos_e_o_arquivo_e_gravado()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        var mudou = 0;
        g.Mudou += () => mudou++;

        Assert.True((await g.AdicionarAsync(C, [Arquivo(1), Arquivo(2), Arquivo(3)])).Ok);
        Assert.True((await g.AdicionarAsync(C, [Arquivo(4)])).Ok);

        Assert.Equal(["content://musica/1", "content://musica/2", "content://musica/3", "content://musica/4"], Uris(g.Playlist(C)));
        Assert.Equal(4, g.Playlist(C).Select(f => f.Id).Distinct().Count());
        var primeira = g.Playlist(C)[0];
        Assert.Equal(("Faixa 1", "Music", TimeSpan.FromSeconds(101)), (primeira.Nome, primeira.Pasta, primeira.Duracao));
        Assert.Empty(g.Playlist(A));
        Assert.Equal(2, repo.Gravacoes);
        Assert.Equal(2, mudou);
        Assert.Equal(g.Playlist(C), GerentePlaylists.DeJson(repo.Texto!).PorMemoria[C]);
        Assert.Contains("\"formato\": 2", repo.Texto!);
    }

    [Fact]
    public async Task O_mesmo_arquivo_duas_vezes_vira_duas_faixas()
    {
        var g = await Novo(new TextoMemoria());

        await g.AdicionarAsync(A, [Arquivo(1), Arquivo(1)]);

        Assert.Equal(2, g.Playlist(A).Count);
        Assert.NotEqual(g.Playlist(A)[0].Id, g.Playlist(A)[1].Id);
    }

    [Fact]
    public async Task Mover_a_3_para_a_1_e_a_1_para_o_fim()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(B, [Arquivo(1), Arquivo(2), Arquivo(3)]);

        Assert.True((await g.MoverAsync(B, 2, 0)).Ok);
        Assert.Equal(["content://musica/3", "content://musica/1", "content://musica/2"], Uris(g.Playlist(B)));

        Assert.True((await g.MoverAsync(B, 0, 2)).Ok);
        Assert.Equal(["content://musica/1", "content://musica/2", "content://musica/3"], Uris(g.Playlist(B)));
        Assert.Equal(Uris(g.Playlist(B)), Uris(GerentePlaylists.DeJson(repo.Texto!).PorMemoria[B]));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public async Task Mover_fora_da_lista_e_recusado_sem_gravar(int de, int para)
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1), Arquivo(2), Arquivo(3)]);

        var r = await g.MoverAsync(A, de, para);

        Assert.False(r.Ok);
        Assert.Equal(GerentePlaylists.MsgPosicao, r.Mensagem);
        Assert.Equal(1, repo.Gravacoes);
    }

    [Fact]
    public async Task Remover_tira_so_aquela_faixa_e_uma_inexistente_e_recusada()
    {
        var g = await Novo(new TextoMemoria());
        await g.AdicionarAsync(A, [Arquivo(1), Arquivo(2)]);
        var id = g.Playlist(A)[0].Id;

        Assert.True((await g.RemoverAsync(A, id)).Ok);
        Assert.Equal(["content://musica/2"], Uris(g.Playlist(A)));

        var r = await g.RemoverAsync(A, id);
        Assert.False(r.Ok);
        Assert.Equal(GerentePlaylists.MsgNaoExiste, r.Mensagem);
    }

    [Fact]
    public async Task Sem_memoria_do_celular_e_recusado()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);

        Assert.Equal(GerentePlaylists.MsgSemMemoria, (await g.AdicionarAsync(null, [Arquivo(1)])).Mensagem);
        Assert.Equal(GerentePlaylists.MsgSemMemoria, (await g.RemoverAsync("", "x")).Mensagem);
        Assert.Equal(GerentePlaylists.MsgSemMemoria, (await g.MoverAsync(null, 0, 1)).Mensagem);
        Assert.Equal(0, repo.Gravacoes);
    }

    [Fact]
    public async Task Reabrir_com_o_mesmo_repositorio_as_playlists_e_as_pendentes_voltam_na_ordem()
    {
        var repo = new TextoMemoria(Formato1([], ["x1", "x2"], []));
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1), Arquivo(2)]);
        await g.AdicionarAsync(B, [Arquivo(3)]);
        await g.AdicionarAsync(C, [Arquivo(4), Arquivo(5), Arquivo(6)]);
        await g.MoverAsync(C, 2, 0);

        var outro = await Novo(repo);

        foreach (var m in new[] { A, B, C })
            Assert.Equal(g.Playlist(m), outro.Playlist(m));
        Assert.Equal(["content://musica/6", "content://musica/4", "content://musica/5"], Uris(outro.Playlist(C)));
        var pendente = Assert.Single(outro.Pendentes);
        Assert.Equal("MUSICA ORIG", pendente.Nome);
        Assert.Equal(["content://x1", "content://x2"], Uris(pendente.Faixas));
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("[]")]
    [InlineData("{\"formato\":3,\"playlists\":[]}")]
    [InlineData("{\"formato\":1,\"playlists\":{}}")]
    [InlineData("{\"formato\":2}")]
    [InlineData("")]
    public async Task Arquivo_estragado_vira_nenhuma_playlist_e_adicionar_funciona(string estragado)
    {
        var repo = new TextoMemoria(estragado);
        var g = await Novo(repo);

        Assert.Empty(g.Playlist(B));
        Assert.Empty(g.Pendentes);

        Assert.True((await g.AdicionarAsync(B, [Arquivo(1)])).Ok);
        Assert.Equal(["content://musica/1"], Uris(g.Playlist(B)));
        Assert.Equal(["content://musica/1"], Uris(GerentePlaylists.DeJson(repo.Texto!).PorMemoria[B]));
    }

    [Fact]
    public void Faixa_sem_id_sem_uri_ou_repetida_e_descartada_e_as_outras_ficam()
    {
        const string json = """
            {"formato":2,"playlists":[
              {"memoria":"a","faixas":[{"id":"a","uri":"content://1","nome":"Um","pasta":"P","duracaoMs":61000},
               {"uri":"content://sem-id"},
               {"id":"b"},
               {"id":"a","uri":"content://repetido"},
               5,
               {"id":"c","uri":"content://3","duracaoMs":-4}]},
              {"faixas":[{"id":"x","uri":"content://sem-memoria"}]},
              "não é objeto",
              {"memoria":"a","faixas":[{"id":"y","uri":"content://memoria-repetida"}]},
              {"memoria":"d","faixas":[{"id":"d","uri":"content://4"}]}],
             "pendentes":[{"nome":"FILME","faixas":[{"id":"e","uri":"content://5"}]},{"nome":"","faixas":[{"id":"f","uri":"content://6"}]}]}
            """;

        var dados = GerentePlaylists.DeJson(json);

        Assert.Equal(["a", "d"], dados.PorMemoria.Keys.Order());
        Assert.Equal(["content://1", "content://3"], Uris(dados.PorMemoria["a"]));
        Assert.Equal(new Faixa("a", "content://1", "Um", "P", TimeSpan.FromSeconds(61)), dados.PorMemoria["a"][0]);
        Assert.Equal(TimeSpan.Zero, dados.PorMemoria["a"][1].Duracao);
        Assert.Equal(["content://4"], Uris(dados.PorMemoria["d"]));
        var pendente = Assert.Single(dados.Pendentes);
        Assert.Equal(("FILME", "content://5"), (pendente.Nome, pendente.Faixas.Single().Uri));
    }

    [Fact]
    public async Task Falha_ao_gravar_a_lista_nao_muda_e_a_frase_e_a_da_playlist()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1), Arquivo(2)]);
        var antes = g.Playlist(A);
        var texto = repo.Texto;
        repo.FalharAoGravar = true;
        var mudou = 0;
        g.Mudou += () => mudou++;

        var adicionar = await g.AdicionarAsync(A, [Arquivo(3)]);
        var mover = await g.MoverAsync(A, 1, 0);
        var remover = await g.RemoverAsync(A, antes[0].Id);

        Assert.All([adicionar, mover, remover], r =>
        {
            Assert.False(r.Ok);
            Assert.Equal("Não consegui guardar a playlist no celular.", r.Mensagem);
        });
        Assert.Same(antes, g.Playlist(A));
        Assert.Equal(texto, repo.Texto);
        Assert.Equal(0, mudou);
        Assert.Equal(1 + 3 * 2, repo.Tentativas); // duas tentativas em cada edição
    }

    [Theory]
    [InlineData("\"61000\"")]
    [InlineData("9223372036854775807")]
    [InlineData("1e30")]
    [InlineData("61000.5")]
    [InlineData("true")]
    public async Task Duracao_que_nao_e_numero_em_ms_valido_vira_0_e_a_faixa_fica(string duracao)
    {
        var json = $$"""
            {"formato":2,"playlists":[{"memoria":"{{A}}","faixas":[{"id":"a","uri":"content://1","duracaoMs":{{duracao}}},{"id":"b","uri":"content://2","duracaoMs":2000}]}],"pendentes":[]}
            """;
        var repo = new TextoMemoria(json);

        var g = await Novo(repo); // Carregada completa sem falhar

        Assert.Equal(["content://1", "content://2"], Uris(g.Playlist(A)));
        Assert.Equal(TimeSpan.Zero, g.Playlist(A)[0].Duracao);
        Assert.Equal(TimeSpan.FromSeconds(2), g.Playlist(A)[1].Duracao);
        Assert.True((await g.AdicionarAsync(A, [Arquivo(3)])).Ok); // as edições seguem valendo
        Assert.Equal(3, GerentePlaylists.DeJson(repo.Texto!).PorMemoria[A].Count);
    }

    // ----------------------------------------------------------------- migração e adoção (8d)

    [Fact]
    public async Task Formato_1_cada_lista_nao_vazia_vira_pendente_com_o_nome_do_prototipo_e_as_vazias_somem()
    {
        // Posição 0 vazia, posição 1 com 2 faixas, posição 2 com 3.
        var repo = new TextoMemoria(Formato1([], ["o1", "o2"], ["f1", "f2", "f3"]));

        var g = await Novo(repo);

        Assert.Equal(["MUSICA ORIG", "FILME"], g.Pendentes.Select(p => p.Nome));
        Assert.Equal(["content://o1", "content://o2"], Uris(g.Pendentes[0].Faixas));
        Assert.Equal(["content://f1", "content://f2", "content://f3"], Uris(g.Pendentes[1].Faixas));
        Assert.Empty(g.Dados.PorMemoria);
        Assert.Equal(0, repo.Gravacoes); // só ler não grava
    }

    [Fact]
    public async Task Adotar_a_pendente_vai_para_a_primeira_memoria_com_o_mesmo_nome_sem_diferenca_de_caixa()
    {
        var repo = new TextoMemoria(Formato1([], ["o1", "o2"], ["f1", "f2", "f3"]));
        var g = await Novo(repo);

        var adotadas = await g.AdotarAsync([(A, "Musica Alta"), (B, "filme"), (C, "FILME")]);

        Assert.Equal(1, adotadas);
        Assert.Equal(["content://f1", "content://f2", "content://f3"], Uris(g.Playlist(B))); // a primeira, B
        Assert.Empty(g.Playlist(C));
        Assert.Equal("MUSICA ORIG", Assert.Single(g.Pendentes).Nome); // sem correspondente, espera
        var gravado = GerentePlaylists.DeJson(repo.Texto!);
        Assert.Equal(["content://f1", "content://f2", "content://f3"], Uris(gravado.PorMemoria[B]));
        Assert.Equal("MUSICA ORIG", Assert.Single(gravado.Pendentes).Nome);
        Assert.Contains("\"formato\": 2", repo.Texto!);
    }

    [Fact]
    public async Task Adocao_tardia_a_memoria_criada_depois_recebe_as_faixas_e_a_pendente_sai()
    {
        var repo = new TextoMemoria(Formato1([], ["o1", "o2"], []));
        var g = await Novo(repo);
        Assert.Equal(0, await g.AdotarAsync([(A, "FILME")]));
        Assert.Equal(0, repo.Gravacoes); // nada a adotar: nada gravado

        Assert.Equal(1, await g.AdotarAsync([(A, "FILME"), (B, "MUSICA ORIG")]));

        Assert.Equal(["content://o1", "content://o2"], Uris(g.Playlist(B)));
        Assert.Empty(g.Pendentes);
    }

    [Fact]
    public async Task Adotar_numa_memoria_que_ja_tem_playlist_poe_as_faixas_no_fim()
    {
        var repo = new TextoMemoria(Formato1([], [], ["f1"]));
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1)]);

        await g.AdotarAsync([(A, "FILME")]);

        Assert.Equal(["content://musica/1", "content://f1"], Uris(g.Playlist(A)));
    }

    [Fact]
    public async Task Adocao_que_nao_grava_deixa_a_pendente_esperando()
    {
        var repo = new TextoMemoria(Formato1([], [], ["f1"]));
        var g = await Novo(repo);
        repo.FalharAoGravar = true;

        Assert.Equal(0, await g.AdotarAsync([(A, "FILME")]));

        Assert.Empty(g.Playlist(A));
        Assert.Equal("FILME", Assert.Single(g.Pendentes).Nome);
    }

    [Fact]
    public async Task Apagar_a_playlist_de_memorias_apagadas_tira_so_as_delas()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1)]);
        await g.AdicionarAsync(B, [Arquivo(2)]);
        await g.AdicionarAsync(C, [Arquivo(3)]);

        Assert.True(await g.ApagarAsync([A, C, "sem-playlist"]));

        Assert.Empty(g.Playlist(A));
        Assert.Empty(g.Playlist(C));
        Assert.Equal(["content://musica/2"], Uris(g.Playlist(B)));
        Assert.Equal([B], GerentePlaylists.DeJson(repo.Texto!).PorMemoria.Keys);
    }

    [Fact]
    public async Task Apagar_sem_playlist_nao_grava()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);

        Assert.True(await g.ApagarAsync([A]));

        Assert.Equal(0, repo.Gravacoes);
    }

    [Fact]
    public async Task Esvaziar_uma_playlist_tira_a_memoria_do_arquivo()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(A, [Arquivo(1)]);

        Assert.True((await g.RemoverAsync(A, g.Playlist(A)[0].Id)).Ok);

        Assert.Empty(GerentePlaylists.DeJson(repo.Texto!).PorMemoria);
    }
}
