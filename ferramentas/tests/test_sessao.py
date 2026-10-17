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
from unittest import mock

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


class MemoriaTest(Base):
    """memoria e preset gravam estado/memorias.json (o painel só lê) e anotam no diário."""

    def memorias(self) -> dict:
        return json.loads((self.estado / "memorias.json").read_text(encoding="utf-8"))

    def test_memoria_do_dsp_com_origem_e_hora(self):
        codigo, saida = self.rodar("memoria", "1", "SPL", "HIGH", "--por", "ia")
        self.assertEqual(codigo, 0)
        self.assertIn("Memória 1 registrada: SPL HIGH, por IA.", saida)
        dados = self.memorias()
        self.assertEqual(dados["formato"], 1)
        self.assertEqual(dados["dsp"]["1"], {"nome": "SPL HIGH", "por": "ia", "hora": "2026-10-17T12:01:00"})
        self.assertIsNone(dados["dsp"]["2"])
        self.assertEqual(dados["presets"], [])
        self.assertNotIn("erro", dados)
        self.assertFalse((self.estado / "memorias.json.tmp").exists())
        self.assertIn("## 12:01 · Memória 1 salva", self.diario())
        self.assertIn("- Memória 1 do DSP: SPL HIGH (por IA)", self.diario())

    def test_memoria_substituida_guarda_o_nome_anterior_no_diario(self):
        self.rodar("memoria", "3", "Filme", "--por", "ia")
        codigo, saida = self.rodar("memoria", "3", "Filme-EQ", "--por", "denis")
        self.assertEqual(codigo, 0)
        self.assertIn("(antes: Filme)", saida)
        self.assertEqual(self.memorias()["dsp"]["3"]["nome"], "Filme-EQ")
        self.assertEqual(self.memorias()["dsp"]["3"]["por"], "denis")
        self.assertIn("- Antes: Filme", self.diario())

    def test_preset_do_celular_e_mesmo_nome_substitui(self):
        self.rodar("preset", "Rock", "--por", "ia")
        self.rodar("preset", "Sertanejo", "--por", "denis")
        codigo, saida = self.rodar("preset", "rock", "--por", "denis")
        self.assertEqual(codigo, 0)
        self.assertIn("(substituído)", saida)
        presets = self.memorias()["presets"]
        self.assertEqual([p["nome"] for p in presets], ["Sertanejo", "rock"])
        self.assertEqual(presets[1]["por"], "denis")
        self.assertIn("## 12:01 · Preset do celular salvo", self.diario())

    def test_erros_de_uso(self):
        for argv in (["memoria", "4", "X", "--por", "ia"],  # só 1 a 3
                     ["memoria", "1", "X", "--por", "app"]):
            with self.assertRaises(SystemExit) as ctx, contextlib.redirect_stderr(io.StringIO()):
                sessao.main(argv, estado=self.estado)
            self.assertEqual(ctx.exception.code, 1)
        self.assertEqual(self.rodar("memoria", "1", "X")[0], 1)  # falta --por
        self.assertEqual(self.rodar("memoria", "1")[0], 1)  # sem nome nem --limpar
        self.assertEqual(self.rodar("memoria", "1", "X", "--limpar")[0], 1)  # --limpar vai sozinho
        self.assertEqual(self.rodar("preset", "Rock")[0], 1)  # falta --por
        self.assertEqual(self.rodar("preset", "  ", "--por", "ia")[0], 1)
        self.assertFalse((self.estado / "memorias.json").exists())

    def test_limpar_memoria_e_remover_preset(self):
        self.rodar("memoria", "2", "SPL-EQ", "--por", "ia")
        self.rodar("preset", "Rock", "--por", "ia")
        self.rodar("preset", "Pop", "--por", "denis")
        codigo, saida = self.rodar("memoria", "2", "--limpar")
        self.assertEqual(codigo, 0)
        self.assertIn("Memória 2 limpa (antes: SPL-EQ).", saida)
        self.assertIsNone(self.memorias()["dsp"]["2"])
        self.assertIn("## 12:04 · Memória 2 limpa", self.diario())
        codigo, saida = self.rodar("memoria", "2", "--limpar")
        self.assertEqual((codigo, "já estava vazia" in saida), (0, True))
        codigo, saida = self.rodar("preset", "rock", "--remover")
        self.assertEqual(codigo, 0)
        self.assertIn("Preset removido: rock.", saida)
        self.assertEqual([p["nome"] for p in self.memorias()["presets"]], ["Pop"])
        self.assertIn("## 12:06 · Preset do celular removido", self.diario())
        codigo, saida = self.rodar("preset", "Jazz", "--remover")
        self.assertEqual((codigo, "não estava registrado" in saida), (0, True))

    def test_gravacao_espera_leitor_do_windows_soltar_o_arquivo(self):
        chamadas = []
        real = sessao.os.replace

        def replace_teimoso(origem, destino):
            chamadas.append(1)
            if len(chamadas) < 3:
                raise PermissionError("em uso")
            return real(origem, destino)

        with mock.patch.object(sessao.os, "replace", replace_teimoso), \
                mock.patch.object(sessao.time, "sleep", lambda s: None):
            codigo, _ = self.rodar("preset", "Rock", "--por", "ia")
        self.assertEqual(codigo, 0)
        self.assertEqual(len(chamadas), 3)
        self.assertEqual(self.memorias()["presets"][0]["nome"], "Rock")

    def test_memorias_ilegivel_nao_grava(self):
        self.estado.mkdir(parents=True, exist_ok=True)
        (self.estado / "memorias.json").write_text("{", encoding="utf-8")
        codigo, saida = self.rodar("memoria", "1", "X", "--por", "ia")
        self.assertEqual(codigo, 1)
        self.assertIn("Nada foi gravado", saida)
        self.assertEqual((self.estado / "memorias.json").read_text(encoding="utf-8"), "{")

    def test_validar_completa_slots_e_descarta_lixo(self):
        dados = sessao.validar_memorias({"formato": 1, "dsp": {"2": {"nome": "SPL-EQ", "por": "ninguém"}},
                                         "presets": [None, {"nome": "Pop", "por": "ia"}]})
        self.assertEqual(dados["dsp"]["2"], {"nome": "SPL-EQ", "por": None, "hora": None})
        self.assertIsNone(dados["dsp"]["1"])
        self.assertEqual(dados["presets"], [{"nome": "Pop", "por": "ia", "hora": None}])
        with self.assertRaises(sessao.ProgressoIlegivel):
            sessao.validar_memorias({"formato": 2})
        with self.assertRaises(sessao.ProgressoIlegivel):
            sessao.validar_memorias({"formato": 1, "dsp": {"1": "SPL"}})


class ModoTest(Base):
    """sessao.py modo grava estado/memorias.json → modo_ativo, o que a Vitrine do painel mostra."""

    def memorias(self) -> dict:
        return json.loads((self.estado / "memorias.json").read_text(encoding="utf-8"))

    def test_modo_registrado_pela_ia_por_padrao(self):
        codigo, saida = self.rodar("modo", "Filme")
        self.assertEqual(codigo, 0)
        self.assertIn("Modo da Vitrine: Filme, por IA.", saida)
        self.assertEqual(self.memorias()["modo_ativo"],
                         {"nome": "Filme", "por": "ia", "hora": "2026-10-17T12:01:00"})
        self.assertIn("## 12:01 · Modo da Vitrine", self.diario())
        self.assertIn("- Filme (por IA)", self.diario())

    def test_nome_sem_acento_ou_sem_aspas_vira_o_canonico(self):
        self.assertEqual(self.rodar("modo", "musica", "spl")[0], 0)
        self.assertEqual(self.memorias()["modo_ativo"]["nome"], "Música High")
        codigo, saida = self.rodar("modo", "MÚSICA-EQ", "--por", "denis")
        self.assertEqual(codigo, 0)
        self.assertIn("(antes: Música High)", saida)
        self.assertEqual(self.memorias()["modo_ativo"]["nome"], "Música Original")
        self.assertEqual(self.memorias()["modo_ativo"]["por"], "denis")

    def test_nenhum_apaga_o_campo(self):
        self.rodar("modo", "Filme")
        codigo, saida = self.rodar("modo", "nenhum")
        self.assertEqual(codigo, 0)
        self.assertIn("apagado (antes: Filme)", saida)
        self.assertNotIn("modo_ativo", self.memorias())  # sem registro = sem o campo
        self.assertIsNone(sessao.carregar_memorias(self.estado)["modo_ativo"])
        codigo, saida = self.rodar("modo", "nenhum")
        self.assertEqual((codigo, "já estava sem modo" in saida), (0, True))

    def test_modo_desconhecido_nao_grava(self):
        for nome in ("Rock", "SPL HIGH", "Filme-EQ 2"):
            with self.subTest(nome=nome):
                codigo, saida = self.rodar("modo", nome)
                self.assertEqual(codigo, 1)
                self.assertIn("Modo desconhecido", saida)
        self.assertFalse((self.estado / "memorias.json").exists())
        with self.assertRaises(SystemExit) as ctx, contextlib.redirect_stderr(io.StringIO()):
            sessao.main(["modo", "Filme", "--por", "app"], estado=self.estado)
        self.assertEqual(ctx.exception.code, 1)

    def test_memorias_ilegivel_nao_grava_o_modo(self):
        self.estado.mkdir(parents=True, exist_ok=True)
        (self.estado / "memorias.json").write_text("{", encoding="utf-8")
        codigo, saida = self.rodar("modo", "Filme")
        self.assertEqual(codigo, 1)
        self.assertIn("Nada foi gravado", saida)
        self.assertEqual((self.estado / "memorias.json").read_text(encoding="utf-8"), "{")

    def test_memoria_e_preset_preservam_o_modo(self):
        self.rodar("modo", "Música High")
        self.rodar("memoria", "1", "SPL HIGH", "--por", "ia")
        self.rodar("preset", "Rock", "--por", "denis")
        dados = self.memorias()
        self.assertEqual(dados["modo_ativo"]["nome"], "Música High")
        self.assertEqual(dados["dsp"]["1"]["nome"], "SPL HIGH")
        # E o modo sobrevive a limpar a memória.
        self.rodar("memoria", "1", "--limpar")
        self.assertEqual(self.memorias()["modo_ativo"]["nome"], "Música High")

    def test_validar_ignora_modo_estranho_sem_perder_o_resto(self):
        for estranho in ({"nome": "Rock", "por": "ia"}, "Filme", {"por": "ia"}, {"nome": "nenhum"}):
            with self.subTest(estranho=estranho):
                dados = sessao.validar_memorias({"formato": 1, "dsp": {"1": {"nome": "SPL HIGH", "por": "ia"}},
                                                 "modo_ativo": estranho})
                self.assertIsNone(dados["modo_ativo"])
                self.assertEqual(dados["dsp"]["1"]["nome"], "SPL HIGH")
        dados = sessao.validar_memorias({"formato": 1, "modo_ativo": {"nome": "filme", "por": "x", "hora": 3}})
        self.assertEqual(dados["modo_ativo"], {"nome": "Filme", "por": None, "hora": None})

    def test_gravacoes_concorrentes_nao_disputam_o_temporario(self):
        import threading
        agora = dt.datetime(2026, 10, 17, 14, 0)
        erros = []

        def gravar(nome):
            try:
                for _ in range(15):
                    sessao.gravar_modo(self.estado, nome, "denis", agora)
            except Exception as erro:  # noqa: BLE001
                erros.append(erro)

        fios = [threading.Thread(target=gravar, args=(n,)) for n in ("Filme", "Música High", "Música Original")]
        for f in fios:
            f.start()
        for f in fios:
            f.join()
        self.assertEqual(erros, [])
        self.assertIn(self.memorias()["modo_ativo"]["nome"], sessao.MODOS)
        self.assertEqual(list(self.estado.glob("*.tmp")), [])

    def test_gravar_modo_anota_no_diario_com_quem_gravou(self):
        agora = dt.datetime(2026, 10, 17, 14, 0)
        sessao.gravar_modo(self.estado, "Música Original", "denis", agora)
        sessao.gravar_modo(self.estado, "nenhum", "ia", agora)
        diario = self.diario()
        self.assertIn("## 14:00 · Modo da Vitrine\n- Música Original (por Denis)", diario)
        self.assertIn("## 14:00 · Modo da Vitrine apagado\n- Antes: Música Original\n- Por IA", diario)

    def test_gravar_modo_devolve_novo_e_anterior(self):
        agora = dt.datetime(2026, 10, 17, 14, 0)
        novo, anterior = sessao.gravar_modo(self.estado, "Filme", "denis", agora)
        self.assertEqual((novo["nome"], novo["por"], anterior), ("Filme", "denis", None))
        novo, anterior = sessao.gravar_modo(self.estado, "nenhum", "denis", agora)
        self.assertEqual((novo, anterior["nome"]), (None, "Filme"))
        with self.assertRaises(sessao.ModoInvalido):
            sessao.gravar_modo(self.estado, "Rock", "denis", agora)
        with self.assertRaises(sessao.ModoInvalido):
            sessao.gravar_modo(self.estado, "Filme", "app", agora)
        with self.assertRaises(sessao.ModoInvalido):
            sessao.gravar_modo(self.estado, None, "denis", agora)


class RoteiroTest(unittest.TestCase):
    def test_passos_por_etapa_batem_com_os_titulos_do_roteiro(self):
        import re
        roteiro = (Path(__file__).resolve().parents[2] / ".claude" / "skills" / "regulagem"
                   / "references" / "roteiro.md").read_text(encoding="utf-8")
        titulos = {int(m.group(1)): m.group(2) for m in re.finditer(
            r"^## Etapa (\d+) · .*?\(M = ([^)]*)\)", roteiro, re.MULTILINE)}
        self.assertEqual(set(titulos), set(sessao.PASSOS_POR_ETAPA))
        for etapa, passos in sessao.PASSOS_POR_ETAPA.items():
            if etapa == 6:  # varia por gênero
                continue
            with self.subTest(etapa=etapa):
                m = re.match(r"(\d+)\s*(?:[:;]|$)", titulos[etapa])
                self.assertIsNotNone(m, titulos[etapa])
                self.assertEqual(int(m.group(1)), passos)


if __name__ == "__main__":
    unittest.main()
