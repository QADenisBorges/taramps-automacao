"""Testes do guardião mínimo, com o retrato real (estado/leituras/2026-10-16_1747.json) e o
estado/guardiao.json de verdade como fixture."""

from __future__ import annotations

import copy
import dataclasses
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import guardiao as gd  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
RETRATO = json.loads((RAIZ / "estado" / "leituras" / "2026-10-16_1747.json").read_text(encoding="utf-8"))
CFG = gd.carregar_config(RAIZ / "estado" / "guardiao.json")
# A regra 6 é por saída: desligar o padrão global não basta quando cada saída tem o campo.
SEM_REGRA6 = dataclasses.replace(
    CFG, regra6=False, saidas=tuple(dataclasses.replace(s, regra6=False) for s in CFG.saidas))

# O retrato real com as violações já existentes corrigidas: JBL com BT24 em 100 Hz, sub com
# BT24 em 40 Hz (o reforço de +3 dB em 50 Hz passa) e attack da ponte igual.
CORRIGIDO = {("042", 0): 11, ("040", 0): 100, ("042", 1): 11, ("040", 1): 100,
             ("040", 2): 40, ("040", 3): 40, ("072", 3): 50.0}


def variante(mudancas: dict, base: dict = RETRATO) -> dict:
    r = copy.deepcopy(base)
    for x in r["leituras"]:
        chave = (x["codigo"], x["canal"])
        if chave in mudancas:
            x["valor"] = mudancas[chave]
    return r


def avaliar(*ajustes: str, retrato: dict = RETRATO, cfg: gd.Config = CFG) -> gd.Decisao:
    return gd.avaliar_lote(retrato, [gd.interpretar(a) for a in ajustes], cfg)


def regras(d: gd.Decisao) -> set[int]:
    return {r.regra for r in d.recusas}


class CurvasTest(unittest.TestCase):
    def test_filtros(self):
        self.assertAlmostEqual(gd.ganho_filtro(11, 100, 100), -3.01, places=2)  # BT24
        self.assertAlmostEqual(gd.ganho_filtro(3, 100, 100), -6.02, places=2)  # LR24
        self.assertAlmostEqual(gd.ganho_filtro(9, 100, 100), -3.01, places=2)  # BT12
        self.assertEqual(gd.ganho_filtro(0, 100, 10), 0.0)  # OFF
        self.assertAlmostEqual(gd.ganho_filtro(11, 100, 50), -24.1, places=1)  # 1 oitava abaixo
        self.assertAlmostEqual(gd.ganho_filtro(9, 100, 50), -12.3, places=1)
        self.assertAlmostEqual(gd.ganho_filtro(11, 100, 200, passa_alta=False), -24.1, places=1)
        self.assertAlmostEqual(gd.ganho_filtro(11, 100, 10000), 0.0, places=3)

    def test_pico(self):
        self.assertAlmostEqual(gd.ganho_pico(63, 6.0, 1.0, 63), 6.0, places=6)
        self.assertAlmostEqual(gd.ganho_pico(63, -6.0, 1.0, 63), -6.0, places=6)
        self.assertLess(abs(gd.ganho_pico(63, 6.0, 1.0, 5000)), 0.05)
        self.assertEqual(gd.ganho_pico(63, 0.0, 1.0, 63), 0.0)

    def test_referencia_e_butterworth(self):
        self.assertAlmostEqual(gd.referencia(100, 24, 50), gd.ganho_filtro(11, 100, 50))


class ConfigTest(unittest.TestCase):
    def test_limites_da_decisao_a(self):
        s = CFG.saidas
        self.assertEqual([(x.trava, x.f_min, x.inclinacao, x.teto_limiter) for x in s], [
            ("passa-alta", 100, 24, -11.0), ("passa-alta", 100, 24, -11.0),
            ("subsônico", 35, 24, -6.0), ("subsônico", 35, 24, -6.0)])
        self.assertTrue(CFG.regra6)
        self.assertEqual(CFG.g_ef_max, -1.0)
        # master + ganho + maior reforço de EQ + 6 dB na rota A+B (saídas 1, 3 e 4).
        self.assertEqual(CFG.nivel_base, {0: -5.0, 1: -11.0, 2: -1.0, 3: -1.0})

    def test_config_ausente_ou_ruim(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(gd.ConfigInvalida):
                gd.carregar_config(Path(tmp) / "guardiao.json")
            ruim = Path(tmp) / "ruim.json"
            dados = json.loads((RAIZ / "estado" / "guardiao.json").read_text(encoding="utf-8"))
            del dados["saidas"]["4"]
            ruim.write_text(json.dumps(dados), encoding="utf-8")
            with self.assertRaises(gd.ConfigInvalida):
                gd.carregar_config(ruim)


class EstadoLidoTest(unittest.TestCase):
    def test_violacoes_ja_existentes_no_retrato_real(self):
        chk = gd.checar(gd.estado_do_retrato(RETRATO), CFG)
        violadas = {i for i, c in chk.items() if c.sev > gd.EPS}
        self.assertEqual(violadas, {"trava:1", "trava:2", "trava:3", "trava:4", "ponte:072"})

    def test_retrato_corrigido_nao_tem_violacao(self):
        chk = gd.checar(gd.estado_do_retrato(variante(CORRIGIDO)), CFG)
        self.assertEqual({i for i, c in chk.items() if c.sev > gd.EPS}, set())

    def test_leitura_fora_da_faixa_e_recusada(self):
        r = copy.deepcopy(RETRATO)
        x = next(x for x in r["leituras"] if (x["codigo"], x["canal"]) == ("092", 2))
        x.update(valor=0.05, fora_da_faixa=True)
        self.assertRaises(gd.EstadoInvalido, gd.estado_do_retrato, r)
        d = avaliar("081:0=-12.0", retrato=r)
        self.assertFalse(d.aprovado)
        self.assertIn("fora da faixa", d.recusas[0].motivo)
        # O limiter em OFF (0.5) é valor especial, não fora da faixa.
        off = variante({("071", 1): 0.5})
        self.assertIn("071", {k[0] for k in gd.estado_do_retrato(off)})

    def test_leitura_incompleta_e_recusada(self):
        d = avaliar("081:0=-12.0", retrato=variante({("050", 2): None}))
        self.assertFalse(d.aprovado)
        self.assertIn("leitura incompleta", d.recusas[0].motivo)


class MatrizTest(unittest.TestCase):
    """Os casos da matriz de I/O da spec, com o retrato real."""

    def test_ajuste_seguro(self):
        d = avaliar("081:0=-12.0")
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["081:0=-12.0"])
        self.assertEqual(len(d.alertas), 5)  # as violações que já estavam no aparelho
        self.assertTrue(d.alertas[0].startswith("trava 1: passa-alta da saída 1"))

    def test_passa_alta_da_jbl_em_off(self):
        for retrato in (RETRATO, variante(CORRIGIDO)):
            with self.subTest(corrigido=retrato is not RETRATO):
                d = avaliar("042:0=0", retrato=retrato)
                self.assertFalse(d.aprovado)
                self.assertEqual(regras(d), {1})
                self.assertIn("desligado", d.recusas[0].motivo)
                seguro = d.recusas[0].seguro
                self.assertIn("042:0=11", seguro)  # pronto para colar
                self.assertNotIn("042:0=10", seguro)  # BT18 não basta
                for item in seguro.split(", "):
                    self.assertTrue(avaliar(item, retrato=retrato).aprovado, item)

    def test_passa_alta_da_jbl_abaixo_do_minimo(self):
        d = avaliar("040:0=80", retrato=variante(CORRIGIDO))
        self.assertEqual(regras(d), {1})
        self.assertIn("abaixo do mínimo", d.recusas[0].motivo)
        # Tolerância de 0,5 dB (Design Notes): BT24 em 99 Hz fica a ~0,35 dB da referência.
        self.assertEqual(d.recusas[0].seguro, "040:0=99")
        self.assertFalse(avaliar("040:0=98", retrato=variante(CORRIGIDO)).aprovado)
        d = avaliar("042:0=9", retrato=variante(CORRIGIDO))  # BT12
        self.assertEqual(regras(d), {1})

    def test_reforco_de_eq_abaixo_do_corte(self):
        d = avaliar("091:0=+6", retrato=variante(CORRIGIDO | {("090", 0): 50}))
        self.assertFalse(d.aprovado)
        trava = [r for r in d.recusas if r.regra == 1]
        self.assertEqual(len(trava), 1)
        self.assertIn("reforço de EQ abaixo do corte da saída 1", trava[0].motivo)
        self.assertTrue(trava[0].seguro.startswith("091:0="), trava[0].seguro)
        # Retrato real: a frequência e o ganho no mesmo lote.
        self.assertIn(1, regras(avaliar("090:0=50", "091:0=6")))

    def test_exemplo_das_design_notes(self):
        # JBL com BT24 em 100 Hz e EQ de entrada a +6 dB em 63 Hz.
        d = avaliar("031=63", "030=6", retrato=variante(CORRIGIDO), cfg=SEM_REGRA6)
        trava = [r for r in d.recusas if r.regra == 1]
        self.assertEqual(len(trava), 1)
        self.assertIn("reforço de EQ abaixo do corte da saída 1 (JBL L)", trava[0].motivo)
        self.assertIn("também na saída 2", trava[0].motivo)

    def test_limiter_off_ou_acima_do_teto(self):
        d = avaliar("071:2=0.5")
        self.assertEqual(regras(d), {3})
        self.assertIn("OFF", d.recusas[0].motivo)
        self.assertEqual(d.recusas[0].seguro, "071:2=-6.0")
        d = avaliar("071:0=-10.0")
        self.assertEqual(regras(d), {3})
        self.assertEqual(d.recusas[0].seguro, "071:0=-11.0")
        self.assertTrue(avaliar("071:0=-12.0").aprovado)

    def test_ponte(self):
        d = avaliar("081:2=-12.0")
        self.assertTrue(d.aprovado)
        self.assertEqual(d.ordem, ["081:2=-12.0", "081:3=-12.0"])
        self.assertEqual(d.espelhados, ["081:3=-12.0"])
        d = avaliar("081:3=-12.0")  # pedido na saída 4: a 3 vem primeiro
        self.assertEqual(d.ordem, ["081:2=-12.0", "081:3=-12.0"])

    def test_ponte_com_pedidos_diferentes(self):
        d = avaliar("081:2=-12.0", "081:3=-11.0")
        self.assertEqual(regras(d), {4})
        self.assertEqual(gd.pre_checar([gd.interpretar("081:2=-12.0"), gd.interpretar("081:3=-11.0")])[0].regra, 4)

    def test_attack_da_ponte_corrigido(self):
        d = avaliar("072:2=50.0")
        self.assertTrue(d.aprovado)
        self.assertEqual(d.ordem, ["072:3=50.0"])
        self.assertEqual(d.sem_mudanca, ["072:2=50.0"])

    def test_subir_nivel_com_regra6(self):
        d = avaliar("080=+1.0")  # fora da faixa do master: barrado antes da simulação
        self.assertEqual(regras(d), {5})
        self.assertIn("de -80 a 0", d.recusas[0].seguro)
        d = avaliar("081:0=-10.0")
        self.assertEqual(regras(d), {6})
        self.assertEqual(d.recusas[0].seguro, "081:0=-11.0")
        self.assertTrue(avaliar("081:0=-10.0", cfg=SEM_REGRA6).aprovado)

    def test_reforco_de_eq_conta_na_regra6(self):
        d = avaliar("091:1=3", retrato=variante(CORRIGIDO))  # EQ da saída 2 em 1 kHz
        self.assertEqual(regras(d), {6})
        self.assertTrue(avaliar("091:1=3", "081:1=-14.0", retrato=variante(CORRIGIDO)).aprovado)

    def test_rota_a_mais_b_conta_na_regra6(self):
        d = avaliar("010:1=a+b", retrato=variante(CORRIGIDO))  # saída 2: +6 dB
        self.assertEqual(regras(d), {6})
        self.assertTrue(avaliar("010:1=a+b", retrato=variante(CORRIGIDO), cfg=SEM_REGRA6).aprovado)
        self.assertTrue(avaliar("010:1=a+b", "081:1=-17.0", retrato=variante(CORRIGIDO)).aprovado)
        self.assertTrue(gd.toca("nivel:2", ("010", 1), "a+b"))

    def test_banda_do_eq_grafico(self):
        d = avaliar("020:0=12", retrato=variante(CORRIGIDO))  # 25 Hz: abaixo dos dois cortes
        self.assertTrue({1, 2} <= regras(d), [r.texto() for r in d.recusas])
        self.assertTrue(avaliar("020:10=3", retrato=variante(CORRIGIDO), cfg=SEM_REGRA6).aprovado)
        self.assertEqual(regras(avaliar("020:10=3", retrato=variante(CORRIGIDO))), {6})

    def test_regra7_g_ef(self):
        # Sub: −9 + pico ~2,8 + 6 (A+B) ≈ −0,2 dB > −1 dB.
        d = avaliar("081:2=-9.0", cfg=SEM_REGRA6)
        self.assertEqual(regras(d), {7})
        self.assertTrue(avaliar("081:2=-10.5", cfg=SEM_REGRA6).aprovado)

    def test_ordem_insegura_tem_ordem_segura(self):
        # Pedido: frequência primeiro. BT12 em 100 Hz pioraria a saída 1; o filtro vai antes.
        d = avaliar("040:0=100", "042:0=11")
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["042:0=11 (BT24)", "040:0=100"])

    def test_sem_ordem_segura_recusa_o_lote(self):
        real = gd._Avaliador.problemas

        def todo_intermediario_ruim(self, escritas, final):
            if final:
                return real(self, escritas, final)
            return [gd.Checagem("trava:1", 1, 1, 1.0, "intermediário inseguro")]

        with mock.patch.object(gd._Avaliador, "problemas", todo_intermediario_ruim):
            d = avaliar("042:0=11", "081:0=-12.0")
        self.assertFalse(d.aprovado)
        self.assertEqual(regras(d), {8})
        self.assertIsNotNone(d.recusas[0].seguro)

    def test_codigo_proibido(self):
        for ajuste in ("241:1", "241:1=1", "240:0=1", "242", "004", "000=AB12", "001=x",
                       "202=1", "213=1", "252=1", "220=0", "002=x", "231=x", "999=1", "005"):
            with self.subTest(ajuste=ajuste):
                pedido = gd.interpretar(ajuste)
                pre = gd.pre_checar([pedido])
                self.assertEqual([r.regra for r in pre], [5])
                d = gd.avaliar_lote(RETRATO, [pedido], CFG)
                self.assertFalse(d.aprovado)
                self.assertEqual(regras(d), {5})

    def test_formato_e_faixa(self):
        for ajuste in ("081=-12.0", "080:0=-3.0", "081:4=-1.0", "081:0", "042:0=16", "042:0=BT99",
                       "040:0=0", "081:0=-12.05", "060:0=90", "010:0=c"):
            with self.subTest(ajuste=ajuste):
                self.assertEqual([r.regra for r in gd.pre_checar([gd.interpretar(ajuste)])], [5])
        # Fora da faixa ou absurdo: barrado antes de qualquer conta, com a faixa segura.
        for ajuste, faixa in (("080=+1.0", "de -80 a 0"), ("092:2=0", "de 0.1 a 10"),
                              ("032=0", "de 0.4 a 10"), ("091:0=1e5", "de -12 a 12"),
                              ("081:0=1e308", "de -45 a 15"), ("081:0=-1e308", "de -45 a 15"),
                              ("040:0=1e300", "de 10 a 22000")):
            with self.subTest(ajuste=ajuste):
                pre = gd.pre_checar([gd.interpretar(ajuste)])
                self.assertEqual([r.regra for r in pre], [5])
                self.assertIn(faixa, pre[0].seguro)
                self.assertEqual(regras(avaliar(ajuste)), {5})


def config_com(mudar) -> gd.Config:
    """O guardiao.json real, alterado por `mudar(dados)`, carregado pelo caminho de verdade."""
    dados = json.loads((RAIZ / "estado" / "guardiao.json").read_text(encoding="utf-8"))
    mudar(dados)
    with tempfile.TemporaryDirectory() as tmp:
        caminho = Path(tmp) / "guardiao.json"
        caminho.write_text(json.dumps(dados), encoding="utf-8")
        return gd.carregar_config(caminho)


TETO_DEMO = {"teto_limiter_db": -9.8, "exige_passa_alta_hz": 150, "exige_inclinacao_db_oit": 24,
             "proibe_rota_ab": True}


def _demo_nas_jbl(dados):
    for n in ("1", "2"):
        dados["saidas"][n]["teto_demo"] = dict(TETO_DEMO)
        dados["saidas"][n]["regra6"] = False


CFG_DEMO = config_com(_demo_nas_jbl)

# O crossover planejado: JBL em LR24 a 150 Hz, saída 1 na rota A e saída 2 na rota B.
LR24 = 3
DEMO = CORRIGIDO | {("042", 0): LR24, ("040", 0): 150, ("042", 1): LR24, ("040", 1): 150,
                    ("010", 0): "a"}
TEXTO_RECUSA = ("071:0=-9.8 · trava 3: limiter da saída 1 em −9,8 dB só vale com passa-alta "
                "≥ 150 Hz/24 dB/oit e rota sem A+B (teto de demonstração); fora disso o teto é "
                "−11,0 dB. Seguro: 071:0=-11.0.")


class TetoAmarradoTest(unittest.TestCase):
    """Matriz da spec-guardiao-teto-amarrado.md."""

    def test_config_migrada_sem_teto_demo_e_com_regra6_em_todas(self):
        self.assertEqual([s.teto_demo for s in CFG.saidas], [None] * 4)
        self.assertEqual([s.regra6 for s in CFG.saidas], [True] * 4)
        self.assertEqual([s.teto_demo for s in CFG_DEMO.saidas],
                         [gd.TetoDemo(-9.8, 150, 24, True)] * 2 + [None] * 2)
        self.assertEqual([s.regra6 for s in CFG_DEMO.saidas], [False, False, True, True])

    def test_teto_demo_com_corte_certo(self):
        d = avaliar("071:0=-9.8", retrato=variante(DEMO), cfg=CFG_DEMO)
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["071:0=-9.8"])
        lido = gd.estado_do_retrato(variante(DEMO))
        self.assertEqual(gd.teto_efetivo(CFG_DEMO.saidas[0], lido), -9.8)
        # Acima do próprio teto de demonstração continua recusado.
        d = avaliar("071:0=-9.5", retrato=variante(DEMO), cfg=CFG_DEMO)
        self.assertEqual(regras(d), {3})
        self.assertIn("teto de demonstração -9.8", d.recusas[0].motivo)
        self.assertEqual(d.recusas[0].seguro, "071:0=-9.8")

    def test_teto_demo_com_corte_baixo(self):
        for corte in ({("042", 0): 11, ("040", 0): 120},  # BT24 em 120 Hz
                      {("042", 0): 1, ("040", 0): 150}):  # LR12 em 150 Hz (a trava 1 já barra)
            with self.subTest(corte=corte):
                retrato = variante(DEMO | corte)
                lido = gd.estado_do_retrato(retrato)
                self.assertEqual(gd.teto_efetivo(CFG_DEMO.saidas[0], lido), -11.0)
                d = avaliar("071:0=-9.8", retrato=retrato, cfg=CFG_DEMO)
                self.assertFalse(d.aprovado)
                self.assertIn(3, regras(d))
        d = avaliar("071:0=-9.8", retrato=variante(DEMO | {("042", 0): 11, ("040", 0): 120}),
                    cfg=CFG_DEMO)
        self.assertEqual(regras(d), {3})
        self.assertEqual(d.recusas[0].texto(), TEXTO_RECUSA)

    def test_teto_demo_com_rota_a_mais_b(self):
        retrato = variante(DEMO | {("010", 0): "a+b"})
        d = avaliar("071:0=-9.8", retrato=retrato, cfg=CFG_DEMO)
        self.assertEqual(regras(d), {3})
        self.assertEqual(d.recusas[0].texto(), TEXTO_RECUSA)
        # Tirar o A+B no mesmo lote resolve; a rota vai antes do limiter.
        d = avaliar("071:0=-9.8", "010:0=a", retrato=retrato, cfg=CFG_DEMO)
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["010:0=a", "071:0=-9.8"])

        # proibe_rota_ab false desfaria a amarra: o arquivo é recusado ao carregar.
        def sem_proibir(dados):
            _demo_nas_jbl(dados)
            dados["saidas"]["1"]["teto_demo"]["proibe_rota_ab"] = False
        with self.assertRaises(gd.ConfigInvalida) as erro:
            config_com(sem_proibir)
        self.assertIn("proibe_rota_ab precisa ser true", str(erro.exception))

    def test_baixar_o_corte_com_o_limiter_alto(self):
        retrato = variante(DEMO | {("071", 0): -9.8})
        violadas = {i for i, c in gd.checar(gd.estado_do_retrato(retrato), CFG_DEMO).items()
                    if c.sev > gd.EPS}
        self.assertEqual(violadas, set())
        d = avaliar("040:0=120", retrato=retrato, cfg=CFG_DEMO)
        self.assertEqual(regras(d), {3})
        self.assertEqual(d.recusas[0].item, "040:0=120")
        self.assertIn("só vale com passa-alta ≥ 150 Hz/24 dB/oit", d.recusas[0].motivo)
        self.assertEqual(d.recusas[0].seguro, "040:0=150")
        for ajuste in ("010:0=a+b", "042:0=0", "040:0=149"):
            with self.subTest(ajuste=ajuste):
                self.assertIn(3, regras(avaliar(ajuste, retrato=retrato, cfg=CFG_DEMO)))
        # O mesmo lote com o limiter no teto base passa: o limiter desce antes.
        d = avaliar("040:0=120", "071:0=-11.0", retrato=retrato, cfg=CFG_DEMO)
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["071:0=-11.0", "040:0=120"])
        # BT24 também é 24 dB/oit: o teto de demonstração continua valendo.
        self.assertTrue(avaliar("042:0=BT24", retrato=retrato, cfg=CFG_DEMO).aprovado)

    def test_limiter_alto_com_corte_baixo_ja_lido(self):
        # Violação já existente: limiter em −9,8 com o corte em 120 Hz (teto efetivo −11,0).
        retrato = variante(DEMO | {("071", 0): -9.8, ("040", 0): 120})
        chk = gd.checar(gd.estado_do_retrato(retrato), CFG_DEMO)
        self.assertGreater(chk["limiter:1"].sev, gd.EPS)
        self.assertIn("só vale com passa-alta", chk["limiter:1"].motivo)
        d = avaliar("040:0=130", retrato=retrato, cfg=CFG_DEMO)  # mexe sem resolver
        self.assertEqual(regras(d), {3})
        self.assertIn("já estava em violação", d.recusas[0].motivo)
        d = avaliar("040:0=150", retrato=retrato, cfg=CFG_DEMO)  # cumprir a condição resolve
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])

    def test_motivo_do_teto_demo_so_em_violacao(self):
        retrato = variante(DEMO | {("040", 0): 120, ("071", 0): -12.0})
        chk = gd.checar(gd.estado_do_retrato(retrato), CFG_DEMO)
        self.assertEqual(chk["limiter:1"].sev, 0.0)
        self.assertNotIn("só vale com", chk["limiter:1"].motivo)

    def test_sem_teto_demo_nada_muda(self):
        d = avaliar("071:0=-9.8", retrato=variante(DEMO))
        self.assertEqual(regras(d), {3})
        self.assertIn("acima do teto -11.0 dB (0,56 V)", d.recusas[0].motivo)
        self.assertEqual(d.recusas[0].seguro, "071:0=-11.0")
        # Sem teto_demo, o corte e a rota não tocam no limiter.
        for chave in (("040", 0), ("042", 0), ("010", 0)):
            self.assertFalse(gd.toca("limiter:1", chave, 120, CFG))
            self.assertTrue(gd.toca("limiter:1", chave, 120, CFG_DEMO))
        self.assertFalse(gd.toca("limiter:1", ("040", 1), 120, CFG_DEMO))
        self.assertFalse(gd.toca("limiter:1", ("040", 0), 120))

    def test_teto_demo_no_sub_e_formato_invalido(self):
        def no_sub(dados):
            dados["saidas"]["3"]["teto_demo"] = dict(TETO_DEMO)
        with self.assertRaises(gd.ConfigInvalida) as erro:
            config_com(no_sub)
        self.assertIn("saída 3", str(erro.exception))
        self.assertIn("passa-alta", str(erro.exception))
        ruins = [
            (lambda t: t.pop("proibe_rota_ab"), "falta proibe_rota_ab"),
            (lambda t: t.update(exige_inclinacao_db_oit=25), "múltiplo de 6"),
            (lambda t: t.update(exige_inclinacao_db_oit=True), "múltiplo de 6"),
            (lambda t: t.update(proibe_rota_ab="sim"), "proibe_rota_ab precisa ser true"),
            (lambda t: t.update(teto_limiter_db=3.0), "teto_limiter_db"),
            (lambda t: t.update(exige_passa_alta_hz="150"), "exige_passa_alta_hz"),
            # Amarras da auditoria: nada que desfaça o teto amarrado ao corte.
            (lambda t: t.update(teto_limiter_db=0.0), "no máximo -8 dB"),
            (lambda t: t.update(teto_limiter_db=-7.9), "no máximo -8 dB"),
            (lambda t: t.update(teto_limiter_db=-11.0), "acima do teto base (-11 dB)"),
            (lambda t: t.update(teto_limiter_db=-12.0), "acima do teto base (-11 dB)"),
            (lambda t: t.update(exige_passa_alta_hz=149), "≥ 150 Hz"),
            (lambda t: t.update(exige_inclinacao_db_oit=18), "≥ 24"),
            (lambda t: t.update(proibe_rota_ab=False), "proibe_rota_ab precisa ser true"),
        ]
        for i, (estragar, trecho) in enumerate(ruins):
            def mudar(dados, estragar=estragar):
                _demo_nas_jbl(dados)
                estragar(dados["saidas"]["1"]["teto_demo"])
            with self.subTest(caso=i, trecho=trecho):
                with self.assertRaises(gd.ConfigInvalida) as erro:
                    config_com(mudar)
                self.assertIn(trecho, str(erro.exception))

        # O corte exigido também não fica abaixo do f_min da própria saída.
        def f_min_alto(dados):
            _demo_nas_jbl(dados)
            dados["saidas"]["1"]["f_min_hz"] = 160
        with self.assertRaises(gd.ConfigInvalida) as erro:
            config_com(f_min_alto)
        self.assertIn("≥ 160 Hz", str(erro.exception))

        # No limite das amarras, carrega.
        def no_limite(dados):
            _demo_nas_jbl(dados)
            dados["saidas"]["1"]["teto_demo"].update(teto_limiter_db=-8.0, exige_passa_alta_hz=160,
                                                     exige_inclinacao_db_oit=48)
        self.assertEqual(config_com(no_limite).saidas[0].teto_demo, gd.TetoDemo(-8.0, 160, 48, True))
        self.assertRaises(gd.ConfigInvalida, config_com,
                          lambda dados: dados["saidas"]["1"].update(teto_demo=-9.8))
        self.assertRaises(gd.ConfigInvalida, config_com,
                          lambda dados: dados["saidas"]["2"].update(regra6="não"))

    def test_regra6_por_saida(self):
        retrato = variante(CORRIGIDO)
        d = avaliar("081:0=-9.0", retrato=retrato, cfg=CFG_DEMO)
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertIn(6, regras(avaliar("081:0=-9.0", retrato=retrato)))  # arquivo migrado
        self.assertIn(6, regras(avaliar("081:2=-9.0", retrato=retrato, cfg=CFG_DEMO)))
        chk = gd.checar(gd.estado_do_retrato(retrato), CFG_DEMO)
        self.assertEqual({i for i in chk if i.startswith("nivel:")}, {"nivel:3", "nivel:4"})

    def test_regra6_global_e_o_padrao_das_saidas_sem_o_campo(self):
        self.assertNotIn("regra6", json.loads(
            (RAIZ / "estado" / "guardiao.json").read_text(encoding="utf-8")))  # só por saída

        def global_desligada(dados):
            dados["regra6"] = False
            for n in ("1", "2", "3", "4"):
                dados["saidas"][n].pop("regra6")
            dados["saidas"]["3"]["regra6"] = True
            dados["saidas"]["4"]["regra6"] = True
        self.assertEqual([s.regra6 for s in config_com(global_desligada).saidas],
                         [False, False, True, True])

        def sem_campos(dados):
            dados.pop("regra6", None)
            for n in ("1", "2", "3", "4"):
                dados["saidas"][n].pop("regra6")
        self.assertEqual([s.regra6 for s in config_com(sem_campos).saidas], [True] * 4)

    def test_ponte_com_regra6_diferente_e_recusada(self):
        for n in ("3", "4"):
            def mudar(dados, n=n):
                dados["saidas"][n]["regra6"] = False
            with self.subTest(saida=n):
                with self.assertRaises(gd.ConfigInvalida) as erro:
                    config_com(mudar)
                self.assertIn("saídas 3 e 4", str(erro.exception))

        def global_desligada_e_so_a_3_ligada(dados):
            dados["regra6"] = False
            dados["saidas"]["4"].pop("regra6")
        self.assertRaises(gd.ConfigInvalida, config_com, global_desligada_e_so_a_3_ligada)

    def test_karina_aplica_o_teto_de_demonstracao(self):
        # Critério de aceite: teto_demo −9,8 nas saídas 1 e 2 e regra 6 desligada só nelas.
        retrato = variante(DEMO)
        d = avaliar("071:0=-9.8", "071:1=-9.8", retrato=retrato, cfg=CFG_DEMO)
        self.assertTrue(d.aprovado, [r.texto() for r in d.recusas])
        self.assertEqual(d.ordem, ["071:0=-9.8", "071:1=-9.8"])
        d = avaliar("081:2=-9.0", retrato=retrato, cfg=CFG_DEMO)
        self.assertFalse(d.aprovado)
        self.assertIn(6, regras(d))


class PrincipioSeisTest(unittest.TestCase):
    def test_mexer_em_saida_em_violacao_sem_resolver(self):
        d = avaliar("090:0=500")
        self.assertEqual(regras(d), {1})
        self.assertIn("já estava em violação", d.recusas[0].motivo)

    def test_resolver_a_violacao_passa(self):
        self.assertTrue(avaliar("042:0=11").aprovado)
        self.assertTrue(avaliar("042:1=BT24").aprovado)
        d = avaliar("040:2=40")
        self.assertTrue(d.aprovado)
        self.assertEqual(d.ordem, ["040:2=40", "040:3=40"])

    def test_subsonico_em_35_nao_basta_com_o_reforco_em_50(self):
        d = avaliar("040:2=35")
        self.assertEqual(regras(d), {2})
        self.assertEqual(len(d.recusas), 1)  # saídas 3 e 4 numa linha só
        self.assertIn("reforço de EQ abaixo do corte", d.recusas[0].motivo)
        self.assertIn("também na saída 4", d.recusas[0].motivo)
        self.assertEqual(d.recusas[0].seguro, "040:2=39")
        self.assertTrue(avaliar("040:2=39").aprovado)

    def test_global_mexe_em_todas_as_saidas(self):
        d = avaliar("030=-3")
        self.assertEqual(regras(d), {1, 2})
        self.assertTrue(avaliar("030=-3", retrato=variante(CORRIGIDO)).aprovado)

    def test_mute(self):
        d = avaliar("100:1=0")  # tirar o mute da saída 2, que está em violação
        self.assertEqual(regras(d), {1})
        self.assertTrue(avaliar("100:0=1").aprovado)  # mutar nunca piora
        self.assertTrue(avaliar("100:1=0", retrato=variante(CORRIGIDO)).aprovado)

    def test_sem_mudanca(self):
        d = avaliar("081:0=-11.0")
        self.assertTrue(d.aprovado)
        self.assertEqual(d.ordem, [])
        self.assertEqual(d.sem_mudanca, ["081:0=-11.0"])


class OrdemTest(unittest.TestCase):
    def test_procura(self):
        self.assertEqual(gd.procurar_ordem(3, lambda s: True), [0, 1, 2])
        self.assertEqual(gd.procurar_ordem(2, lambda s: s == frozenset({1})), [1, 0])
        self.assertIsNone(gd.procurar_ordem(2, lambda s: False))
        self.assertEqual(gd.procurar_ordem(1, lambda s: False), [0])  # só o estado final
        with self.assertRaises(gd.LimiteDaOrdem):
            gd.procurar_ordem(12, lambda s: len(s) < 11, limite=50)


class AprovacaoTest(unittest.TestCase):
    def setUp(self):
        self.aprovacao = avaliar("081:2=-12.0").aprovacao

    def test_aprovacao_valida(self):
        self.assertEqual(gd.item_aprovado(self.aprovacao, 0), gd.Escrita("081", 2, -12.0))
        self.assertEqual(gd.item_aprovado(self.aprovacao, 1), gd.Escrita("081", 3, -12.0))

    def test_selo_amarra_as_leituras(self):
        outra = variante({("050", 0): 10.0})  # mesmo início, uma leitura diferente
        a = avaliar("081:2=-12.0", retrato=outra).aprovacao
        self.assertNotEqual(a.retrato, self.aprovacao.retrato)
        self.assertEqual(a.retrato.split("|")[0], self.aprovacao.retrato.split("|")[0])
        trocada = dataclasses.replace(self.aprovacao, retrato=a.retrato)
        with self.assertRaises(gd.EscritaNaoAprovada):
            gd.item_aprovado(trocada, 0)

    def test_sem_aprovacao_forjada_ou_alterada(self):
        forjada = gd.Aprovacao((gd.Escrita("071", 0, 0.5),), "x", "0" * 64)
        alterada = dataclasses.replace(self.aprovacao, escritas=(gd.Escrita("071", 2, 0.5),
                                                                 gd.Escrita("071", 3, 0.5)))
        for aprovacao, indice in ((None, 0), ("aprovado", 0), (forjada, 0), (alterada, 0),
                                  (self.aprovacao, 2), (self.aprovacao, -1), (self.aprovacao, True)):
            with self.subTest(aprovacao=aprovacao, indice=indice):
                with self.assertRaises(gd.EscritaNaoAprovada):
                    gd.item_aprovado(aprovacao, indice)


class LoteTest(unittest.TestCase):
    def test_interpretar(self):
        self.assertEqual(gd.interpretar("081:0=-12.0"), gd.Pedido("081", 0, "-12.0", "081:0=-12.0"))
        self.assertEqual(gd.interpretar("080=+1"), gd.Pedido("080", None, "+1", "080=+1"))
        self.assertEqual(gd.interpretar("241:1"), gd.Pedido("241", 1, None, "241:1"))
        for ruim in ("abc", "81:0=1", "081:0:1=2", ""):
            with self.subTest(ruim=ruim):
                with self.assertRaises(gd.PedidoInvalido):
                    gd.interpretar(ruim)

    def test_ler_lote(self):
        lote = gd.ler_lote(["081:0=-12.0", {"codigo": "042", "canal": 0, "valor": 11}])
        self.assertEqual([(p.codigo, p.canal, p.valor) for p in lote],
                         [("081", 0, "-12.0"), ("042", 0, 11)])
        self.assertEqual(len(gd.ler_lote({"ajustes": ["080=-3.0"]})), 1)
        for ruim in ({"x": 1}, "081:0=1", [{"canal": 0}], [{"codigo": "081", "canal": True}]):
            with self.subTest(ruim=ruim):
                with self.assertRaises(gd.PedidoInvalido):
                    gd.ler_lote(ruim)

    def test_senha_de_000_e_001_nunca_aparece(self):
        for ajuste in ("000=AB12", "001=AB12", "000:1=AB12", " 000 = AB12 "):
            with self.subTest(ajuste=ajuste):
                pedido = gd.interpretar(ajuste)
                self.assertNotIn("AB12", pedido.original)
                r = gd.pre_checar([pedido])
                self.assertNotIn("AB12", r[0].texto())
        self.assertNotIn("AB12", gd.interpretar_objeto({"codigo": "000", "valor": "AB12"}).original)
        with self.assertRaises(gd.PedidoInvalido) as erro:
            gd.interpretar("000x=AB12")
        self.assertNotIn("AB12", str(erro.exception))

    def test_repetido_com_valores_diferentes(self):
        d = avaliar("081:0=-12.0", "081:0=-13.0")
        self.assertEqual(regras(d), {0})
        self.assertTrue(avaliar("081:0=-12.0", "081:0=-12").aprovado)

    def test_lote_vazio(self):
        self.assertFalse(gd.avaliar_lote(RETRATO, [], CFG).aprovado)


if __name__ == "__main__":
    unittest.main()
