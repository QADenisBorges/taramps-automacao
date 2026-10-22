using System.Text.RegularExpressions;
using BgsControle.Nucleo;

namespace BgsControle.Telas.Testes;

/// <summary>A monitoração ao vivo (história 7b) contra o processador simulado: voltímetro e clip.</summary>
public sealed class MonitoracaoViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly Bancada _b = new();
    private readonly MonitoracaoViewModel _vm;

    public MonitoracaoViewModelTestes() => _vm = _b.CriarMonitoracaoViewModel();

    /// <summary>Critério de aceite: a monitoração só ouve; só a senha e as leituras do catálogo chegaram.</summary>
    public void Dispose()
    {
        _vm.Dispose();
        Assert.All(_b.Simulador.Escritas, e => Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e), e));
        Assert.Empty(_b.Tela.Erros);
    }

    private bool[] Clips() => _vm.Saidas.Select(s => s.Aceso).ToArray();

    [Fact]
    public async Task Sem_valor_mostra_um_traco_e_os_clips_apagados()
    {
        Assert.Equal("—", _vm.Voltimetro);
        Assert.Equal(["Saída 1", "Saída 2", "Saída 3", "Saída 4"], _vm.Saidas.Select(s => s.Nome));
        Assert.Equal("Entrada", _vm.Entrada.Nome);

        await _b.Conectar();

        Assert.Equal("—", _vm.Voltimetro); // a consulta #005# responde ERRO 0
        Assert.Equal([false, false, false, false], Clips());
        Assert.False(_vm.Entrada.Aceso);
    }

    [Fact]
    public async Task Voltimetro_espontaneo_mostra_12_4_V()
    {
        await _b.Conectar();

        _b.Simulador.EmitirVoltimetro(124);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("12,4 V", _vm.Voltimetro);
    }

    [Fact]
    public async Task Clip_da_saida_3_acende_e_apaga_3_s_depois()
    {
        await _b.Conectar();

        _b.Simulador.EmitirClip(4);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal([false, false, true, false], Clips());
        Assert.True(_vm.Saida(3).Aceso);

        await _b.Andar(TimeSpan.FromSeconds(2.9));
        Assert.True(_vm.Saida(3).Aceso);
        await _b.Andar(TimeSpan.FromMilliseconds(200));
        Assert.False(_vm.Saida(3).Aceso);
    }

    [Fact]
    public async Task Clip_da_entrada_acende()
    {
        await _b.Conectar();

        _b.Simulador.EmitirClipEntrada(true);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Entrada.Aceso);
        Assert.Equal([false, false, false, false], Clips());
    }

    [Fact]
    public async Task Na_queda_o_voltimetro_volta_ao_traco_e_os_clips_apagam()
    {
        await _b.Conectar();
        _b.Simulador.EmitirVoltimetro(124);
        _b.Simulador.EmitirClip(0b1111);
        _b.Simulador.EmitirClipEntrada(true);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal([true, true, true, true], Clips());

        _b.Simulador.Derrubar();
        await Apoio.Esperar(() => _b.Sessao.Estado == EstadoSessao.Desconectado);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("—", _vm.Voltimetro);
        Assert.Equal([false, false, false, false], Clips());
        Assert.False(_vm.Entrada.Aceso);
    }
}
