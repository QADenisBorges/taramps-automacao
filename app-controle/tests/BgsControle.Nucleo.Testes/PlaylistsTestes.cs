using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// As playlists do Player (história 8b) no núcleo: o <c>playlists.json</c>, uma playlist por posição
/// de memória, e as edições que só valem se gravaram. Nada aqui fala com o processador.
/// </summary>
public sealed class PlaylistsTestes
{
    private static ArquivoAudio Arquivo(int i) =>
        new($"content://musica/{i}", $"Faixa {i}", "Music", TimeSpan.FromSeconds(100 + i));

    private static async Task<GerentePlaylists> Novo(TextoMemoria repositorio)
    {
        var g = new GerentePlaylists(repositorio, new LogBle());
        await g.Carregada;
        return g;
    }

    private static string[] Uris(IEnumerable<Faixa> faixas) => faixas.Select(f => f.Uri).ToArray();

    [Fact]
    public async Task Sem_arquivo_sao_3_playlists_vazias()
    {
        var g = await Novo(new TextoMemoria());

        Assert.All(Enumerable.Range(0, 3), n => Assert.Empty(g.Playlist(n)));
        Assert.Empty(g.Playlist(3));
        Assert.Empty(g.Playlist(-1));
    }

    [Fact]
    public async Task Adicionar_varios_entram_no_fim_na_ordem_com_ids_unicos_e_o_arquivo_e_gravado()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        var mudou = 0;
        g.Mudou += () => mudou++;

        Assert.True((await g.AdicionarAsync(2, [Arquivo(1), Arquivo(2), Arquivo(3)])).Ok);
        Assert.True((await g.AdicionarAsync(2, [Arquivo(4)])).Ok);

        Assert.Equal(["content://musica/1", "content://musica/2", "content://musica/3", "content://musica/4"], Uris(g.Playlist(2)));
        Assert.Equal(4, g.Playlist(2).Select(f => f.Id).Distinct().Count());
        var primeira = g.Playlist(2)[0];
        Assert.Equal(("Faixa 1", "Music", TimeSpan.FromSeconds(101)), (primeira.Nome, primeira.Pasta, primeira.Duracao));
        Assert.Empty(g.Playlist(0));
        Assert.Equal(2, repo.Gravacoes);
        Assert.Equal(2, mudou);
        Assert.Equal(g.Playlist(2), GerentePlaylists.DeJson(repo.Texto!)[2]);
    }

    [Fact]
    public async Task O_mesmo_arquivo_duas_vezes_vira_duas_faixas()
    {
        var g = await Novo(new TextoMemoria());

        await g.AdicionarAsync(0, [Arquivo(1), Arquivo(1)]);

        Assert.Equal(2, g.Playlist(0).Count);
        Assert.NotEqual(g.Playlist(0)[0].Id, g.Playlist(0)[1].Id);
    }

    [Fact]
    public async Task Mover_a_3_para_a_1_e_a_1_para_o_fim()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(1, [Arquivo(1), Arquivo(2), Arquivo(3)]);

        Assert.True((await g.MoverAsync(1, 2, 0)).Ok);
        Assert.Equal(["content://musica/3", "content://musica/1", "content://musica/2"], Uris(g.Playlist(1)));

        Assert.True((await g.MoverAsync(1, 0, 2)).Ok);
        Assert.Equal(["content://musica/1", "content://musica/2", "content://musica/3"], Uris(g.Playlist(1)));
        Assert.Equal(Uris(g.Playlist(1)), Uris(GerentePlaylists.DeJson(repo.Texto!)[1]));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public async Task Mover_fora_da_lista_e_recusado_sem_gravar(int de, int para)
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(0, [Arquivo(1), Arquivo(2), Arquivo(3)]);

        var r = await g.MoverAsync(0, de, para);

        Assert.False(r.Ok);
        Assert.Equal(GerentePlaylists.MsgPosicao, r.Mensagem);
        Assert.Equal(1, repo.Gravacoes);
    }

    [Fact]
    public async Task Remover_tira_so_aquela_faixa_e_uma_inexistente_e_recusada()
    {
        var g = await Novo(new TextoMemoria());
        await g.AdicionarAsync(0, [Arquivo(1), Arquivo(2)]);
        var id = g.Playlist(0)[0].Id;

        Assert.True((await g.RemoverAsync(0, id)).Ok);
        Assert.Equal(["content://musica/2"], Uris(g.Playlist(0)));

        var r = await g.RemoverAsync(0, id);
        Assert.False(r.Ok);
        Assert.Equal(GerentePlaylists.MsgNaoExiste, r.Mensagem);
    }

    [Fact]
    public async Task Memoria_fora_de_0_a_2_e_recusada()
    {
        var g = await Novo(new TextoMemoria());

        Assert.Equal(GerentePlaylists.MsgMemoria, (await g.AdicionarAsync(3, [Arquivo(1)])).Mensagem);
        Assert.Equal(GerentePlaylists.MsgMemoria, (await g.RemoverAsync(-1, "x")).Mensagem);
        Assert.Equal(GerentePlaylists.MsgMemoria, (await g.MoverAsync(3, 0, 1)).Mensagem);
    }

    [Fact]
    public async Task Reabrir_com_o_mesmo_repositorio_as_3_playlists_voltam_na_ordem()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(0, [Arquivo(1), Arquivo(2)]);
        await g.AdicionarAsync(1, [Arquivo(3)]);
        await g.AdicionarAsync(2, [Arquivo(4), Arquivo(5), Arquivo(6)]);
        await g.MoverAsync(2, 2, 0);

        var outro = await Novo(repo);

        foreach (var n in new[] { 0, 1, 2 })
            Assert.Equal(g.Playlist(n), outro.Playlist(n));
        Assert.Equal(["content://musica/6", "content://musica/4", "content://musica/5"], Uris(outro.Playlist(2)));
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("[]")]
    [InlineData("{\"formato\":2,\"playlists\":[[{\"id\":\"a\",\"uri\":\"content://x\"}],[],[]]}")]
    [InlineData("{\"formato\":1,\"playlists\":{}}")]
    [InlineData("{\"formato\":1}")]
    [InlineData("")]
    public async Task Arquivo_estragado_vira_3_vazias_e_adicionar_funciona(string estragado)
    {
        var repo = new TextoMemoria(estragado);
        var g = await Novo(repo);

        Assert.All(Enumerable.Range(0, 3), n => Assert.Empty(g.Playlist(n)));

        Assert.True((await g.AdicionarAsync(1, [Arquivo(1)])).Ok);
        Assert.Equal(["content://musica/1"], Uris(g.Playlist(1)));
        Assert.Equal(["content://musica/1"], Uris(GerentePlaylists.DeJson(repo.Texto!)[1]));
    }

    [Fact]
    public void Faixa_sem_id_sem_uri_ou_repetida_e_descartada_e_as_outras_ficam()
    {
        const string json = """
            {"formato":1,"playlists":[
              [{"id":"a","uri":"content://1","nome":"Um","pasta":"P","duracaoMs":61000},
               {"uri":"content://sem-id"},
               {"id":"b"},
               {"id":"a","uri":"content://repetido"},
               5,
               {"id":"c","uri":"content://3","duracaoMs":-4}],
              "não é lista",
              [{"id":"d","uri":"content://4"}],
              [{"id":"e","uri":"content://sobra"}]]}
            """;

        var listas = GerentePlaylists.DeJson(json);

        Assert.Equal(3, listas.Length);
        Assert.Equal(["content://1", "content://3"], Uris(listas[0]));
        Assert.Equal(new Faixa("a", "content://1", "Um", "P", TimeSpan.FromSeconds(61)), listas[0][0]);
        Assert.Equal(TimeSpan.Zero, listas[0][1].Duracao);
        Assert.Empty(listas[1]);
        Assert.Equal(["content://4"], Uris(listas[2]));
    }

    [Fact]
    public async Task Falha_ao_gravar_a_lista_nao_muda_e_a_frase_e_a_da_playlist()
    {
        var repo = new TextoMemoria();
        var g = await Novo(repo);
        await g.AdicionarAsync(0, [Arquivo(1), Arquivo(2)]);
        var antes = g.Playlist(0);
        var texto = repo.Texto;
        repo.FalharAoGravar = true;
        var mudou = 0;
        g.Mudou += () => mudou++;

        var adicionar = await g.AdicionarAsync(0, [Arquivo(3)]);
        var mover = await g.MoverAsync(0, 1, 0);
        var remover = await g.RemoverAsync(0, antes[0].Id);

        Assert.All([adicionar, mover, remover], r =>
        {
            Assert.False(r.Ok);
            Assert.Equal("Não consegui guardar a playlist no celular.", r.Mensagem);
        });
        Assert.Same(antes, g.Playlist(0));
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
            {"formato":1,"playlists":[[{"id":"a","uri":"content://1","duracaoMs":{{duracao}}},{"id":"b","uri":"content://2","duracaoMs":2000}],[],[]]}
            """;
        var repo = new TextoMemoria(json);

        var g = await Novo(repo); // Carregada completa sem falhar

        Assert.Equal(["content://1", "content://2"], Uris(g.Playlist(0)));
        Assert.Equal(TimeSpan.Zero, g.Playlist(0)[0].Duracao);
        Assert.Equal(TimeSpan.FromSeconds(2), g.Playlist(0)[1].Duracao);
        Assert.True((await g.AdicionarAsync(0, [Arquivo(3)])).Ok); // as edições seguem valendo
        Assert.Equal(3, GerentePlaylists.DeJson(repo.Texto!)[0].Count);
    }
}
