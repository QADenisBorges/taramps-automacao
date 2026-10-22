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
/// A história 9b nas telas, contra o processador simulado: a tela IA pela rede (fechada, pareando
/// com o código, pareado, erro, Revogar, modo do toque, cliente sumido) e a folha "A IA pede para …",
/// mais o critério de aceite de ponta a ponta (estado, ganho da saída 3 e memória 2 com o toque).
/// </summary>
public sealed class IaPelaRedeViewModelTestes : IDisposable
{
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    private readonly Bancada _b = new(usoCompartilhado: true);
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly PortaComandos _porta;
    private readonly IaPelaRedeViewModel _vm;
    private readonly ConfirmacaoIaViewModel _folha;
    private readonly List<string> _respostas = [];

    public IaPelaRedeViewModelTestes() : this(0)
    {
    }

    private IaPelaRedeViewModelTestes(int numero)
    {
        _pareamento = new Pareamento(new RepositorioTextoArquivos(_b.ArquivosUso, RepositorioTextoArquivos.ArquivoPortaIa), _b.Relogio, _b.Log);
        _confirmacoes = new PedidosConfirmacao(_b.Relogio, _b.Log);
        _porta = NovaPorta(numero);
        _vm = new IaPelaRedeViewModel(_porta, Uso, _b.Tela, _b.Relogio);
        _folha = new ConfirmacaoIaViewModel(_confirmacoes, _b.Tela, _b.Relogio);
    }

    private UsoCompartilhado Uso => _b.Uso!;
    private Historico Historico => _b.Historico!;

    public void Dispose()
    {
        _vm.Dispose();
        _folha.Dispose();
        _porta.Dispose();
        Assert.Empty(_b.Tela.Erros);
        Assert.DoesNotContain("AB12", Historico.ParaJson(Historico.Entradas));
        Assert.All(_respostas, r => Assert.DoesNotContain("AB12", r));
    }

    private PortaComandos NovaPorta(int numero) =>
        new(_b.Sessao, _b.Chaves, Uso, Historico, _pareamento, _confirmacoes, new RedeFalsa(), _b.Estado, _b.Escritor,
            _b.Memorias, _b.Gerador, _b.Comandos, _b.Monitor, _b.Log, _b.Relogio, numero);

    private async Task Conectar()
    {
        await Historico.Carregado;
        await Uso.Carregado;
        await _pareamento.Carregado;
        await _b.Conectar();
    }

    private async Task LigarPorta()
    {
        Assert.True((await _b.Chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
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

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Cliente_pareado_le_o_estado_muda_o_ganho_da_saida_3_e_salva_a_memoria_2_com_o_toque()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        var controle = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 2);
        Assert.NotEqual("+2,0 dB", controle.TextoValor); // a semente traz outro valor

        var estado = await Pedir("GET", "/estado", chave: chave);
        Assert.Equal(200, estado.Status);
        Assert.True(Ok(estado));

        var ajuste = Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave);
        await _b.Rodar(ajuste);
        Assert.True(Ok(await ajuste));
        // O controle da saída 3 mostra o valor novo em até 1 s (aqui, sem o relógio andar mais).
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("+2,0 dB", controle.TextoValor);

        var salvar = Pedir("POST", "/memoria/salvar", """{"memoria":2}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_folha.FolhaAberta);
        await _folha.Confirmar.ExecutarAsync();
        await _b.Rodar(salvar);
        Assert.True(Ok(await salvar));

        var daIa = Historico.Entradas.Where(e => e.Origem == OrigemComando.Ia && e.Enviado).Select(e => e.Tipo).ToArray();
        Assert.Contains(TipoComando.AjusteAudio, daIa);
        Assert.Contains(TipoComando.SalvarMemoria, daIa);
        using var h = _b.CriarHistoricoViewModel();
        await _b.Tela.EsvaziarAsync();
        Assert.Contains(h.Linhas, l => l.Origem == "IA");
    }

    // ----------------------------------------------------------------- tela IA pela rede

    [Fact]
    public async Task Com_a_chave_desligada_a_tela_mostra_Fechada_sem_codigo()
    {
        await Conectar();
        await _porta.Inicio;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(EstadoIaPelaRede.Fechada, _vm.Estado);
        Assert.Equal(IaPelaRedeViewModel.MsgFechada, _vm.TextoEstado);
        Assert.Null(_vm.Codigo);
        Assert.Null(_vm.Endereco);
        Assert.False(_vm.PodeRevogar);
    }

    [Fact]
    public async Task Ligar_a_chave_abre_a_porta_e_mostra_o_codigo_e_o_endereco()
    {
        await Conectar();
        await LigarPorta();

        Assert.Equal(EstadoIaPelaRede.Pareando, _vm.Estado);
        Assert.Matches(@"^\d{6}$", _vm.Codigo);
        Assert.Equal(_pareamento.Codigo, _vm.Codigo);
        Assert.Equal($"192.168.0.5:{_porta.NumeroPorta}", _vm.Endereco);
        Assert.Null(_vm.Cliente);
    }

    [Fact]
    public async Task Pareado_mostra_o_cliente_e_a_data_e_o_codigo_some()
    {
        await Conectar();
        await LigarPorta();

        await Parear();

        Assert.Equal(EstadoIaPelaRede.Pareado, _vm.Estado);
        Assert.Null(_vm.Codigo);
        Assert.Equal("Claude", _vm.Cliente);
        Assert.Equal("21/10/2026 12:00", _vm.PareadoEm);
        Assert.True(_vm.PodeRevogar);
    }

    [Fact]
    public async Task Revogar_para_a_IA_apaga_o_cliente_e_mostra_um_codigo_novo()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();

        await _vm.Revogar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(EstadoIaPelaRede.Pareando, _vm.Estado);
        Assert.Matches(@"^\d{6}$", _vm.Codigo);
        Assert.Null(_vm.Cliente);
        Assert.True(Uso.Parada);
        Assert.Equal(401, (await Pedir("GET", "/estado", chave: chave)).Status);
    }

    [Fact]
    public async Task Revogar_sem_cliente_nao_faz_nada()
    {
        await Conectar();
        await LigarPorta();
        var codigo = _vm.Codigo;

        await _vm.Revogar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(Uso.Parada);
        Assert.Equal(codigo, _vm.Codigo);
        Assert.Equal(EstadoIaPelaRede.Pareando, _vm.Estado);
    }

    [Fact]
    public async Task PARAR_IA_da_tela_IA_ativa_tambem_revoga()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        using var ia = _b.CriarIaAtivaViewModel();

        await ia.PararIa.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(EstadoIaPelaRede.Pareando, _vm.Estado);
        Assert.Equal(401, (await Pedir("GET", "/estado", chave: chave)).Status);
    }

    [Fact]
    public async Task O_seletor_do_modo_do_toque_troca_entre_travar_e_pausar()
    {
        await Conectar();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Travar);
        Assert.Equal("Travar até PARAR IA", IaPelaRedeViewModel.TextoTravar);
        Assert.Equal("Pausar a IA", IaPelaRedeViewModel.TextoPausar);

        await _vm.PausarIa.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Pausar);
        Assert.Equal(ModoToque.PausarIa, Uso.Modo);

        await _vm.TravarAtePararIa.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Travar);
        Assert.Null(_vm.Mensagem);
    }

    [Fact]
    public async Task Cliente_sumido_mostra_A_IA_perdeu_a_conexao()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        Assert.Equal(200, (await Pedir("POST", "/tarefa", """{"frase":"IA ajustando o EQ da FILME"}""", chave)).Status);

        _b.Relogio.Avancar(PortaComandos.PrazoCliente);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("A IA perdeu a conexão.", _vm.AvisoConexao);
        Assert.False(Uso.IaAtiva);
    }

    [Fact]
    public async Task Porta_ocupada_mostra_Erro_com_o_numero()
    {
        await Conectar();
        var ocupante = new TcpListener(IPAddress.IPv6Any, 0);
        ocupante.Server.DualMode = true;
        ocupante.Start();
        try
        {
            var numero = ((IPEndPoint)ocupante.LocalEndpoint).Port;
            using var porta = NovaPorta(numero);
            using var vm = new IaPelaRedeViewModel(porta, Uso, _b.Tela, _b.Relogio);
            Assert.True((await _b.Chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
            await porta.AtualizarAsync();
            await _b.Tela.EsvaziarAsync();

            Assert.Equal(EstadoIaPelaRede.Erro, vm.Estado);
            Assert.Equal($"Não consegui abrir a porta {numero}.", vm.TextoEstado);
        }
        finally
        {
            ocupante.Stop();
        }
    }

    [Fact]
    public async Task Desligar_a_chave_na_tela_fecha_a_porta_e_apaga_o_cliente()
    {
        await Conectar();
        await LigarPorta();
        await Parear();
        using var chaves = new ChavesViewModel(_b.Chaves, _b.Tela, _b.Relogio);

        await chaves.Item(Chave.IaPelaRede).Desligar.ExecutarAsync();
        await _porta.AtualizarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(EstadoIaPelaRede.Fechada, _vm.Estado);
        Assert.False(_pareamento.Pareado);
        Assert.Null(_vm.Cliente);
    }

    // ----------------------------------------------------------------- folha "A IA pede para …"

    [Fact]
    public async Task A_folha_mostra_o_pedido_o_risco_e_o_botao_e_Nao_deixar_recusa()
    {
        await Conectar();
        await LigarPorta();
        var chave = await Parear();
        Assert.False(_folha.FolhaAberta);

        var pedido = Pedir("POST", "/memoria/carregar", """{"memoria":3}""", chave);
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_folha.FolhaAberta);
        Assert.Equal("A IA pede para carregar a FILME", _folha.Titulo);
        Assert.Equal(ChavesPadrao.De(Chave.CarregarMemoria).Risco, _folha.Risco);
        Assert.Equal("Carregar a FILME", _folha.TextoConfirmar);
        Assert.Equal("Não deixar", _folha.TextoRecusar);

        await _folha.Recusar.ExecutarAsync();
        var r = await pedido;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário recusou na tela.", Motivo(r));
        Assert.False(_folha.FolhaAberta);
        Assert.Equal("O usuário recusou na tela.", Historico.Entradas[0].Motivo);
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
