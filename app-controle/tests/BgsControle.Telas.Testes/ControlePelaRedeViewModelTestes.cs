using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas.Testes;

/// <summary>Uma rede local falsa: as faixas privadas, sem olhar a sub-rede; o celular em 192.168.0.5.</summary>
internal sealed class RedeFalsa : IRedeLocal
{
    public bool Aceita(IPAddress remoto) => RedeLocal.Privado(remoto) && !IPAddress.IsLoopback(RedeLocal.Normalizar(remoto));

    public string? Endereco => "192.168.0.5";
}

/// <summary>
/// A história 9c nas telas, contra o processador simulado: a chave "Controle pela rede" no grupo
/// APARELHO das Configurações (desligada, ligar com a folha do risco, o código, pareado com o nome e a
/// data, o endereço, o erro, desligar revoga), a folha de risco do app (Confirmar e Cancelar, sem a IA),
/// o toque que sempre pausa, e o critério de aceite de ponta a ponta (estado, ganho da saída 3 e o
/// remote out com o toque; o histórico lido pela porta).
/// </summary>
public sealed class ControlePelaRedeViewModelTestes : IDisposable
{
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    private readonly Bancada _b = new(usoCompartilhado: true, setups: true);
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly PortaComandos _porta;
    private readonly ChavesViewModel _chaves;
    private readonly ControlePelaRedeViewModel _vm;
    private readonly FolhaRiscoViewModel _folha;
    private readonly List<string> _respostas = [];

    public ControlePelaRedeViewModelTestes()
    {
        _pareamento = new Pareamento(new RepositorioTextoArquivos(_b.ArquivosUso, RepositorioTextoArquivos.ArquivoPortaIa), _b.Relogio, _b.Log);
        _confirmacoes = new PedidosConfirmacao(_b.Relogio, _b.Log);
        _porta = NovaPorta(0);
        _chaves = new ChavesViewModel(_b.Chaves, _b.Tela, _b.Relogio);
        _vm = new ControlePelaRedeViewModel(_porta, _chaves, _b.Tela, _b.Relogio);
        _folha = new FolhaRiscoViewModel(_confirmacoes, _b.Tela, _b.Relogio);
    }

    private UsoCompartilhado Uso => _b.Uso!;
    private Historico Historico => _b.Historico!;

    public void Dispose()
    {
        _vm.Dispose();
        _chaves.Dispose();
        _folha.Dispose();
        _porta.Dispose();
        Assert.Empty(_b.Tela.Erros);
        Assert.DoesNotContain("AB12", Historico.ParaJson(Historico.Entradas));
        Assert.All(_respostas, r => Assert.DoesNotContain("AB12", r));
    }

    private PortaComandos NovaPorta(int numero) =>
        new(_b.Sessao, _b.Chaves, Uso, Historico, _pareamento, _confirmacoes, new RedeFalsa(), _b.Estado, _b.Escritor,
            _b.Setups!, _b.Gerador, _b.Comandos, _b.Monitor, _b.Log, _b.Relogio, numero);

    private async Task Conectar()
    {
        await Historico.Carregado;
        await _pareamento.Carregado;
        await _b.Conectar();
    }

    /// <summary>Liga a chave como o Denis: Lig. na linha, e "Ligar a chave" na folha do risco.</summary>
    private async Task LigarPorta()
    {
        await _vm.Linha.Ligar.ExecutarAsync();
        Assert.True(_chaves.FolhaAberta);
        await _chaves.ConfirmarFolha.ExecutarAsync();
        await _porta.AtualizarAsync();
        await _b.Tela.EsvaziarAsync();
    }

    private async Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null)
    {
        var r = await _porta.AtenderAsync(PedidoPorta.De(metodo, alvo, Casa, corpo, chave));
        lock (_respostas)
            _respostas.Add(r.Json);
        return r;
    }

    private async Task<string> Parear()
    {
        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{_pareamento.Codigo}}","nome":"Claude"}""");
        Assert.Equal(200, r.Status);
        await _b.Tela.EsvaziarAsync();
        return JsonDocument.Parse(r.Json).RootElement.GetProperty("chave").GetString()!;
    }

    private static bool Ok(RespostaPorta r) => JsonDocument.Parse(r.Json).RootElement.GetProperty("ok").GetBoolean();

    private static string? Motivo(RespostaPorta r) =>
        JsonDocument.Parse(r.Json).RootElement.TryGetProperty("motivo", out var m) ? m.GetString() : null;

    /// <summary>O ganho da saída 3 (canal 2) pelo campo, como o Denis faz.</summary>
    private async Task GanhoPelaTela(string texto)
    {
        using var c = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 2);
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = texto;
        await _b.Rodar(c.Confirmar.ExecutarAsync());
        await _b.Rodar(c.UltimaEscrita);
        Assert.Null(c.Aviso); // o toque da tela nunca é recusado pelo uso compartilhado
    }

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Cliente_pareado_le_o_estado_muda_o_ganho_da_saida_3_e_desliga_o_remote_out_com_o_toque()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        var controle = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 2);
        Assert.NotEqual("+2,0 dB", controle.TextoValor); // a semente traz outro valor

        var estado = await Pedir("GET", "/estado", chave: chave);
        Assert.Equal(200, estado.Status);
        Assert.True(Ok(estado));
        Assert.False(JsonDocument.Parse(estado.Json).RootElement.GetProperty("ia").TryGetProperty("modoToque", out _));

        var ajuste = Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave);
        await _b.Rodar(ajuste);
        Assert.True(Ok(await ajuste));
        // O controle da saída 3 mostra o valor novo em até 1 s (aqui, sem o relógio andar mais).
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("+2,0 dB", controle.TextoValor);

        var remote = Pedir("POST", "/aparelho/remote-out", """{"ligado":false}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_folha.FolhaAberta);
        await _folha.Confirmar.ExecutarAsync();
        await _b.Rodar(remote);
        Assert.True(Ok(await remote));
        // As memórias do aparelho não têm caminho na porta (o salvar desde a 8c, o carregar desde 29/10/2026).
        Assert.Equal(404, (await Pedir("POST", "/memoria/salvar", """{"memoria":2}""", chave)).Status);
        Assert.Equal(404, (await Pedir("POST", "/memoria/carregar", """{"memoria":2}""", chave)).Status);

        var daPorta = Historico.Entradas.Where(e => e.Origem == OrigemComando.Ia && e.Enviado).Select(e => e.Tipo).ToArray();
        Assert.Contains(TipoComando.AjusteAudio, daPorta);
        Assert.Contains(TipoComando.RemoteOut, daPorta);
        Assert.DoesNotContain(TipoComando.SalvarMemoria, daPorta);
    }

    [Fact]
    public async Task Um_ajuste_pela_tela_aparece_no_historico_lido_pela_porta_com_a_origem_tela()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();

        await GanhoPelaTela("3");

        var r = await Pedir("GET", "/historico?limite=5", chave: chave);
        Assert.Equal(200, r.Status);
        // O ganho da saída 3 (o link 3–4 espelha na saída 4, que também entra).
        var entrada = JsonDocument.Parse(r.Json).RootElement.GetProperty("entradas").EnumerateArray()
            .First(e => e.GetProperty("codigo").GetString() == "081" && e.GetProperty("indice").GetInt32() == 2);
        Assert.Equal("Tela", entrada.GetProperty("origem").GetString());
        Assert.Equal("3.0", entrada.GetProperty("valor").GetString());
        Assert.True(entrada.GetProperty("enviado").GetBoolean());
    }

    [Fact]
    public async Task O_reaplicar_sozinho_da_diferenca_leve_escreve_como_App_e_nao_prende_o_ajuste_para_a_porta()
    {
        _b.Simulador.Definir("081", 0, "+4.0"); // só o ganho da saída 1 difere da FILME
        await Conectar();
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0)); // reaplicado sozinho na conferência

        var reaplicadas = Historico.Entradas.Where(e => e.Endereco is { Codigo: "081", Indice: 0 } && e.Enviado).ToArray();
        Assert.NotEmpty(reaplicadas);
        Assert.All(reaplicadas, e => Assert.Equal(OrigemComando.App, e.Origem));
        Assert.Equal(EstadoIa.Inativa, Uso.Estado); // não contou como toque nem como pedido da porta

        await LigarPorta();
        var chave = await Parear();
        var pedido = Pedir("POST", "/ajuste", """{"codigo":"081","indice":0,"valor":9.0}""", chave);
        await _b.Rodar(pedido);
        var r = await pedido;

        Assert.NotEqual(UsoCompartilhado.MsgMexendo, Motivo(r));
        Assert.Equal(200, r.Status);
        Assert.Equal("+9.0", _b.Simulador.Valor("081", 0));
    }

    // ----------------------------------------------------------------- a chave no grupo APARELHO

    [Fact]
    public async Task A_linha_e_a_chave_Controle_pela_rede_com_o_risco_e_a_tela_Chaves_nao_a_lista()
    {
        await Conectar();

        Assert.Equal("Controle pela rede", _vm.Linha.Nome);
        Assert.Equal("Deixa um computador da casa comandar o som pela rede.", _vm.Linha.Risco);
        Assert.Same(_chaves.Item(Chave.ControlePelaRede), _vm.Linha);
        Assert.DoesNotContain(_chaves.Itens, i => i.Chave == Chave.ControlePelaRede);
        Assert.Equal(ChavesPadrao.Todas.Count - 1, _chaves.Itens.Count);
    }

    [Fact]
    public async Task Com_a_chave_desligada_nada_aparece_embaixo_dela()
    {
        await Conectar();
        await _porta.Inicio;
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Ligada);
        Assert.Null(_vm.Codigo);
        Assert.Null(_vm.Endereco);
        Assert.Null(_vm.Cliente);
        Assert.False(_vm.Pareado);
        Assert.Null(_vm.Erro);
    }

    [Fact]
    public async Task Desistir_da_folha_do_risco_deixa_a_chave_desligada()
    {
        await Conectar();

        await _vm.Linha.Ligar.ExecutarAsync();
        Assert.Equal("Ligar a chave “Controle pela rede”?", _chaves.FolhaTitulo);
        Assert.Equal("Deixa um computador da casa comandar o som pela rede.", _chaves.FolhaRisco);
        await _chaves.DesistirFolha.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_b.Chaves.Ligada(Chave.ControlePelaRede));
        Assert.False(_vm.Ligada);
        Assert.False(_porta.Aberta);
    }

    [Fact]
    public async Task Ligar_a_chave_abre_a_porta_e_mostra_o_codigo_de_6_digitos_e_o_endereco_embaixo()
    {
        await Conectar();
        await LigarPorta();

        Assert.True(_vm.Ligada);
        Assert.True(_porta.Aberta);
        Assert.Matches(@"^\d{6}$", _vm.Codigo);
        Assert.Equal(_pareamento.Codigo, _vm.Codigo);
        Assert.Equal($"192.168.0.5:{_porta.NumeroPorta}", _vm.Endereco);
        Assert.False(_vm.Pareado);
        Assert.Null(_vm.Cliente);
    }

    [Fact]
    public async Task Pareado_mostra_o_nome_e_a_data_no_lugar_do_codigo()
    {
        await Conectar();
        await LigarPorta();

        await Parear();

        Assert.True(_vm.Pareado);
        Assert.Null(_vm.Codigo);
        Assert.Equal("Claude", _vm.Cliente);
        Assert.Equal("21/10/2026 12:00", _vm.PareadoEm);
        Assert.NotNull(_vm.Endereco);
    }

    [Fact]
    public async Task Desligar_a_chave_fecha_a_porta_revoga_o_cliente_e_ao_religar_vem_um_codigo_novo()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();

        await _vm.Linha.Desligar.ExecutarAsync();
        await _porta.AtualizarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Ligada);
        Assert.False(_porta.Aberta);
        Assert.False(_pareamento.Pareado);
        Assert.True(Uso.Parada); // o PARAR IA interno, só como efeito do revogar
        Assert.Null(_vm.Cliente);
        Assert.Null(_vm.Codigo);

        await LigarPorta();
        Assert.Matches(@"^\d{6}$", _vm.Codigo);
        Assert.False(_vm.Pareado);
        Assert.Equal(401, (await Pedir("GET", "/estado", chave: chave)).Status);
    }

    [Fact]
    public async Task Porta_ocupada_mostra_o_erro_embaixo_da_chave()
    {
        await Conectar();
        var ocupante = new TcpListener(IPAddress.IPv6Any, 0);
        ocupante.Server.DualMode = true;
        ocupante.Start();
        try
        {
            var numero = ((IPEndPoint)ocupante.LocalEndpoint).Port;
            using var porta = NovaPorta(numero);
            using var vm = new ControlePelaRedeViewModel(porta, _chaves, _b.Tela, _b.Relogio);
            Assert.True((await _b.Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
            await porta.AtualizarAsync();
            await _b.Tela.EsvaziarAsync();

            Assert.True(vm.Ligada);
            Assert.Equal($"Não consegui abrir a porta {numero}.", vm.Erro);
            Assert.Null(vm.Codigo);
        }
        finally
        {
            ocupante.Stop();
        }
    }

    // ----------------------------------------------------------------- o toque sempre pausa

    [Fact]
    public async Task Toque_com_a_tarefa_ativa_escreve_o_ganho_pausa_e_a_porta_recusa_esse_ajuste_por_10_s()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        Assert.Equal(200, (await Pedir("POST", "/tarefa", """{"frase":"ajustando o ganho"}""", chave)).Status);
        Assert.Equal(EstadoIa.Ativa, Uso.Estado);

        await GanhoPelaTela("4");

        Assert.Equal("+4.0", _b.Simulador.Valor("081", 2));
        Assert.Equal(EstadoIa.Pausada, Uso.Estado);
        var pedidoMesmo = Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":1.0}""", chave);
        await _b.Rodar(pedidoMesmo);
        var mesmo = await pedidoMesmo;
        Assert.Equal(409, mesmo.Status);
        Assert.Contains(Motivo(mesmo), new[] { UsoCompartilhado.MsgMexendo, UsoCompartilhado.MsgPausada });

        await _b.Andar(TimeSpan.FromSeconds(10.5), passoMs: 100);
        Assert.Equal(EstadoIa.Ativa, Uso.Estado);
        var depois = Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":1.0}""", chave);
        await _b.Rodar(depois);
        Assert.True(Ok(await depois));
    }

    // ----------------------------------------------------------------- a folha de risco do app

    [Fact]
    public async Task A_folha_de_risco_diz_o_que_muda_com_Confirmar_e_Cancelar_e_Cancelar_recusa()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        Assert.False(_folha.FolhaAberta);

        var pedido = Pedir("POST", "/aparelho/remote-out", """{"ligado":false}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_folha.FolhaAberta);
        Assert.Equal("Desligar o remote out?", _folha.Titulo);
        Assert.Equal(ChavesPadrao.De(Chave.RemoteOut).Risco, _folha.Risco);
        Assert.Equal("Confirmar", _folha.TextoConfirmar);
        Assert.Equal("Cancelar", _folha.TextoCancelar);
        Assert.Equal("Sem resposta em 60 s, o pedido é recusado.", _folha.TextoPrazo);

        await _folha.Cancelar.ExecutarAsync();
        var r = await pedido;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário recusou na tela.", Motivo(r));
        Assert.False(_folha.FolhaAberta);
        Assert.Equal("O usuário recusou na tela.", Historico.Entradas[0].Motivo);
    }

    [Fact]
    public async Task Aplicar_a_FILME_pela_porta_abre_a_folha_Aplicar_a_memoria_FILME_sem_a_IA()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();

        var pedido = Pedir("POST", "/memoria/aplicar", """{"memoria":"FILME"}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Aplicar a memória FILME?", _folha.Titulo);
        Assert.Equal("Troca todo o som de uma vez.", _folha.Risco);
        foreach (var texto in new[] { _folha.Titulo, _folha.Risco, _folha.TextoConfirmar, _folha.TextoCancelar, _folha.TextoPrazo })
            Assert.DoesNotMatch(@"\bIA\b", texto!);

        await _folha.Cancelar.ExecutarAsync();
        Assert.Equal("O usuário recusou na tela.", Motivo(await pedido));
    }

    [Fact]
    public async Task A_folha_fecha_sozinha_sem_resposta_em_60_s()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();

        var pedido = Pedir("POST", "/aparelho/remote-out", """{"ligado":false}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_folha.FolhaAberta);
        Assert.Equal("Liga ou corta o som de repente.", _folha.Risco);

        _b.Relogio.Avancar(PedidosConfirmacao.Prazo);
        var r = await pedido;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("O usuário não confirmou a tempo.", Motivo(r));
        Assert.False(_folha.FolhaAberta);
    }
}
