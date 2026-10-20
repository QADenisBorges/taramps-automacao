"""Testes de salvar memória pela IA (memoria.py), com o processador falso que guarda o gerador,
as memórias e os nomes (test_tom.ProcessadorGerador). Nunca conecta no aparelho de verdade
(spec-gerador-memoria.md)."""

from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import memoria as mm  # noqa: E402
import painel  # noqa: E402
import sessao  # noqa: E402
import test_tom as tt  # noqa: E402


class Execucao:
    def __init__(self, transporte, argv, memorias: dict | None = None):
        self.tmp = tempfile.TemporaryDirectory()
        self.estado = Path(self.tmp.name) / "estado"
        self.estado.mkdir()
        shutil.copy(tt.GUARDIAO, self.estado / "guardiao.json")
        if memorias is not None:
            dados = sessao.memorias_vazias()
            dados.pop("erro")
            dados["dsp"].update(memorias)
            (self.estado / "memorias.json").write_text(json.dumps(dados), encoding="utf-8")
        saida = io.StringIO()
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": tt.SENHA}), \
                contextlib.redirect_stdout(saida):
            self.codigo = mm.main(list(argv), transporte=transporte, estado=self.estado,
                                  tempos=tt.TEMPOS)
        self.saida = saida.getvalue()

    def registro(self) -> dict:
        arquivos = list((self.estado / "ajustes").glob("*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def memorias(self) -> dict:
        return sessao.carregar_memorias(self.estado)

    def fechar(self):
        self.tmp.cleanup()


DENIS = {"nome": "FILME", "por": "denis", "hora": "2026-10-17T10:00:00"}
IA = {"nome": "MUSICA HIGH", "por": "ia", "hora": "2026-10-17T18:00:00"}


class MemoriaTest(unittest.TestCase):
    def rodar(self, transporte, argv, **kw) -> Execucao:
        ex = Execucao(transporte, argv, **kw)
        self.addCleanup(ex.fechar)
        return ex

    def assert_nada_enviado(self, falso):
        self.assertEqual(falso.enviados, [])
        self.assertEqual(falso.conexoes, 0)

    def test_nomes_so_le(self):
        falso = tt.ProcessadorGerador()
        falso.nomes = {1: "MUSICA HIGH", 2: "MUSICA ORIG", 3: "SPL"}
        ex = self.rodar(falso, ["nomes"])
        self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
        self.assertIn("Memória 1: MUSICA HIGH", ex.saida)
        self.assertIn("Memória 2: MUSICA ORIG", ex.saida)
        self.assertIn("Memória 3: SPL", ex.saida)
        self.assertEqual(falso.especiais, [])  # nenhuma escrita
        self.assertFalse((ex.estado / "memorias.json").exists())

    def test_salvar_memoria(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "2", "MUSICA ORIG"])
        self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*240*1*", "*232*MUSICA ORIG*"])
        self.assertEqual(falso.memorias[1], falso.valores)  # o estado salvo no índice 1
        self.assertIn("232#", falso.enviados[falso.enviados.index("*232*MUSICA ORIG*"):])  # relê
        # Leitura nova completa antes de salvar.
        self.assertLess(falso.enviados.index("#080#"), falso.enviados.index("*240*1*"))
        r = ex.registro()
        self.assertEqual((r["tipo"], r["status"]), ("memoria", "salvo"))
        self.assertEqual(r["nome_relido"], "MUSICA ORIG")
        self.assertNotIn("aviso", r)
        backup = next((ex.estado / "backups").glob("*.json"))
        sha = hashlib.sha256(backup.read_bytes()).hexdigest()
        self.assertEqual(r["retrato_sha256"], sha)
        m = ex.memorias()["dsp"]["2"]
        self.assertEqual((m["nome"], m["por"], m["retrato"]), ("MUSICA ORIG", "ia", sha))
        self.assertIsNone(ex.memorias()["dsp"]["1"])
        diario = next((ex.estado / "sessoes").glob("*.md")).read_text(encoding="utf-8")
        self.assertIn("Memória 2 do DSP: MUSICA ORIG (por IA)", diario)
        self.assertIn(sha, diario)
        self.assertNotIn(tt.SENHA, next((ex.estado / "log-ble").glob("*-memoria.log"))
                         .read_text(encoding="utf-8"))
        self.assertFalse(falso.conectado)
        # O painel lê o registro e as memórias.
        self.assertEqual(painel.ajustes(ex.estado)[0]["status"], "salvo")
        self.assertEqual(painel.memorias(ex.estado)["dsp"]["2"]["nome"], "MUSICA ORIG")

    def test_minusculas_viram_maiusculas(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "1", " musica high "])
        self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*240*0*", "*231*MUSICA HIGH*"])

    def test_nome_invalido_recusa_sem_conectar(self):
        for nome in ("MÚSICA", "ABCDEFGHIJKLM", "", "   ", "FILME*", "A#B", "UM-DOIS"):
            with self.subTest(nome=nome):
                falso = tt.ProcessadorGerador()
                ex = self.rodar(falso, ["salvar", "3", nome])
                self.assertEqual(ex.codigo, mm.SAIDA_RECUSADO, ex.saida)
                self.assert_nada_enviado(falso)
                self.assertEqual(ex.registro()["status"], "recusado")
                self.assertIn("Nada foi enviado", ex.saida)

    def test_slot_invalido(self):
        falso = tt.ProcessadorGerador()
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            self.rodar(falso, ["salvar", "4", "X"])
        self.assertEqual(falso.enviados, [])

    def test_sobrescrever_do_denis_exige_a_opcao(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "1", "NOVA"], memorias={"1": DENIS})
        self.assertEqual(ex.codigo, mm.SAIDA_RECUSADO, ex.saida)
        self.assert_nada_enviado(falso)
        self.assertIn("--sobrescrever", ex.saida)
        self.assertIn("FILME", ex.saida)
        self.assertEqual(ex.memorias()["dsp"]["1"]["nome"], "FILME")

        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "1", "NOVA", "--sobrescrever"], memorias={"1": DENIS})
        self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*240*0*", "*231*NOVA*"])
        self.assertEqual(ex.memorias()["dsp"]["1"]["por"], "ia")

    def test_registro_sem_dono_conta_como_do_denis(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "1", "NOVA"],
                        memorias={"1": {"nome": "VELHA", "por": None, "hora": None}})
        self.assertEqual(ex.codigo, mm.SAIDA_RECUSADO, ex.saida)
        self.assert_nada_enviado(falso)

    def test_vazio_ou_da_ia_salva_sem_a_opcao(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["salvar", "1", "MUSICA HIGH"], memorias={"1": IA, "2": DENIS})
        self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
        self.assertEqual(ex.memorias()["dsp"]["2"], DENIS)  # a outra fica como estava

    def test_violacao_no_estado_nao_salva(self):
        falso = tt.ProcessadorGerador(iniciais={})  # retrato real: travas 1 e 2
        ex = self.rodar(falso, ["salvar", "3", "FILME"])
        self.assertEqual(ex.codigo, mm.SAIDA_RECUSADO, ex.saida)
        self.assertEqual(falso.especiais, [])
        self.assertIn("trava 2", ex.saida)
        self.assertIn("não se salva estado em violação", ex.saida)
        self.assertIsNone(ex.memorias()["dsp"]["3"])

    def test_regra6_tambem_barra_o_salvar(self):
        falso = tt.ProcessadorGerador(iniciais={**tt.CORRIGIDO, ("081", 1): "-9.0"})
        ex = self.rodar(falso, ["salvar", "3", "FILME"])
        self.assertEqual(ex.codigo, mm.SAIDA_RECUSADO, ex.saida)
        self.assertIn("regra 6", ex.saida)
        self.assertEqual(falso.especiais, [])

    def test_nome_relido_diferente_avisa_e_segue(self):
        for relido in ("FILM", ""):
            with self.subTest(relido=relido):
                falso = tt.ProcessadorGerador(nome_relido={3: relido})
                ex = self.rodar(falso, ["salvar", "3", "FILME"])
                self.assertEqual(ex.codigo, mm.SAIDA_OK, ex.saida)
                self.assertIn("Aviso: o nome relido", ex.saida)
                self.assertIn("O salvar vale", ex.saida)
                self.assertIn("aviso", ex.registro())
                self.assertEqual(ex.memorias()["dsp"]["3"]["nome"], "FILME")

    def test_queda_depois_do_salvar_nao_registra(self):
        class Cai(tt.ProcessadorGerador):
            async def escrever(self, dados, preferir_resposta):
                await super().escrever(dados, preferir_resposta)
                if dados == b"*240*2*":
                    self.conectado = False
                    import asyncio
                    asyncio.get_running_loop().call_later(0.002, self.ao_cair)

        falso = Cai()
        ex = self.rodar(falso, ["salvar", "3", "FILME"])
        self.assertEqual(ex.codigo, mm.SAIDA_INTERROMPIDO, ex.saida)
        self.assertIn("A memória pode ter sido salva", ex.saida)
        self.assertIsNone(ex.memorias()["dsp"]["3"])
        self.assertEqual(ex.registro()["status"], "interrompido")

    def test_guardiao_json_nunca_e_escrito(self):
        ex = self.rodar(tt.ProcessadorGerador(), ["salvar", "2", "X"])
        self.assertEqual((ex.estado / "guardiao.json").read_bytes(), tt.GUARDIAO.read_bytes())

    def test_sessao_memoria_aceita_o_retrato(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        estado = Path(tmp.name)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(sessao.main(["memoria", "1", "X", "--por", "ia", "--retrato", "ab"],
                                         estado=estado), 0)
            self.assertEqual(sessao.carregar_memorias(estado)["dsp"]["1"]["retrato"], "ab")
            self.assertEqual(sessao.main(["memoria", "1", "Y", "--por", "denis"], estado=estado), 0)
            self.assertNotIn("retrato", sessao.carregar_memorias(estado)["dsp"]["1"])
            self.assertEqual(sessao.main(["memoria", "1", "--limpar", "--retrato", "ab"],
                                         estado=estado), sessao.SAIDA_USO)


if __name__ == "__main__":
    unittest.main()
