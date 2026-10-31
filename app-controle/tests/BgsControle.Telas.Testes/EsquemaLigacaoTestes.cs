using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Setups;
using BgsControle.Telas.Esquema;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 10d: o esquema de ligação plano do Aplicando, montado só com os dados do Setup: o PRO 2.4BT com CH1 a
/// CH4, os equipamentos que cada canal alimenta e os fios, nas posições do mock (viewBox 230 × 215).
/// </summary>
public sealed class EsquemaLigacaoTestes
{
    private static FonteSinal Ch(int n) => FonteSinal.DoCanal(n - 1);

    [Fact]
    public void Casa_2_1_as_JBL_em_CH1_e_CH2_e_o_Sub_em_CH3_e_CH4()
    {
        var e = EsquemaLigacao.Montar(ConhecimentoHerdado.EquipamentosCasa);

        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], e.Portas.Select(p => p.Rotulo));
        Assert.Equal([34d, 80d, 126d, 172d], e.Portas.Select(p => p.Y));
        // O módulo não toca: entram os alto-falantes que ele alimenta.
        Assert.Equal(["JBL L", "JBL R", "Sub"], e.Equipamentos.Select(x => x.Nome));
        Assert.Equal([[0], [1], [2, 3]], e.Equipamentos.Select(x => x.Canais.ToArray()));
        // Repartidos em 190 a partir de y 14, 84 × 20 (como o mock: 14 + i·190/3 + 190/6 − 10).
        Assert.Equal(14 + 190 / 6.0 - 10, e.Equipamentos[0].Y, 6);
        Assert.True(e.Equipamentos[0].Y < e.Equipamentos[1].Y && e.Equipamentos[1].Y < e.Equipamentos[2].Y);
        // Um fio por canal: o Sub recebe dois.
        Assert.Equal([0, 1, 2, 3], e.Fios.Select(f => f.Canal));
        Assert.Equal(2, e.Fios.Count(f => f.AteY == e.Equipamentos[2].MeioY));
        Assert.All(e.Fios, f => Assert.Equal(EsquemaLigacao.PortaY(f.Canal), f.DeY));
        // O link 3–4 sai das ligações: o CH3 e o CH4 acendem juntos.
        Assert.False(e.Link12);
        Assert.True(e.Link34);
        Assert.Equal([2, 3], e.Juntos(3));
        Assert.Equal([0], e.Juntos(0));
        Assert.Equal("CH1 alimenta JBL L. CH2 alimenta JBL R. CH3 alimenta Sub. CH4 alimenta Sub.", e.Descricao);
        Assert.False(e.Vazio);
    }

    [Fact]
    public void Um_Setup_de_4_canais_cada_um_com_o_seu_equipamento()
    {
        Equipamento Caixa(string id, string nome, int canal) =>
            new(id, GrupoEquipamento.AmplificaETocam, "caixa ativa", nome) { Entradas = [new Entrada(1, Ch(canal))] };
        IReadOnlyList<Equipamento> lista =
        [
            Caixa("fl", "Frente E", 1),
            Caixa("fr", "Frente D", 2),
            Caixa("tl", "Trás E", 3),
            Caixa("tr", "Trás D", 4),
        ];

        var e = EsquemaLigacao.Montar(lista);

        Assert.Equal(["Frente E", "Frente D", "Trás E", "Trás D"], e.Equipamentos.Select(x => x.Nome));
        Assert.Equal([[0], [1], [2], [3]], e.Equipamentos.Select(x => x.Canais.ToArray()));
        Assert.Equal(4, e.Fios.Count);
        Assert.Equal(190 / 4.0, e.Equipamentos[1].Y - e.Equipamentos[0].Y, 6);
        Assert.False(e.Link12);
        Assert.False(e.Link34);
        Assert.Equal([2], e.Juntos(2));
    }

    [Fact]
    public void Canal_sem_quem_toca_mostra_o_que_ele_alimenta_e_sem_Setup_so_o_processador()
    {
        IReadOnlyList<Equipamento> lista =
        [
            new Equipamento("m", GrupoEquipamento.Amplificador, "módulo", "Módulo") { Entradas = [new Entrada(1, Ch(1)), new Entrada(2, null)] },
        ];

        var e = EsquemaLigacao.Montar(lista);

        Assert.Equal(["Módulo"], e.Equipamentos.Select(x => x.Nome));
        Assert.Equal("CH2 não alimenta nenhum equipamento.", e.DescreverCanal(1));
        Assert.True(EsquemaLigacao.Montar(null).Vazio);
        Assert.True(EsquemaLigacao.Montar([]).Vazio);
        Assert.Equal(4, EsquemaLigacao.Nenhum.Portas.Count);
    }
}
