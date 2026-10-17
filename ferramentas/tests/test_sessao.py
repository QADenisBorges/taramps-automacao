"""Testes do sessao.py: grava e retoma o progresso, recusa medida repetida sem --refazer e
gera o diário do dia. Tudo numa pasta temporária."""

from __future__ import annotations

import contextlib
import datetime as dt
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import sessao  # noqa: E402

DIA = dt.datetime(2026, 10, 17, 12, 0, 0)


class Base(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.estado = Path(self.tmp.name) / "estado"
        self.pasta = self.estado / "sessoes"
        self.relogio = DIA

    def rodar(self, *argv: str, minutos: int = 1) -> tuple[int, str]:
        self.relogio += dt.timedelta(minutes=minutos)
        saida = io.StringIO()
        with contextlib.redirect_stdout(saida):
            codigo = sessao.main(list(argv), estado=self.estado, agora=self.relogio)
        return codigo, saida.getvalue()

    def passo(self, etapa: int, passo: int, *extra: str) -> tuple[int, str]:
        return self.rodar("passo", "--etapa", str(etapa), "--passo", str(passo),
                          "--pedido", f"pedido {etapa}.{passo}", "--resposta", "ok", *extra)

    def progresso(self) -> dict:
        return json.loads((self.pasta / "progresso.json").read_text(encoding="utf-8"))

    def diario(self) -> str:
        return (self.pasta / "2026-10-17.md").read_text(encoding="utf-8")


class ProgressoTest(Base):
    def test_sessao_nova_comeca_na_etapa_0(self):
        codigo, saida = self.rodar("progresso")
        self.assertEqual(codigo, 0)
        self.assertIn("Agora: 12:01", saida)
        self.assertIn("Sessão nova", saida)
        self.assertIn("Retomar: etapa 0, passo 1", saida)
        self.assertFalse(self.pasta.exists())  # só mostrar não grava nada

    def test_grava_e_retoma_do_passo_seguinte(self):
        codigo, saida = self.rodar("passo", "--etapa", "1", "--passo", "1",
                                   "--pedido", "Confirme a cadeia", "--resposta", "ok")
        self.assertEqual(codigo, 0)
        self.assertIn("Registrado: etapa 1, passo 1", saida)
        self.rodar("passo", "--etapa", "1", "--passo", "2", "--pedido", "Saída 1 → CH1?",
                   "--resposta", "ok", "--duvida")
        dados = self.progresso()
        self.assertEqual((dados["etapa"], dados["passo"], dados["travou"]), (1, 2, False))
        self.assertEqual(dados["passos_feitos"], ["1.1", "1.2"])

        codigo, saida = self.rodar("progresso")
        self.assertEqual(codigo, 0)
        self.assertIn("Último passo registrado: etapa 1, passo 2 (concluído)", saida)
        self.assertIn("Retomar: etapa 1, passo 3\n", saida)

    def test_etapa_completa_retoma_na_seguinte(self):
        self.passo(0, 3)
        _, saida = self.rodar("progresso")
        self.assertIn("Retomar: etapa 1, passo 1 (a etapa 0 acabou)", saida)
        self.passo(8, 1)
        _, saida = self.rodar("progresso")
        self.assertIn("Retomar: etapa 8, passo 1 (roteiro concluído)", saida)

    def test_etapa_6_tem_passos_variaveis(self):
        self.passo(6, 3)
        _, saida = self.rodar("progresso")
        self.assertIn("Retomar: etapa 6, passo 4 (ou etapa 7, passo 1, se a etapa 6 acabou)", saida)

    def test_passo_que_travou_e_retomado_no_mesmo_lugar(self):
        self.rodar("passo", "--etapa", "2", "--passo", "1", "--pedido", "Feche o app",
                   "--resposta", "não achei", "--travou")
        dados = self.progresso()
        self.assertTrue(dados["travou"])
        self.assertEqual(dados["passos_feitos"], [])
        _, saida = self.rodar("progresso")
        self.assertIn("passo 1 (travou)", saida)
        self.assertIn("Retomar: etapa 2, passo 1 (o mesmo passo: travou)", saida)

        # Concluído depois: sai do estado travado e entra nos feitos uma vez só.
        self.passo(2, 1)
        self.passo(2, 1)
        dados = self.progresso()
        self.assertFalse(dados["travou"])
        self.assertEqual(dados["passos_feitos"], ["2.1"])

    def test_travar_de_novo_tira_dos_feitos(self):
        self.passo(2, 1)
        self.passo(2, 2)
        self.passo(2, 1, "--travou")
        self.assertEqual(self.progresso()["passos_feitos"], ["2.2"])

    def test_progresso_ilegivel_nao_grava(self):
        self.pasta.mkdir(parents=True)
        (self.pasta / "progresso.json").write_text("{ quebrado", encoding="utf-8")
        codigo, saida = self.rodar("ideia", "x")
        self.assertEqual(codigo, 1)
        self.assertIn("Não consegui ler o progresso", saida)
        self.assertEqual((self.pasta / "progresso.json").read_text(encoding="utf-8"), "{ quebrado")

    def test_formato_desconhecido_nao_grava(self):
        self.pasta.mkdir(parents=True)
        (self.pasta / "progresso.json").write_text('{"formato": 2}', encoding="utf-8")
        codigo, saida = self.rodar("ideia", "x")
        self.assertEqual(codigo, 1)
        self.assertIn("formato desconhecido", saida)
        self.assertEqual((self.pasta / "progresso.json").read_text(encoding="utf-8"), '{"formato": 2}')

    def test_campos_fora_do_formato(self):
        self.pasta.mkdir(parents=True)
        for conteudo in ('{"formato": 1, "medidas": []}', '{"formato": 1, "ideias": {}}',
                         '{"formato": 1, "passos_feitos": "1.1"}', '{"formato": 1, "etapa": "1"}'):
            with self.subTest(conteudo=conteudo):
                (self.pasta / "progresso.json").write_text(conteudo, encoding="utf-8")
                codigo, saida = self.rodar("progresso")
                self.assertEqual(codigo, 1)
                self.assertIn("fora do formato", saida)

    def test_passo_nulo_vale_zero(self):
        self.pasta.mkdir(parents=True)
        (self.pasta / "progresso.json").write_text('{"formato": 1, "etapa": 1, "passo": null}',
                                                   encoding="utf-8")
        codigo, saida = self.rodar("progresso")
        self.assertEqual(codigo, 0)
        self.assertIn("Retomar: etapa 1, passo 1", saida)

    def test_gravacao_atomica_deixa_bak_e_usa_o_bak_se_o_principal_quebrar(self):
        self.passo(1, 1)
        self.assertFalse((self.pasta / "progresso.json.bak").exists())
        self.passo(1, 2)
        self.assertFalse((self.pasta / "progresso.json.tmp").exists())
        bak = json.loads((self.pasta / "progresso.json.bak").read_text(encoding="utf-8"))
        self.assertEqual(bak["passos_feitos"], ["1.1"])
        self.assertEqual(self.progresso()["passos_feitos"], ["1.1", "1.2"])

        (self.pasta / "progresso.json").write_text("{ quebrado", encoding="utf-8")
        codigo, saida = self.rodar("progresso")
        self.assertEqual(codigo, 0)
        self.assertIn("Usei a cópia anterior", saida)
        self.assertIn("Retomar: etapa 1, passo 2", saida)  # o .bak tinha só o 1.1

    def test_erro_de_uso_sai_com_1(self):
        with self.assertRaises(SystemExit) as ctx, contextlib.redirect_stderr(io.StringIO()):
            sessao.main(["passo", "--etapa", "1"], estado=self.estado)
        self.assertEqual(ctx.exception.code, 1)

    def test_novo_arquiva_e_recomeca(self):
        self.passo(1, 1)
        self.rodar("medida", "jbl_l_ohm", "3.4", "ohm")
        codigo, saida = self.rodar("novo")
        self.assertEqual(codigo, 0)
        arquivo = self.pasta / "progresso-2026-10-17_1203.json"
        self.assertIn(arquivo.name, saida)
        self.assertTrue(arquivo.exists())
        self.assertFalse((self.pasta / "progresso.json").exists())
        self.assertFalse((self.pasta / "progresso.json.bak").exists())
        self.assertEqual(json.loads(arquivo.read_text(encoding="utf-8"))["passos_feitos"], ["1.1"])
        _, saida = self.rodar("progresso")
        self.assertIn("Sessão nova", saida)
        self.assertIn("Medidas (0)", saida)
        self.assertIn("Sessão recomeçada do zero", self.diario())
        codigo, saida = self.rodar("novo")
        self.assertEqual(codigo, 0)
        self.assertIn("Nada para arquivar", saida)


class MedidaTest(Base):
    def test_guarda_medida_com_numero_e_unidade(self):
        self.rodar("passo", "--etapa", "1", "--passo", "4", "--pedido", "Meça a JBL L",
                   "--resposta", "3,4")
        codigo, saida = self.rodar("medida", "jbl_l_ohm", "3,4", "ohm")
        self.assertEqual(codigo, 0)
        self.assertIn("jbl_l_ohm = 3.4 ohm", saida)
        m = self.progresso()["medidas"]["jbl_l_ohm"]
        self.assertEqual(m["valor"], 3.4)
        self.assertEqual((m["etapa"], m["passo"]), (1, 4))  # herda o passo atual
        self.assertEqual(m["anteriores"], [])

    def test_medida_em_texto_e_com_etapa_explicita(self):
        codigo, _ = self.rodar("medida", "zumbido", "não", "-", "--etapa", "4", "--passo", "3")
        self.assertEqual(codigo, 0)
        m = self.progresso()["medidas"]["zumbido"]
        self.assertEqual((m["valor"], m["unidade"], m["etapa"], m["passo"]), ("não", "-", 4, 3))

    def test_nao_finito_fica_texto(self):
        for texto in ("nan", "inf", "pular"):
            with self.subTest(texto=texto):
                self.rodar("medida", f"m_{texto}", texto, "-")
                self.assertEqual(self.progresso()["medidas"][f"m_{texto}"]["valor"], texto)

    def test_recusa_medida_repetida_sem_motivo(self):
        self.rodar("medida", "saida1_vac", "11.0", "Vac")
        codigo, saida = self.rodar("medida", "saida1_vac", "11.4", "Vac")
        self.assertEqual(codigo, 2)
        self.assertIn("Já registrada: saida1_vac = 11 Vac", saida)
        self.assertIn("--refazer MOTIVO", saida)
        self.assertEqual(self.progresso()["medidas"]["saida1_vac"]["valor"], 11.0)  # ficou a 1ª

    def test_refazer_com_motivo_guarda_a_anterior(self):
        self.rodar("medida", "saida1_vac", "11.0", "Vac")
        codigo, saida = self.rodar("medida", "saida1_vac", "11.4", "Vac",
                                   "--refazer", "mudou o ganho do módulo")
        self.assertEqual(codigo, 0)
        self.assertIn("refeita: mudou o ganho do módulo", saida)
        m = self.progresso()["medidas"]["saida1_vac"]
        self.assertEqual(m["valor"], 11.4)
        self.assertEqual(len(m["anteriores"]), 1)
        self.assertEqual(m["anteriores"][0]["valor"], 11.0)
        self.assertEqual(m["anteriores"][0]["motivo_da_refeita"], "mudou o ganho do módulo")
        _, saida = self.rodar("progresso")
        self.assertIn("saida1_vac = 11.4 Vac", saida)
        self.assertIn("refeita 1×", saida)


class IdeiaTest(Base):
    def test_ideia_vai_para_depois_e_confirma_em_uma_linha(self):
        codigo, saida = self.rodar("ideia", "trocar", "o", "cabo", "do", "sub")
        self.assertEqual(codigo, 0)
        self.assertEqual(saida.strip().count("\n"), 0)
        self.assertIn("Anotado para depois: trocar o cabo do sub", saida)
        self.assertEqual(self.progresso()["ideias"][0]["texto"], "trocar o cabo do sub")
        _, saida = self.rodar("progresso")
        self.assertIn("Para depois (1)", saida)
        self.assertIn("trocar o cabo do sub", saida)

    def test_ideia_vazia_e_erro_de_uso(self):
        codigo, _ = self.rodar("ideia", "  ")
        self.assertEqual(codigo, 1)


class DiarioTest(Base):
    def test_gera_o_diario_do_dia(self):
        self.rodar("passo", "--etapa", "1", "--passo", "1", "--pedido", "Confirme a cadeia",
                   "--resposta", "ok")
        self.rodar("passo", "--etapa", "1", "--passo", "2", "--pedido", "Meça a JBL L",
                   "--resposta", "o que é Re?", "--duvida", "--travou")
        self.rodar("medida", "jbl_l_ohm", "3.4", "ohm")
        self.rodar("ideia", "luz no rack")
        texto = self.diario()
        self.assertTrue(texto.startswith("# Sessão de regulagem · 17/10/2026"))
        self.assertIn("## 12:01 · Etapa 1, passo 1", texto)
        self.assertIn("- Pedido: Confirme a cadeia", texto)
        self.assertIn("- Resposta: ok", texto)
        self.assertIn("- Dúvida: não", texto)
        self.assertIn("- Travou: não", texto)
        self.assertIn("## 12:02 · Etapa 1, passo 2", texto)
        self.assertIn("- Dúvida: sim", texto)
        self.assertIn("- Travou: sim", texto)
        self.assertIn("## 12:03 · Medida: jbl_l_ohm", texto)
        self.assertIn("- jbl_l_ohm = 3.4 ohm", texto)
        self.assertIn("## 12:04 · Ideia para depois", texto)
        self.assertIn("- luz no rack", texto)
        self.assertEqual(texto.count("# Sessão de regulagem"), 1)  # cabeçalho uma vez só

    def test_outro_dia_abre_outro_diario(self):
        self.rodar("passo", "--etapa", "1", "--passo", "1", "--pedido", "a", "--resposta", "ok")
        self.relogio = DIA + dt.timedelta(days=1)
        self.rodar("passo", "--etapa", "1", "--passo", "2", "--pedido", "b", "--resposta", "ok")
        self.assertTrue((self.pasta / "2026-10-17.md").exists())
        self.assertTrue((self.pasta / "2026-10-18.md").exists())
        self.assertEqual(self.progresso()["passos_feitos"], ["1.1", "1.2"])  # progresso é um só


if __name__ == "__main__":
    unittest.main()
