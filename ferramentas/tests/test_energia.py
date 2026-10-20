"""Testes do remote out pela IA (energia.py, gd.avaliar_energia), com o processador falso de
test_tom. Nunca conecta no aparelho de verdade."""

from __future__ import annotations

import contextlib
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

import energia as en  # noqa: E402
import guardiao as gd  # noqa: E402
import protocolo_taramps as pt  # noqa: E402
import test_tom as tt  # noqa: E402


class Execucao:
    def __init__(self, transporte, argv):
        self.tmp = tempfile.TemporaryDirectory()
        self.estado = Path(self.tmp.name) / "estado"
        self.estado.mkdir()
        shutil.copy(tt.GUARDIAO, self.estado / "guardiao.json")
        saida = io.StringIO()
        with mock.patch.dict(os.environ, {"TARAMPS_SENHA": tt.SENHA}), \
                contextlib.redirect_stdout(saida):
            self.codigo = en.main(list(argv), transporte=transporte, estado=self.estado,
                                  tempos=tt.TEMPOS)
        self.saida = saida.getvalue()

    def registro(self) -> dict:
        arquivos = list((self.estado / "ajustes").glob("*.json"))
        assert len(arquivos) == 1, arquivos
        return json.loads(arquivos[0].read_text(encoding="utf-8"))

    def fechar(self):
        self.tmp.cleanup()


class EnergiaTest(unittest.TestCase):
    def rodar(self, transporte, argv) -> Execucao:
        ex = Execucao(transporte, argv)
        self.addCleanup(ex.fechar)
        return ex

    def test_desliga_sem_leitura(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["desliga"])
        self.assertEqual(ex.codigo, en.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*252*0*"])
        self.assertNotIn("#080#", falso.enviados)
        reg = ex.registro()
        self.assertEqual(reg["tipo"], "energia")
        self.assertEqual(reg["status"], "enviado")

    def test_desliga_mesmo_com_violacao(self):
        falso = tt.ProcessadorGerador(iniciais={})  # retrato real: travas 1 e 2
        ex = self.rodar(falso, ["desliga"])
        self.assertEqual(ex.codigo, en.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*252*0*"])

    def test_liga_le_antes(self):
        falso = tt.ProcessadorGerador()
        ex = self.rodar(falso, ["liga"])
        self.assertEqual(ex.codigo, en.SAIDA_OK, ex.saida)
        self.assertEqual(falso.especiais, ["*252*1*"])
        self.assertLess(falso.enviados.index("#080#"), falso.enviados.index("*252*1*"))

    def test_liga_recusa_com_violacao(self):
        falso = tt.ProcessadorGerador(iniciais={})
        ex = self.rodar(falso, ["liga"])
        self.assertEqual(ex.codigo, en.SAIDA_RECUSADO, ex.saida)
        self.assertEqual(falso.especiais, [])
        self.assertIn("não se liga o módulo em violação", ex.saida)


class AprovacaoEnergiaTest(unittest.TestCase):
    def test_formato(self):
        self.assertEqual(pt.montar_comando_especial("252", 0), "*252*0*")
        self.assertEqual(pt.montar_comando_especial("252", True), "*252*1*")
        for valor in ("2", "01", "-1", "x"):
            with self.subTest(valor=valor):
                with self.assertRaises(pt.EscritaInvalida):
                    pt.montar_comando_especial("252", valor)

    def test_so_com_a_aprovacao_de_energia(self):
        desligar = gd.avaliar_energia(False, None, None).aprovacao
        self.assertEqual(pt.verificar_comando_especial("*252*0*", desligar), b"*252*0*")
        with self.assertRaises(gd.EscritaNaoAprovada):  # desligar não libera ligar
            pt.verificar_comando_especial("*252*1*", desligar)
        with self.assertRaises(gd.EscritaNaoAprovada):
            pt.verificar_comando_especial("*252*0*", gd.aprovar_desligar_gerador())
        with self.assertRaises(gd.EscritaNaoAprovada):  # a aprovação de energia não vale no lote
            gd.item_aprovado(desligar, 0)

    def test_ligar_sem_leitura_recusa(self):
        self.assertFalse(gd.avaliar_energia(True, None, None).aprovado)

    def test_lote_e_trava_de_escrita_continuam_recusando(self):
        with self.assertRaises(pt.EscritaInvalida):
            pt.verificar_escrita("*252*0*")
        with self.assertRaises(pt.EnvioRecusado):
            pt.verificar_envio("*252*0*")


if __name__ == "__main__":
    unittest.main()
