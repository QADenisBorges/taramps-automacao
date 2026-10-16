"""Testes do protocolo_taramps com dados sintéticos (nenhum dado real do processador)."""

from __future__ import annotations

import io
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import protocolo_taramps as pt  # noqa: E402


def consulta(codigo: str, canal: int | None = None) -> pt.Consulta:
    return pt.Consulta(pt.POR_CODIGO[codigo], canal)


def casar(partes: list[bytes], c: pt.Consulta, final: bool = False):
    m = pt.Montador()
    for parte in partes:
        m.alimentar(parte)
    return m.casar(c, final=final)


class CatalogoTest(unittest.TestCase):
    def test_84_consultas_do_marco1(self):
        lista = pt.consultas_marco1()
        self.assertEqual(len(lista), 84)
        self.assertEqual(len({(c.codigo, c.canal) for c in lista}), 84)
        por_canal = [p for p in pt.CATALOGO if p.escopo == pt.ESCOPO_CANAL]
        self.assertEqual(len(por_canal), 16)

    def test_texto_das_consultas(self):
        self.assertEqual(consulta("080").texto, "#080#")
        self.assertEqual(consulta("081", 3).texto, "#081#3#")
        self.assertEqual(consulta("020", 14).texto, "020#14")
        self.assertEqual(consulta("220").texto, "#220#")

    def test_toda_consulta_passa_pela_trava(self):
        for c in pt.consultas_marco1():
            self.assertEqual(pt.verificar_envio(c.texto), c.texto.encode("ascii"))


class TravaTest(unittest.TestCase):
    def test_recusa_escrita(self):
        for texto in ("*081*0*0.0*", "*080*-3.0*", "020*3*1.5", "*004*", "*241*0*", "*252*1*",
                      "*001*AB12*CD34*", "*000*AB12*extra*", "#081#0#\n", "#081#0#-3.0#", "", "#81#"):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(texto)

    def test_lista_de_permissao(self):
        for texto in ("#251#", "#003#", "#000#", "#081#99#", "#081#4#", "#081#", "#080#0#",
                      "#005#1#", "020#99", "020#15", "#020#3#", "#020#"):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(texto)

    def test_recusa_codigos_perigosos_ate_como_consulta(self):
        for codigo in ("004", "241", "242", "240", "202", "252", "001"):
            with self.subTest(codigo=codigo):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(f"#{codigo}#")

    def test_aceita_consultas_e_autenticacao(self):
        for texto in ("#081#0#", "#092#3#", "#080#", "#220#", "020#0", "020#14", "*000*AB12*",
                      "#005#", "#111#", "#150#"):
            with self.subTest(texto=texto):
                self.assertEqual(pt.verificar_envio(texto), texto.encode("ascii"))

    def test_autenticacao_em_maiusculas(self):
        self.assertEqual(pt.montar_autenticacao("ab12"), "*000*AB12*")
        for ruim in ("abc", "abcde", "ab 1", "", "ab*1"):
            with self.assertRaises(ValueError):
                pt.montar_autenticacao(ruim)


class MontadorTest(unittest.TestCase):
    def test_separador_cerquilha_e_asterisco(self):
        for cru in (b"#081#0#-12.5#", b"*081*0*-12.5*"):
            with self.subTest(cru=cru):
                r = casar([cru], consulta("081", 0))
                self.assertEqual(r.valor, "-12.5")
                self.assertEqual(r.orfaos, [])

    def test_fragmentos_sao_juntados(self):
        c = consulta("071", 0)
        m = pt.Montador()
        m.alimentar(b"#071#0#-1")
        self.assertIsNone(m.casar(c))
        m.alimentar(b"2.5#")
        self.assertEqual(m.casar(c).valor, "-12.5")

    def test_fragmento_cortado_logo_depois_do_canal_espera(self):
        c = consulta("081", 2)
        m = pt.Montador()
        m.alimentar(b"#081#2#")
        self.assertIsNone(m.casar(c))  # pode ser o canal, com o valor a caminho
        m.alimentar(b"3.0#")
        self.assertEqual(m.casar(c).valor, "3.0")

    def test_resposta_sem_canal_aceita_no_fim_da_espera(self):
        c = consulta("081", 1)
        self.assertIsNone(casar([b"#081#-3.0#"], c))
        r = casar([b"#081#-3.0#"], c, final=True)
        self.assertEqual(r.valor, "-3.0")
        self.assertEqual(r.nota, "resposta sem canal")

    def test_orfao_antes_da_resposta(self):
        r = casar([b"#005#123#", b"#081#0#-1.0#"], consulta("081", 0))
        self.assertEqual(r.valor, "-1.0")
        self.assertEqual(r.orfaos, ["#005#123#"])

    def test_orfao_colado_no_mesmo_quadro(self):
        r = casar([b"#150#3##081#0#-1.0#"], consulta("081", 0))
        self.assertEqual(r.valor, "-1.0")
        self.assertEqual(r.orfaos, ["#150#3#"])

    def test_valor_que_parece_codigo_nao_quebra_o_quadro(self):
        r = casar([b"#040#0#100#"], consulta("040", 0))
        self.assertEqual(r.valor, "100")

    def test_quadros_separados_por_quebra_de_linha(self):
        r = casar([b"#111#1#\r\n#080#-3.0#\r\n"], consulta("080"))
        self.assertEqual(r.valor, "-3.0")
        self.assertEqual(r.orfaos, ["#111#1#"])

    def test_outro_canal_vira_orfao(self):
        c = consulta("081", 1)
        self.assertIsNone(casar([b"#081#0#-1.0#"], c))
        r = casar([b"#081#0#-1.0#"], c, final=True)
        self.assertIsNone(r.cru)
        self.assertEqual(r.orfaos, ["#081#0#-1.0#"])

    def test_eq_grafico_sem_separadores_nas_pontas(self):
        c = consulta("020", 3)
        self.assertIsNone(casar([b"020#3#-2.5"], c))  # espera o silêncio
        self.assertEqual(casar([b"020#3#-2.5"], c, final=True).valor, "-2.5")
        self.assertEqual(casar([b"#020#3#-2.5#"], c).valor, "-2.5")

    def test_eq_grafico_com_as_15_bandas(self):
        bandas = "#".join(f"{v:.1f}" for v in range(-7, 8))
        r = casar([f"#020#{bandas}#".encode()], consulta("020", 0))
        self.assertEqual(r.valor, "-7.0")
        self.assertEqual(r.nota, "a resposta trouxe as 15 bandas")

    def test_autenticacao(self):
        c = pt.Consulta(pt.AUTENTICACAO)
        self.assertEqual(casar([b"#000#ERRO#"], c).valor, "ERRO")
        self.assertIsNone(casar([b"#000#PRO24BT"], c))  # espera o resto
        r = casar([b"#000#PRO24BT", b" V1.07#"], c, final=True)
        self.assertEqual(r.valor, "PRO24BT V1.07")
        self.assertEqual(pt.firmware_de(r.valor), "1.07")

    def test_eco_da_consulta_nao_vira_valor(self):
        c = consulta("081", 0)
        r = casar([b"#081#0#", b"#081#0#-12.5#"], c)
        self.assertEqual(r.valor, "-12.5")
        self.assertEqual(r.orfaos, ["#081#0#"])
        so_eco = casar([b"#081#0#"], c, final=True)
        self.assertIsNone(so_eco.cru)
        self.assertIsNone(casar([b"020#3"], consulta("020", 3), final=True).cru)

    def test_eco_da_senha_nao_vira_sucesso(self):
        c = pt.Consulta(pt.AUTENTICACAO)
        m = pt.Montador()
        m.alimentar(b"*000*AB12*")
        self.assertIsNone(m.casar(c, final=True, enviado="*000*AB12*").cru)
        m.alimentar(b"*000*AB12*#000#ERRO#")
        self.assertEqual(m.casar(c, enviado="*000*AB12*").valor, "ERRO")

    def test_nada_chegou(self):
        m = pt.Montador()
        self.assertIsNone(m.casar(consulta("080")))
        r = m.casar(consulta("080"), final=True)
        self.assertIsNone(r.cru)
        self.assertEqual(r.orfaos, [])


class ConversaoTest(unittest.TestCase):
    def conv(self, codigo, texto):
        return pt.converter(pt.POR_CODIGO[codigo], texto)

    def test_numeros(self):
        self.assertEqual(self.conv("081", "-12.5"), {"valor": -12.5})
        self.assertEqual(self.conv("040", "80"), {"valor": 80})
        self.assertEqual(self.conv("073", "500"), {"valor": 500})

    def test_fora_da_faixa_sinalizado(self):
        self.assertTrue(self.conv("081", "20.0")["fora_da_faixa"])
        self.assertTrue(self.conv("040", "5")["fora_da_faixa"])
        self.assertTrue(self.conv("042", "16")["fora_da_faixa"])
        self.assertTrue(self.conv("060", "90")["fora_da_faixa"])
        self.assertTrue(self.conv("010", "c")["fora_da_faixa"])
        self.assertNotIn("fora_da_faixa", self.conv("081", "15.0"))

    def test_especiais_e_rotulos(self):
        self.assertEqual(self.conv("071", "0.5"), {"valor": 0.5, "rotulo": "OFF"})
        self.assertEqual(self.conv("042", "3"), {"valor": 3, "rotulo": "LR24"})
        self.assertEqual(self.conv("220", "255"), {"valor": 255, "rotulo": "Custom"})
        self.assertEqual(self.conv("010", "ab"), {"valor": "a+b", "rotulo": "A+B"})
        self.assertEqual(self.conv("060", "180"), {"valor": 180, "rotulo": "180°"})
        self.assertEqual(self.conv("072", "0.05")["rotulo"], "auto")

    def test_booleanos(self):
        self.assertEqual(self.conv("100", "1"), {"valor": True})
        self.assertEqual(self.conv("100", "2"), {"valor": True})  # ≠0 = mutado
        self.assertEqual(self.conv("070", "0"), {"valor": False})

    def test_ilegivel(self):
        self.assertEqual(self.conv("081", "abc"), {"valor": None, "motivo": "valor ilegível"})
        self.assertEqual(self.conv("081", "nan")["valor"], None)

    def test_leitura_no_formato_do_retrato(self):
        c = consulta("071", 0)
        item = pt.leitura(c, pt.Casamento("#071#0#-12.5#", "-12.5"))
        self.assertEqual(item, {"codigo": "071", "canal": 0, "nome": "limiter_threshold",
                                "valor": -12.5, "unidade": "dB", "cru": "#071#0#-12.5#"})
        falha = pt.leitura(c, None, "timeout")
        self.assertIsNone(falha["valor"])
        self.assertEqual(falha["motivo"], "timeout")


class MascaraTest(unittest.TestCase):
    def test_senha_mascarada_no_log(self):
        destino = io.StringIO()
        log = pt.LogBruto(destino, senha="ab12")
        log.registrar(pt.TX, b"*000*AB12*")
        log.registrar(pt.RX, b"#000#AB12 PRO 1.07#")  # eco hipotético da senha
        log.registrar(pt.TX, b"#081#0#")
        texto = destino.getvalue()
        self.assertIn("|*000*****|", texto)
        self.assertIn("2a 30 30 30 2a ** ** ** ** 2a", texto)
        self.assertNotIn("AB12", texto.upper())
        self.assertNotIn("41 42 31 32", texto)
        self.assertIn("23 30 38 31 23 30 23  |#081#0#|", texto)

    def test_mascara_sem_saber_a_senha(self):
        hexa, ascii_ = pt.mascarar(b"*000*ZZ99*")
        self.assertEqual(ascii_, "*000*****")
        self.assertNotIn("5a", hexa)

    def test_recusa_nao_mostra_a_senha(self):
        for texto in ("*000*AB12*x*", "*001*AB12*CD34*"):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado) as ctx:
                    pt.verificar_envio(texto)
                self.assertNotIn("AB12", str(ctx.exception))
                self.assertNotIn("CD34", str(ctx.exception))

    def test_troca_de_senha_esconde_as_duas(self):
        hexa, ascii_ = pt.mascarar(b"*001*AB12*CD34*")
        self.assertEqual(ascii_, "*001*" + "*" * 10)
        self.assertEqual(hexa, "2a 30 30 31 2a ** ** ** ** 2a ** ** ** ** 2a")

    def test_toda_ocorrencia_no_mesmo_quadro(self):
        _, ascii_ = pt.mascarar(b"*000*AB12**001*EF56*CD34*")
        for senha in ("AB12", "EF56", "CD34"):
            self.assertNotIn(senha, ascii_)

    def test_senha_em_qualquer_quadro_e_partida_entre_fragmentos(self):
        destino = io.StringIO()
        log = pt.LogBruto(destino, senha="ab12")
        log.registrar(pt.RX, b"#081#0#ab12#")  # sem prefixo de senha
        log.registrar(pt.RX, b"*000*")
        log.registrar(pt.RX, b"AB12*")  # eco que chegou no fragmento seguinte
        log.registrar(pt.RX, b"#000#xx A")
        log.registrar(pt.RX, b"B12#")  # senha partida: o fim é escondido pelo contexto
        texto = destino.getvalue()
        self.assertNotIn("AB12", texto.upper())
        self.assertNotIn("41 42 31 32", texto)
        self.assertNotIn("B12", texto.upper())
        self.assertNotIn("42 31 32", texto)


def retrato_sintetico(falhar: set[tuple[str, int | None]] = frozenset()) -> dict:
    leituras = []
    for c in pt.consultas_marco1():
        p = c.parametro
        exemplo = {"texto": "a", "bool": "1", "graus": "180", "indice": "3"}.get(p.tipo, "1.0")
        if p.codigo == "220":
            exemplo = "0"
        cas = None if (c.codigo, c.canal) in falhar else pt.Casamento(c.texto + exemplo + "#", exemplo)
        leituras.append(pt.leitura(c, cas, "timeout"))
    return {"leituras": leituras}


class ConferenciaTest(unittest.TestCase):
    def test_amostra_cobre_tipos_e_os_4_canais_nos_criticos(self):
        amostra = pt.amostra_conferencia(retrato_sintetico())
        self.assertGreaterEqual(len(amostra), 30)
        self.assertLessEqual(len(amostra), 36)
        self.assertEqual({x["tipo"] for x in amostra}, {p.tipo for p in pt.CATALOGO})
        criticos = {(x["codigo"], x["canal"]) for x in amostra if x["critico"]}
        esperado = {(p.codigo, c) for p in pt.CATALOGO if p.critico for c in pt.CANAIS}
        self.assertEqual(criticos, esperado)  # todo crítico nos 4 canais
        self.assertIn("020", {x["codigo"] for x in amostra})
        self.assertEqual(len({(x["codigo"], x["canal"]) for x in amostra}), len(amostra))
        telas = [pt.ORDEM_TELAS.index(x["tela"]) for x in amostra]
        self.assertEqual(telas, sorted(telas))
        for x in amostra:
            self.assertTrue(x["mostrado"])
            self.assertTrue(x["cru"])

    def test_amostra_pula_o_canal_que_falhou(self):
        amostra = pt.amostra_conferencia(retrato_sintetico({("081", 2)}))
        canais = {x["canal"] for x in amostra if x["codigo"] == "081"}
        self.assertEqual(canais, {0, 1, 3})

    def test_formatar_valor(self):
        self.assertEqual(pt.formatar_valor({"codigo": "081", "valor": -3.0, "unidade": "dB"}), "-3.0 dB")
        self.assertEqual(pt.formatar_valor({"codigo": "042", "valor": 3, "rotulo": "LR24"}), "LR24")
        self.assertEqual(pt.formatar_valor({"codigo": "100", "valor": True}), "mutado")
        self.assertEqual(pt.formatar_valor({"codigo": "070", "valor": False}), "desligado")
        self.assertEqual(pt.formatar_valor({"codigo": "080", "valor": None}), "sem leitura")
        self.assertEqual(pt.formatar_valor({"codigo": "071", "valor": -6.0, "unidade": "dB"}),
                         "-6.0 dB (no app: 1.00V)")


class EscritaTest(unittest.TestCase):
    """Montagem e validação de escrita, separadas da trava de leitura (MAPA §2 e §4)."""

    def test_formatos_do_fio(self):
        casos = [
            (("081", 0, -12.0), "*081*0*-12.0*"),
            (("081", 3, "6"), "*081*3*6.0*"),
            (("080", None, 0), "*080*0.0*"),
            (("080", None, -0.0), "*080*0.0*"),
            (("020", 3, 2.5), "020*3*2.5"),
            (("020", 14, -12), "020*14*-12.0"),
            (("042", 0, 11), "*042*0*11*"),
            (("040", 2, 35), "*040*2*35*"),
            (("010", 2, "A+B"), "*010*2*a+b*"),
            (("010", 1, "ab"), "*010*1*a+b*"),
            (("060", 3, 180), "*060*3*180*"),
            (("100", 1, True), "*100*1*1*"),
            (("070", 0, 0), "*070*0*0*"),
            (("071", 0, 0.5), "*071*0*0.5*"),  # OFF passa no formato; o guardião barra
            (("072", 2, 50), "*072*2*50.0*"),
            (("073", 2, 500), "*073*2*500*"),
            (("050", 0, "26,5"), "*050*0*26.5*"),
            (("032", None, 1.4), "*032*1.4*"),
            (("031", None, 63), "*031*63*"),
        ]
        for args, esperado in casos:
            with self.subTest(args=args):
                self.assertEqual(pt.montar_escrita(*args), esperado)
                self.assertEqual(pt.verificar_escrita(esperado), esperado.encode("ascii"))

    def test_valores_invalidos(self):
        for args in (("081", 4, -12.0), ("081", None, -12.0), ("080", 0, -3.0),
                     ("081", 0, -12.05), ("081", 0, 16), ("080", None, 1.0), ("220", None, 0),
                     ("000", None, "AB12"), ("241", 1, 1), ("072", 0, 0.05), ("040", 0, 10.5),
                     ("042", 0, 16), ("060", 0, 90), ("010", 0, "c"), ("100", 0, 2),
                     ("020", 15, 0.0), ("071", 0, 1.0), ("081", 0, True), ("081", 0, "x"),
                     ("081", 0, float("nan"))):
            with self.subTest(args=args):
                with self.assertRaises(pt.EscritaInvalida):
                    pt.montar_escrita(*args)

    def test_verificar_escrita_so_aceita_o_formato_exato(self):
        for texto in ("*081*0*-12*", "*081*0*+6.0*", "*004*", "*004*1*", "*241*1*", "*242*0*",
                      "*000*AB12*", "*001*AB12*CD34*", "*252*1*", "*202*1*", "*220*0*",
                      "#081#0#", "020*3*2.5*", "*020*3*2.5*", "*081*0*-12.0", "*081*0*-12.0**",
                      "*081*0*-12.0*\n", ""):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EscritaInvalida):
                    pt.verificar_escrita(texto)

    def test_trava_de_leitura_continua_recusando_escrita(self):
        for texto in ("*081*0*-12.0*", "020*3*2.5", "*042*0*11*"):
            with self.subTest(texto=texto):
                with self.assertRaises(pt.EnvioRecusado):
                    pt.verificar_envio(texto)

    def test_mesmo_valor(self):
        self.assertTrue(pt.mesmo_valor("081", -12.0, -12.0))
        self.assertTrue(pt.mesmo_valor("010", "a+b", "A+B"))
        self.assertTrue(pt.mesmo_valor("100", True, True))
        self.assertTrue(pt.mesmo_valor("040", 100, 100))
        self.assertFalse(pt.mesmo_valor("081", -12.0, -11.0))
        self.assertFalse(pt.mesmo_valor("081", -12.0, None))
        self.assertFalse(pt.mesmo_valor("072", 50.0, 50.1))
        self.assertFalse(pt.mesmo_valor("042", 11, 11.4))  # inteiro no fio: compara exato
        self.assertFalse(pt.mesmo_valor("040", 100, 100.4))
        self.assertTrue(pt.mesmo_valor("040", 100, 100.0))

    def test_releitura_ignora_eco_no_formato_de_escrita(self):
        c = pt.Consulta(pt.POR_CODIGO["081"], 0)
        m = pt.Montador()
        m.alimentar(b"*081*0*-12.0*\r\n#081#0#-11.0#\r\n")
        r = m.casar(c, sem_asterisco=True)
        self.assertEqual((r.valor, r.orfaos), ("-11.0", ["*081*0*-12.0*"]))
        m.alimentar(b"020*3*2.5\r\n")
        r = m.casar(pt.Consulta(pt.POR_CODIGO["020"], 3), final=True, sem_asterisco=True)
        self.assertIsNone(r.cru)
        self.assertEqual(r.orfaos, ["020*3*2.5"])


if __name__ == "__main__":
    unittest.main()
