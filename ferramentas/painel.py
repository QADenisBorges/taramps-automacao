#!/usr/bin/env python3
"""Painel BGS Sound Lab: a Vitrine (TV, tela cheia) e os Bastidores (o estado do PRO 2.4BT +
AUDIO numa tela só), no navegador.

Uso (na pasta do projeto):

    uv run ferramentas/painel.py
        Sobe em http://127.0.0.1:8765 e abre o navegador. Ctrl+C para parar.

    uv run ferramentas/painel.py --sem-navegador
        Só sobe o servidor (para testes e para abrir à mão).

O servidor é só biblioteca padrão. Ele lê a pasta estado/ (a última leitura em
estado/leituras/, as memórias e o modo da Vitrine em estado/memorias.json, o progresso em
estado/sessoes/progresso.json e os ajustes em estado/ajustes/). Dentro de estado/ ele só
escreve pelo POST /api/modo (o seletor de modo dos Bastidores): regrava estado/memorias.json
inteiro por sessao.gravar_modo (gravação atômica, com o modo_ativo novo e o resto como estava)
e anota "Modo da Vitrine" no diário do dia, em estado/sessoes/. Fora do projeto, grava o
arquivo da senha no login único. A leitura do aparelho é delegada ao CLI ler_processador.py,
em subprocesso. A página (painel/index.html, com os módulos painel/*.js) busca /api/estado a
cada 3 s e desenha tudo a partir dela.

Rotas:
    GET  /            a página (painel/index.html; sem CDN, sem fonte nem áudio de fora)
    GET  /<arquivo>   os arquivos de painel/ (.js, .css, .svg…), só os que estão direto na
                      pasta: nada de subir de pasta, caminho absoluto ou extensão desconhecida
    GET  /api/estado  o estado montado na hora a partir dos arquivos (inclui modo_ativo)
    POST /api/senha   login único: grava ~/.taramps/senha (4 caracteres, sem BOM nem quebra)
    POST /api/ler     roda ler_processador.py em subprocesso (só leitura), um por vez
    POST /api/modo    {"nome": "Filme" | "Música High" | "Música Original" | "nenhum"}: regrava
                      estado/memorias.json com modo_ativo = {nome, por: "denis", hora} ("nenhum"
                      apaga o campo) e anota no diário da sessão

Nunca escreve no processador nem expõe ajustar_processador.py, nem pelo /api/modo: o modo é só
o que a Vitrine diz. A senha nunca aparece em log, resposta, HTML ou console. Só escuta em localhost. Arquivo
corrompido em estado/ é ignorado: o servidor nunca cai por causa de um JSON inválido.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import os
import subprocess
import socket
import sys
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parent))

import guardiao as gd  # noqa: E402
import ler_processador as lp  # noqa: E402
import protocolo_taramps as pt  # noqa: E402
import sessao  # noqa: E402

RAIZ = lp.RAIZ
PASTA_PAINEL = Path(__file__).resolve().parent / "painel"
ARQUIVO_HTML = PASTA_PAINEL / "index.html"
PORTA = 8765
ANTIGA_MIN = 60  # leitura com mais de 1 h é "antiga"
ABERTO_MAX_MIN = 10  # registro "em andamento" parado há mais que isso não conta como aberto
MAX_AJUSTES = 30  # linhas da linha do tempo do agente
TIMEOUT_LER_S = 120  # o leitor leva ~13 s; preso além disso é cancelado
MAX_CORPO = 4096  # bytes de corpo aceitos num POST
PONTOS_POR_OITAVA = 12  # resolução das curvas desenhadas

# Arquivos que a rota estática serve de painel/, por extensão. O resto é 404.
TIPOS_ESTATICOS = {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".svg": "image/svg+xml",
    ".png": "image/png",
    ".ico": "image/x-icon",
}

# O "Ler agora" roda o leitor em subprocesso, com uv run (spec). Se o uv não estiver no PATH,
# cai para o Python atual (o mesmo ambiente, com o bleak).
COMANDO_LER = ["uv", "run", "ferramentas/ler_processador.py"]
COMANDO_LER_RESERVA = [sys.executable, "ferramentas/ler_processador.py"]

MENSAGENS_LER = {
    lp.SAIDA_OK: "Leitura nova.",
    lp.SAIDA_FALHAS: "Leitura com falhas ou erro de uso.",
    lp.SAIDA_SENHA: "Senha recusada. A senha guardada está certa?",
    lp.SAIDA_SEM_APARELHO: "Processador não encontrado. O celular ainda está conectado a ele?",
    lp.SAIDA_QUEDA: "A conexão caiu no meio. Tente de novo.",
    lp.SAIDA_SEM_RESPOSTA: "O processador não respondeu à senha. Tente de novo.",
}

# As 10 telas do app, na ordem do painel: (id, título, nome da tela no catálogo).
TELAS = (
    ("home", "Home", "Home"),
    ("roteamento", "Roteamento", "Roteam. Ent/Saída"),
    ("alinhamento", "Alinhamento", "Alinhamento"),
    ("fase", "Fase", "Fase"),
    ("eq_entrada", "EQ Param Entrada", "EQ Param Entr"),
    ("crossover", "Crossover", "Crossover"),
    ("limiter", "Limiter", "Limiter"),
    ("nivel", "Nível das Saídas", "Nível das Saídas"),
    ("eq_saida", "EQ Param Saída", "EQ Param Saída"),
    ("eq15", "EQ 15 Bandas", "EQ 15 Bandas"),
)
_TELA_POR_CATALOGO = {catalogo: ident for ident, _, catalogo in TELAS}

# Nome público, para os testes trocarem o caminho sem tocar na casa de verdade.
arquivo_senha = lp.arquivo_senha


def senha_disponivel() -> bool:
    """O leitor vai achar a senha sem perguntar: TARAMPS_SENHA ou o arquivo. Nunca lê o valor."""
    if os.environ.get("TARAMPS_SENHA"):
        return True
    try:
        return arquivo_senha().exists()
    except OSError:
        return False


# --------------------------------------------------------------------------- arquivos


def _relativo(caminho: Path) -> str:
    return lp.relativo(caminho)


def _ler_json(caminho: Path):
    """O JSON do arquivo, ou None se não existe, não abre ou está corrompido."""
    try:
        return json.loads(caminho.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def _hora(iso) -> str | None:
    quando = _quando(iso)
    return quando.strftime("%H:%M") if quando else None


def _quando(iso) -> dt.datetime | None:
    """datetime local sem fuso (um ISO com fuso é convertido para a hora local)."""
    if not isinstance(iso, str):
        return None
    try:
        quando = dt.datetime.fromisoformat(iso)
    except ValueError:
        return None
    if quando.tzinfo is not None:
        try:
            quando = quando.astimezone().replace(tzinfo=None)
        except (OverflowError, OSError, ValueError):
            return None
    return quando


# --------------------------------------------------------------------------- leitura


def ultimo_retrato(pasta: Path) -> tuple[str, dict] | None:
    """(nome do arquivo, retrato) da leitura mais recente que abre. Ignora as conferências e
    os arquivos corrompidos."""
    try:
        arquivos = sorted(pasta.glob("*.json"), key=lambda p: p.stem, reverse=True)
    except OSError:
        return None
    for caminho in arquivos:
        if caminho.name.endswith("-conferencia.json"):
            continue
        dados = _ler_json(caminho)
        if isinstance(dados, dict) and isinstance(dados.get("leituras"), list):
            return caminho.name, dados
    return None


def _volts(db) -> float | None:
    """V = 2,0 × 10^(dB/20), como o app mostra (MAPA §7). None para OFF (0.5)."""
    if isinstance(db, bool) or not isinstance(db, (int, float)) or db == 0.5:
        return None
    return round(2.0 * 10 ** (db / 20), 2)


def _item(x: dict) -> dict | None:
    """Uma leitura do retrato pronta para o painel: tela, rótulo e valor como o app mostra."""
    if not isinstance(x, dict):
        return None
    p = pt.POR_CODIGO.get(x.get("codigo"))
    if p is None:
        return None
    try:
        mostrado = pt.formatar_valor(dict(x, codigo=p.codigo))
    except (TypeError, ValueError):  # valor fora do tipo (ex.: texto onde era número)
        mostrado = str(x.get("valor"))
    item = {
        "codigo": p.codigo,
        "canal": x.get("canal"),
        "nome": p.nome,
        "rotulo_parametro": p.rotulo,
        "tela": _TELA_POR_CATALOGO.get(p.tela),
        "valor": x.get("valor"),
        "unidade": p.unidade,
        "mostrado": mostrado,
        "rotulo": x.get("rotulo"),
        "fora_da_faixa": bool(x.get("fora_da_faixa")),
    }
    if p.escopo == pt.ESCOPO_BANDA and isinstance(x.get("canal"), int) \
            and 0 <= x["canal"] < len(pt.BANDAS_HZ):
        item["banda_hz"] = pt.BANDAS_HZ[x["canal"]]
    if p.codigo == "071":
        v = x.get("valor")
        item["volts"] = _volts(v)
        if isinstance(v, (int, float)) and not isinstance(v, bool):
            item["mostrado"] = ("OFF" if v == 0.5 else f"{v:.1f} dB · {item['volts']:.2f} V"
                                if item["volts"] is not None else item["mostrado"])
    return item


def resumo_leitura(nome: str, retrato: dict, agora: dt.datetime) -> dict:
    """Cabeçalho da leitura: hora, idade, firmware, completa ou não."""
    fim = _quando(retrato.get("fim")) or _quando(retrato.get("inicio"))
    idade = None
    if fim is not None:
        idade = max(0, int((agora - fim).total_seconds() // 60))
    status = retrato.get("status")
    leituras = [x for x in retrato.get("leituras", []) if isinstance(x, dict)]
    lidos = sum(1 for x in leituras if x.get("valor") is not None)
    total = retrato.get("total") if isinstance(retrato.get("total"), int) else len(leituras)
    incompleta = status != "completo" or lidos < total
    dispositivo = retrato.get("dispositivo") if isinstance(retrato.get("dispositivo"), dict) else {}
    return {
        "arquivo": f"estado/leituras/{nome}",
        "inicio": retrato.get("inicio"),
        "fim": retrato.get("fim"),
        "hora": _hora(retrato.get("fim")) or _hora(retrato.get("inicio")),
        "data": fim.strftime("%d/%m") if fim else None,
        "idade_min": idade,
        "antiga": idade is not None and idade > ANTIGA_MIN,
        "incompleta": incompleta,
        "motivo": retrato.get("motivo"),
        "status": status,
        "lidos": lidos,
        "total": total,
        "firmware": retrato.get("firmware"),
        "aparelho": retrato.get("aparelho") or "PRO 2.4BT + AUDIO",
        "dispositivo": {"nome": dispositivo.get("nome"), "endereco": dispositivo.get("endereco")},
    }


# --------------------------------------------------------------------------- curvas


def _grade_curvas() -> list[float]:
    n = math.ceil(math.log2(gd.F_GRADE_MAX / gd.F_GRADE_MIN) * PONTOS_POR_OITAVA)
    return [round(gd.F_GRADE_MIN * (gd.F_GRADE_MAX / gd.F_GRADE_MIN) ** (i / n), 3)
            for i in range(n + 1)]


def _numero(v) -> float | None:
    if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v):
        return None
    return float(v)


def curvas(valores: dict) -> dict:
    """Curvas em dB (mesma fórmula do guardião) para o crossover e os EQs de cada saída, o
    EQ de entrada e as 15 bandas. `valores` = {(código, canal): valor}. Saída que não tem os
    valores todos fica sem curva (None)."""
    freqs = _grade_curvas()

    def v(codigo, canal=None):
        return _numero(valores.get((codigo, canal)))

    crossover: dict[str, list | None] = {}
    eq_saida: dict[str, list | None] = {}
    for c in pt.CANAIS:
        hi, hf, li, lf = v("042", c), v("040", c), v("043", c), v("041", c)
        if None in (hi, hf, li, lf) or int(hi) not in pt.FILTROS or int(li) not in pt.FILTROS \
                or hf <= 0 or lf <= 0:
            crossover[str(c)] = None
        else:
            crossover[str(c)] = [round(gd.ganho_filtro(int(hi), hf, f, True)
                                       + gd.ganho_filtro(int(li), lf, f, False), 2) for f in freqs]
        f0, g, q = v("090", c), v("091", c), v("092", c)
        if None in (f0, g, q) or f0 <= 0 or q <= 0:
            eq_saida[str(c)] = None
        else:
            eq_saida[str(c)] = [round(gd.ganho_pico(f0, g, q, f), 2) for f in freqs]

    f0, g, q = v("031"), v("030"), v("032")
    eq_entrada = None
    if None not in (f0, g, q) and f0 > 0 and q > 0:
        eq_entrada = [round(gd.ganho_pico(f0, g, q, f), 2) for f in freqs]

    bandas = [v("020", b) for b in range(len(pt.BANDAS_HZ))]
    eq15 = None
    if None not in bandas:
        eq15 = [round(sum(gd.ganho_pico(hz, g, gd.Q_BANDAS, f)
                          for hz, g in zip(pt.BANDAS_HZ, bandas) if g), 2) for f in freqs]
    return {"freqs": freqs, "crossover": crossover, "eq_saida": eq_saida,
            "eq_entrada": eq_entrada, "eq15": eq15}


# --------------------------------------------------------------------------- guardião


def violacoes(retrato: dict | None, estado: Path) -> dict:
    """Regra 6 (por saída), tetos do limiter e a lista das violações do último retrato, pelas
    checagens do guardião.

    `regra6` é True só com a regra ligada em todas as saídas (False com alguma desligada);
    `regra6_saidas` traz cada uma. Em `tetos`, o `efetivo` é o teto que vale na leitura atual
    (o de demonstração só com o corte e a rota amarrados), ou None sem leitura avaliável."""
    saida: dict = {"regra6": None, "regra6_saidas": None, "tetos": None, "violacoes": None,
                   "motivo": None, "config": None}
    try:
        cfg = gd.carregar_config(estado / "guardiao.json")
    except gd.ConfigInvalida as erro:
        saida["motivo"] = f"guardiao.json: {erro}"
        return saida
    saida["regra6_saidas"] = {str(s.numero): s.regra6 for s in cfg.saidas}
    saida["regra6"] = all(s.regra6 for s in cfg.saidas)
    saida["tetos"] = {
        str(s.numero): {"base": s.teto_limiter,
                        "demo": s.teto_demo.teto_limiter if s.teto_demo else None,
                        "condicoes": s.teto_demo.condicoes() if s.teto_demo else None,
                        "efetivo": None, "demo_valendo": None}
        for s in cfg.saidas}
    saida["config"] = _relativo(estado / "guardiao.json")
    if retrato is None:
        saida["motivo"] = "sem leitura"
        return saida
    try:
        lido = gd.estado_do_retrato(retrato)
        checagens = gd.checar(lido, cfg)
    except (gd.EstadoInvalido, KeyError, TypeError, ValueError) as erro:
        saida["motivo"] = str(erro) or "leitura que o guardião não consegue avaliar"
        return saida
    for s in cfg.saidas:
        vale = gd.demo_valendo(s, lido)
        saida["tetos"][str(s.numero)].update(efetivo=gd.teto_efetivo(s, lido), demo_valendo=vale)
    lista = [
        {"id": c.id, "regra": c.regra, "rotulo": gd.ROTULOS.get(c.regra, f"regra {c.regra}"),
         "saida": c.saida, "sev": round(c.sev, 2), "motivo": c.motivo}
        for c in checagens.values() if c.sev > gd.EPS
    ]
    lista.sort(key=lambda x: (x["regra"], x["saida"] or 0, x["id"]))
    saida["violacoes"] = lista
    return saida


# --------------------------------------------------------------------------- memórias e agente


def memorias(estado: Path) -> dict:
    """estado/memorias.json (mantido pelo sessao.py). Corrompido ou ausente = vazio."""
    try:
        return sessao.carregar_memorias(estado)
    except sessao.ProgressoIlegivel:
        vazio = sessao.memorias_vazias()
        vazio["erro"] = "memorias.json ilegível"
        return vazio


def progresso(estado: Path) -> dict | None:
    pasta = estado / "sessoes"
    try:
        dados = sessao.carregar(pasta, avisar=lambda *_: None)
    except sessao.ProgressoIlegivel:
        return None
    etapa, passo, nota = sessao.retomada(dados)
    return {
        "etapa": dados["etapa"],
        "passo": dados["passo"],
        "travou": dados["travou"],
        "atualizado": dados["atualizado"],
        "hora": _hora(dados["atualizado"]),
        "passos_feitos": len(dados["passos_feitos"]),
        "retomar": {"etapa": etapa, "passo": passo, "nota": nota,
                    "total": sessao.PASSOS_POR_ETAPA.get(etapa)},
        "medidas": len(dados["medidas"]),
        "ideias": [i.get("texto") for i in dados["ideias"] if isinstance(i, dict)],
    }


def _lista(valor) -> list:
    return [x for x in valor if x is not None] if isinstance(valor, list) else []


# --------------------------------------------------------------------------- frases da linha do tempo

_MENOS = "−"  # sinal de menos tipográfico, como no resto do painel
_FRASE_ROTULO = {
    "080": "Master", "081": "Ganho", "040": "Passa-alta", "041": "Passa-baixa",
    "050": "Atraso", "060": "Fase", "072": "Attack do limiter", "073": "Release do limiter",
    "090": "Frequência do EQ", "091": "Ganho do EQ", "092": "Q do EQ", "010": "Roteamento",
    "030": "Ganho do EQ de entrada", "031": "Frequência do EQ de entrada",
    "032": "Q do EQ de entrada",
}
_COM_SINAL = {"091", "030", "020"}  # ganhos de EQ: o "+" ajuda a ler o reforço
_VERDADE = {"1", "true", "on", "sim", "ligado"}
_FALSO = {"0", "false", "off", "nao", "não", "desligado"}  # fora das duas listas: o texto cru
_ROTEAMENTO_POR_TEXTO = {"a": "A", "b": "B", "a+b": "A+B", "ab": "A+B"}
_FILTRO_POR_NOME = {nome.lower(): i for i, nome in pt.FILTROS.items()}


def _num_br(v: float, casas: int = 1, sinal: bool = False) -> str:
    """Número com vírgula decimal e o sinal de menos tipográfico ("−12,0")."""
    texto = f"{abs(v):.{casas}f}"
    zero = float(texto) == 0
    prefixo = _MENOS if v < 0 and not zero else ("+" if sinal and v > 0 and not zero else "")
    return prefixo + texto.replace(".", ",")


def _valor_frase(p: pt.Parametro, numero: float) -> str:
    especial = (p.especiais or {}).get(numero)
    if especial is not None:  # ex.: attack 0,01 / 0,05 = "auto"
        return str(especial)
    casas = 0 if p.casas == 0 and numero.is_integer() else max(p.casas, 1)
    texto = _num_br(numero, casas, sinal=p.codigo in _COM_SINAL)
    unidade = "°" if p.unidade == "graus" else (f" {p.unidade}" if p.unidade else "")
    return f"{texto}{unidade}"


def frase(texto) -> str:
    """Uma escrita do lote em português simples, para a linha do tempo da KARINA:
    "081:0=-12.0" → "Ganho da saída 1 para −12,0 dB". Fora do formato, devolve o texto."""
    texto = str(texto)
    try:
        pedido = gd.interpretar(texto)
    except gd.PedidoInvalido:
        return texto
    p = pt.POR_CODIGO.get(pedido.codigo)
    if p is None or pedido.valor is None:
        return texto
    c = pedido.canal
    saida = f" da saída {c + 1}" if p.escopo == pt.ESCOPO_CANAL and isinstance(c, int) else ""
    bruto = str(pedido.valor).strip()
    try:
        if p.codigo in ("100", "070"):
            if bruto.lower() not in _VERDADE | _FALSO:
                return texto  # valor estranho: sem frase confiante
            ligado = bruto.lower() in _VERDADE
            if p.codigo == "100":
                return f"Saída {c + 1} {'mutada' if ligado else 'com som'}" if saida else texto
            return f"Limiter automático{saida} {'ligado' if ligado else 'desligado'}"
        if p.codigo == "010":
            rota = _ROTEAMENTO_POR_TEXTO.get(bruto.lower().replace(" ", ""))
            return f"Roteamento{saida} para {rota}" if rota else texto
        if p.codigo in ("042", "043"):
            qual = "Passa-alta" if p.codigo == "042" else "Passa-baixa"
            indice = _FILTRO_POR_NOME.get(bruto.lower())
            if indice is None:
                numero = pt.ler_numero(bruto)
                if not numero.is_integer() or int(numero) not in pt.FILTROS:
                    return texto  # índice que o aparelho não tem
                indice = int(numero)
            if indice == 0:
                return f"{qual}{saida} desligado"
            return f"Filtro do {qual.lower()}{saida} para {pt.FILTROS[indice]}"
        numero = pt.ler_numero(bruto)
        if p.codigo == "071":
            if numero == 0.5:
                return f"Limiter{saida} desligado"
            return (f"Limiter{saida} para {_num_br(numero)} dB "
                    f"({_num_br(2.0 * 10 ** (numero / 20), 2)} V)")
        if p.codigo == "020":
            if not isinstance(c, int) or not 0 <= c < len(pt.BANDAS_HZ):
                return texto
            return f"Banda de {pt.BANDAS_HZ[c]} Hz para {_valor_frase(p, numero)}"
        rotulo = _FRASE_ROTULO.get(p.codigo, p.rotulo)
        return f"{rotulo}{saida} para {_valor_frase(p, numero)}"
    except (pt.EscritaInvalida, ValueError, TypeError, OverflowError):
        return texto


def _ajuste(nome: str, dados: dict, agora: dt.datetime) -> dict:
    decisao = dados.get("decisao") if isinstance(dados.get("decisao"), dict) else None
    aprovado = decisao.get("aprovado") if decisao else None
    recusas, recusas_det = [], []
    if decisao:
        for r in _lista(decisao.get("recusas")):
            if isinstance(r, dict):
                item = f"{r.get('item')} · " if r.get("item") else ""
                recusas.append(f"{item}{r.get('rotulo') or ''}: {r.get('motivo') or ''}".strip())
                recusas_det.append({campo: str(r[campo]) if r.get(campo) is not None else None
                                    for campo in ("item", "rotulo", "motivo")})
    status = dados.get("status") if isinstance(dados.get("status"), str) else None
    fim = dados.get("fim") if isinstance(dados.get("fim"), str) else None
    em_andamento = dados.get("em_andamento") if isinstance(dados.get("em_andamento"), str) else None
    aberto = fim is None or status in (None, "em andamento")
    if aberto:
        # Segunda guarda: um "em andamento" parado há mais de 10 min é um processo que morreu
        # sem gravar o fim (o registro é regravado a cada passo enquanto roda).
        marca = _quando(fim) or _quando(dados.get("inicio"))
        if marca is not None and (agora - marca).total_seconds() > ABERTO_MAX_MIN * 60:
            aberto = False
            status = "interrompido?"
    inicio = _quando(dados.get("inicio"))
    lote = [str(x) for x in _lista(dados.get("lote"))]
    return {
        "arquivo": f"estado/ajustes/{nome}",
        "inicio": dados.get("inicio") if isinstance(dados.get("inicio"), str) else None,
        "hora": _hora(dados.get("inicio")),
        "data": inicio.strftime("%d/%m") if inicio else None,
        "fim": fim,
        "simular": bool(dados.get("simular")),
        "lote": lote,
        "frases": [frase(x) for x in lote],
        "status": status,
        "decisao": None if aprovado is None else ("APROVADO" if aprovado else "RECUSADO"),
        "ordem": [str(x) for x in _lista(decisao.get("ordem") if decisao else None)],
        "aplicados": [str(x) for x in _lista(dados.get("aplicados"))],
        "nao_aplicados": [str(x) for x in _lista(dados.get("nao_aplicados"))],
        "recusas": recusas,
        "recusas_det": recusas_det,
        "alertas": len(_lista(decisao.get("alertas") if decisao else None)),
        "em_andamento": em_andamento,
        "aberto": aberto,
    }


def ajustes(estado: Path, agora: dt.datetime | None = None) -> list[dict]:
    """Os ajustes mais recentes primeiro, já resumidos. Arquivo corrompido é ignorado."""
    agora = agora or dt.datetime.now()
    pasta = estado / "ajustes"
    try:  # pelo nome sem extensão: "2021-2" vem depois de "2021"
        arquivos = sorted(pasta.glob("*.json"), key=lambda p: p.stem, reverse=True)
    except OSError:
        return []
    lista = []
    for caminho in arquivos:
        dados = _ler_json(caminho)
        if isinstance(dados, dict) and "lote" in dados:
            try:
                lista.append(_ajuste(caminho.name, dados, agora))
            except (TypeError, ValueError, AttributeError):
                continue  # registro fora do formato: ignorado, como um JSON corrompido
        if len(lista) >= MAX_AJUSTES:
            break
    return lista


def ajuste_aberto(estado: Path, agora: dt.datetime | None = None) -> dict | None:
    """O registro de ajuste em curso, se houver (o agente está escrevendo no aparelho)."""
    return next((a for a in ajustes(estado, agora) if a["aberto"]), None)


# --------------------------------------------------------------------------- leitor em subprocesso


class Leitor:
    """Roda ler_processador.py num subprocesso, um por vez, e guarda o resultado."""

    def __init__(self, comando: list[str] | None = None, cwd: Path | None = None,
                 reserva: list[str] | None = None):
        self.comando = list(comando or COMANDO_LER)
        self.reserva = list(reserva) if reserva is not None else list(COMANDO_LER_RESERVA)
        self.cwd = cwd or RAIZ
        self._lock = threading.Lock()
        self._thread: threading.Thread | None = None
        self.inicio: dt.datetime | None = None
        self.resultado: dict | None = None

    @property
    def lendo(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def iniciar(self) -> bool:
        """True se começou; False se já havia uma leitura rodando."""
        with self._lock:
            if self.lendo:
                return False
            self.inicio = dt.datetime.now()
            self.resultado = None
            self._thread = threading.Thread(target=self._rodar, daemon=True)
            self._thread.start()
            return True

    def esperar(self, timeout: float | None = None) -> None:
        if self._thread is not None:
            self._thread.join(timeout)

    def _executar(self, comando: list[str]) -> subprocess.CompletedProcess:
        ambiente = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUTF8="1")
        return subprocess.run(
            comando, cwd=str(self.cwd), stdin=subprocess.DEVNULL, capture_output=True,
            text=True, encoding="utf-8", errors="replace", env=ambiente, timeout=TIMEOUT_LER_S)

    @staticmethod
    def _ultima_linha(texto) -> str:
        linhas = [l.strip() for l in (texto or "").splitlines() if l.strip()]
        return linhas[-1][:200] if linhas else ""

    def _rodar(self) -> None:
        inicio = dt.datetime.now()
        try:
            try:
                proc = self._executar(self.comando)
            except FileNotFoundError:
                if not self.reserva:
                    raise
                proc = self._executar(self.reserva)
            codigo = proc.returncode
            ultima = self._ultima_linha(proc.stdout) or self._ultima_linha(proc.stderr)
            mensagem = MENSAGENS_LER.get(codigo, f"O leitor saiu com código {codigo}.")
        except subprocess.TimeoutExpired:
            codigo, ultima = None, ""
            mensagem = "O leitor travou; cancelei."
        except Exception as erro:  # noqa: BLE001 - uv ausente, permissão etc.
            codigo, ultima = None, ""
            mensagem = f"Não consegui rodar o leitor: {type(erro).__name__}."
        self.resultado = {
            "codigo": codigo,
            "ok": codigo == lp.SAIDA_OK,
            "mensagem": mensagem,
            "detalhe": ultima,
            "inicio": inicio.isoformat(timespec="seconds"),
            "fim": dt.datetime.now().isoformat(timespec="seconds"),
            "segundos": int((dt.datetime.now() - inicio).total_seconds()),
        }

    def situacao(self, agora: dt.datetime | None = None) -> dict:
        agora = agora or dt.datetime.now()
        lendo = self.lendo
        segundos = int((agora - self.inicio).total_seconds()) if lendo and self.inicio else 0
        return {"lendo": lendo, "segundos": max(0, segundos), "resultado": self.resultado}


# --------------------------------------------------------------------------- estado completo


def montar_estado(estado: Path, *, agora: dt.datetime | None = None,
                  leitor: Leitor | None = None, senha_guardada: bool | None = None) -> dict:
    """Tudo o que a página desenha, lido na hora dos arquivos de estado/."""
    agora = agora or dt.datetime.now()
    problemas: list[str] = []

    def secao(nome: str, funcao, padrao):
        """Uma seção com dado estranho degrada sozinha: as outras continuam."""
        try:
            return funcao()
        except Exception as erro:  # noqa: BLE001 - nunca derrubar o /api/estado
            problemas.append(f"{nome}: {type(erro).__name__}")
            return padrao

    def leituras_():
        achado = ultimo_retrato(estado / "leituras")
        if achado is None:
            return None, None, [], {}
        nome, retrato = achado
        itens, valores = [], {}
        for x in retrato.get("leituras", []):
            item = _item(x)
            if item is not None:
                itens.append(item)
                valores[(item["codigo"], item["canal"])] = item["valor"]
        return resumo_leitura(nome, retrato, agora), retrato, itens, valores

    leitura, retrato, itens, valores = secao("leituras", leituras_, (None, None, [], {}))
    telas = [{"id": ident, "titulo": titulo, "itens": [x for x in itens if x["tela"] == ident]}
             for ident, titulo, _ in TELAS]
    lista_ajustes = secao("ajustes", lambda: ajustes(estado, agora), [])
    if senha_guardada is None:
        senha_guardada = senha_disponivel()
    situacao = leitor.situacao(agora) if leitor else {"lendo": False, "segundos": 0,
                                                       "resultado": None}
    mem = secao("memorias", lambda: memorias(estado), sessao.memorias_vazias())
    return {
        "agora": agora.isoformat(timespec="seconds"),
        "senha_guardada": bool(senha_guardada),
        "nome": "Denis Borges",
        "sistema": "Home Theater 2.1",
        "leitura": leitura,
        "telas": telas,
        "curvas": secao("curvas", lambda: curvas(valores) if retrato is not None else None, None),
        "guardiao": secao("guardiao", lambda: violacoes(retrato, estado),
                          {"regra6": None, "regra6_saidas": None, "tetos": None, "violacoes": None,
                           "motivo": "sem avaliação", "config": None}),
        "memorias": mem,
        # O modo da Vitrine ({nome, por, hora}), declarado pela /regulagem ou pelo seletor dos
        # Bastidores. None = sem registro: a Vitrine diz só "Som ajustado pela KARINA".
        "modo_ativo": mem.get("modo_ativo") if isinstance(mem, dict) else None,
        "agente": {
            "progresso": secao("progresso", lambda: progresso(estado), None),
            "ajustes": lista_ajustes,
            "em_andamento": next((a for a in lista_ajustes if a["aberto"]), None),
            "lendo": situacao["lendo"],
        },
        "leitor": situacao,
        "problemas": problemas,
    }


# --------------------------------------------------------------------------- HTTP


_HOSTS_LOCAIS = {"localhost", "127.0.0.1", "[::1]", "::1"}
_IPS_LOCAIS = {"127.0.0.1", "::1"}


def arquivo_estatico(pasta: Path, caminho_url: str) -> Path | None:
    """O arquivo de painel/ pedido por GET /<nome>, ou None. Só nomes simples, direto na pasta,
    com extensão de TIPOS_ESTATICOS: nada de "..", barras invertidas, unidade (C:), caminho
    absoluto, subpasta ou arquivo oculto, mesmo codificados em %xx."""
    nome = unquote(caminho_url or "")
    if not nome.startswith("/"):
        return None
    nome = nome[1:]
    if (not nome or "/" in nome or "\\" in nome or ":" in nome or "\x00" in nome
            or nome.startswith(".") or nome != nome.strip()):
        return None
    if Path(nome).suffix.lower() not in TIPOS_ESTATICOS:
        return None
    try:
        base = pasta.resolve()
        alvo = (base / nome).resolve()
    except (OSError, ValueError, RuntimeError):
        return None
    if alvo.parent != base or not alvo.is_file():
        return None
    return alvo


class _Handler(BaseHTTPRequestHandler):
    server_version = "painel/1"
    sys_version = ""
    protocol_version = "HTTP/1.1"
    timeout = 10  # conexão keep-alive parada não prende a thread para sempre

    def log_message(self, fmt, *args):  # noqa: D401 - silencioso: o polling é a cada 3 s
        return

    # ---- respostas

    def _responder(self, codigo: int, corpo: bytes, tipo: str) -> None:
        self.send_response(codigo)
        self.send_header("Content-Type", tipo)
        self.send_header("Content-Length", str(len(corpo)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(corpo)

    def _json(self, codigo: int, dados) -> None:
        corpo = json.dumps(dados, ensure_ascii=False).encode("utf-8")
        self._responder(codigo, corpo, "application/json; charset=utf-8")

    def _html(self) -> None:
        try:
            corpo = self.server.html.read_bytes()
        except OSError:
            self._responder(500, "painel/index.html não encontrado".encode("utf-8"),
                            "text/plain; charset=utf-8")
            return
        self._responder(200, corpo, "text/html; charset=utf-8")

    def _estatico(self, caminho: str) -> None:
        alvo = arquivo_estatico(self.server.html.parent, caminho)
        if alvo is None:
            if caminho == "/favicon.ico":  # o navegador pede sozinho; sem ícone não é erro
                self._responder(204, b"", "image/x-icon")
                return
            self._json(404, {"erro": "não existe"})
            return
        try:
            corpo = alvo.read_bytes()
        except OSError:
            self._json(404, {"erro": "não existe"})
            return
        self._responder(200, corpo, TIPOS_ESTATICOS[alvo.suffix.lower()])

    def _cliente_local(self) -> bool:
        """O pedido veio do próprio notebook? Com --rede, os outros aparelhos só olham."""
        return (self.client_address[0] or "") in _IPS_LOCAIS

    def _origem_ok(self) -> bool:
        """POST só da própria página: sem Origin (ferramentas locais) ou Origin em localhost."""
        origem = self.headers.get("Origin")
        if not origem:
            return True
        try:
            host = urlsplit(origem).hostname or ""
        except ValueError:
            return False
        return host in _HOSTS_LOCAIS

    def _corpo_json(self):
        try:
            tamanho = int(self.headers.get("Content-Length") or 0)
        except ValueError:
            return None
        if tamanho < 0 or tamanho > MAX_CORPO:
            return None
        bruto = self.rfile.read(tamanho) if tamanho else b"{}"
        try:
            dados = json.loads(bruto.decode("utf-8"))
        except (UnicodeDecodeError, ValueError):
            return None
        return dados if isinstance(dados, dict) else None

    # ---- rotas

    def do_HEAD(self):  # noqa: N802 - nome do BaseHTTPRequestHandler
        self.do_GET()

    def do_GET(self):  # noqa: N802
        caminho = urlsplit(self.path).path
        if caminho in ("/", "/index.html"):
            self._html()
        elif caminho == "/api/estado":
            try:
                dados = montar_estado(self.server.estado, leitor=self.server.leitor)
            except Exception as erro:  # noqa: BLE001 - nunca derrubar o servidor
                self._json(500, {"erro": f"não consegui montar o estado: {type(erro).__name__}"})
                return
            if not self._cliente_local():
                # Outro aparelho na rede: só acompanha. Nunca vê o login nem dispara nada.
                dados = dict(dados, somente_leitura=True, senha_guardada=True)
            self._json(200, dados)
        elif caminho.startswith("/api/"):
            self._json(404, {"erro": "não existe"})
        else:
            self._estatico(caminho)

    def do_POST(self):  # noqa: N802
        caminho = urlsplit(self.path).path
        if not self._cliente_local():
            self._json(403, {"ok": False, "erro": "na rede o painel é só para acompanhar"})
            return
        if not self._origem_ok():
            self._json(403, {"ok": False, "erro": "só a própria página pode chamar"})
            return
        if caminho not in ("/api/senha", "/api/ler", "/api/modo"):
            self._json(404, {"ok": False, "erro": "não existe"})
            return
        dados = self._corpo_json()
        if dados is None:
            self._json(400, {"ok": False, "erro": "corpo inválido"})
            return
        if caminho == "/api/senha":
            self._senha(dados)
        elif caminho == "/api/modo":
            self._modo(dados)
        else:
            self._ler()

    def _modo(self, dados: dict) -> None:
        """O seletor de modo dos Bastidores: só o campo modo_ativo de estado/memorias.json.
        Não toca o processador."""
        nome = dados.get("nome")
        try:
            with self.server.trava_modo:
                novo, _ = sessao.gravar_modo(self.server.estado, nome, "denis", dt.datetime.now())
        except sessao.ProgressoIlegivel:
            self._json(409, {"ok": False, "erro": "Não consegui ler as memórias; nada foi gravado."})
            return
        except sessao.ModoInvalido:
            self._json(400, {"ok": False,
                             "erro": "Modo inválido: Filme, Música High, Música Original ou nenhum."})
            return
        except OSError:
            self._json(500, {"ok": False, "erro": "Não consegui gravar o modo."})
            return
        self._json(200, {"ok": True, "modo_ativo": novo})

    def _senha(self, dados: dict) -> None:
        senha = dados.get("senha")
        senha = senha.strip() if isinstance(senha, str) else ""
        try:
            pt.montar_autenticacao(senha)
        except ValueError:
            self._json(400, {"ok": False, "erro": "A senha tem 4 letras ou números."})
            return
        try:
            gravar_senha(senha)
        except FileExistsError:
            self._json(409, {"ok": False, "erro": "Senha já guardada."})
            return
        except OSError:
            self._json(500, {"ok": False, "erro": "Não consegui gravar o arquivo da senha."})
            return
        self._json(200, {"ok": True})

    def _ler(self) -> None:
        leitor = self.server.leitor
        if leitor is None:
            self._json(503, {"ok": False, "erro": "leitor desligado"})
            return
        if not senha_disponivel():  # o leitor pediria a senha no terminal e ficaria preso
            self._json(409, {"ok": False, "erro": "sem senha guardada: faça o login primeiro"})
            return
        try:
            aberto = ajuste_aberto(self.server.estado)
        except Exception:  # noqa: BLE001 - na dúvida, não bloqueia a leitura
            aberto = None
        if aberto is not None:
            self._json(409, {"ok": False, "erro": "o agente está ajustando o aparelho; espere acabar"})
            return
        if not leitor.iniciar():
            self._json(409, {"ok": False, "erro": "já estou lendo"})
            return
        self._json(200, {"ok": True})


def ips_da_rede() -> list[str]:
    """IPs IPv4 do notebook na rede local (para o link do celular), sem 127.x."""
    ips: list[str] = []
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect(("10.255.255.255", 1))  # não envia nada; só escolhe a interface
            ips.append(s.getsockname()[0])
    except OSError:
        pass
    try:
        ips += socket.gethostbyname_ex(socket.gethostname())[2]
    except OSError:
        pass
    vistos: list[str] = []
    for ip in ips:
        if not ip.startswith("127.") and ip not in vistos:
            vistos.append(ip)
    return vistos


def gravar_senha(senha: str) -> Path:
    """Grava ~/.taramps/senha no formato que as ferramentas leem: 4 caracteres, sem BOM nem
    quebra de linha. Nunca sobrescreve um arquivo que já existe (FileExistsError)."""
    caminho = arquivo_senha()
    if caminho.exists():
        raise FileExistsError(str(caminho))
    caminho.parent.mkdir(parents=True, exist_ok=True)
    temporario = caminho.with_name(caminho.name + ".tmp")
    temporario.write_bytes(senha.encode("ascii"))
    os.replace(temporario, caminho)
    try:
        os.chmod(caminho, 0o600)  # só o dono lê (no Windows vale só o "somente leitura": ignorado)
    except OSError:
        pass
    return caminho


class Servidor(ThreadingHTTPServer):
    daemon_threads = True
    # No Windows, SO_REUSEADDR deixaria dois painéis na mesma porta sem erro: aqui o segundo
    # precisa falhar com "O painel já está aberto?".
    allow_reuse_address = sys.platform != "win32"

    def __init__(self, estado: Path, porta: int = PORTA, leitor: Leitor | None = None,
                 html: Path = ARQUIVO_HTML, rede: bool = False):
        self.estado = estado
        self.leitor = leitor
        self.html = html  # a pasta dele é a da rota estática
        self.trava_modo = threading.Lock()  # dois cliques no seletor não gravam ao mesmo tempo
        self.rede = rede
        super().__init__(("0.0.0.0" if rede else "127.0.0.1", porta), _Handler)

    def handle_error(self, request, client_address):
        """Navegador que fecha a conexão keep-alive não é erro: fica em silêncio."""
        erro = sys.exc_info()[1]
        if isinstance(erro, (ConnectionError, TimeoutError)):
            return
        super().handle_error(request, client_address)

    @property
    def porta(self) -> int:
        return self.server_address[1]

    @property
    def url(self) -> str:
        # 127.0.0.1 e não localhost: o servidor só escuta em IPv4 e "localhost" pode virar ::1.
        return f"http://127.0.0.1:{self.porta}"


# --------------------------------------------------------------------------- CLI


def main(argv: list[str] | None = None, *, estado: Path | None = None) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description="Painel do protótipo (só leitura, só localhost).")
    ap.add_argument("--sem-navegador", action="store_true", help="não abre o navegador")
    ap.add_argument("--porta", type=int, default=PORTA)
    ap.add_argument("--rede", action="store_true",
                    help="aceita outros aparelhos do Wi-Fi, só para acompanhar (sem login, sem Ler agora)")
    args = ap.parse_args(argv)
    estado = estado or RAIZ / "estado"
    if not ARQUIVO_HTML.exists():
        print(f"Falta {_relativo(ARQUIVO_HTML)}.")
        return 1
    try:
        servidor = Servidor(estado, args.porta, Leitor(), rede=args.rede)
    except OSError as erro:
        print(f"Não consegui abrir a porta {args.porta}: {erro}. O painel já está aberto?")
        return 1
    print(f"Painel em {servidor.url}  (Ctrl+C para parar)")
    if args.rede:
        for ip in ips_da_rede():
            print(f"Na rede (só para acompanhar): http://{ip}:{servidor.porta}")
    if not args.sem_navegador:
        threading.Timer(0.5, lambda: webbrowser.open(servidor.url)).start()
    try:
        servidor.serve_forever(poll_interval=0.5)
    except KeyboardInterrupt:
        print("\nPainel fechado.")
    finally:
        servidor.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
