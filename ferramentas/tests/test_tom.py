"""Testes do gerador de tom (tom.py, cliente, protocolo e guardião), com um processador falso
que guarda o estado do gerador (200, 201, 202), partindo do retrato real
(estado/leituras/2026-10-16_1747.json) e do estado/guardiao.json real. Nunca conecta no
aparelho de verdade (spec-gerador-memoria.md)."""

from __future__ import annotations

import asyncio
import contextlib
import dataclasses
import io
import json
import os
import re
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import ajustar_processador as ap  # noqa: E402
import cliente_ble as cb  # noqa: E402
import guardiao as gd  # noqa: E402
import painel  # noqa: E402
import protocolo_taramps as pt  # noqa: E402
import test_ajustar_processador as tap  # noqa: E402
import tom as tm  # noqa: E402

RETRATO = tap.RETRATO
GUARDIAO = tap.GUARDIAO
SENHA = tap.SENHA
TEMPOS = tap.TEMPOS
# O retrato real com as violações corrigidas (como em test_guardiao.CORRIGIDO), no texto que o
# aparelho responde.
CORRIGIDO = {("042", 0): "11", ("040", 0): "100", ("042", 1): "11", ("040", 1): "100",
             ("040", 2): "40", ("040", 3): "40", ("072", 3): "50.0"}
CAMINHO_PROVADO = "estado/auditorias/provado-teste.json"

_ESPECIAL_RE = re.compile(r"\*(\d{3})\*([^*]*)\*")


class ProcessadorGerador(tap.Processador):
    """O processador falso de test_ajustar_processador, mais o gerador (200/201/202), as
    memórias (240) e os nomes (231–233). `cair_ao_ligar`: a conexão cai logo depois do
    `*202*1*`; `sumir_depois_de_cair`: depois da queda, o scan não acha mais o aparelho."""

    def __init__(self, *, cair_ao_ligar: bool = False, sumir_depois_de_cair: bool = False,
                 gerador_ligado: bool = False, nome_relido: dict | None = None,
                 mudo_no_202_depois_de_desligar: int = 0, **kw):
        kw.setdefault("iniciais", CORRIGIDO)
        super().__init__(**kw)
        self.gerador = {"200": "-20.0", "201": "1000", "202": "1" if gerador_ligado else "0"}
        self.memorias: dict[int, dict] = {}
        self.nomes = {1: "", 2: "", 3: ""}
        self.nome_relido = nome_relido or {}
        self.especiais: list[str] = []
        self.cair_ao_ligar = cair_ao_ligar
        self.sumir = sumir_depois_de_cair
        self.caiu = False
        self.scans = 0
        self.conexoes = 0
        self.mudo_202 = mudo_no_202_depois_de_desligar
        self.historico_202: list[str] = []  # cada mudança do 202, na ordem

    async def procurar(self):
        self.scans += 1
        if self.caiu and self.sumir:
            return None
        return tap.Dispositivo()

    async def conectar(self, dispositivo):
        self.conectado = True
        self.conexoes += 1

    def ligado(self) -> bool:
        return self.gerador["202"] == "1"

    async def escrever(self, dados: bytes, preferir_resposta: bool):
        texto = dados.decode("ascii")
        if not self.conectado:
            raise RuntimeError("sem conexão no falso")
        m = _ESPECIAL_RE.fullmatch(texto)
        if m and m.group(1) in pt.CODIGOS_ESPECIAIS:
            self.enviados.append(texto)
            self.especiais.append(texto)
            codigo, valor = m.groups()
            if codigo in ("200", "201", "202"):
                self.gerador[codigo] = valor
                if codigo == "202":
                    self.historico_202.append(valor)
                if texto == "*202*1*" and self.cair_ao_ligar and not self.caiu:
                    self.caiu = True
                    self.conectado = False
                    asyncio.get_running_loop().call_later(0.002, self.ao_cair)
            elif codigo == "240":
                self.memorias[int(valor)] = dict(self.valores)
            elif codigo == "252":
                self.remote_out = valor
            else:
                self.nomes[int(codigo[2])] = valor
            return
        if texto in ("#200#", "#201#", "#202#"):
            self.enviados.append(texto)
            codigo = texto[1:4]
            if codigo == "202" and self.mudo_202 and self.historico_202[-1:] == ["0"]:
                self.mudo_202 -= 1
                return
            self._mandar(f"#{codigo}#{self.gerador[codigo]}#\r\n")
            return
        if re.fullmatch(r"23[123]#", texto):
            self.enviados.append(texto)
            n = int(texto[2])
            self._mandar(f"{texto}{self.nome_relido.get(n, self.nomes[n])}\r\n")
            return
        return await super().escrever(dados, preferir_resposta)


def preparar(estado: Path, *, liberado: bool = False, caminho: str | None = None,
             arquivo_caminho: dict | None = None) -> None:
    dados = json.loads(GUARDIAO.read_text(encoding="utf-8"))
    dados["gerador"] = {"liberado": liberado, "caminho_provado": caminho}
    (estado / "guardiao.json").write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")
    if arquivo_caminho is not None:
        destino = estado.parent / CAMINHO_PROVADO
        destino.parent.mkdir(parents=True, exist_ok=True)
        destino.write_text(json.dumps(arquivo_caminho), encoding="utf-8")


PROVADO = {"tipo": "caminho-gerador", "aprovado": True, "restaurado": True}


async def _rapido(segundos):
    await asyncio.sleep(0.01)


class Execucao:
    def __init__(self, transporte, argv=(), *, entradas=(), dormir=_rapido, liberado=True,
                 caminho=CAMINHO_PROVADO, arquivo_caminho=PROVADO, fabrica=None):
        self.tmp = tempfile.TemporaryDirectory()
        self.estado = Path(self.tmp.name) / "estado"
        self.estado.mkdir()
        preparar(self.estado, liberado=liberado, caminho=caminho, arquivo_caminho=arquivo_caminho)
        respostas = iter(entradas)
        self.perguntas: list[str] = []

        def entrada(pergunta=""):
            self.perguntas.append(pergunta)
            return next(respostas)

        saida = io.StringIO()
        self.excecao = None
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": SENHA}), \
                contextlib.redirect_stdout(saida):
            try:
                self.codigo = tm.main(list(argv), transporte=transporte, estado=self.estado,
                                      tempos=TEMPOS, entrada=entrada, dormir=dormir,
                                      fabrica_transporte=fabrica or (lambda: transporte))
            except BaseException as erro:  # noqa: BLE001 - o teste confere a exceção
                self.codigo, self.excecao = None, erro
        self.saida = saida.getvalue()

    def registro(self) -> dict:
        arquivos = list((self.estado / "ajustes").glob("*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def auditoria(self) -> dict:
        arquivos = list((self.estado / "auditorias").glob("*_caminho-gerador*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def fechar(self):
        self.tmp.cleanup()


class Base(unittest.TestCase):
    def rodar(self, transporte, argv, **kw) -> Execucao:
        ex = Execucao(transporte, argv, **kw)
        self.addCleanup(ex.fechar)
        return ex

    def assert_desligado_e_relido(self, falso: ProcessadorGerador):
        self.assertFalse(falso.ligado())
        self.assertEqual(falso.especiais[-1], "*202*0*")
        i = len(falso.enviados) - 1 - falso.enviados[::-1].index("*202*0*")
        self.assertIn("#202#", falso.enviados[i + 1:])  # releitura depois do desligar

    def assert_nada_enviado(self, falso: ProcessadorGerador):
        self.assertEqual(falso.enviados, [])
        self.assertFalse(falso.conectado)
        self.assertEqual(falso.conexoes, 0)


# --------------------------------------------------------------------------- matriz da spec


class TomTest(Base):
    def test_tom_liberado_liga_espera_desliga_e_rele(self):
        falso = ProcessadorGerador()
        esperas: list = []

        async def dormir(s):
            esperas.append((s, falso.ligado()))
            await asyncio.sleep(0.01)

        ex = self.rodar(falso, ["400", "--db", "0", "--s", "20"], dormir=dormir)
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*200*0.0*", "*201*400*", "*202*1*", "*202*0*"])
        self.assertEqual(esperas, [(20.0, True)])  # esperou com o tom ligado
        self.assert_desligado_e_relido(falso)
        self.assertEqual(falso.historico_202, ["1", "0"])
        r = ex.registro()
        self.assertEqual((r["tipo"], r["status"]), ("tom", "tocado"))
        self.assertTrue(r["gerador_desligado"])
        self.assertTrue(r["decisao"]["aprovado"])
        self.assertEqual(r["aplicados"], ["tom 400 Hz, 0.0 dB, 20 s"])
        self.assertEqual([c["status"] for c in r["comandos"]],
                         ["igual", "igual", "enviado", "relido 0"])
        self.assertIn("Gerador desligado (relido 202 = 0)", ex.saida)
        self.assertFalse(falso.conectado)
        # Leitura nova completa antes do tom, com backup.
        self.assertEqual(len(list((ex.estado / "backups").glob("*.json"))), 1)
        self.assertLess(falso.enviados.index("#080#"), falso.enviados.index("*200*0.0*"))
        log = next((ex.estado / "log-ble").glob("*-tom.log")).read_text(encoding="utf-8")
        self.assertIn("|*202*1*|  especial", log)
        self.assertNotIn(SENHA, log)

    def test_sem_caminho_provado_recusa_antes_de_conectar(self):
        casos = {
            "liberado false": dict(liberado=False),
            "sem caminho_provado": dict(caminho=None),
            "arquivo ausente": dict(arquivo_caminho=None),
            "arquivo não aprovado": dict(arquivo_caminho=dict(PROVADO, aprovado=False)),
            "arquivo não restaurado": dict(arquivo_caminho=dict(PROVADO, restaurado=None)),
        }
        for nome, kw in casos.items():
            with self.subTest(nome):
                falso = ProcessadorGerador()
                ex = self.rodar(falso, ["400"], **kw)
                self.assertEqual(ex.codigo, tm.SAIDA_RECUSADO, ex.saida)
                self.assertNotEqual(ex.codigo, 0)
                self.assert_nada_enviado(falso)
                self.assertIn("Nada foi enviado", ex.saida)
                self.assertEqual(ex.registro()["status"], "recusado")

    def test_violacao_no_estado_recusa_e_diz_a_trava(self):
        # O retrato real: subsônico em 28 Hz (trava 2) e JBL em BT12 (trava 1).
        falso = ProcessadorGerador(iniciais={})
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_RECUSADO, ex.saida)
        self.assertEqual(falso.especiais, [])
        self.assertIn("trava 2", ex.saida)
        self.assertIn("subsônico da saída 3", ex.saida)
        self.assertIn("o tom só toca com o guardião limpo", ex.saida)
        self.assertEqual(ex.registro()["status"], "recusado")

    def test_regra6_nao_barra_o_tom_mas_trava3_barra(self):
        # Nível acima da linha de base (regra 6) não barra; limiter acima do teto (trava 3) barra.
        falso = ProcessadorGerador(iniciais={**CORRIGIDO, ("081", 1): "-9.0"})
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)
        self.assertIn("regra 6", ex.saida)  # aparece como alerta
        falso = ProcessadorGerador(iniciais={**CORRIGIDO, ("071", 0): "-5.0"})
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_RECUSADO, ex.saida)
        self.assertIn("trava 3", ex.saida)
        self.assertEqual(falso.especiais, [])

    def test_limites_recusam_antes_de_conectar(self):
        for argv in (["400", "--s", "60"], ["400", "--s", "30.1"], ["400", "--s", "0"],
                     ["5", "--s", "10"], ["20001"], ["400.5"], ["400", "--db", "0.5"],
                     ["400", "--db", "-60.5"], ["400", "--db", "-3.25"]):
            with self.subTest(argv=argv):
                falso = ProcessadorGerador()
                ex = self.rodar(falso, argv)
                self.assertEqual(ex.codigo, tm.SAIDA_RECUSADO, ex.saida)
                self.assert_nada_enviado(falso)
        falso = ProcessadorGerador()
        ex = self.rodar(falso, ["400", "--s", "30", "--db", "-60"])
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)  # os limites valem inclusive
        self.assertIn("duração do tom vai até 30 s", self.rodar(ProcessadorGerador(),
                                                                ["400", "--s", "60"]).saida)

    def test_ctrl_c_no_meio_desliga_e_rele(self):
        for interrupcao in (KeyboardInterrupt, asyncio.CancelledError):
            with self.subTest(interrupcao=interrupcao.__name__):
                falso = ProcessadorGerador()

                async def dormir(s):
                    await asyncio.sleep(0.005)
                    raise interrupcao()

                ex = self.rodar(falso, ["400", "--s", "20"], dormir=dormir)
                self.assertIsInstance(ex.excecao, interrupcao)
                self.assert_desligado_e_relido(falso)
                r = ex.registro()
                self.assertEqual(r["status"], "interrompido")
                self.assertTrue(r["gerador_desligado"])
                self.assertTrue(tm._CONTROLE.tratado)  # o atexit não precisa agir

    def test_excecao_no_meio_desliga_e_rele(self):
        falso = ProcessadorGerador()

        async def dormir(s):
            raise RuntimeError("falha qualquer")

        ex = self.rodar(falso, ["400"], dormir=dormir)
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assert_desligado_e_relido(falso)

    def test_queda_com_o_tom_ligado_reconecta_e_desliga(self):
        falso = ProcessadorGerador(cair_ao_ligar=True)
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.conexoes, 2)  # reconectou
        self.assert_desligado_e_relido(falso)
        self.assertEqual(falso.enviados.count(f"*000*{SENHA}*"), 2)  # senha de novo
        self.assertNotIn("DESLIGUE", ex.saida)
        self.assertTrue(ex.registro()["gerador_desligado"])

    def test_queda_sem_reconexao_avisa_desligue_o_modulo(self):
        falso = ProcessadorGerador(cair_ao_ligar=True, sumir_depois_de_cair=True)
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_GERADOR_LIGADO, ex.saida)
        self.assertNotEqual(ex.codigo, 0)
        self.assertTrue(falso.ligado())
        self.assertEqual(falso.scans, 1 + tm.TENTATIVAS_DESLIGAR)  # 3 tentativas de reconectar
        self.assertIn(tm.AVISO_DESLIGUE, ex.saida)
        self.assertEqual(ex.saida.count(tm.AVISO_DESLIGUE), 1)
        r = ex.registro()
        self.assertEqual(r["status"], "gerador talvez ligado")
        self.assertFalse(r["gerador_desligado"])

    def test_releitura_sem_confirmar_tenta_de_novo(self):
        # A 1ª releitura do 202 depois do desligar não responde: manda *202*0* outra vez.
        falso = ProcessadorGerador(mudo_no_202_depois_de_desligar=1)
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais.count("*202*0*"), 2)
        self.assert_desligado_e_relido(falso)

    def test_gerador_ja_ligado_desliga_e_nao_toca(self):
        falso = ProcessadorGerador(gerador_ligado=True)
        ex = self.rodar(falso, ["400"])
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.especiais, ["*202*0*"])
        self.assertFalse(falso.ligado())
        self.assertIn("já estava ligado", ex.saida)

    def test_ganho_divergente_nao_liga(self):
        class Teimoso(ProcessadorGerador):
            async def escrever(self, dados, preferir_resposta):
                await super().escrever(dados, preferir_resposta)
                if dados == b"*200*-10.0*":
                    self.gerador["200"] = "-20.0"  # não aplica

        falso = Teimoso()
        ex = self.rodar(falso, ["400", "--db", "-10"])
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertNotIn("*202*1*", falso.especiais)
        self.assertIn("O tom não foi ligado", ex.saida)

    def test_painel_le_o_registro_do_tom(self):
        ex = self.rodar(ProcessadorGerador(), ["400"])
        lista = painel.ajustes(ex.estado)
        self.assertEqual(len(lista), 1)
        self.assertEqual((lista[0]["status"], lista[0]["decisao"]), ("tocado", "APROVADO"))
        self.assertEqual(lista[0]["lote"], ["tom 400 Hz, -20.0 dB, 20 s"])

    def test_guardiao_json_nunca_e_escrito(self):
        for argv, kw in ((["400"], {}), (["--caminho"], dict(liberado=False, entradas=[
                "sim", "0,3", "0", "0", "0"]))):
            with self.subTest(argv=argv):
                ex = self.rodar(ProcessadorGerador(), argv, **kw)
                esperado = json.loads(GUARDIAO.read_text(encoding="utf-8"))
                esperado["gerador"] = {"liberado": kw.get("liberado", True),
                                       "caminho_provado": CAMINHO_PROVADO}
                gravado = (ex.estado / "guardiao.json").read_text(encoding="utf-8")
                self.assertEqual(gravado, json.dumps(esperado, ensure_ascii=False))


# --------------------------------------------------------------------------- teste de caminho


class CaminhoTest(Base):
    def test_quatro_telas_restaura_e_grava(self):
        falso = ProcessadorGerador()
        ligados: list = []

        async def dormir(s):
            ligados.append((falso.ligado(), falso.valores[("100", 0)], falso.valores[("040", 0)],
                            falso.valores[("071", 0)]))
            await asyncio.sleep(0.005)

        original = dict(falso.valores)
        ex = self.rodar(falso, ["--caminho"], liberado=False, arquivo_caminho=None,
                        entradas=["sim", "0,30", "0", "20mV", "0,1"], dormir=dormir)
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)
        # Cada tom tocou com a mudança daquela tela, uma por vez.
        self.assertEqual(ligados, [
            (True, "0", "100", "-11.0"), (True, "1", "100", "-11.0"),
            (True, "0", "5000", "-11.0"), (True, "0", "100", "-30.0")])
        self.assertEqual(falso.escritas, ["*100*0*1*", "*100*0*0*", "*040*0*5000*", "*040*0*100*",
                                          "*071*0*-30.0*", "*071*0*-11.0*"])
        self.assertEqual(falso.historico_202, ["1", "0"] * 4)
        self.assert_desligado_e_relido(falso)
        self.assertEqual({k: falso.valores[k] for k in original}, original)
        a = ex.auditoria()
        self.assertEqual(a["tipo"], "caminho-gerador")
        self.assertEqual(a["medidas"], {"ligado": 0.3, "mute": 0.0, "passa_alta_5k": 0.02,
                                        "limiter_30": 0.1})
        self.assertTrue(a["completo"])
        self.assertTrue(a["restaurado"])
        self.assertEqual(a["diferencas"], [])
        self.assertTrue(a["aprovado"])
        self.assertTrue(all(q["ok"] for q in a["quedas"].values()))
        self.assertIn('"liberado": true', ex.saida)
        self.assertIn("Tela 4 de 4", ex.saida)
        # O teste não libera nada sozinho.
        cfg = gd.carregar_config(ex.estado / "guardiao.json")
        self.assertFalse(cfg.gerador.liberado)

    def test_queda_insuficiente_nao_aprova(self):
        falso = ProcessadorGerador()
        ex = self.rodar(falso, ["--caminho"], liberado=False,
                        entradas=["sim", "0,30", "0,29", "0,02", "0,1"])
        self.assertEqual(ex.codigo, tm.SAIDA_OK, ex.saida)
        a = ex.auditoria()
        self.assertFalse(a["aprovado"])
        self.assertFalse(a["quedas"]["mute"]["ok"])
        self.assertIn("Não aprovado", ex.saida)

    def test_sem_confirmar_o_modulo_desligado_nao_conecta(self):
        falso = ProcessadorGerador()
        ex = self.rodar(falso, ["--caminho"], liberado=False, entradas=["não"])
        self.assertEqual(ex.codigo, tm.SAIDA_USO, ex.saida)
        self.assert_nada_enviado(falso)

    def test_cancelar_no_meio_restaura_e_rele(self):
        falso = ProcessadorGerador()
        original = dict(falso.valores)
        ex = self.rodar(falso, ["--caminho"], liberado=False, entradas=["sim", "0,3", "x"])
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.escritas, ["*100*0*1*", "*100*0*0*"])
        self.assertEqual({k: falso.valores[k] for k in original}, original)
        self.assert_desligado_e_relido(falso)
        a = ex.auditoria()
        self.assertFalse(a["completo"])
        self.assertTrue(a["restaurado"])
        self.assertFalse(a["aprovado"])

    def test_falha_no_meio_restaura(self):
        falso = ProcessadorGerador()
        chamadas = []

        async def dormir(s):
            chamadas.append(s)
            if len(chamadas) == 3:  # 3ª tela: passa-alta em 5 kHz aplicado
                raise RuntimeError("falha no meio")
            await asyncio.sleep(0.005)

        original = dict(falso.valores)
        ex = self.rodar(falso, ["--caminho"], liberado=False, entradas=["sim", "0,3", "0"],
                        dormir=dormir)
        self.assertEqual(ex.codigo, tm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.escritas[-2:], ["*040*0*5000*", "*040*0*100*"])
        self.assertEqual({k: falso.valores[k] for k in original}, original)
        self.assert_desligado_e_relido(falso)
        self.assertTrue(ex.auditoria()["restaurado"])

    def test_caminho_com_violacao_nao_toca(self):
        falso = ProcessadorGerador(iniciais={})
        ex = self.rodar(falso, ["--caminho"], liberado=False, entradas=["sim"])
        self.assertEqual(ex.codigo, tm.SAIDA_RECUSADO, ex.saida)
        self.assertEqual(falso.especiais, [])
        self.assertEqual(falso.escritas, [])
        self.assertFalse(ex.auditoria()["aprovado"])

    def test_aprovado_libera_o_tom_quando_o_denis_edita(self):
        falso = ProcessadorGerador()
        ex = self.rodar(falso, ["--caminho"], liberado=False,
                        entradas=["sim", "0,30", "0", "0,02", "0,1"])
        arquivo = next((ex.estado / "auditorias").glob("*_caminho-gerador*.json"))
        cfg = gd.carregar_config(ex.estado / "guardiao.json")
        self.assertIsNotNone(tm.motivo_sem_caminho(cfg, ex.estado))
        liberado = dataclasses.replace(cfg, gerador=gd.Gerador(True, str(arquivo)))
        self.assertIsNone(tm.motivo_sem_caminho(liberado, ex.estado))


# --------------------------------------------------------------------------- atexit


class AtexitTest(unittest.TestCase):
    def controle(self, fabrica):
        c = tm._Controle(senha=SENHA, tempos=TEMPOS, fabrica=fabrica)
        c.cliente = cb.ClienteTaramps(ProcessadorGerador(), pt.LogBruto(io.StringIO()), **TEMPOS)
        c.cliente.gerador_talvez_ligado = True
        return c

    def test_saida_do_processo_com_o_gerador_ligado_desliga(self):
        falso = ProcessadorGerador(gerador_ligado=True)
        c = self.controle(lambda: falso)
        saidas: list = []
        with mock.patch.object(tm, "_CONTROLE", c), contextlib.redirect_stdout(io.StringIO()) as out:
            tm._ao_sair(saidas.append)
        self.assertEqual(saidas, [])
        self.assertFalse(falso.ligado())
        self.assertIn("Gerador desligado na saída do processo", out.getvalue())

    def test_saida_sem_conseguir_desligar_avisa_e_sai_com_codigo(self):
        falso = ProcessadorGerador(gerador_ligado=True)
        falso.caiu, falso.sumir = True, True
        c = self.controle(lambda: falso)
        saidas: list = []
        with mock.patch.object(tm, "_CONTROLE", c), contextlib.redirect_stdout(io.StringIO()) as out:
            tm._ao_sair(saidas.append)
        self.assertEqual(saidas, [tm.SAIDA_GERADOR_LIGADO])
        self.assertIn(tm.AVISO_DESLIGUE, out.getvalue())

    def test_tratado_ou_desligado_nao_faz_nada(self):
        for tratado, talvez in ((True, True), (False, False)):
            falso = ProcessadorGerador(gerador_ligado=True)
            c = self.controle(lambda: falso)
            c.tratado, c.cliente.gerador_talvez_ligado = tratado, talvez
            saidas: list = []
            with mock.patch.object(tm, "_CONTROLE", c):
                tm._ao_sair(saidas.append)
            self.assertEqual((saidas, falso.enviados), ([], []))


# --------------------------------------------------------------------------- camadas


class ComandoEspecialTest(unittest.TestCase):
    def setUp(self):
        self.cfg = gd.carregar_config(GUARDIAO)
        self.retrato = _variante(CORRIGIDO)
        self.tom = gd.avaliar_tom(self.retrato, self.cfg, 400, -10.0, 20,
                                  teste_de_caminho=True).aprovacao

    def test_textos_exatos_com_aprovacao(self):
        self.assertEqual(self.tom.comandos, ("*200*-10.0*", "*201*400*", "*202*1*", "*202*0*"))
        for texto in self.tom.comandos:
            self.assertEqual(pt.verificar_comando_especial(texto, self.tom), texto.encode())
        mem = gd.avaliar_salvar_memoria(self.retrato, self.cfg, 3, "filme").aprovacao
        self.assertEqual(mem.comandos, ("*240*2*", "*233*FILME*"))
        for texto in mem.comandos:
            pt.verificar_comando_especial(texto, mem)
        pt.verificar_comando_especial("*202*0*", gd.aprovar_desligar_gerador())

    def test_sem_aprovacao_ou_fora_dela_recusa(self):
        ajuste = gd.avaliar_lote(self.retrato, [gd.interpretar("081:0=-12.0")], self.cfg).aprovacao
        forjada = gd.AprovacaoEspecial("tom", ("*202*1*",), "x", "0" * 64)
        alterada = dataclasses.replace(self.tom, comandos=self.tom.comandos + ("*201*40*",))
        for aprovacao in (None, "aprovado", ajuste, forjada, alterada):
            with self.subTest(aprovacao=aprovacao):
                with self.assertRaises(gd.EscritaNaoAprovada):
                    pt.verificar_comando_especial("*202*1*" if aprovacao is not alterada
                                                  else "*201*40*", aprovacao)
        with self.assertRaises(gd.EscritaNaoAprovada):  # válido, mas fora desta aprovação
            pt.verificar_comando_especial("*201*1000*", self.tom)
        with self.assertRaises(gd.EscritaNaoAprovada):  # desligar não liga
            pt.verificar_comando_especial("*202*1*", gd.aprovar_desligar_gerador())
        with self.assertRaises(gd.EscritaNaoAprovada):  # a aprovação especial não vale no lote
            gd.item_aprovado(self.tom, 0)

    def test_formato_exato(self):
        for texto in ("*200*-10*", "*200*-10.00*", "*200*+0.0*", "*200*-0.0*", "*200*1.0*",
                      "*200*-61.0*", "*201*400.0*", "*201*9*", "*201*20001*", "*202*2*",
                      "*202*01*", "*240*3*", "*240*-1*", "*231*musica*", "*231* FILME*",
                      "*231*ABCDEFGHIJKLM*", "*231**", "*231*MÚSICA*", "*234*X*", "*241*0*",
                      "*242*", "*210*100*", "*213*1*", "*252*1*", "*004*", "*000*AB12*",
                      "*001*AB12*CD34*", "231*FILME", "#202#", "*081*0*-12.0*", "*202*1*\r\n"):
            with self.subTest(texto=texto):
                with self.assertRaises((pt.EscritaInvalida, gd.EscritaNaoAprovada)):
                    pt.verificar_comando_especial(texto, self.tom)

    def test_outros_caminhos_continuam_recusando(self):
        for texto in ("*202*1*", "*202*0*", "*200*-10.0*", "*201*400*", "*240*1*", "*232*X*"):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(texto)
                with self.assertRaises(pt.EscritaInvalida):
                    pt.verificar_escrita(texto)
        # A leitura do gerador e dos nomes tem verificador próprio; a trava de leitura não muda.
        for texto in ("#200#", "#201#", "#202#", "231#", "232#", "233#"):
            with self.subTest(texto=texto):
                self.assertEqual(pt.verificar_consulta_especial(texto), texto.encode())
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(texto)
        for texto in ("234#", "#241#", "#210#", "230#", "231#1", "#202#0#", "*202*0*", "#081#0#",
                      "*000*AB12*", None):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_consulta_especial(texto)

    def test_ajustar_processador_continua_recusando_pela_regra5(self):
        for ajuste in ("202=1", "202=0", "200=-10.0", "201=400", "240=1", "240:1=1", "232=X",
                       "210=100", "241=1"):
            with self.subTest(ajuste=ajuste):
                falso = tap.Processador()
                ex = tap.Execucao(falso, [ajuste])
                self.addCleanup(ex.fechar)
                self.assertEqual(ex.codigo, ap.SAIDA_RECUSADO, ex.saida)
                self.assertIn("regra 5", ex.saida)
                self.assertEqual(falso.enviados, [])

    def test_avaliar_tom_limites_e_liberacao(self):
        liberado = dataclasses.replace(self.cfg, gerador=gd.Gerador(True, "x"))
        self.assertTrue(gd.avaliar_tom(self.retrato, liberado, 10, -60, 30).aprovado)
        self.assertTrue(gd.avaliar_tom(self.retrato, liberado, 20000, 0, 0.5).aprovado)
        self.assertFalse(gd.avaliar_tom(self.retrato, self.cfg, 400, -10, 20).aprovado)
        for hz, db, s in ((9, -10, 20), (20001, -10, 20), (400, 0.1, 20), (400, -10, 31),
                          (400, -10, 0), (400, -10, float("nan")), (400.5, -10, 20)):
            with self.subTest(hz=hz, db=db, s=s):
                d = gd.avaliar_tom(self.retrato, liberado, hz, db, s)
                self.assertFalse(d.aprovado)
                self.assertEqual({r.regra for r in d.recusas}, {5})


class GeradorConfigTest(unittest.TestCase):
    def carregar(self, gerador) -> gd.Config:
        dados = json.loads(GUARDIAO.read_text(encoding="utf-8"))
        if gerador is ...:
            dados.pop("gerador", None)
        else:
            dados["gerador"] = gerador
        with tempfile.TemporaryDirectory() as tmp:
            caminho = Path(tmp) / "guardiao.json"
            caminho.write_text(json.dumps(dados), encoding="utf-8")
            return gd.carregar_config(caminho)

    def test_arquivo_real_migrado_nao_libera(self):
        dados = json.loads(GUARDIAO.read_text(encoding="utf-8"))
        self.assertEqual({k: v for k, v in dados["gerador"].items() if not k.startswith("_")},
                         {"liberado": False, "caminho_provado": None})
        self.assertEqual(gd.carregar_config(GUARDIAO).gerador, gd.Gerador(False, None))

    def test_bloco(self):
        self.assertEqual(self.carregar(...).gerador, gd.Gerador())  # sem o bloco: não liberado
        self.assertEqual(self.carregar(None).gerador, gd.Gerador())
        self.assertEqual(self.carregar({"liberado": True, "caminho_provado": " a.json "}).gerador,
                         gd.Gerador(True, "a.json"))
        for ruim in ({"liberado": 1}, {"liberado": "true"}, {"caminho_provado": ""},
                     {"caminho_provado": 3}, [], "sim"):
            with self.subTest(ruim=ruim):
                with self.assertRaises(gd.ConfigInvalida):
                    self.carregar(ruim)


def _variante(mudancas: dict) -> dict:
    import copy
    r = copy.deepcopy(RETRATO)
    for x in r["leituras"]:
        chave = (x["codigo"], x["canal"])
        if chave in mudancas:
            conv = pt.converter(pt.POR_CODIGO[x["codigo"]], mudancas[chave])
            x["valor"] = conv["valor"]
    return r


class ClienteEspecialTest(unittest.TestCase):
    def setUp(self):
        cfg = gd.carregar_config(GUARDIAO)
        self.aprovacao = gd.avaliar_tom(_variante(CORRIGIDO), cfg, 400, -10.0, 20,
                                        teste_de_caminho=True).aprovacao

    def cliente(self, falso):
        falso.conectado = True
        return cb.ClienteTaramps(falso, pt.LogBruto(io.StringIO()), **TEMPOS)

    def test_sem_aprovacao_recusa_antes_do_ble(self):
        falso = ProcessadorGerador()
        for aprovacao in (None, gd.AprovacaoEspecial("tom", ("*202*1*",), "", "0" * 64)):
            with self.assertRaises(gd.EscritaNaoAprovada):
                asyncio.run(self.cliente(falso).enviar_especial(aprovacao, "*202*1*"))
        self.assertEqual(falso.enviados, [])

    def test_ligar_uma_vez_desligar_quantas_precisar(self):
        async def rodar(falso):
            c = self.cliente(falso)
            await c.enviar_especial(self.aprovacao, "*202*1*")
            self.assertTrue(c.gerador_talvez_ligado)
            with self.assertRaises(gd.EscritaNaoAprovada):
                await c.enviar_especial(self.aprovacao, "*202*1*")
            self.assertTrue(await c.desligar_gerador(self.aprovacao))
            self.assertTrue(await c.desligar_gerador(gd.aprovar_desligar_gerador()))
            return c

        falso = ProcessadorGerador()
        c = asyncio.run(rodar(falso))
        self.assertFalse(c.gerador_talvez_ligado)
        self.assertEqual(falso.especiais, ["*202*1*", "*202*0*", "*202*0*"])


if __name__ == "__main__":
    unittest.main()
