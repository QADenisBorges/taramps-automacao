"""Testes do painel.py: montagem do estado com os retratos reais, ajuste em andamento, arquivo
corrompido ignorado, senha gravada no formato certo e nunca devolvida, leitura antiga marcada e
o botão "Ler agora" num subprocesso falso. Tudo numa pasta temporária; a casa de verdade não é
tocada."""

from __future__ import annotations

import contextlib
import datetime as dt
import http.client
import io
import json
import os
import re
import shutil
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import ajustar_processador as ap  # noqa: E402
import guardiao as gd  # noqa: E402
import ler_processador as lp  # noqa: E402
import painel  # noqa: E402
import protocolo_taramps as pt  # noqa: E402
import sessao  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
RETRATO_REAL = RAIZ / "estado" / "leituras" / "2026-10-16_1747.json"
GUARDIAO_REAL = RAIZ / "estado" / "guardiao.json"
AGORA = dt.datetime(2026, 10, 16, 18, 0, 0)  # 13 min depois da leitura das 17:47
SENHA = "qz7k"


def _python(codigo: str, dorme: float = 0.0) -> list[str]:
    return [sys.executable, "-c", f"import sys, time; time.sleep({dorme}); print('saida do leitor'); "
                                  f"sys.exit({codigo})"]


class Base(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.estado = Path(self.tmp.name) / "estado"
        (self.estado / "leituras").mkdir(parents=True)
        (self.estado / "ajustes").mkdir()
        (self.estado / "sessoes").mkdir()
        shutil.copy(GUARDIAO_REAL, self.estado / "guardiao.json")

    def com_retrato_real(self) -> Path:
        destino = self.estado / "leituras" / RETRATO_REAL.name
        shutil.copy(RETRATO_REAL, destino)
        return destino

    def montar(self, **kw) -> dict:
        kw.setdefault("agora", AGORA)
        kw.setdefault("senha_guardada", True)
        return painel.montar_estado(self.estado, **kw)

    @staticmethod
    def item(estado: dict, tela: str, codigo: str, canal=None) -> dict | None:
        t = next(t for t in estado["telas"] if t["id"] == tela)
        return next((x for x in t["itens"] if x["codigo"] == codigo and x["canal"] == canal), None)


class EstadoTest(Base):
    def test_retrato_real_de_16_10(self):
        self.com_retrato_real()
        e = self.montar()
        self.assertEqual(e["nome"], "Denis Borges")
        leitura = e["leitura"]
        self.assertEqual((leitura["firmware"], leitura["lidos"], leitura["total"]), ("R003", 84, 84))
        self.assertEqual((leitura["hora"], leitura["data"], leitura["idade_min"]), ("17:47", "16/10", 12))
        self.assertFalse(leitura["antiga"])
        self.assertFalse(leitura["incompleta"])
        self.assertEqual([t["id"] for t in e["telas"]], [
            "home", "roteamento", "alinhamento", "fase", "eq_entrada", "crossover", "limiter",
            "nivel", "eq_saida", "eq15"])
        # Crossover como o app mostra: 150 / 120 / 28 Hz.
        self.assertEqual([self.item(e, "crossover", "040", c)["valor"] for c in range(4)], [150, 120, 28, 28])
        self.assertEqual(self.item(e, "crossover", "042", 0)["mostrado"], "BT12")
        self.assertEqual(self.item(e, "crossover", "043", 2)["mostrado"], "LR24")
        # Limiter em dB e volts: 0,56 V nas JBL e 1,00 V no sub.
        self.assertEqual([self.item(e, "limiter", "071", c)["volts"] for c in range(4)], [0.56, 0.56, 1.0, 1.0])
        self.assertEqual(self.item(e, "limiter", "071", 0)["mostrado"], "-11.0 dB · 0.56 V")
        # Saída 2 mutada.
        self.assertEqual([self.item(e, "home", "100", c)["valor"] for c in range(4)], [False, True, False, False])
        self.assertEqual(self.item(e, "home", "100", 1)["mostrado"], "mutado")
        self.assertEqual(self.item(e, "nivel", "080")["mostrado"], "0.0 dB")
        self.assertEqual(self.item(e, "eq15", "220")["mostrado"], "Custom")
        self.assertEqual(self.item(e, "eq15", "020", 14)["banda_hz"], 16000)
        self.assertEqual(len(next(t for t in e["telas"] if t["id"] == "eq15")["itens"]), 16)

    def test_violacoes_do_guardiao_no_cabecalho(self):
        self.com_retrato_real()
        g = self.montar()["guardiao"]
        self.assertTrue(g["regra6"])
        rotulos = [v["rotulo"] for v in g["violacoes"]]
        self.assertEqual(rotulos, ["trava 1", "trava 1", "trava 2", "trava 2", "regra 4"])
        self.assertIn("BT12 em 150 Hz", g["violacoes"][0]["motivo"])
        self.assertIn("50.0 × 50.1", g["violacoes"][4]["motivo"])

    def test_curvas_com_a_formula_do_guardiao(self):
        self.com_retrato_real()
        cv = self.montar()["curvas"]
        freqs = cv["freqs"]
        self.assertEqual((freqs[0], freqs[-1]), (10.0, 22000.0))
        i150 = min(range(len(freqs)), key=lambda i: abs(freqs[i] - 150))
        # BT12 em 150 Hz: −3 dB no corte; LPF OFF não soma nada.
        self.assertAlmostEqual(cv["crossover"]["0"][i150], -3.0, delta=0.2)
        self.assertLess(cv["crossover"]["0"][0], -40)  # 10 Hz, bem abaixo do corte
        i50 = min(range(len(freqs)), key=lambda i: abs(freqs[i] - 50))
        self.assertAlmostEqual(cv["eq_saida"]["2"][i50], 3.0, delta=0.1)  # +3 dB em 50 Hz no sub
        self.assertEqual(cv["eq_saida"]["0"][i50], 0.0)
        self.assertTrue(all(v == 0.0 for v in cv["eq15"]))
        self.assertTrue(all(v == 0.0 for v in cv["eq_entrada"]))

    def test_sem_leitura(self):
        e = self.montar()
        self.assertIsNone(e["leitura"])
        self.assertIsNone(e["curvas"])
        self.assertTrue(all(t["itens"] == [] for t in e["telas"]))
        self.assertIsNone(e["guardiao"]["violacoes"])
        self.assertEqual(e["guardiao"]["motivo"], "sem leitura")
        self.assertTrue(e["guardiao"]["regra6"])

    def test_leitura_antiga_e_incompleta_marcadas(self):
        caminho = self.com_retrato_real()
        self.assertTrue(self.montar(agora=AGORA + dt.timedelta(hours=2))["leitura"]["antiga"])
        self.assertFalse(self.montar(agora=AGORA + dt.timedelta(minutes=40))["leitura"]["antiga"])
        retrato = json.loads(caminho.read_text(encoding="utf-8"))
        retrato["status"], retrato["motivo"] = "incompleto", "a conexão caiu"
        retrato["leituras"] = retrato["leituras"][:40]
        retrato["lidos"] = 40
        caminho.write_text(json.dumps(retrato), encoding="utf-8")
        e = self.montar()
        self.assertTrue(e["leitura"]["incompleta"])
        self.assertEqual((e["leitura"]["lidos"], e["leitura"]["total"]), (40, 84))
        self.assertIsNone(e["guardiao"]["violacoes"])
        self.assertIn("incompleta", e["guardiao"]["motivo"])
        # As 40 primeiras leituras param no meio do limiter auto (070): 3 saídas lidas.
        self.assertEqual(len(next(t for t in e["telas"] if t["id"] == "limiter")["itens"]), 3)
        self.assertIsNone(e["curvas"]["eq15"])  # bandas não lidas: sem curva, sem erro

    def test_conferencia_nao_conta_como_leitura(self):
        self.com_retrato_real()
        (self.estado / "leituras" / "2026-10-16_1900-conferencia.json").write_text(
            '{"retrato": "x", "itens": []}', encoding="utf-8")
        self.assertEqual(self.montar()["leitura"]["hora"], "17:47")

    def test_arquivos_corrompidos_sao_ignorados(self):
        self.com_retrato_real()
        (self.estado / "leituras" / "2026-10-16_2359.json").write_text("{corrompido", encoding="utf-8")
        (self.estado / "leituras" / "2026-10-16_2358.json").write_text('"texto"', encoding="utf-8")
        (self.estado / "ajustes" / "2026-10-16_2359.json").write_text("{", encoding="utf-8")
        (self.estado / "memorias.json").write_text("{", encoding="utf-8")
        (self.estado / "sessoes" / "progresso.json").write_text("{", encoding="utf-8")
        e = self.montar()
        self.assertEqual(e["leitura"]["hora"], "17:47")
        self.assertEqual(e["agente"]["ajustes"], [])
        self.assertIsNone(e["agente"]["progresso"])
        self.assertEqual(e["memorias"]["dsp"], {"1": None, "2": None, "3": None})
        self.assertEqual(e["memorias"]["erro"], "memorias.json ilegível")

    def test_item_do_limiter_em_off_e_com_valor_estranho(self):
        off = painel._item({"codigo": "071", "canal": 2, "valor": 0.5, "rotulo": "OFF", "cru": "#071#2#0.5#"})
        self.assertEqual((off["mostrado"], off["volts"]), ("OFF", None))
        estranho = painel._item({"codigo": "071", "canal": 2, "valor": "abc"})
        self.assertIsNone(estranho["volts"])
        self.assertIsInstance(estranho["mostrado"], str)
        self.assertIsNone(painel._item({"codigo": "071", "canal": 2, "valor": True})["volts"])
        self.assertIsNone(painel._item({"codigo": "999", "canal": 0, "valor": 1}))

    def test_leitura_com_fuso_horario_vira_hora_local(self):
        caminho = self.com_retrato_real()
        retrato = json.loads(caminho.read_text(encoding="utf-8"))
        retrato["inicio"], retrato["fim"] = "2026-10-16T17:47:20+00:00", "2026-10-16T17:47:33+00:00"
        caminho.write_text(json.dumps(retrato), encoding="utf-8")
        esperado = dt.datetime.fromisoformat("2026-10-16T17:47:33+00:00").astimezone().replace(tzinfo=None)
        leitura = self.montar(agora=esperado + dt.timedelta(minutes=5))["leitura"]
        self.assertEqual((leitura["hora"], leitura["idade_min"]), (esperado.strftime("%H:%M"), 5))

    def test_secao_quebrada_degrada_sozinha(self):
        self.com_retrato_real()
        with mock.patch.object(painel, "ajustes", side_effect=RuntimeError("boom")):
            e = self.montar()
        self.assertEqual(e["leitura"]["firmware"], "R003")
        self.assertEqual(e["agente"]["ajustes"], [])
        self.assertEqual(e["problemas"], ["ajustes: RuntimeError"])

    def test_guardiao_json_invalido_nao_derruba(self):
        self.com_retrato_real()
        (self.estado / "guardiao.json").write_text("{}", encoding="utf-8")
        g = self.montar()["guardiao"]
        self.assertIsNone(g["violacoes"])
        self.assertIsNone(g["regra6"])
        self.assertIn("guardiao.json", g["motivo"])


class AgenteTest(Base):
    def ajuste(self, nome: str, **campos) -> Path:
        dados = {"formato": 1, "inicio": "2026-10-16T20:21:47", "fim": "2026-10-16T20:22:03",
                 "simular": False, "lote": ["081:0=-12.0"], "status": "aplicado",
                 "decisao": {"aprovado": True, "ordem": ["081:0=-12.0"], "recusas": [], "alertas": ["x"]},
                 "aplicados": ["081:0=-12.0"], "nao_aplicados": [], "em_andamento": None}
        dados.update(campos)
        caminho = self.estado / "ajustes" / f"{nome}.json"
        caminho.write_text(json.dumps(dados), encoding="utf-8")
        return caminho

    def test_ajuste_em_andamento_aparece_e_some(self):
        caminho = self.ajuste("2026-10-16_2030", fim=None, status="em andamento",
                              em_andamento="081:0=-12.0", aplicados=[])
        a = self.montar()["agente"]
        self.assertEqual(a["em_andamento"]["em_andamento"], "081:0=-12.0")
        self.assertTrue(a["ajustes"][0]["aberto"])
        self.assertFalse(a["lendo"])
        # O arquivo fecha com o resultado: some do "em andamento" e vira linha da linha do tempo.
        self.ajuste(caminho.stem)
        a = self.montar()["agente"]
        self.assertIsNone(a["em_andamento"])
        linha = a["ajustes"][0]
        self.assertEqual((linha["hora"], linha["decisao"], linha["aplicados"]), ("20:21", "APROVADO", ["081:0=-12.0"]))
        self.assertFalse(linha["aberto"])

    def test_registro_gravado_pelo_ajustar_em_andamento_conta_como_aberto(self):
        cfg = gd.carregar_config(self.estado / "guardiao.json")
        reg = ap._Registro(self.estado, "2026-10-17_1400", [gd.interpretar("081:0=-12.0")], False, cfg)
        reg.dados["em_andamento"] = "081:0=-12.0"
        reg.gravar("em andamento")  # como o ajustar faz antes de cada escrita: o fim fica preenchido
        gravado = json.loads(reg.caminho.read_text(encoding="utf-8"))
        self.assertIsNotNone(gravado["fim"])
        a = self.montar(agora=dt.datetime.now())["agente"]
        self.assertEqual(a["em_andamento"]["em_andamento"], "081:0=-12.0")
        self.assertEqual(a["ajustes"][0]["status"], "em andamento")
        self.assertIsNotNone(painel.ajuste_aberto(self.estado))

    def test_em_andamento_parado_ha_mais_de_10_min_nao_e_aberto(self):
        fim = (AGORA - dt.timedelta(minutes=11)).isoformat(timespec="seconds")
        self.ajuste("2026-10-16_1740", inicio=fim, fim=fim, status="em andamento",
                    em_andamento="081:0=-12.0", aplicados=[])
        a = self.montar()["agente"]
        self.assertIsNone(a["em_andamento"])
        self.assertEqual((a["ajustes"][0]["aberto"], a["ajustes"][0]["status"]), (False, "interrompido?"))
        self.assertIsNone(painel.ajuste_aberto(self.estado, AGORA))
        # Com 9 min ainda conta como aberto.
        fim = (AGORA - dt.timedelta(minutes=9)).isoformat(timespec="seconds")
        self.ajuste("2026-10-16_1740", inicio=fim, fim=fim, status="em andamento",
                    em_andamento="081:0=-12.0", aplicados=[])
        self.assertIsNotNone(self.montar()["agente"]["em_andamento"])

    def test_registro_com_campos_estranhos_nao_derruba(self):
        self.ajuste("2026-10-16_2030", decisao={"aprovado": True, "alertas": "x", "recusas": None, "ordem": 3},
                    inicio="2026-10-16T20:21:47+00:00", fim="2026-10-16T20:22:03-03:00",
                    lote=None, aplicados="081", status=5)
        a = self.montar()["agente"]["ajustes"][0]
        self.assertEqual((a["alertas"], a["recusas"], a["ordem"], a["lote"], a["aplicados"]), (0, [], [], [], []))
        self.assertEqual(a["hora"], dt.datetime.fromisoformat("2026-10-16T20:21:47+00:00").astimezone().strftime("%H:%M"))
        self.assertIsNone(a["status"])

    def test_lote_em_curso_antes_da_primeira_escrita(self):
        self.ajuste("2026-10-16_2030", fim=None, status="em andamento", aplicados=[], decisao=None)
        a = self.montar()["agente"]
        self.assertIsNone(a["em_andamento"]["em_andamento"])
        self.assertEqual(a["em_andamento"]["lote"], ["081:0=-12.0"])

    def test_linha_do_tempo_com_os_ajustes_reais(self):
        for nome in ("2026-10-16_2021", "2026-10-16_2021-2", "2026-10-16_2022"):
            shutil.copy(RAIZ / "estado" / "ajustes" / f"{nome}.json", self.estado / "ajustes")
        lista = self.montar()["agente"]["ajustes"]
        self.assertEqual([x["decisao"] for x in lista], ["APROVADO", "APROVADO", "RECUSADO"])
        self.assertEqual(lista[0]["hora"], "20:22")
        recusado = lista[2]
        self.assertTrue(recusado["simular"])
        self.assertEqual(recusado["lote"], ["042:0=0"])
        self.assertIn("trava 1: passa-alta da saída 1 (JBL L) desligado", recusado["recusas"][0])
        self.assertEqual(recusado["alertas"], 5)
        # A frase em português vem com o código exato ao lado.
        self.assertEqual(lista[0]["frases"], ["Ganho da saída 1 para −11,0 dB"])
        self.assertEqual(recusado["recusas_det"], [{"item": "042:0=0 (OFF)", "rotulo": "trava 1",
                                                    "motivo": "passa-alta da saída 1 (JBL L) desligado"}])

    def test_progresso_e_memorias(self):
        agora = dt.datetime(2026, 10, 17, 14, 0)
        with contextlib.redirect_stdout(io.StringIO()):
            sessao.main(["passo", "--etapa", "1", "--passo", "2", "--pedido", "x", "--resposta", "ok"],
                        estado=self.estado, agora=agora)
            sessao.main(["memoria", "1", "SPL", "HIGH", "--por", "ia"], estado=self.estado, agora=agora)
            sessao.main(["preset", "Rock", "--por", "denis"], estado=self.estado, agora=agora)
        e = self.montar()
        p = e["agente"]["progresso"]
        self.assertEqual((p["etapa"], p["passo"], p["travou"], p["passos_feitos"]), (1, 2, False, 1))
        self.assertEqual(p["retomar"], {"etapa": 1, "passo": 3, "nota": "", "total": 11})
        m = e["memorias"]
        self.assertEqual(m["dsp"]["1"], {"nome": "SPL HIGH", "por": "ia", "hora": "2026-10-17T14:00:00"})
        self.assertIsNone(m["dsp"]["2"])
        self.assertEqual(m["presets"], [{"nome": "Rock", "por": "denis", "hora": "2026-10-17T14:00:00"}])


class ModoAtivoTest(Base):
    def test_sem_registro_o_estado_nao_tem_modo(self):
        e = self.montar()
        self.assertIsNone(e["modo_ativo"])
        self.assertIsNone(e["memorias"]["modo_ativo"])

    def test_modo_gravado_pela_regulagem_aparece_no_estado(self):
        with contextlib.redirect_stdout(io.StringIO()):
            sessao.main(["modo", "Filme"], estado=self.estado, agora=dt.datetime(2026, 10, 17, 12, 40))
        e = self.montar()
        self.assertEqual(e["modo_ativo"], {"nome": "Filme", "por": "ia", "hora": "2026-10-17T12:40:00"})
        self.assertEqual(e["memorias"]["modo_ativo"], e["modo_ativo"])

    def test_memorias_ilegivel_sem_modo(self):
        (self.estado / "memorias.json").write_text("{", encoding="utf-8")
        self.assertIsNone(self.montar()["modo_ativo"])


class FraseTest(unittest.TestCase):
    """A linha do tempo da KARINA: primeiro a frase, depois o código exato."""

    def test_frases_das_escritas(self):
        casos = {
            "081:0=-12.0": "Ganho da saída 1 para −12,0 dB",
            "081:0=-11.0": "Ganho da saída 1 para −11,0 dB",
            "042:0=0": "Passa-alta da saída 1 desligado",
            "042:2=BT24": "Filtro do passa-alta da saída 3 para BT24",
            "040:0=100": "Passa-alta da saída 1 para 100 Hz",
            "071:2=-6.0": "Limiter da saída 3 para −6,0 dB (1,00 V)",
            "071:2=0.5": "Limiter da saída 3 desligado",
            "020:5=-2.0": "Banda de 250 Hz para −2,0 dB",
            "091:2=3": "Ganho do EQ da saída 3 para +3,0 dB",
            "100:1=1": "Saída 2 mutada",
            "080=-3": "Master para −3,0 dB",
            "070:0=1": "Limiter automático da saída 1 ligado",
            "070:0=0": "Limiter automático da saída 1 desligado",
            "100:0=0": "Saída 1 com som",
            "010:1=a+b": "Roteamento da saída 2 para A+B",
            "010:1=ab": "Roteamento da saída 2 para A+B",
            "043:2=0": "Passa-baixa da saída 3 desligado",
            "072:0=0.05": "Attack do limiter da saída 1 para auto",
            "092:1=1": "Q do EQ da saída 2 para 1,0",  # unidade vazia: sem espaço sobrando
        }
        for texto, esperado in casos.items():
            with self.subTest(texto=texto):
                self.assertEqual(painel.frase(texto), esperado)
        # Valor duvidoso: nada de frase confiante, fica o código como veio.
        for estranho in ("xx", "999:0=1", "081:0=abc", "020:40=1", "042:0=1.7", "042:0=99", "100:0=xyz",
                         "100:0=1.0", "070:0=talvez", "010:0=c"):
            with self.subTest(estranho=estranho):
                self.assertEqual(painel.frase(estranho), estranho)  # fora do formato: o próprio texto


class ServidorBase(Base):
    """Servidor de verdade numa porta livre, com o arquivo da senha numa pasta temporária."""

    def setUp(self):
        super().setUp()
        self.senha = Path(self.tmp.name) / "casa" / ".taramps" / "senha"
        patch = mock.patch.object(painel, "arquivo_senha", lambda: self.senha)
        patch.start()
        self.addCleanup(patch.stop)
        ambiente = mock.patch.dict(os.environ, {})
        ambiente.start()
        self.addCleanup(ambiente.stop)
        os.environ.pop("TARAMPS_SENHA", None)  # sem senha nenhuma até o teste dizer o contrário
        self.leitor = painel.Leitor(_python(0), cwd=self.estado, reserva=[])
        self.servidor = painel.Servidor(self.estado, 0, self.leitor)
        self.thread = threading.Thread(target=self.servidor.serve_forever, kwargs={"poll_interval": 0.05},
                                       daemon=True)
        self.thread.start()
        self.addCleanup(self.servidor.server_close)
        self.addCleanup(self.servidor.shutdown)
        self.url = f"http://127.0.0.1:{self.servidor.porta}"

    def pedir(self, caminho: str, corpo: dict | None = None, origem: str | None = None):
        dados = json.dumps(corpo).encode("utf-8") if corpo is not None else None
        req = urllib.request.Request(self.url + caminho, data=dados, method="POST" if dados is not None else "GET")
        if dados is not None:
            req.add_header("Content-Type", "application/json")
        if origem:
            req.add_header("Origin", origem)
        try:
            with urllib.request.urlopen(req, timeout=5) as resp:
                return resp.status, resp.read().decode("utf-8")
        except urllib.error.HTTPError as erro:
            return erro.code, erro.read().decode("utf-8")


class SenhaTest(ServidorBase):
    def test_login_unico_grava_no_formato_e_nunca_devolve(self):
        self.com_retrato_real()
        codigo, corpo = self.pedir("/api/estado")
        self.assertEqual(codigo, 200)
        self.assertFalse(json.loads(corpo)["senha_guardada"])

        codigo, corpo = self.pedir("/api/senha", {"senha": "abc"})
        self.assertEqual(codigo, 400)
        self.assertIn("4 letras", corpo)
        self.assertFalse(self.senha.exists())
        codigo, _ = self.pedir("/api/senha", {"senha": "ab!d"})
        self.assertEqual(codigo, 400)
        self.assertFalse(self.senha.exists())

        codigo, corpo = self.pedir("/api/senha", {"senha": f" {SENHA} "})
        self.assertEqual(codigo, 200)
        self.assertEqual(json.loads(corpo), {"ok": True})
        self.assertNotIn(SENHA, corpo)
        self.assertEqual(self.senha.read_bytes(), SENHA.encode("ascii"))  # sem BOM, sem quebra
        self.assertFalse(self.senha.with_name("senha.tmp").exists())
        # O mesmo formato que o leitor lê.
        with mock.patch.object(lp, "arquivo_senha", lambda: self.senha), \
                mock.patch.dict("os.environ", {}, clear=False):
            import os
            os.environ.pop("TARAMPS_SENHA", None)
            self.assertEqual(lp.obter_senha(), SENHA)
        self.assertEqual(pt.montar_autenticacao(self.senha.read_text()), f"*000*{SENHA.upper()}*")

        # Com o arquivo presente, o login não aparece mais e um novo POST não sobrescreve.
        codigo, corpo = self.pedir("/api/estado")
        self.assertTrue(json.loads(corpo)["senha_guardada"])
        self.assertNotIn(SENHA, corpo)
        self.assertNotIn(SENHA.upper(), corpo)
        codigo, corpo = self.pedir("/api/senha", {"senha": "AAAA"})
        self.assertEqual(codigo, 409)
        self.assertEqual(self.senha.read_bytes(), SENHA.encode("ascii"))
        codigo, html = self.pedir("/")
        self.assertEqual(codigo, 200)
        self.assertNotIn(SENHA, html)
        self.assertNotIn(SENHA.upper(), html)

    def test_senha_nao_vai_para_o_console(self):
        saida, erro = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(saida), contextlib.redirect_stderr(erro):
            self.pedir("/api/senha", {"senha": SENHA})
            self.pedir("/api/senha", {"senha": SENHA})
        self.assertNotIn(SENHA, saida.getvalue() + erro.getvalue())
        self.assertNotIn(SENHA.upper(), saida.getvalue() + erro.getvalue())

    def test_post_de_outra_origem_e_recusado(self):
        codigo, _ = self.pedir("/api/senha", {"senha": SENHA}, origem="http://exemplo.com")
        self.assertEqual(codigo, 403)
        self.assertFalse(self.senha.exists())
        codigo, _ = self.pedir("/api/ler", {}, origem="http://exemplo.com")
        self.assertEqual(codigo, 403)
        codigo, _ = self.pedir("/api/senha", {"senha": SENHA}, origem=f"http://localhost:{self.servidor.porta}")
        self.assertEqual(codigo, 200)

    def test_corpo_invalido_e_rota_inexistente(self):
        self.assertEqual(self.pedir("/api/senha", {"outra": 1})[0], 400)
        codigo, corpo = self.pedir("/api/senha", [1, 2])  # JSON que não é objeto
        self.assertEqual((codigo, json.loads(corpo)["erro"]), (400, "corpo inválido"))
        self.assertEqual(self.pedir("/api/ler", "texto")[0], 400)
        self.assertEqual(self.pedir("/nada")[0], 404)
        self.assertEqual(self.pedir("/api/nada", {})[0], 404)
        self.assertEqual(self.pedir("/api/estado")[0], 200)

    def test_senha_na_variavel_de_ambiente_tambem_conta(self):
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": "ZZZZ"}):
            self.assertTrue(json.loads(self.pedir("/api/estado")[1])["senha_guardada"])
        self.assertFalse(json.loads(self.pedir("/api/estado")[1])["senha_guardada"])


class PaginaTest(ServidorBase):
    def test_pagina_inline_sem_cdn(self):
        codigo, html = self.pedir("/")
        self.assertEqual(codigo, 200)
        for trecho in ("Denis", "Home Theater 2.1", "regulado por IA com guardião", "Ler agora",
                       "/api/estado", "/api/senha", "/api/ler", "type=\"password\"", "maxlength=\"4\""):
            self.assertIn(trecho, html)
        # Sem CDN: nada carregado de fora (só o xmlns do SVG, que não é um pedido de rede).
        self.assertNotIn("https://", html)
        self.assertNotIn('src="http', html)
        self.assertNotIn('href="http', html)
        self.assertNotIn("<script src", html)
        self.assertNotIn("<link ", html)
        self.assertNotIn("@import", html)
        self.assertNotIn("cdn", html.lower())
        e = json.loads(self.pedir("/api/estado")[1])
        self.assertEqual([t["titulo"] for t in e["telas"]], [
            "Home", "Roteamento", "Alinhamento", "Fase", "EQ Param Entrada", "Crossover",
            "Limiter", "Nível das Saídas", "EQ Param Saída", "EQ 15 Bandas"])

    def test_estado_pela_api_com_o_retrato_real(self):
        self.com_retrato_real()
        codigo, corpo = self.pedir("/api/estado")
        e = json.loads(corpo)
        self.assertEqual(e["leitura"]["firmware"], "R003")
        self.assertEqual(len(e["guardiao"]["violacoes"]), 5)
        self.assertEqual(e["leitor"], {"lendo": False, "segundos": 0, "resultado": None})


class PaginaNovaTest(ServidorBase):
    """A Vitrine e os Bastidores: módulos locais, nada técnico na Vitrine, som só na abertura."""

    PASTA = Path(painel.__file__).resolve().parent / "painel"

    def vitrine(self, html: str) -> str:
        inicio = html.index('<section id="palco"')
        return html[inicio:html.index("</section>", inicio)]

    def test_modulos_locais_sao_servidos_e_sem_cdn(self):
        codigo, html = self.pedir("/")
        self.assertEqual(codigo, 200)
        modulos = re.findall(r"""from\s+['"]\./([\w.-]+\.js)['"]""", html)
        self.assertEqual(sorted(modulos), ["abertura.js", "anel.js"])
        for nome in modulos:
            with self.subTest(nome=nome):
                with urllib.request.urlopen(f"{self.url}/{nome}", timeout=5) as resp:
                    self.assertEqual(resp.status, 200)
                    self.assertTrue(resp.headers["Content-Type"].startswith("text/javascript"))
                    js = resp.read().decode("utf-8")
                for proibido in ("https://", "http://", "cdn", "@import", "<audio", ".mp3", ".wav", "fetch("):
                    self.assertNotIn(proibido, js.lower() if proibido == "cdn" else js)

    def test_vitrine_sem_texto_tecnico(self):
        _, html = self.pedir("/")
        vitrine = self.vitrine(html)
        for marca in ("BGS", "Sound Lab", "Denis Borges", "KARINA", "Som ajustado pela", "em evolução",
                      "Toque para começar"):
            self.assertIn(marca, vitrine)
        texto = re.sub(r"<[^>]+>", " ", vitrine)  # só o que o convidado lê
        for tecnico in ("Hz", "dB", "BLE", "firmware", "Firmware", "DSP", "R003", "guardião", "agente"):
            self.assertNotIn(tecnico, texto)

    def test_som_so_na_abertura_e_microfone_so_escuta(self):
        abertura = (self.PASTA / "abertura.js").read_text(encoding="utf-8")
        anel = (self.PASTA / "anel.js").read_text(encoding="utf-8")
        _, html = self.pedir("/")
        for trecho in ("createDynamicsCompressor", "threshold.value = TETO_DB", "TETO_DB = -12",
                       "RAZAO = 20", "attack.value = 0.003", "ctx.close()", "createConvolver"):
            self.assertIn(trecho, abertura)
        # Só a abertura liga algo à saída de áudio; o anel e a página nunca.
        self.assertIn("ctx.destination", abertura)
        self.assertNotIn("destination", anel)
        self.assertNotIn("destination", html)
        self.assertNotIn("getUserMedia", abertura)
        self.assertNotIn("getUserMedia", html)  # o pedido do microfone mora no anel.js
        self.assertIn("echoCancellation: false", anel)
        self.assertIn("'granted'", anel)  # a Vitrine só abre o microfone já autorizado

    def test_bastidores_mantem_as_funcoes(self):
        _, html = self.pedir("/")
        for trecho in ("/api/estado", "/api/ler", "/api/senha", "/api/modo", "POLL_MS = 3000", "Ler agora",
                       "voltar à Vitrine", "KARINA", "Jornada", "feito", "planejado", "em evolução",
                       'data-modo="Filme"', 'data-modo="Música SPL"', 'data-modo="Música EQ"', 'data-modo="nenhum"',
                       'role="dialog"', "escrevendo", "prefers-reduced-motion", "As 10 telas do app"):
            self.assertIn(trecho, html)


class ModoApiTest(ServidorBase):
    def memorias(self) -> dict:
        return json.loads((self.estado / "memorias.json").read_text(encoding="utf-8"))

    def test_seletor_grava_pelo_denis_e_o_estado_mostra(self):
        codigo, corpo = self.pedir("/api/modo", {"nome": "Música SPL"})
        self.assertEqual(codigo, 200)
        resposta = json.loads(corpo)
        self.assertTrue(resposta["ok"])
        self.assertEqual((resposta["modo_ativo"]["nome"], resposta["modo_ativo"]["por"]), ("Música SPL", "denis"))
        self.assertEqual(self.memorias()["modo_ativo"]["nome"], "Música SPL")
        e = json.loads(self.pedir("/api/estado")[1])
        self.assertEqual((e["modo_ativo"]["nome"], e["modo_ativo"]["por"]), ("Música SPL", "denis"))
        # Vale a última gravação, de qualquer origem.
        with contextlib.redirect_stdout(io.StringIO()):
            sessao.main(["modo", "Filme"], estado=self.estado)
        self.assertEqual(json.loads(self.pedir("/api/estado")[1])["modo_ativo"]["por"], "ia")
        diario = next((self.estado / "sessoes").glob("*.md")).read_text(encoding="utf-8")
        self.assertIn("Modo da Vitrine", diario)
        self.assertIn("- Música SPL (por Denis)", diario)  # o seletor também fica no diário
        codigo, corpo = self.pedir("/api/modo", {"nome": "nenhum"})
        self.assertEqual((codigo, json.loads(corpo)["modo_ativo"]), (200, None))
        self.assertNotIn("modo_ativo", self.memorias())
        self.assertIsNone(json.loads(self.pedir("/api/estado")[1])["modo_ativo"])

    def test_nome_invalido_nao_grava(self):
        for corpo in ({"nome": "Rock"}, {"nome": 3}, {}, {"nome": ""}):
            with self.subTest(corpo=corpo):
                self.assertEqual(self.pedir("/api/modo", corpo)[0], 400)
        self.assertFalse((self.estado / "memorias.json").exists())

    def test_memorias_ilegivel_recusa_sem_estragar(self):
        (self.estado / "memorias.json").write_text("{", encoding="utf-8")
        codigo, corpo = self.pedir("/api/modo", {"nome": "Filme"})
        self.assertEqual(codigo, 409)
        self.assertFalse(json.loads(corpo)["ok"])
        self.assertEqual((self.estado / "memorias.json").read_text(encoding="utf-8"), "{")

    def test_outra_origem_e_recusada(self):
        self.assertEqual(self.pedir("/api/modo", {"nome": "Filme"}, origem="http://exemplo.com")[0], 403)
        self.assertFalse((self.estado / "memorias.json").exists())

    def test_modo_preserva_memorias_e_nao_toca_o_aparelho(self):
        with contextlib.redirect_stdout(io.StringIO()):
            sessao.main(["memoria", "3", "Filme-EQ", "--por", "ia"], estado=self.estado)
        self.assertEqual(self.pedir("/api/modo", {"nome": "Filme"})[0], 200)
        self.assertEqual(self.memorias()["dsp"]["3"]["nome"], "Filme-EQ")
        self.assertEqual(list((self.estado / "ajustes").iterdir()), [])  # nenhum ajuste no processador
        self.assertFalse(self.leitor.lendo)


class EstaticoTest(ServidorBase):
    """GET /<arquivo> serve só o que está direto em painel/, sem sair da pasta."""

    def setUp(self):
        super().setUp()
        raiz = Path(self.tmp.name)
        self.pasta = raiz / "web"
        self.pasta.mkdir()
        (self.pasta / "index.html").write_text("<p>pagina</p>", encoding="utf-8")
        (self.pasta / "app.js").write_text("export const x = 1;", encoding="utf-8")
        (self.pasta / "estilo.css").write_text("p{}", encoding="utf-8")
        (self.pasta / "segredo.txt").write_text("nao", encoding="utf-8")
        (self.pasta / ".oculto.js").write_text("nao", encoding="utf-8")
        (self.pasta / "sub").mkdir()
        (self.pasta / "sub" / "dentro.js").write_text("nao", encoding="utf-8")
        (raiz / "fora.js").write_text("FORA", encoding="utf-8")
        self.servidor.html = self.pasta / "index.html"

    def bruto(self, caminho: str) -> tuple[int, bytes, str]:
        conexao = http.client.HTTPConnection("127.0.0.1", self.servidor.porta, timeout=5)
        try:
            conexao.request("GET", caminho)  # sem normalizar: ".." e %xx chegam como vieram
            resp = conexao.getresponse()
            return resp.status, resp.read(), resp.getheader("Content-Type") or ""
        finally:
            conexao.close()

    def test_serve_os_arquivos_da_pasta_com_o_tipo_certo(self):
        codigo, corpo, tipo = self.bruto("/app.js")
        self.assertEqual((codigo, corpo), (200, b"export const x = 1;"))
        self.assertTrue(tipo.startswith("text/javascript"))
        codigo, _, tipo = self.bruto("/estilo.css")
        self.assertEqual(codigo, 200)
        self.assertTrue(tipo.startswith("text/css"))
        self.assertEqual(self.bruto("/app.js?v=2")[0], 200)
        self.assertEqual(self.bruto("/")[1], b"<p>pagina</p>")

    def test_nao_sai_da_pasta(self):
        for caminho in ("/../fora.js", "/%2e%2e/fora.js", "/..%2ffora.js", "/..%5cfora.js", "/%2e%2e%5cfora.js",
                        "/sub/../../fora.js", "/sub/dentro.js", "/sub%2fdentro.js", "//fora.js",
                        "/C:/Windows/win.ini", "/C:%5cWindows%5cwin.ini", "/.oculto.js", "/segredo.txt",
                        "/app.js%00.js", "/app.js.", "/app.js%20", "/nada.js", "/painel.py", "/../painel.py",
                        "/" + str(Path(self.tmp.name) / "fora.js").replace("\\", "/")):
            with self.subTest(caminho=caminho):
                codigo, corpo, _ = self.bruto(caminho)
                self.assertEqual(codigo, 404)
                self.assertNotIn(b"FORA", corpo)

    def test_favicon_ausente_nao_e_erro(self):
        self.assertEqual(self.bruto("/favicon.ico")[0], 204)

    def test_rota_de_api_desconhecida_continua_404(self):
        self.assertEqual(self.bruto("/api/nada.js")[0], 404)

    def test_funcao_sozinha(self):
        self.assertEqual(painel.arquivo_estatico(self.pasta, "/app.js"), (self.pasta / "app.js").resolve())
        self.assertIsNone(painel.arquivo_estatico(self.pasta, "/%2e%2e/fora.js"))
        self.assertIsNone(painel.arquivo_estatico(self.pasta, "app.js"))
        self.assertIsNone(painel.arquivo_estatico(self.pasta, "/"))


class LerAgoraTest(ServidorBase):
    def setUp(self):
        super().setUp()
        os.environ["TARAMPS_SENHA"] = "TEST"  # o leitor falso ignora; só libera o botão

    def esperar(self, segundos: float = 5.0) -> dict:
        self.leitor.esperar(segundos)
        return json.loads(self.pedir("/api/estado")[1])

    def test_sem_senha_recusa_antes_de_rodar(self):
        os.environ.pop("TARAMPS_SENHA", None)
        codigo, corpo = self.pedir("/api/ler", {})
        self.assertEqual(codigo, 409)
        self.assertIn("senha", json.loads(corpo)["erro"])
        self.assertFalse(self.leitor.lendo)
        self.assertIsNone(self.leitor.resultado)

    def test_com_ajuste_aberto_recusa(self):
        (self.estado / "ajustes" / "2026-10-16_2030.json").write_text(json.dumps({
            "formato": 1, "inicio": dt.datetime.now().isoformat(timespec="seconds"), "fim": None,
            "lote": ["081:0=-12.0"], "status": "em andamento", "em_andamento": None}), encoding="utf-8")
        codigo, corpo = self.pedir("/api/ler", {})
        self.assertEqual(codigo, 409)
        self.assertIn("ajustando", json.loads(corpo)["erro"])
        self.assertFalse(self.leitor.lendo)

    def test_reserva_entra_quando_o_comando_nao_existe(self):
        self.leitor.comando = ["programa-que-nao-existe-xyz"]
        self.leitor.reserva = _python(0)
        self.pedir("/api/ler", {})
        r = self.esperar()["leitor"]["resultado"]
        self.assertEqual((r["codigo"], r["ok"]), (0, True))

    def test_leitor_preso_e_cancelado(self):
        self.leitor.comando = _python(0, dorme=5)
        self.leitor.cwd = Path(tempfile.gettempdir())  # o filho morto solta a pasta com atraso no Windows
        with mock.patch.object(painel, "TIMEOUT_LER_S", 0.3):
            self.pedir("/api/ler", {})
            r = self.esperar(10)["leitor"]["resultado"]
        self.assertIsNone(r["codigo"])
        self.assertEqual(r["mensagem"], "O leitor travou; cancelei.")
        self.assertFalse(self.leitor.lendo)
        self.assertEqual(self.pedir("/api/ler", {})[0], 200)  # o botão volta a funcionar

    def test_sem_stdout_usa_o_stderr_como_detalhe(self):
        self.leitor.comando = [sys.executable, "-c", "import sys; sys.stderr.write('deu ruim\\n'); sys.exit(1)"]
        self.pedir("/api/ler", {})
        r = self.esperar()["leitor"]["resultado"]
        self.assertEqual((r["codigo"], r["detalhe"]), (1, "deu ruim"))

    def test_ler_agora_roda_uma_vez_e_informa_o_resultado(self):
        self.leitor.comando = _python(0, dorme=0.6)
        codigo, corpo = self.pedir("/api/ler", {})
        self.assertEqual((codigo, json.loads(corpo)), (200, {"ok": True}))
        e = json.loads(self.pedir("/api/estado")[1])
        self.assertTrue(e["leitor"]["lendo"])
        self.assertTrue(e["agente"]["lendo"])
        self.assertEqual(self.pedir("/api/ler", {})[0], 409)  # um por vez
        e = self.esperar()
        self.assertFalse(e["leitor"]["lendo"])
        r = e["leitor"]["resultado"]
        self.assertEqual((r["codigo"], r["ok"], r["mensagem"]), (0, True, "Leitura nova."))
        self.assertEqual(r["detalhe"], "saida do leitor")

    def test_codigo_de_saida_vira_mensagem_curta(self):
        self.leitor.comando = _python(3)
        self.pedir("/api/ler", {})
        r = self.esperar()["leitor"]["resultado"]
        self.assertEqual(r["codigo"], 3)
        self.assertIn("celular", r["mensagem"])
        self.leitor.comando = _python(2)
        self.pedir("/api/ler", {})
        r = self.esperar()["leitor"]["resultado"]
        self.assertIn("Senha recusada", r["mensagem"])

    def test_leitor_sem_comando_nao_derruba(self):
        self.leitor.comando = ["programa-que-nao-existe-xyz"]
        self.pedir("/api/ler", {})
        r = self.esperar()["leitor"]["resultado"]
        self.assertIsNone(r["codigo"])
        self.assertIn("Não consegui rodar", r["mensagem"])

    def test_comando_padrao_e_o_leitor_por_uv(self):
        self.assertEqual(painel.COMANDO_LER, ["uv", "run", "ferramentas/ler_processador.py"])
        self.assertNotIn("ajustar", " ".join(painel.COMANDO_LER))


class CliTest(unittest.TestCase):
    def test_sem_navegador_sobe_e_responde(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        estado = Path(tmp.name) / "estado"
        estado.mkdir()
        servidor = painel.Servidor(estado, 0, None)
        porta = servidor.porta
        servidor.server_close()
        saida = io.StringIO()
        resultado: dict = {}

        def rodar():
            with contextlib.redirect_stdout(saida):
                resultado["codigo"] = painel.main(["--sem-navegador", "--porta", str(porta)], estado=estado)

        def servir_um_pedido(self_servidor, **kw):
            self_servidor.timeout = 5
            self_servidor.handle_request()  # atende o GET abaixo de verdade e depois "Ctrl+C"
            raise KeyboardInterrupt

        def pedir():
            for _ in range(50):
                try:
                    with urllib.request.urlopen(f"http://127.0.0.1:{porta}/api/estado", timeout=5) as resp:
                        resultado["get"] = (resp.status, json.loads(resp.read().decode("utf-8")))
                        return
                except (urllib.error.URLError, ConnectionError, TimeoutError):
                    time.sleep(0.1)

        with mock.patch.object(painel.Servidor, "serve_forever", servir_um_pedido):
            cliente = threading.Thread(target=pedir, daemon=True)
            cliente.start()
            thread = threading.Thread(target=rodar)
            thread.start()
            thread.join(10)
            cliente.join(5)
        self.assertEqual(resultado.get("codigo"), 0)
        self.assertEqual(resultado["get"][0], 200)
        self.assertIsNone(resultado["get"][1]["leitura"])
        self.assertIn(f"http://127.0.0.1:{porta}", saida.getvalue())
        self.assertIn("fechado", saida.getvalue())

    def test_segunda_instancia_na_mesma_porta_falha(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        estado = Path(tmp.name) / "estado"
        estado.mkdir()
        primeiro = painel.Servidor(estado, 0, None)
        self.addCleanup(primeiro.server_close)
        saida = io.StringIO()
        with contextlib.redirect_stdout(saida):
            codigo = painel.main(["--sem-navegador", "--porta", str(primeiro.porta)], estado=estado)
        self.assertEqual(codigo, 1)
        self.assertIn("já está aberto", saida.getvalue())


if __name__ == "__main__":
    unittest.main()
