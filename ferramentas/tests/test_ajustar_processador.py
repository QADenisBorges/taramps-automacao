"""Testes da escrita (cliente e CLI) com um processador falso que guarda estado, partindo
do retrato real (estado/leituras/2026-10-16_1747.json) e do estado/guardiao.json real."""

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

import ajustar_processador as ap  # noqa: E402
import cliente_ble as cb  # noqa: E402
import guardiao as gd  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
RETRATO = json.loads((RAIZ / "estado" / "leituras" / "2026-10-16_1747.json").read_text(encoding="utf-8"))
GUARDIAO = RAIZ / "estado" / "guardiao.json"
SENHA = "QZ7K"
TEMPOS = {"intervalo": 0.0, "timeout": 0.2, "ocioso": 0.03, "janela_ack": 0.02}


class Dispositivo:
    name = "2.4BT AUDIO"
    address = "00:80:E1:22:B6:30"


def eh_escrita(texto: str) -> bool:
    return (texto.startswith("*") and not texto.startswith("*000*")) or texto.startswith("020*")


class Processador:
    """Processador falso: responde às consultas a partir do estado e aplica as escritas."""

    def __init__(self, *, divergir: dict | None = None, cair_na_escrita: int | None = None,
                 mudos: set | None = None, mudo_depois_de_escrever: set | None = None,
                 eco_escrita: bool = False, travar_escrita: set | None = None,
                 eco_tardio: bool = False, iniciais: dict | None = None,
                 resposta_000: str = "#000#PRO2.4MIR003#", interromper_na_escrita: int | None = None):
        self.ao_receber = lambda dados: None
        self.ao_cair = lambda: None
        self.valores: dict = {}
        for x in RETRATO["leituras"]:
            self.valores[(x["codigo"], x["canal"])] = pt.analisar(x["cru"]).segmentos[-1]
        self.valores.update(iniciais or {})
        self.eco_tardio = eco_tardio  # repete a escrita (formato *CCC*c*v*) antes da releitura
        self._eco_pendente: str | None = None
        self.resposta_000 = resposta_000
        self.interromper_na_escrita = interromper_na_escrita
        self.divergir = divergir or {}
        self.cair_na_escrita = cair_na_escrita
        self.mudos = mudos or set()
        self.mudo_depois = mudo_depois_de_escrever or set()
        self.eco_escrita = eco_escrita
        self.travar_escrita = travar_escrita or set()
        self.enviados: list[str] = []
        self.escritas: list[str] = []
        self.conectado = False
        self.ao_escrever_primeira = None  # chamado antes de aplicar a 1ª escrita
        self.propriedades = {"1122": ["write"], "1123": ["notify"]}

    async def procurar(self):
        return Dispositivo()

    async def conectar(self, dispositivo):
        self.conectado = True

    async def desconectar(self):
        self.conectado = False

    def _mandar(self, texto: str, atraso: float = 0.002) -> None:
        asyncio.get_running_loop().call_later(atraso, self.ao_receber, texto.encode())

    def _aplicar(self, texto: str) -> None:
        if texto.startswith("020*"):
            codigo, canal, valor = "020", *texto[4:].split("*")
        else:
            segs = texto.strip("*").split("*")
            codigo, canal, valor = (segs[0], None, segs[1]) if len(segs) == 2 else segs
        canal = int(canal) if canal is not None else None
        valor = self.divergir.get(texto, valor)
        p = pt.POR_CODIGO[codigo]
        if p.casas == 1:
            valor = f"{float(valor):+.1f}"  # o aparelho responde com sinal: +0.0, -11.0
        self.valores[(codigo, canal)] = valor

    async def escrever(self, dados: bytes, preferir_resposta: bool):
        texto = dados.decode("ascii")
        self.enviados.append(texto)
        if eh_escrita(texto):
            self.escritas.append(texto)
            if len(self.escritas) == 1 and self.ao_escrever_primeira:
                self.ao_escrever_primeira()
            if len(self.escritas) == self.interromper_na_escrita:
                raise KeyboardInterrupt  # Ctrl+C com a escrita a caminho
            if texto in self.travar_escrita:
                await asyncio.sleep(30)
            if self.cair_na_escrita is not None and len(self.escritas) >= self.cair_na_escrita:
                asyncio.get_running_loop().call_later(0.005, self.ao_cair)
                return
            self._aplicar(texto)
            if self.eco_tardio:
                self._eco_pendente = texto
            if self.eco_escrita:
                self._mandar("OK\r\n")
            return
        if texto.startswith("*000*"):
            self._mandar(f"{self.resposta_000}\r\n" if texto == f"*000*{SENHA}*" else "#000#ERRO#\r\n")
            return
        if texto in self.mudos or (self.escritas and texto in self.mudo_depois):
            return
        atraso = 0.002
        if self._eco_pendente:  # eco atrasado: chega depois da janela, antes da resposta
            self._mandar(self._eco_pendente + "\r\n")
            self._eco_pendente, atraso = None, 0.006
        if texto.startswith("020#"):
            b = int(texto[4:])
            self._mandar(f"020#{b}#{self.valores[('020', b)]}\r\n", atraso)
            return
        q = pt.analisar(texto)
        canal = int(q.segmentos[0]) if q.segmentos else None
        segs = [q.codigo] + ([str(canal)] if canal is not None else []) + [self.valores[(q.codigo, canal)]]
        self._mandar("#" + "#".join(segs) + "#\r\n", atraso)


class Execucao:
    def __init__(self, transporte, argv=(), senha=SENHA, antes=None):
        self.tmp = tempfile.TemporaryDirectory()
        self.estado = Path(self.tmp.name) / "estado"
        self.estado.mkdir()
        shutil.copy(GUARDIAO, self.estado / "guardiao.json")
        if antes:
            antes(self)
        saida = io.StringIO()
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": senha}), contextlib.redirect_stdout(saida):
            self.codigo = ap.main(list(argv), transporte=transporte, estado=self.estado, tempos=TEMPOS)
        self.saida = saida.getvalue()

    def resultado(self) -> dict:
        arquivos = list((self.estado / "ajustes").glob("*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def backups(self) -> list[Path]:
        return list((self.estado / "backups").glob("*.json"))

    def todos_os_textos(self) -> str:
        textos = [self.saida]
        textos += [p.read_text(encoding="utf-8") for p in self.estado.rglob("*") if p.is_file()]
        return "\n".join(textos)

    def fechar(self):
        self.tmp.cleanup()


class AjusteTest(unittest.TestCase):
    def rodar(self, transporte, argv, **kw) -> Execucao:
        ex = Execucao(transporte, argv, **kw)
        self.addCleanup(ex.fechar)
        return ex

    def assert_nada_escrito(self, falso: Processador):
        self.assertEqual(falso.escritas, [])
        for texto in falso.enviados:  # tudo que saiu passa pela trava de leitura
            pt.verificar_envio(texto)

    # -- matriz da spec

    def test_ajuste_seguro_backup_antes_e_releitura_igual(self):
        falso = Processador()
        backups_na_hora: list = []
        ex_ref: list = []
        falso.ao_escrever_primeira = lambda: backups_na_hora.extend(ex_ref[0].backups())
        ex = self.rodar(falso, ["081:0=-12.0"], antes=lambda e: ex_ref.append(e))
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(len(backups_na_hora), 1)  # backup gravado antes da 1ª escrita
        backup = json.loads(backups_na_hora[0].read_text(encoding="utf-8"))
        self.assertEqual((backup["status"], backup["lidos"]), ("completo", 84))
        self.assertEqual(falso.escritas, ["*081*0*-12.0*"])
        i = falso.enviados.index("*081*0*-12.0*")
        self.assertEqual(falso.enviados[i + 1:], ["#081#0#"])  # releitura logo depois
        r = ex.resultado()
        self.assertEqual(r["status"], "aplicado")
        self.assertEqual(r["aplicados"], ["081:0=-12.0"])
        self.assertEqual(r["escritas"][0]["status"], "igual")
        self.assertEqual(r["lote_reverso"], ["081:0=-11.0"])
        self.assertEqual(r["nao_aplicados"], [])
        self.assertIn("Para desfazer: uv run ferramentas/ajustar_processador.py 081:0=-11.0", ex.saida)
        import hashlib
        self.assertEqual(r["guardiao"]["sha256"], hashlib.sha256(GUARDIAO.read_bytes()).hexdigest())
        # Regra 6 por saída: o registro guarda o valor de cada uma.
        self.assertEqual(r["guardiao"]["regra6"], {"1": True, "2": True, "3": True, "4": True})
        self.assertEqual(r["estado_relido"][0]["valor"], -12.0)
        self.assertTrue(r["decisao"]["aprovado"])
        self.assertIn("1/1 081:0=-12.0 … igual", ex.saida)
        self.assertFalse(falso.conectado)

    def test_recusa_nao_manda_nenhum_byte_de_escrita(self):
        for ajuste, trava in (("042:0=0", "trava 1"), ("071:2=0.5", "trava 3"),
                              ("081:0=-10.0", "regra 6"), ("090:0=50", "trava 1")):
            with self.subTest(ajuste=ajuste):
                falso = Processador()
                ex = self.rodar(falso, [ajuste])
                self.assertEqual(ex.codigo, ap.SAIDA_RECUSADO, ex.saida)
                self.assert_nada_escrito(falso)
                self.assertIn("RECUSADO", ex.saida)
                self.assertIn(trava, ex.saida)
                self.assertIn("Seguro:", ex.saida)
                self.assertEqual(ex.resultado()["status"], "recusado")
                self.assertEqual(len(ex.backups()), 1)

    def test_reforco_abaixo_do_corte_no_lote(self):
        falso = Processador()
        ex = self.rodar(falso, ["090:0=50", "091:0=+6"])
        self.assertEqual(ex.codigo, ap.SAIDA_RECUSADO)
        self.assert_nada_escrito(falso)

    def test_ponte_escreve_as_duas_saidas(self):
        falso = Processador()
        ex = self.rodar(falso, ["081:2=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(falso.escritas, ["*081*2*-12.0*", "*081*3*-12.0*"])
        self.assertIn("Espelhado na ponte: 081:3=-12.0", ex.saida)
        self.assertEqual(ex.resultado()["aplicados"], ["081:2=-12.0", "081:3=-12.0"])

    def test_releitura_divergente_na_ponte_para_o_lote(self):
        falso = Processador(divergir={"*081*3*-12.0*": "-11.0"})
        ex = self.rodar(falso, ["081:2=-12.0", "081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.escritas, ["*081*2*-12.0*", "*081*3*-12.0*"])  # 081:0 nunca saiu
        r = ex.resultado()
        self.assertEqual(r["status"], "interrompido")
        self.assertEqual(r["aplicados"], ["081:2=-12.0"])
        self.assertEqual(r["nao_aplicados"], ["081:3=-12.0", "081:0=-12.0"])
        self.assertEqual(r["escritas"][1]["status"], "divergente")
        relido = {(x["codigo"], x["canal"]): x["valor"] for x in r["estado_relido"]}
        self.assertEqual(relido, {("081", 2): -12.0, ("081", 3): -11.0, ("081", 0): -11.0})
        self.assertIn("PAREI", ex.saida)
        self.assertIn("ATENÇÃO: a ponte pode ter ficado desigual", ex.saida)
        self.assertIn("ajustar_processador.py 081:2=-10.0", ex.saida)
        self.assertEqual(r["lote_reverso"], ["081:2=-10.0"])
        self.assertIn("Para desfazer o que foi tentado: uv run ferramentas/ajustar_processador.py "
                      "081:2=-10.0", ex.saida)
        self.assertIsNone(r["em_andamento"])

    def test_eco_atrasado_da_escrita_nao_confirma_a_releitura(self):
        # O aparelho não aplica (fica -11.0) e repete *081*0*-12.0* depois da janela de ack.
        falso = Processador(divergir={"*081*0*-12.0*": "-11.0"}, eco_tardio=True)
        ex = self.rodar(falso, ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_INTERROMPIDO, ex.saida)
        r = ex.resultado()
        self.assertEqual((r["escritas"][0]["status"], r["escritas"][0]["relido"]), ("divergente", -11.0))
        log = next((ex.estado / "log-ble").glob("*-ajuste.log")).read_text(encoding="utf-8")
        self.assertRegex(log, r"ORFAO .*\|\*081\*0\*-12\.0\*\|")

    def test_global_e_banda_bytes_exatos(self):
        corrigido = {("042", 0): "11", ("040", 0): "100", ("042", 1): "11", ("040", 1): "100",
                     ("040", 2): "40", ("040", 3): "40", ("072", 3): "50.0"}
        falso = Processador(iniciais=corrigido)
        ex = self.rodar(falso, ["080=-1.0", "020:3=-2.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(falso.escritas, ["*080*-1.0*", "020*3*-2.0"])
        for escrita, releitura in (("*080*-1.0*", "#080#"), ("020*3*-2.0", "020#3")):
            i = falso.enviados.index(escrita)
            self.assertEqual(falso.enviados[i + 1], releitura)
        r = ex.resultado()
        self.assertEqual([x["status"] for x in r["escritas"]], ["igual", "igual"])
        self.assertEqual([x["relido"] for x in r["escritas"]], [-1.0, -2.0])
        self.assertEqual(r["lote_reverso"], ["020:3=0.0", "080=0.0"])

    def test_registro_em_andamento_enquanto_roda(self):
        """O painel lê o registro durante a execução: no scan ele já existe como "em andamento"
        e, na hora da escrita, traz o item em curso."""
        caixa: dict = {}
        vistos: list = []

        def registro():
            arquivos = list((caixa["estado"] / "ajustes").glob("*.json"))
            self.assertFalse(list((caixa["estado"] / "ajustes").glob("*.tmp")))  # troca atômica
            return json.loads(arquivos[0].read_text(encoding="utf-8")) if arquivos else None

        class Espiao(Processador):
            async def procurar(self):
                vistos.append(("scan", registro()))
                return await super().procurar()

            async def escrever(self, dados, preferir_resposta):
                if eh_escrita(dados.decode("ascii")):
                    vistos.append(("escrita", registro()))
                return await super().escrever(dados, preferir_resposta)

        ex = self.rodar(Espiao(), ["081:0=-12.0"], antes=lambda e: caixa.__setitem__("estado", e.estado))
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual([v[0] for v in vistos], ["scan", "escrita"])
        no_scan, na_escrita = vistos[0][1], vistos[1][1]
        self.assertEqual((no_scan["status"], no_scan["em_andamento"], no_scan["lote"]),
                         ("em andamento", None, ["081:0=-12.0"]))
        self.assertEqual((na_escrita["status"], na_escrita["em_andamento"]), ("em andamento", "081:0=-12.0"))
        final = ex.resultado()
        self.assertEqual((final["status"], final["em_andamento"]), ("aplicado", None))

    def test_ctrl_c_no_scan_nao_deixa_o_registro_em_andamento(self):
        class Interrompido(Processador):
            async def procurar(self):
                raise KeyboardInterrupt

        tmp = []
        with self.assertRaises(KeyboardInterrupt):
            Execucao(Interrompido(), ["081:0=-12.0"], antes=tmp.append)
        self.addCleanup(tmp[0].fechar)
        r = json.loads(next((tmp[0].estado / "ajustes").glob("*.json")).read_text(encoding="utf-8"))
        self.assertEqual(r["status"], "interrompido")
        self.assertIsNone(r["em_andamento"])

    def test_sem_aparelho_fecha_o_registro(self):
        class Sumido(Processador):
            async def procurar(self):
                return None

        ex = self.rodar(Sumido(), ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_SEM_APARELHO, ex.saida)
        self.assertEqual(ex.resultado()["status"], "sem conexão")

    def test_ctrl_c_no_meio_do_lote_fica_no_registro(self):
        falso = Processador(interromper_na_escrita=2)
        tmp = []
        with self.assertRaises(KeyboardInterrupt):
            Execucao(falso, ["081:0=-12.0", "081:1=-12.0"], antes=tmp.append)
        self.addCleanup(tmp[0].fechar)
        r = json.loads(next((tmp[0].estado / "ajustes").glob("*.json")).read_text(encoding="utf-8"))
        self.assertEqual(r["status"], "interrompido")
        self.assertEqual(r["aplicados"], ["081:0=-12.0"])
        self.assertEqual(r["em_andamento"], "081:1=-12.0")
        self.assertEqual(r["nao_aplicados"], ["081:1=-12.0"])
        self.assertEqual(r["lote_reverso"], ["081:1=-11.0", "081:0=-11.0"])

    def test_queda_na_escrita(self):
        falso = Processador(cair_na_escrita=1)
        ex = self.rodar(falso, ["081:0=-12.0", "081:1=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(len(falso.escritas), 1)
        r = ex.resultado()
        self.assertEqual(r["aplicados"], [])
        self.assertEqual(r["nao_aplicados"], ["081:0=-12.0", "081:1=-12.0"])
        self.assertEqual(r["escritas"][0]["status"], "queda")
        self.assertIn("Sem conexão para reler", ex.saida)

    def test_releitura_sem_resposta_para(self):
        falso = Processador(mudo_depois_de_escrever={"#081#0#"})
        ex = self.rodar(falso, ["081:0=-12.0", "081:1=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(falso.escritas, ["*081*0*-12.0*"])
        self.assertEqual(ex.resultado()["escritas"][0]["status"], "sem resposta")

    def test_escrita_presa_vira_timeout(self):
        falso = Processador(travar_escrita={"*081*0*-12.0*"})
        ex = self.rodar(falso, ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_INTERROMPIDO, ex.saida)
        self.assertEqual(ex.resultado()["escritas"][0]["status"], "timeout")

    def test_ordem_segura(self):
        falso = Processador()
        ex = self.rodar(falso, ["040:0=100", "042:0=11"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(falso.escritas, ["*042*0*11*", "*040*0*100*"])

    def test_codigo_proibido_nem_conecta(self):
        falso = Processador()
        ex = self.rodar(falso, ["241:1"])
        self.assertEqual(ex.codigo, ap.SAIDA_RECUSADO)
        self.assertEqual(falso.enviados, [])
        self.assertFalse(falso.conectado)
        self.assertIn("regra 5", ex.saida)
        self.assertEqual(ex.resultado()["status"], "recusado")
        self.assertEqual(ex.backups(), [])

    # -- fluxo

    def test_simular_nao_escreve(self):
        falso = Processador()
        ex = self.rodar(falso, ["--simular", "081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assert_nada_escrito(falso)
        self.assertIn("APROVADO", ex.saida)
        self.assertIn("Simulação: nada foi escrito", ex.saida)
        self.assertEqual(ex.resultado()["status"], "simulado")

    def test_leitura_incompleta_nao_escreve(self):
        falso = Processador(mudos={"#050#2#"})
        ex = self.rodar(falso, ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_LEITURA, ex.saida)
        self.assert_nada_escrito(falso)
        self.assertIn("Leitura incompleta", ex.saida)
        self.assertEqual(ex.resultado()["status"], "leitura incompleta")

    def test_senha_nao_aparece_em_estado_nem_no_terminal(self):
        ex = self.rodar(Processador(), ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertNotIn(SENHA.lower(), ex.todos_os_textos().lower())

    def test_resposta_000_com_a_senha_nao_vaza(self):
        falso = Processador(resposta_000=f"#000#PRO2.4MI{SENHA}#")
        ex = self.rodar(falso, ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(len(ex.backups()), 1)
        backup = json.loads(ex.backups()[0].read_text(encoding="utf-8"))
        self.assertIsNone(backup["firmware"])
        self.assertNotIn(SENHA.lower(), ex.todos_os_textos().lower())  # estado/ + terminal

    def test_pedido_de_senha_nao_mostra_nem_grava_a_senha(self):
        falso = Processador()
        ex = self.rodar(falso, ["000=AB12", "001=CD34"])
        self.assertEqual(ex.codigo, ap.SAIDA_RECUSADO)
        self.assertEqual(falso.enviados, [])
        for segredo in ("AB12", "CD34"):
            self.assertNotIn(segredo, ex.todos_os_textos())
        self.assertEqual(ex.resultado()["lote"], ["000=****", "001=****"])

    def test_senha_recusada(self):
        falso = Processador()
        ex = self.rodar(falso, ["081:0=-12.0"], senha="AAAA")
        self.assertEqual(ex.codigo, ap.SAIDA_SENHA)
        self.assertEqual(falso.escritas, [])
        self.assertEqual(ex.resultado()["status"], "senha recusada")

    def test_arquivo_de_lote(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        lote = Path(tmp.name) / "lote.json"
        lote.write_text(json.dumps(
            {"ajustes": ["081:0=-12.0", {"codigo": "042", "canal": 1, "valor": "BT24"}]}),
            encoding="utf-8")
        falso = Processador()
        ex = self.rodar(falso, ["--arquivo", str(lote)])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(falso.escritas, ["*081*0*-12.0*", "*042*1*11*"])
        falso = Processador()
        ex = self.rodar(falso, ["--arquivo", str(Path(tmp.name) / "nao-existe.json")])
        self.assertEqual(ex.codigo, ap.SAIDA_USO)
        self.assertEqual(falso.enviados, [])

    def test_lote_ilegivel(self):
        falso = Processador()
        ex = self.rodar(falso, ["abc"])
        self.assertEqual(ex.codigo, ap.SAIDA_USO)
        self.assertEqual(falso.enviados, [])

    def test_sem_mudanca(self):
        falso = Processador()
        ex = self.rodar(falso, ["081:0=-11.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        self.assertEqual(falso.escritas, [])
        self.assertIn("Nada a mudar", ex.saida)

    def test_ack_depois_da_escrita_vai_para_o_log_como_orfao(self):
        falso = Processador(eco_escrita=True)
        ex = self.rodar(falso, ["081:0=-12.0"])
        self.assertEqual(ex.codigo, ap.SAIDA_OK, ex.saida)
        log = next((ex.estado / "log-ble").glob("*-ajuste.log")).read_text(encoding="utf-8")
        self.assertRegex(log, r"ORFAO .*\|OK\|  depois da escrita")
        self.assertIn("|*081*0*-12.0*|  escrita", log)

    def test_guardiao_json_nunca_e_escrito(self):
        antes: list = []
        ex = self.rodar(Processador(), ["081:2=-12.0"],
                        antes=lambda e: antes.append((e.estado / "guardiao.json").read_bytes()))
        self.assertEqual((ex.estado / "guardiao.json").read_bytes(), antes[0])

    def test_guardiao_json_invalido_para_tudo(self):
        falso = Processador()
        ex = self.rodar(falso, ["081:0=-12.0"],
                        antes=lambda e: (e.estado / "guardiao.json").write_text("{}", encoding="utf-8"))
        self.assertEqual(ex.codigo, ap.SAIDA_USO)
        self.assertEqual(falso.enviados, [])


class ClienteEscritaTest(unittest.TestCase):
    """O único caminho de escrita exige a aprovação do guardião."""

    def setUp(self):
        cfg = gd.carregar_config(GUARDIAO)
        lote = [gd.interpretar("081:0=-12.0"), gd.interpretar("081:1=-12.0")]
        self.aprovacao = gd.avaliar_lote(RETRATO, lote, cfg).aprovacao

    def cliente(self, falso):
        return cb.ClienteTaramps(falso, pt.LogBruto(io.StringIO()), **TEMPOS)

    def test_sem_aprovacao_recusa_antes_do_ble(self):
        forjada = gd.Aprovacao((gd.Escrita("071", 0, 0.5),), "x", "0" * 64)
        alterada = dataclasses.replace(self.aprovacao, escritas=(gd.Escrita("071", 0, 0.5),
                                                                 gd.Escrita("081", 1, -12.0)))
        for aprovacao in (None, "aprovado", forjada, alterada):
            with self.subTest(aprovacao=aprovacao):
                falso = Processador()
                with self.assertRaises(gd.EscritaNaoAprovada):
                    asyncio.run(self.cliente(falso).escrever(aprovacao, 0))
                self.assertEqual(falso.enviados, [])

    def test_fora_da_ordem_aprovada(self):
        falso = Processador()
        with self.assertRaises(gd.EscritaNaoAprovada):
            asyncio.run(self.cliente(falso).escrever(self.aprovacao, 1))
        self.assertEqual(falso.enviados, [])

    def test_escreve_na_ordem_e_nao_repete(self):
        async def rodar(falso):
            cliente = self.cliente(falso)
            r0 = await cliente.escrever(self.aprovacao, 0)
            r1 = await cliente.escrever(self.aprovacao, 1)
            with self.assertRaises(gd.EscritaNaoAprovada):
                await cliente.escrever(self.aprovacao, 1)  # não escreve de novo
            return r0, r1

        falso = Processador()
        r0, r1 = asyncio.run(rodar(falso))
        self.assertEqual((r0["status"], r1["status"]), ("igual", "igual"))
        self.assertEqual(falso.escritas, ["*081*0*-12.0*", "*081*1*-12.0*"])

    def test_divergencia_mata_a_aprovacao(self):
        async def rodar(falso):
            cliente = self.cliente(falso)
            r0 = await cliente.escrever(self.aprovacao, 0)
            with self.assertRaises(gd.EscritaNaoAprovada):
                await cliente.escrever(self.aprovacao, 1)
            return r0

        falso = Processador(divergir={"*081*0*-12.0*": "-11.5"})
        r0 = asyncio.run(rodar(falso))
        self.assertEqual((r0["status"], r0["relido"]), ("divergente", -11.5))
        self.assertEqual(falso.escritas, ["*081*0*-12.0*"])

    def test_enviar_continua_so_leitura(self):
        falso = Processador()
        with self.assertRaises(pt.EnvioRecusado):
            asyncio.run(self.cliente(falso).enviar("*081*0*-12.0*"))
        self.assertEqual(falso.enviados, [])


if __name__ == "__main__":
    unittest.main()
