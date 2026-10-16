"""Testes do cliente BLE e do CLI com um processador falso (sem bleak e sem aparelho)."""

from __future__ import annotations

import asyncio
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import cliente_ble as cb  # noqa: E402
import ler_processador as lp  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

SENHA = "QZ7K"
TEMPOS = {"intervalo": 0.0, "timeout": 0.2, "ocioso": 0.03}

VALORES = {
    "080": "-3.0", "081": ["-16.4", "-16.5", "-12.0", "-12.0"], "100": "0",
    "010": ["a", "b", "a+b", "ab"], "060": ["0", "0", "180", "180"], "040": "80", "041": "22000",
    "042": "3", "043": "0", "050": "26.0", "070": "1", "071": ["-6.0", "-6.0", "0.5", "0.5"],
    "072": "1.0", "073": "500", "090": "1000", "091": "0.0", "092": "0.4", "030": "0.0",
    "031": "1000", "032": "1.0", "220": "255",
}


def valor_de(codigo: str, canal: int | None) -> str:
    if codigo == "020":
        return f"{canal - 7:.1f}"
    v = VALORES[codigo]
    return v[canal] if isinstance(v, list) else v


class Dispositivo:
    name = "PRO 2.4BT"
    address = "AA:BB:CC:DD:EE:FF"


class Falso:
    """Processador falso: responde às consultas a partir de VALORES."""

    def __init__(self, *, encontrado=True, senha=SENHA, sep="#", fragmento=0,
                 mudos=(), cair_no_envio=None, orfao=None, erro_em=(), travar_em=(),
                 resposta_000="#000#PRO 2.4BT AUDIO V1.07#"):
        self.ao_receber = lambda dados: None
        self.ao_cair = lambda: None
        self.encontrado = encontrado
        self.senha = senha
        self.sep = sep
        self.fragmento = fragmento
        self.mudos = set(mudos)
        self.cair_no_envio = cair_no_envio
        self.orfao = orfao
        self.erro_em = set(erro_em)
        self.travar_em = set(travar_em)
        self.resposta_000 = resposta_000  # None = não responde à senha
        self.preferencias: list[tuple[str, bool]] = []
        self.enviados: list[str] = []
        self.conectado = False
        self.propriedades = {"1122": ["write", "write-without-response"], "1123": ["notify"]}

    async def procurar(self):
        return Dispositivo() if self.encontrado else None

    async def conectar(self, dispositivo):
        self.conectado = True

    async def desconectar(self):
        self.conectado = False

    def _resposta(self, texto: str) -> str | None:
        if texto.startswith("*000*"):
            ok = texto == f"*000*{self.senha}*"
            return "#000#ERRO#" if not ok else self.resposta_000
        if texto in self.mudos:
            return None
        q = pt.analisar(texto)
        canal = int(q.segmentos[0]) if q.segmentos else None
        valor = valor_de(q.codigo, canal)
        segs = [q.codigo] + ([str(canal)] if canal is not None else []) + [valor]
        s = self.sep
        return s + s.join(segs) + s

    async def escrever(self, dados: bytes, preferir_resposta: bool):
        texto = dados.decode("ascii")
        self.enviados.append(texto)
        self.preferencias.append((texto, preferir_resposta))
        if texto in self.erro_em:
            raise OSError("falha simulada na escrita")
        if texto in self.travar_em:
            await asyncio.sleep(30)  # write_gatt_char preso
        loop = asyncio.get_running_loop()
        if self.cair_no_envio is not None and len(self.enviados) >= self.cair_no_envio:
            loop.call_later(0.005, self.ao_cair)
            return
        resposta = self._resposta(texto)
        if resposta is None:
            return
        partes = [resposta.encode()]
        if self.fragmento:
            b = resposta.encode()
            partes = [b[i:i + self.fragmento] for i in range(0, len(b), self.fragmento)]
        if self.orfao and len(self.enviados) == 5:
            partes.insert(0, self.orfao)
        for k, parte in enumerate(partes):
            loop.call_later(0.002 * (k + 1), self.ao_receber, parte)


class Execucao:
    def __init__(self, transporte, argv=(), entrada=input, senha=SENHA):
        self.tmp = tempfile.TemporaryDirectory()
        self.estado = Path(self.tmp.name) / "estado"
        saida = io.StringIO()
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": senha}), contextlib.redirect_stdout(saida):
            self.codigo = lp.main(list(argv), transporte=transporte, estado=self.estado,
                                  tempos=TEMPOS, entrada=entrada)
        self.saida = saida.getvalue()

    def retrato(self) -> dict:
        arquivos = list((self.estado / "leituras").glob("*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def todos_os_textos(self) -> str:
        textos = [self.saida]
        if self.estado.exists():
            textos += [p.read_text(encoding="utf-8") for p in self.estado.rglob("*") if p.is_file()]
        return "\n".join(textos)

    def fechar(self):
        self.tmp.cleanup()


class LeituraTest(unittest.TestCase):
    def rodar(self, transporte, **kw) -> Execucao:
        ex = Execucao(transporte, **kw)
        self.addCleanup(ex.fechar)
        return ex

    def test_leitura_feliz(self):
        falso = Falso()
        ex = self.rodar(falso)
        self.assertEqual(ex.codigo, 0, ex.saida)
        r = ex.retrato()
        self.assertEqual(r["status"], "completo")
        self.assertEqual(r["firmware"], "1.07")
        self.assertEqual(len(r["leituras"]), 84)
        self.assertEqual(r["lidos"], 84)
        self.assertEqual(r["falhas"], [])
        porta = {(x["codigo"], x["canal"]): x for x in r["leituras"]}
        self.assertEqual(porta[("081", 1)]["valor"], -16.5)
        self.assertEqual(porta[("081", 1)]["cru"], "#081#1#-16.5#")
        self.assertEqual(porta[("071", 2)]["rotulo"], "OFF")
        self.assertEqual(porta[("010", 3)]["valor"], "a+b")
        self.assertEqual(porta[("020", 0)]["valor"], -7.0)
        self.assertEqual(porta[("220", None)]["rotulo"], "Custom")
        self.assertIn("Firmware 1.07", ex.saida)
        self.assertIn("84 lidos, 0 falhas", ex.saida)
        self.assertFalse(falso.conectado)
        log = (self.estado_log(ex)).read_text(encoding="utf-8")
        self.assertIn("|*000*****|", log)
        self.assertIn("TX    23 30 38 31 23 30 23  |#081#0#|", log)

    def estado_log(self, ex) -> Path:
        logs = list((ex.estado / "log-ble").glob("*.log"))
        self.assertEqual(len(logs), 1)
        return logs[0]

    def test_senha_nao_aparece_em_estado_nem_no_terminal(self):
        ex = self.rodar(Falso())
        self.assertNotIn(SENHA, ex.todos_os_textos())
        self.assertNotIn(SENHA.lower(), ex.todos_os_textos().lower())

    def test_senha_errada(self):
        falso = Falso(senha="AAAA")
        ex = self.rodar(falso)
        self.assertNotEqual(ex.codigo, 0)
        self.assertIn("Senha recusada", ex.saida)
        self.assertEqual(len(falso.enviados), 1)  # nenhuma leitura depois da senha
        self.assertFalse(falso.conectado)
        self.assertFalse((ex.estado / "leituras").exists())
        self.assertNotIn(SENHA, ex.todos_os_textos())

    def test_nao_acha_o_aparelho(self):
        ex = self.rodar(Falso(encontrado=False))
        self.assertNotEqual(ex.codigo, 0)
        self.assertIn(lp.NAO_ENCONTRADO, ex.saida)

    def test_formato_inesperado_asterisco_e_fragmentos_com_orfao(self):
        falso = Falso(sep="*", fragmento=5, orfao=b"#005#121#")
        ex = self.rodar(falso)
        self.assertEqual(ex.codigo, 0, ex.saida)
        r = ex.retrato()
        self.assertEqual(r["lidos"], 84)
        self.assertEqual(r["orfaos"], ["#005#121#"])
        log = self.estado_log(ex).read_text(encoding="utf-8")
        self.assertIn("ORFAO", log)

    def test_consulta_sem_resposta(self):
        falso = Falso(mudos={"#050#2#", "020#4"})
        ex = self.rodar(falso)
        self.assertEqual(ex.codigo, lp.SAIDA_FALHAS)
        r = ex.retrato()
        self.assertEqual(r["status"], "completo")
        self.assertEqual(r["lidos"], 82)
        self.assertEqual({(f["codigo"], f["canal"], f["motivo"]) for f in r["falhas"]},
                         {("050", 2, "timeout"), ("020", 4, "timeout")})
        porta = {(x["codigo"], x["canal"]): x for x in r["leituras"]}
        self.assertIsNone(porta[("050", 2)]["valor"])
        self.assertEqual(porta[("050", 3)]["valor"], 26.0)  # continuou depois da falha
        self.assertIn("050 Delay, saída 3: timeout", ex.saida)

    def test_queda_de_conexao_grava_retrato_incompleto(self):
        falso = Falso(cair_no_envio=11)
        ex = self.rodar(falso)
        self.assertEqual(ex.codigo, lp.SAIDA_QUEDA)
        r = ex.retrato()
        self.assertEqual(r["status"], "incompleto")
        self.assertEqual(len(r["leituras"]), 84)
        self.assertEqual(r["lidos"], 9)  # senha + 9 consultas antes da queda
        self.assertTrue(all(f["motivo"] == "conexão caiu" for f in r["falhas"]))
        self.assertIn("INCOMPLETO", ex.saida)

    def test_erro_do_ble_numa_consulta_nao_derruba_a_leitura(self):
        ex = self.rodar(Falso(erro_em={"#043#1#"}))
        self.assertEqual(ex.codigo, lp.SAIDA_FALHAS)
        r = ex.retrato()
        self.assertEqual(r["lidos"], 83)
        self.assertEqual(r["falhas"][0]["codigo"], "043")
        self.assertIn("erro do BLE", r["falhas"][0]["motivo"])

    def test_escrita_presa_vira_erro_da_consulta(self):
        ex = self.rodar(Falso(travar_em={"#041#2#"}))
        self.assertEqual(ex.codigo, lp.SAIDA_FALHAS)
        r = ex.retrato()
        self.assertEqual(r["lidos"], 83)
        self.assertEqual((r["falhas"][0]["codigo"], r["falhas"][0]["canal"]), ("041", 2))
        self.assertIn("escrita presa", r["falhas"][0]["motivo"])

    def test_tipo_de_escrita_pedido_ao_transporte(self):
        falso = Falso()
        self.rodar(falso)
        self.assertEqual(falso.preferencias[0], (f"*000*{SENHA}*", True))
        self.assertTrue(all(pref is False for _t, pref in falso.preferencias[1:]))
        self.assertEqual(len(falso.preferencias), 85)

    def test_senha_sem_resposta(self):
        ex = self.rodar(Falso(resposta_000=None))
        self.assertEqual(ex.codigo, lp.SAIDA_SEM_RESPOSTA)
        self.assertIn("não respondeu à senha", ex.saida)
        self.assertFalse((ex.estado / "leituras").exists())

    def test_resposta_000_vazia_ou_erro_cortado_nao_e_sucesso(self):
        for resposta in ("#000##", "#000#ER"):
            with self.subTest(resposta=resposta):
                falso = Falso(resposta_000=resposta)
                ex = self.rodar(falso)
                self.assertEqual(ex.codigo, lp.SAIDA_SEM_RESPOSTA)
                self.assertEqual(len(falso.enviados), 1)

    def test_resposta_000_com_a_senha_nao_vaza(self):
        ex = self.rodar(Falso(resposta_000=f"#000#PRO 2.4BT {SENHA}#"))  # firmware = senha
        self.assertEqual(ex.codigo, 0, ex.saida)
        r = ex.retrato()
        self.assertEqual(r["resposta_000"], "(omitida: continha a senha)")
        self.assertIsNone(r["firmware"])
        self.assertNotIn(SENHA, ex.todos_os_textos().upper())

    def test_duas_leituras_no_mesmo_estado(self):
        ex = self.rodar(Falso())
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": SENHA}), \
                contextlib.redirect_stdout(io.StringIO()):
            lp.main([], transporte=Falso(), estado=ex.estado, tempos=TEMPOS)
        self.assertEqual(len(list((ex.estado / "leituras").glob("*.json"))), 2)
        self.assertEqual(len(list((ex.estado / "log-ble").glob("*.log"))), 2)

    def test_aparelho_mudo_para_cedo(self):
        todas = {c.texto for c in pt.consultas_marco1()}
        falso = Falso(mudos=todas)
        ex = self.rodar(falso)
        self.assertEqual(ex.codigo, lp.SAIDA_QUEDA)
        r = ex.retrato()
        self.assertEqual(r["lidos"], 0)
        self.assertEqual(len(falso.enviados), 1 + cb.MAX_TIMEOUTS_SEGUIDOS)


class BrutoTest(unittest.TestCase):
    def test_bruto_mostra_a_resposta(self):
        ex = Execucao(Falso(), argv=["--bruto", "#081#0#"])
        self.addCleanup(ex.fechar)
        self.assertEqual(ex.codigo, 0, ex.saida)
        self.assertIn("RX |#081#0#-16.4#|", ex.saida)

    def test_bruto_com_troca_de_senha_nao_mostra_nenhuma(self):
        falso = Falso()
        ex = Execucao(falso, argv=["--bruto", "*001*AB12*CD34*"])
        self.addCleanup(ex.fechar)
        self.assertNotEqual(ex.codigo, 0)
        self.assertNotIn("AB12", ex.saida)
        self.assertNotIn("CD34", ex.saida)
        self.assertEqual(falso.enviados, [])

    def test_dois_bruto_no_mesmo_minuto_nao_sobrescrevem_o_log(self):
        ex = Execucao(Falso(), argv=["--bruto", "#081#0#"])
        self.addCleanup(ex.fechar)
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": SENHA}), \
                contextlib.redirect_stdout(io.StringIO()):
            lp.main(["--bruto", "#080#"], transporte=Falso(), estado=ex.estado, tempos=TEMPOS)
        self.assertEqual(len(list((ex.estado / "log-ble").glob("*-bruto.log"))), 2)

    def test_bruto_recusa_escrita_antes_do_ble(self):
        falso = Falso()
        ex = Execucao(falso, argv=["--bruto", "*081*0*0.0*"])
        self.addCleanup(ex.fechar)
        self.assertNotEqual(ex.codigo, 0)
        self.assertIn("envio recusado", ex.saida)
        self.assertEqual(falso.enviados, [])
        self.assertFalse(falso.conectado)


class TravaClienteTest(unittest.TestCase):
    def test_cliente_recusa_escrita_antes_do_ble(self):
        async def tentar():
            falso = Falso()
            cliente = cb.ClienteTaramps(falso, pt.LogBruto(io.StringIO()), **TEMPOS)
            with self.assertRaises(pt.EnvioRecusado):
                await cliente.enviar("*081*0*0.0*")
            return falso

        falso = asyncio.run(tentar())
        self.assertEqual(falso.enviados, [])


class FalsoBleakClient:
    """Imita o BleakClient: a 1ª tentativa desconecta (dispara o callback) e falha."""

    instancias: list = []

    def __init__(self, dispositivo, disconnected_callback=None, timeout=0):
        self.callback = disconnected_callback
        self.escritas: list[tuple[bytes, bool]] = []
        FalsoBleakClient.instancias.append(self)
        escrita = SimpleNamespace(properties=["write", "write-without-response"])
        resposta = SimpleNamespace(properties=["notify"])
        caracteristicas = {pt.UUID_ESCRITA: escrita, pt.UUID_RESPOSTA: resposta}
        servico = SimpleNamespace(get_characteristic=caracteristicas.get)
        self.services = SimpleNamespace(
            get_service=lambda u: servico if u == pt.UUID_SERVICO else None)

    async def connect(self):
        if len(FalsoBleakClient.instancias) == 1:
            self.callback(self)
            raise OSError("falha simulada na 1ª tentativa")

    async def disconnect(self):
        if self.callback:
            self.callback(self)

    async def start_notify(self, caracteristica, callback):
        pass

    async def write_gatt_char(self, caracteristica, dados, response=None):
        self.escritas.append((bytes(dados), response))


async def _sem_espera(*_a, **_k):
    return None


class TransporteBleakTest(unittest.TestCase):
    def test_tentativa_que_falha_nao_marca_queda(self):
        FalsoBleakClient.instancias = []

        async def rodar():
            transporte = cb.TransporteBleak()
            cliente = cb.ClienteTaramps(transporte, pt.LogBruto(io.StringIO()), **TEMPOS)
            with mock.patch("bleak.BleakClient", FalsoBleakClient), \
                    mock.patch.object(cb.asyncio, "sleep", _sem_espera):
                await transporte.conectar(object())
            self.assertFalse(cliente.caiu)
            await cliente.enviar("#081#0#")

        asyncio.run(rodar())
        self.assertEqual(len(FalsoBleakClient.instancias), 2)
        self.assertEqual(FalsoBleakClient.instancias[1].escritas, [(b"#081#0#", False)])

    def test_queda_depois_de_conectar_conta(self):
        FalsoBleakClient.instancias = [None]  # pula a falha da 1ª tentativa

        async def rodar():
            transporte = cb.TransporteBleak()
            cliente = cb.ClienteTaramps(transporte, pt.LogBruto(io.StringIO()), **TEMPOS)
            with mock.patch("bleak.BleakClient", FalsoBleakClient):
                await transporte.conectar(object())
            FalsoBleakClient.instancias[-1].callback(None)
            return cliente

        self.assertTrue(asyncio.run(rodar()).caiu)

    def test_queda_com_log_fechado_nao_quebra(self):
        arquivo = io.StringIO()
        cliente = cb.ClienteTaramps(Falso(), pt.LogBruto(arquivo), **TEMPOS)
        arquivo.close()
        cliente._cair()
        self.assertTrue(cliente.caiu)

    def test_tipo_de_escrita_conforme_a_caracteristica(self):
        casos = {
            ("write", "write-without-response"): [True, False],
            ("write",): [True, True],
            ("write-without-response",): [False, False],
        }
        for props, esperado in casos.items():
            with self.subTest(props=props):
                escritas = []

                async def gravar(_c, dados, response=None):
                    escritas.append(response)

                transporte = cb.TransporteBleak()
                transporte._escrita = SimpleNamespace(properties=list(props))
                transporte._cliente = SimpleNamespace(write_gatt_char=gravar)
                asyncio.run(transporte.escrever(b"*000*AB12*", preferir_resposta=True))
                asyncio.run(transporte.escrever(b"#080#", preferir_resposta=False))
                self.assertEqual(escritas, esperado)


class ConferirTest(unittest.TestCase):
    def test_conferir_grava_ao_lado_do_retrato(self):
        leitura = Execucao(Falso())
        self.addCleanup(leitura.fechar)
        arquivo = next((leitura.estado / "leituras").glob("*.json"))
        respostas = iter(["", "x", "-16.0 dB"] + [""] * 60)
        saida = io.StringIO()
        with contextlib.redirect_stdout(saida):
            codigo = lp.main(["--conferir", str(arquivo)], entrada=lambda _p: next(respostas))
        self.assertEqual(codigo, 0)
        resultado = json.loads(arquivo.with_name(f"{arquivo.stem}-conferencia.json")
                               .read_text(encoding="utf-8"))
        self.assertEqual(resultado["diferentes"], 1)
        self.assertEqual(resultado["iguais"], len(resultado["itens"]) - 1)
        diferente = next(x for x in resultado["itens"] if x["confere"] is False)
        self.assertEqual(diferente["app"], "-16.0 dB")
        self.assertIn("[1/", saida.getvalue())

    def test_conferir_pergunta_de_novo_se_a_resposta_nao_e_enter_nem_x(self):
        leitura = Execucao(Falso())
        self.addCleanup(leitura.fechar)
        arquivo = next((leitura.estado / "leituras").glob("*.json"))
        respostas = iter(["n", "nao", "x", "", "s"] + [""] * 60)
        with contextlib.redirect_stdout(io.StringIO()):
            lp.main(["--conferir", str(arquivo)], entrada=lambda _p: next(respostas))
        resultado = json.loads(arquivo.with_name(f"{arquivo.stem}-conferencia.json")
                               .read_text(encoding="utf-8"))
        self.assertIs(resultado["itens"][0]["confere"], False)  # "n" e "nao" não contam
        self.assertIsNone(resultado["itens"][0]["app"])
        self.assertEqual(resultado["diferentes"], 1)

    def test_conferir_para_no_meio_e_salva(self):
        leitura = Execucao(Falso())
        self.addCleanup(leitura.fechar)
        arquivo = next((leitura.estado / "leituras").glob("*.json"))
        respostas = iter(["", ""])

        def entrada(_p):
            try:
                return next(respostas)
            except StopIteration:
                raise EOFError

        with contextlib.redirect_stdout(io.StringIO()):
            lp.main(["--conferir", str(arquivo)], entrada=entrada)
        resultado = json.loads(arquivo.with_name(f"{arquivo.stem}-conferencia.json")
                               .read_text(encoding="utf-8"))
        self.assertEqual(resultado["iguais"], 2)
        self.assertGreater(resultado["sem_resposta"], 0)


if __name__ == "__main__":
    unittest.main()
