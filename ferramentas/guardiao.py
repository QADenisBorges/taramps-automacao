#!/usr/bin/env python3
"""Guardião mínimo da escrita no PRO 2.4BT + AUDIO (spec-escrita-ble-guardiao.md).

Recebe um lote de ajustes e o retrato de uma leitura nova, simula o estado resultante e
só emite uma aprovação (selada, com a ordem de escrita) se as regras passarem:

1. JBL (saídas 1 e 2): passa-alta ligado e curva somada nunca acima da referência abaixo
   do f_min (trava 1).
2. Sub (saídas 3 e 4): subsônico ligado e curva somada nunca acima da referência abaixo do
   f_min (trava 2).
3. Limiter nunca em OFF (0.5) e threshold nunca acima do teto de cada saída (trava 3).
4. Saídas 3 e 4 idênticas em todos os parâmetros. Pedido em uma é espelhado na outra.
5. Só códigos do catálogo do Marco 1, com valor dentro da faixa. Reset, memórias, gerador,
   senha, nome, remote out e preset de EQ são sempre recusados.
6. (ligada por padrão) master + ganho + maior reforço de EQ (+6 dB na rota A+B) de cada
   saída nunca acima da linha de base.
7. G_ef = master + ganho + pico da curva somada (+6 dB na rota A+B) ≤ −1 dB.

Princípio 6: uma escrita não cria violação nova nem piora uma existente, e um lote que mexe
numa saída já em violação só passa se resolver essa violação. Cada estado intermediário do
lote também é checado (sem violação nova nem piora); o guardião procura uma ordem em que
todos sejam seguros. O par de escritas da ponte (saídas 3 e 4) conta como um passo.

A curva somada de uma saída é a soma, em dB, do crossover (Butterworth ou Linkwitz-Riley de
ordem n), do EQ paramétrico de entrada, das 15 bandas (picos com Q ≈ 2,1) e do EQ
paramétrico da saída. Os EQs são biquads de pico (protótipo analógico).

Os limites vêm de estado/guardiao.json, que só o Denis edita. Este módulo só lê o arquivo.
Só usa a biblioteca padrão. Testes em ferramentas/tests/test_guardiao.py.
"""

from __future__ import annotations

import hashlib
import hmac
import json
import math
import re
import secrets
from dataclasses import dataclass, field
from functools import lru_cache
from pathlib import Path
from typing import Callable

import protocolo_taramps as pt

Q_BANDAS = 2.1  # Q das 15 bandas do EQ gráfico
PONTOS_POR_OITAVA = 24
F_GRADE_MIN = 10.0
F_GRADE_MAX = 22000.0
EPS = 0.01  # dB: folga numérica para comparar severidades
MAX_ESTADOS = 4096  # estados intermediários avaliados, no máximo, na procura da ordem
PONTE = (2, 3)  # canais das saídas 3 e 4

CURVA_CANAL = frozenset({"040", "041", "042", "043", "090", "091", "092"})
EQ_CANAL = frozenset({"090", "091", "092"})
CURVA_GLOBAL = frozenset({"030", "031", "032", "020"})

ROTULOS = {
    0: "lote", 1: "trava 1", 2: "trava 2", 3: "trava 3", 4: "regra 4", 5: "regra 5",
    6: "regra 6", 7: "regra 7", 8: "ordem",
}

_GERADOR = "gerador de sinal"
PROIBIDOS = {
    "004": "reset de fábrica",
    "242": "reset das configurações de áudio",
    "240": "salvar memória (o Denis salva pelo app)",
    "241": "carregar memória",
    "000": "senha",
    "001": "troca de senha",
    "002": "nome do produto",
    "231": "nome de memória", "232": "nome de memória", "233": "nome de memória",
    "200": _GERADOR, "201": _GERADOR, "202": _GERADOR,
    "210": _GERADOR, "211": _GERADOR, "212": _GERADOR, "213": _GERADOR,
    "252": "remote out (liga os amplificadores)",
    "220": "preset de EQ (troca as 15 bandas por valores que o guardião não conhece)",
    "005": "voltímetro (só leitura)",
    "111": "clip da entrada (só leitura)",
    "150": "clip das saídas (só leitura)",
}


class ConfigInvalida(ValueError):
    """estado/guardiao.json ausente ou fora do formato."""


class PedidoInvalido(ValueError):
    """Ajuste do lote fora do formato CCC:c=v ou CCC=v."""


class EstadoInvalido(ValueError):
    """Retrato incompleto ou com valores que o guardião não consegue simular."""


class EscritaNaoAprovada(PermissionError):
    """Tentativa de escrever sem uma aprovação válida do guardião."""


# --------------------------------------------------------------------------- configuração


@dataclass(frozen=True)
class Saida:
    numero: int  # 1 a 4
    nome: str
    trava: str  # "passa-alta" (trava 1) | "subsônico" (trava 2)
    f_min: float
    inclinacao: int  # dB/oit da referência
    teto_limiter: float  # dB

    @property
    def canal(self) -> int:
        return self.numero - 1

    @property
    def regra(self) -> int:
        return 1 if self.trava == "passa-alta" else 2


@dataclass(frozen=True)
class Config:
    saidas: tuple[Saida, ...]
    tolerancia: float
    g_ef_max: float
    regra6: bool
    nivel_base: dict  # canal -> master + ganho + maior reforço de EQ da linha de base
    caminho: str = ""


def _num(valor, onde: str, minimo: float | None = None, maximo: float | None = None) -> float:
    if isinstance(valor, bool) or not isinstance(valor, (int, float)) or not math.isfinite(valor):
        raise ConfigInvalida(f"{onde}: número esperado")
    if (minimo is not None and valor < minimo) or (maximo is not None and valor > maximo):
        raise ConfigInvalida(f"{onde}: fora de {minimo:g} a {maximo:g}")
    return float(valor)


def carregar_config(caminho: Path) -> Config:
    """Lê e valida estado/guardiao.json. Nunca escreve nada."""
    try:
        dados = json.loads(Path(caminho).read_text(encoding="utf-8"))
    except FileNotFoundError:
        raise ConfigInvalida(f"{caminho} não existe") from None
    except (OSError, ValueError) as erro:
        raise ConfigInvalida(f"não consegui ler {caminho}: {erro}") from None
    if not isinstance(dados, dict):
        raise ConfigInvalida("o guardiao.json precisa ser um objeto")
    saidas_cfg = dados.get("saidas")
    if not isinstance(saidas_cfg, dict) or set(saidas_cfg) != {"1", "2", "3", "4"}:
        raise ConfigInvalida("o guardiao.json precisa das saídas 1, 2, 3 e 4")
    saidas = []
    for n in range(1, 5):
        s = saidas_cfg[str(n)]
        if not isinstance(s, dict):
            raise ConfigInvalida(f"saída {n}: objeto esperado")
        trava = s.get("trava")
        if trava not in ("passa-alta", "subsônico"):
            raise ConfigInvalida(f"saída {n}: trava é passa-alta ou subsônico")
        inclinacao = s.get("inclinacao_db_oit")
        if inclinacao not in (6, 12, 18, 24, 30, 36, 42, 48) or isinstance(inclinacao, bool):
            raise ConfigInvalida(f"saída {n}: inclinacao_db_oit é múltiplo de 6, de 6 a 48")
        saidas.append(Saida(
            numero=n,
            nome=str(s.get("nome") or f"saída {n}"),
            trava=trava,
            f_min=_num(s.get("f_min_hz"), f"saída {n}, f_min_hz", 20, 22000),
            inclinacao=int(inclinacao),
            teto_limiter=_num(s.get("teto_limiter_db"), f"saída {n}, teto_limiter_db", -48, 0),
        ))
    regra6 = dados.get("regra6", True)
    if not isinstance(regra6, bool):
        raise ConfigInvalida("regra6 é true ou false")
    base = dados.get("linha_de_base")
    if not isinstance(base, dict):
        raise ConfigInvalida("falta a linha_de_base")
    try:
        master = _num(base["master_db"], "linha_de_base.master_db", -80, 0)
        eq_in = base["eq_entrada"]
        global_base = (
            _num(eq_in["freq_hz"], "eq_entrada.freq_hz", 10, 22000),
            _num(eq_in["ganho_db"], "eq_entrada.ganho_db", -12, 12),
            _num(eq_in["q"], "eq_entrada.q", 0.1, 10),
            tuple(_num(g, "eq_grafico_db", -12, 12) for g in base["eq_grafico_db"]),
        )
        if len(global_base[3]) != len(pt.BANDAS_HZ):
            raise ConfigInvalida("eq_grafico_db precisa de 15 valores")
        nivel_base = {}
        for s in saidas:
            ganho = _num(base["ganho_db"][str(s.numero)], f"ganho_db da saída {s.numero}", -45, 15)
            eq = base["eq_saida"][str(s.numero)]
            eq_saida = (
                _num(eq["freq_hz"], f"eq_saida {s.numero}.freq_hz", 10, 22000),
                _num(eq["ganho_db"], f"eq_saida {s.numero}.ganho_db", -12, 12),
                _num(eq["q"], f"eq_saida {s.numero}.q", 0.1, 10),
            )
            rota = base["roteamento"][str(s.numero)]
            if rota not in pt.ROTEAMENTOS:
                raise ConfigInvalida(f"roteamento da saída {s.numero}: a, b ou a+b")
            nivel_base[s.canal] = round(
                master + ganho + _reforco_eq(eq_saida, global_base) + _rota(rota), 3)
    except (KeyError, TypeError) as erro:
        raise ConfigInvalida(f"linha_de_base incompleta: falta {erro}") from None
    return Config(
        saidas=tuple(saidas),
        tolerancia=_num(dados.get("tolerancia_db"), "tolerancia_db", 0, 1),
        g_ef_max=_num(dados.get("g_ef_max_db"), "g_ef_max_db", -48, 0),
        regra6=regra6,
        nivel_base=nivel_base,
        caminho=str(caminho),
    )


# --------------------------------------------------------------------------- curvas


def ganho_filtro(indice: int, fc: float, f: float, passa_alta: bool = True) -> float:
    """Magnitude (dB, ≤ 0) do filtro do crossover na frequência f.

    Butterworth de ordem N: |H|² = 1 / (1 + x^2N). Linkwitz-Riley de ordem N = dois
    Butterworth de ordem N/2 em cascata: |H| = 1 / (1 + x^N). x = fc/f no passa-alta e
    f/fc no passa-baixa; N = inclinação / 6."""
    nome = pt.FILTROS[indice]
    if nome == "OFF":
        return 0.0
    n = int(nome[2:]) / 6
    x = fc / f if passa_alta else f / fc
    if nome.startswith("BT"):
        return -10.0 * math.log10(1.0 + x ** (2 * n))
    return -20.0 * math.log10(1.0 + x ** n)


def ganho_pico(f0: float, ganho: float, q: float, f: float) -> float:
    """Magnitude (dB) de um EQ de pico (biquad, protótipo analógico). Vale `ganho` em f0."""
    if ganho == 0:
        return 0.0
    a = 10.0 ** (ganho / 40.0)
    w = f / f0
    base = (1.0 - w * w) ** 2
    num = base + (w * a / q) ** 2
    den = base + (w / (a * q)) ** 2
    return 10.0 * math.log10(num / den)


def referencia(f_min: float, inclinacao: int, f: float) -> float:
    """Butterworth da inclinação mínima, em f_min (referência das travas 1 e 2)."""
    return -10.0 * math.log10(1.0 + (f_min / f) ** (2 * inclinacao / 6))


def _grade(f_lo: float, f_hi: float, extras=()) -> tuple[float, ...]:
    n = max(1, math.ceil(math.log2(f_hi / f_lo) * PONTOS_POR_OITAVA))
    pontos = {f_lo * (f_hi / f_lo) ** (i / n) for i in range(n + 1)}
    pontos.update(f for f in extras if f_lo <= f <= f_hi)
    return tuple(sorted(pontos))


# Parâmetros de uma saída: (hpf índice, hpf Hz, lpf índice, lpf Hz, eq Hz, eq dB, eq Q).
# Globais: (eq entrada Hz, dB, Q, 15 bandas em dB).


def _picos(eq_saida: tuple, globais: tuple) -> list[tuple[float, float, float]]:
    picos = [(globais[0], globais[1], globais[2]), tuple(eq_saida)]
    picos += [(hz, g, Q_BANDAS) for hz, g in zip(pt.BANDAS_HZ, globais[3])]
    return [(f0, g, q) for f0, g, q in picos if g != 0]


def _soma_eq(picos, f: float) -> float:
    return sum(ganho_pico(f0, g, q, f) for f0, g, q in picos)


def _crossover(ps: tuple, f: float) -> float:
    return ganho_filtro(ps[0], ps[1], f, True) + ganho_filtro(ps[2], ps[3], f, False)


@lru_cache(maxsize=8192)
def _excesso(ps: tuple, globais: tuple, f_min: float, inclinacao: int) -> tuple[float, float]:
    """(excesso da curva somada, excesso só do crossover) sobre a referência, em dB, no
    pior ponto entre 10 Hz e f_min."""
    picos = _picos(ps[4:], globais)
    total = cruz = -math.inf
    for f in _grade(F_GRADE_MIN, f_min, [p[0] for p in picos]):
        ref = referencia(f_min, inclinacao, f)
        xo = _crossover(ps, f)
        cruz = max(cruz, xo - ref)
        total = max(total, xo + _soma_eq(picos, f) - ref)
    return total, cruz


@lru_cache(maxsize=8192)
def _pico_curva(ps: tuple, globais: tuple) -> float:
    """Pico da curva somada (crossover + EQs) entre 10 Hz e 22 kHz."""
    picos = _picos(ps[4:], globais)
    extras = [p[0] for p in picos]
    return max(_crossover(ps, f) + _soma_eq(picos, f)
               for f in _grade(F_GRADE_MIN, F_GRADE_MAX, extras))


@lru_cache(maxsize=8192)
def _reforco_eq(eq_saida: tuple, globais: tuple) -> float:
    """Maior reforço dos EQs (entrada, 15 bandas e saída), sem o crossover. Corte não conta."""
    picos = _picos(eq_saida, globais)
    if not picos:
        return 0.0
    extras = [p[0] for p in picos]
    return max(0.0, max(_soma_eq(picos, f) for f in _grade(F_GRADE_MIN, F_GRADE_MAX, extras)))


# --------------------------------------------------------------------------- estado

Chave = tuple  # (código, canal ou banda ou None)


def estado_do_retrato(retrato: dict) -> dict:
    """{(código, canal): valor} das 84 leituras. Recusa leitura incompleta ou estranha."""
    if not isinstance(retrato, dict):
        raise EstadoInvalido("retrato inválido")
    lidas = {(x.get("codigo"), x.get("canal")): x for x in retrato.get("leituras", [])
             if isinstance(x, dict)}
    estado, faltam = {}, []
    for c in pt.consultas_marco1():
        x = lidas.get((c.codigo, c.canal))
        if x is None or x.get("valor") is None:
            faltam.append(c.descricao())
        elif x.get("fora_da_faixa"):  # o 0.5 (limiter OFF) é especial e não vem marcado
            raise EstadoInvalido(f"valor fora da faixa do app em {c.descricao()}: {x.get('cru')}")
        else:
            estado[(c.codigo, c.canal)] = x["valor"]
    if faltam:
        raise EstadoInvalido(f"leitura incompleta: {len(faltam)} sem valor (ex.: {faltam[0]})")
    for c in pt.CANAIS:
        for codigo in ("042", "043"):
            if estado[(codigo, c)] not in pt.FILTROS:
                raise EstadoInvalido(f"filtro desconhecido em {codigo}:{c}")
        if estado[("010", c)] not in pt.ROTEAMENTOS:
            raise EstadoInvalido(f"roteamento desconhecido em 010:{c}")
    numericos = [k for k in estado if k[0] in CURVA_CANAL - {"042", "043"} | CURVA_GLOBAL
                 | {"080", "081", "071"}]
    for k in numericos:
        v = estado[k]
        if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v):
            raise EstadoInvalido(f"valor estranho em {k[0]}:{k[1]}")
        if k[0] in ("040", "041", "090", "031", "092", "032") and v <= 0:
            raise EstadoInvalido(f"valor estranho em {k[0]}:{k[1]}")
    return estado


def _par_saida(estado: dict, canal: int) -> tuple:
    return tuple(estado[(c, canal)] for c in ("042", "040", "043", "041", "090", "091", "092"))


def _par_global(estado: dict) -> tuple:
    return (estado[("031", None)], estado[("030", None)], estado[("032", None)],
            tuple(estado[("020", b)] for b in range(len(pt.BANDAS_HZ))))


# --------------------------------------------------------------------------- checagens


@dataclass(frozen=True)
class Checagem:
    id: str
    regra: int
    saida: int | None
    sev: float  # 0 = ok; > 0 = tamanho da violação
    motivo: str


def _viola(c: Checagem) -> bool:
    return c.sev > EPS


def _piorou(novo: Checagem, lido: Checagem) -> bool:
    return novo.sev > lido.sev + EPS


def _rota(roteamento: str) -> float:
    """A+B soma as duas origens: +6 dB no pior caso (regras 6 e 7)."""
    return 6.0 if roteamento == "a+b" else 0.0


def _volts(db: float) -> str:
    return f"{2.0 * 10 ** (db / 20):.2f}".replace(".", ",")


def checar(estado: dict, cfg: Config) -> dict[str, Checagem]:
    """Todas as checagens das regras 1 a 4, 6 e 7 sobre um estado."""
    saida: dict[str, Checagem] = {}
    globais = _par_global(estado)
    master = estado[("080", None)]
    for s in cfg.saidas:
        c = s.canal
        ps = _par_saida(estado, c)
        # Travas 1 e 2: passa-alta/subsônico ligado e curva somada sob a referência.
        total, cruz = _excesso(ps, globais, s.f_min, s.inclinacao)
        sev = max(0.0, total - cfg.tolerancia)
        desligado = ps[0] == 0
        if desligado:
            sev = max(sev, 100.0)
            motivo = f"{s.trava} da saída {s.numero} ({s.nome}) desligado"
        elif cruz > cfg.tolerancia:
            motivo = (f"{s.trava} da saída {s.numero} ({s.nome}) abaixo do mínimo "
                      f"({s.f_min:g} Hz, {s.inclinacao} dB/oit): {pt.FILTROS[ps[0]]} em {ps[1]:g} Hz")
        else:
            motivo = f"reforço de EQ abaixo do corte da saída {s.numero} ({s.nome})"
        saida[f"trava:{s.numero}"] = Checagem(f"trava:{s.numero}", s.regra, s.numero, sev, motivo)

        # Trava 3: limiter ligado e threshold no teto ou abaixo.
        thr = estado[("071", c)]
        if thr == 0.5:
            sev, motivo = 100.0, f"limiter da saída {s.numero} em OFF"
        else:
            sev = max(0.0, round(thr - s.teto_limiter, 3))
            motivo = (f"threshold da saída {s.numero} em {thr:.1f} dB, acima do teto "
                      f"{s.teto_limiter:.1f} dB ({_volts(s.teto_limiter)} V)")
        saida[f"limiter:{s.numero}"] = Checagem(f"limiter:{s.numero}", 3, s.numero, sev, motivo)

        # Regra 6: nível nunca acima da linha de base.
        if cfg.regra6:
            nivel = round(master + estado[("081", c)] + _reforco_eq(ps[4:], globais)
                          + _rota(estado[("010", c)]), 3)
            base = cfg.nivel_base[c]
            sev = max(0.0, round(nivel - base, 3))
            motivo = (f"nível da saída {s.numero} {sev:.1f} dB acima da linha de base "
                      f"(master + ganho + maior reforço de EQ + 6 dB na rota A+B)")
            saida[f"nivel:{s.numero}"] = Checagem(f"nivel:{s.numero}", 6, s.numero, sev, motivo)

        # Regra 7: G_ef ≤ −1 dB, +6 dB na rota A+B.
        gef = round(master + estado[("081", c)] + _pico_curva(ps, globais)
                    + _rota(estado[("010", c)]), 3)
        sev = max(0.0, round(gef - cfg.g_ef_max, 3))
        motivo = f"G_ef da saída {s.numero} em {gef:.1f} dB (máximo {cfg.g_ef_max:.1f} dB)"
        saida[f"gef:{s.numero}"] = Checagem(f"gef:{s.numero}", 7, s.numero, sev, motivo)

    # Regra 4: ponte idêntica em todos os parâmetros.
    for p in pt.CATALOGO:
        if p.escopo != pt.ESCOPO_CANAL:
            continue
        a, b = estado[(p.codigo, PONTE[0])], estado[(p.codigo, PONTE[1])]
        if a == b:
            sev = 0.0
        elif isinstance(a, (int, float)) and isinstance(b, (int, float)) \
                and not isinstance(a, bool) and not isinstance(b, bool):
            sev = max(abs(a - b), 2 * EPS)
        else:
            sev = 1.0
        motivo = f"saídas 3 e 4 diferentes em {p.rotulo} ({p.codigo}): {a} × {b}"
        saida[f"ponte:{p.codigo}"] = Checagem(f"ponte:{p.codigo}", 4, None, sev, motivo)
    return saida


def toca(id_checagem: str, chave: Chave, valor_novo) -> bool:
    """O ajuste `chave = valor_novo` mexe no que a checagem avalia?"""
    codigo, canal = chave
    tipo, alvo = id_checagem.split(":")
    if tipo == "ponte":
        return codigo == alvo and canal in PONTE
    c = int(alvo) - 1
    if codigo == "100":  # mute: tirar o mute "liga" a saída; mutar nunca piora
        return canal == c and not valor_novo
    curva = (codigo in CURVA_CANAL and canal == c) or codigo in CURVA_GLOBAL
    if tipo == "trava":
        return curva
    if tipo == "limiter":
        return codigo == "071" and canal == c
    if tipo == "nivel":
        return (codigo == "080" or (codigo in ("081", "010") and canal == c)
                or (codigo in EQ_CANAL and canal == c) or codigo in CURVA_GLOBAL)
    if tipo == "gef":
        return codigo == "080" or (codigo in ("081", "010") and canal == c) or curva
    return True


# --------------------------------------------------------------------------- lote


@dataclass(frozen=True)
class Pedido:
    codigo: str
    canal: int | None
    valor: object  # como veio: texto ou número; None = sem valor
    original: str


@dataclass(frozen=True)
class Escrita:
    codigo: str
    canal: int | None
    valor: object  # já normalizado (ou cru, se fora da faixa: nunca é aprovado)

    @property
    def chave(self) -> Chave:
        return (self.codigo, self.canal)

    def texto(self) -> str:
        onde = f":{self.canal}" if self.canal is not None else ""
        try:
            v = pt.valor_no_fio(self.codigo, self.valor)
        except (TypeError, ValueError):
            v = str(self.valor)
        rotulo = ""
        if self.codigo in ("042", "043") and self.valor in pt.FILTROS:
            rotulo = f" ({pt.FILTROS[self.valor]})"
        return f"{self.codigo}{onde}={v}{rotulo}"


@dataclass
class Recusa:
    regra: int
    motivo: str
    item: str | None = None  # ajuste que barrou (ou None, se for o lote todo)
    seguro: str | None = None  # valor ou caminho seguro

    @property
    def rotulo(self) -> str:
        return ROTULOS.get(self.regra, f"regra {self.regra}")

    def texto(self) -> str:
        item = f"{self.item} · " if self.item else ""
        seguro = f" Seguro: {self.seguro}." if self.seguro else ""
        return f"{item}{self.rotulo}: {self.motivo}.{seguro}"

    def como_dict(self) -> dict:
        return {"regra": self.regra, "rotulo": self.rotulo, "item": self.item,
                "motivo": self.motivo, "seguro": self.seguro}


_PEDIDO_RE = re.compile(r"\s*(\d{3})(?::(\d{1,2}))?(?:\s*=\s*(.*?))?\s*")
_CODIGOS_SENHA = ("000", "001")  # o valor é senha: nunca aparece em mensagem nem registro


def _original(codigo: str, canal, valor, texto: str) -> str:
    """Texto do pedido para mensagens e registro, com a senha de 000/001 mascarada."""
    if codigo in _CODIGOS_SENHA and valor is not None:
        onde = f":{canal}" if canal is not None else ""
        return f"{codigo}{onde}=****"
    return texto


def interpretar(texto: str) -> Pedido:
    """`CCC:c=v` ou `CCC=v`. Sem `=v` o pedido ainda é lido, para a regra 5 barrar o código."""
    if not isinstance(texto, str):
        raise PedidoInvalido("ajuste em texto esperado")
    m = _PEDIDO_RE.fullmatch(texto)
    if not m:
        mostrar = texto.strip()[:3] + "…" if texto.strip()[:3] in _CODIGOS_SENHA else texto
        raise PedidoInvalido(f"{mostrar!r} fora do formato CCC:c=v ou CCC=v")
    canal = int(m.group(2)) if m.group(2) is not None else None
    valor = m.group(3) if m.group(3) not in (None, "") else None
    return Pedido(m.group(1), canal, valor, _original(m.group(1), canal, valor, texto.strip()))


def interpretar_objeto(obj) -> Pedido:
    """{"codigo": "081", "canal": 0, "valor": -12.0} ou o texto "081:0=-12.0"."""
    if isinstance(obj, str):
        return interpretar(obj)
    if not isinstance(obj, dict) or "codigo" not in obj:
        raise PedidoInvalido(f"ajuste inválido: {obj!r}")
    codigo = str(obj["codigo"]).zfill(3) if isinstance(obj["codigo"], int) else str(obj["codigo"])
    if not re.fullmatch(r"\d{3}", codigo):
        raise PedidoInvalido(f"código inválido: {obj['codigo']!r}")
    canal = obj.get("canal")
    if canal is not None and (isinstance(canal, bool) or not isinstance(canal, int)):
        raise PedidoInvalido(f"canal inválido em {codigo}: {canal!r}")
    valor = obj.get("valor")
    onde = f":{canal}" if canal is not None else ""
    return Pedido(codigo, canal, valor, _original(codigo, canal, valor, f"{codigo}{onde}={valor}"))


def ler_lote(dados) -> list[Pedido]:
    """Lote do --arquivo: lista de ajustes (texto ou objeto), ou {"ajustes": [...]}."""
    if isinstance(dados, dict):
        dados = dados.get("ajustes")
    if not isinstance(dados, list):
        raise PedidoInvalido('o arquivo precisa ser uma lista de ajustes ou {"ajustes": [...]}')
    return [interpretar_objeto(x) for x in dados]


_NOMES_FILTRO = {nome.lower(): i for i, nome in pt.FILTROS.items()}


def _valor_pedido(p: pt.Parametro, valor):
    """Valor do pedido, dentro da faixa do catálogo, ou PedidoInvalido."""
    if p.tipo == "texto":
        v = str(valor).strip().lower()
        v = "a+b" if v == "ab" else v
        if v not in pt.ROTEAMENTOS:
            raise PedidoInvalido("roteamento é a, b ou a+b")
        return v
    if p.tipo == "bool":
        v = str(valor).strip().lower()
        if v in ("1", "1.0", "true"):
            return True
        if v in ("0", "0.0", "false"):
            return False
        raise PedidoInvalido(f"{p.rotulo} é 1 ou 0")
    if p.opcoes is pt.FILTROS and isinstance(valor, str) and valor.strip().lower() in _NOMES_FILTRO:
        return _NOMES_FILTRO[valor.strip().lower()]  # aceita também BT24, LR24, OFF…
    try:
        n = pt.ler_numero(valor)
    except pt.EscritaInvalida as erro:
        raise PedidoInvalido(str(erro)) from None
    if p.minimo is not None and not p.minimo <= n <= p.maximo \
            and not (p.especiais and n in p.especiais):
        raise PedidoInvalido(f"{p.rotulo} vai de {p.minimo:g} a {p.maximo:g}")  # antes das contas
    if p.casas == 1:
        if abs(n * 10 - round(n * 10)) > 1e-6:
            raise PedidoInvalido(f"{p.rotulo} tem no máximo uma casa decimal")
        return round(n, 1) + 0.0
    if n != int(n):
        raise PedidoInvalido(f"{p.rotulo} é número inteiro")
    v = int(n)
    if p.opcoes is not None and v not in p.opcoes:
        nomes = "0 a 15 (OFF, LR12…LR48, BT6…BT48)" if p.opcoes is pt.FILTROS else "0 ou 180"
        raise PedidoInvalido(f"{p.rotulo} aceita {nomes}")
    if p.tipo == "hz" and v <= 0:
        raise PedidoInvalido("frequência precisa ser maior que zero")
    return v


def checar_regra5(pedido: Pedido) -> tuple[Escrita | None, Recusa | None]:
    """Regra 5 sem o aparelho: código do catálogo, escopo, formato e faixa. Nenhum valor
    fora da faixa chega à simulação."""
    item = pedido.original
    codigo = pedido.codigo
    if codigo in PROIBIDOS:
        return None, Recusa(5, f"{PROIBIDOS[codigo]} é sempre recusado", item)
    p = pt.POR_CODIGO.get(codigo)
    if p is None:
        return None, Recusa(5, f"{codigo} está fora do catálogo do Marco 1", item)
    if p.escopo == pt.ESCOPO_GLOBAL and pedido.canal is not None:
        return None, Recusa(5, f"{codigo} ({p.rotulo}) não tem canal", item, f"{codigo}=v")
    if p.escopo == pt.ESCOPO_CANAL and pedido.canal not in pt.CANAIS:
        return None, Recusa(5, f"{codigo} ({p.rotulo}) precisa do canal 0 a 3", item, f"{codigo}:c=v")
    if p.escopo == pt.ESCOPO_BANDA and pedido.canal not in range(len(pt.BANDAS_HZ)):
        return None, Recusa(5, f"{codigo} ({p.rotulo}) precisa da banda 0 a 14", item, f"{codigo}:b=v")
    if pedido.valor is None:
        return None, Recusa(5, "falta o valor", item)
    try:
        valor = pt.normalizar_escrita(codigo, pedido.canal, _valor_pedido(p, pedido.valor))
    except (PedidoInvalido, pt.EscritaInvalida) as erro:
        onde = f":{pedido.canal}" if pedido.canal is not None else ""
        faixa = (f"{codigo}{onde}=v, com v de {p.minimo:g} a {p.maximo:g}"
                 if p.minimo is not None else None)
        return None, Recusa(5, str(erro), item, faixa)
    return Escrita(codigo, pedido.canal, valor), None


def _espelhar(escritas: list[Escrita]) -> tuple[list[Escrita], list[Recusa], list[str]]:
    """Espelha na outra saída da ponte e junta repetidos. Pedidos conflitantes viram recusa."""
    alvo: dict[Chave, Escrita] = {}
    recusas: list[Recusa] = []
    espelhados: list[str] = []
    for e in escritas:
        p = pt.POR_CODIGO[e.codigo]
        ponte = p.escopo == pt.ESCOPO_CANAL and e.canal in PONTE
        chaves = [(e.codigo, c) for c in PONTE] if ponte else [e.chave]
        conflito = next((alvo[k] for k in chaves
                         if k in alvo and not _iguais(e.codigo, alvo[k].valor, e.valor)), None)
        if conflito is not None:
            if ponte:
                recusas.append(Recusa(4, "pedidos diferentes para as saídas 3 e 4",
                                      f"{conflito.texto()} × {e.texto()}",
                                      "peça um valor só; ele vale para as duas"))
            else:
                recusas.append(Recusa(0, "o mesmo parâmetro pedido com valores diferentes",
                                      f"{conflito.texto()} × {e.texto()}"))
            continue
        for chave in chaves:
            if chave not in alvo:
                alvo[chave] = Escrita(chave[0], chave[1], e.valor)
                if chave != e.chave:
                    espelhados.append(alvo[chave].texto())
    return list(alvo.values()), recusas, espelhados


def _iguais(codigo: str, a, b) -> bool:
    return type(a) is type(b) and pt.mesmo_valor(codigo, a, b)


def pre_checar(pedidos: list[Pedido]) -> list[Recusa]:
    """O que dá para recusar sem ler o aparelho: regra 5 e conflitos."""
    recusas, escritas = [], []
    for pedido in pedidos:
        e, r = checar_regra5(pedido)
        if r is not None:
            recusas.append(r)
        if e is not None:
            escritas.append(e)
    recusas += _espelhar(escritas)[1]
    return recusas


# --------------------------------------------------------------------------- aprovação

_CHAVE_SELO = secrets.token_bytes(32)  # nasce a cada execução: aprovação não sobrevive ao processo


def _assinatura(escritas: tuple[Escrita, ...], retrato: str) -> str:
    corpo = json.dumps([retrato] + [[e.codigo, e.canal, type(e.valor).__name__, repr(e.valor)]
                                    for e in escritas], ensure_ascii=True)
    return hmac.new(_CHAVE_SELO, corpo.encode("ascii"), hashlib.sha256).hexdigest()


@dataclass(frozen=True)
class Aprovacao:
    """Emitida só por `avaliar_lote`. O selo amarra as escritas, a ordem e a leitura."""

    escritas: tuple[Escrita, ...]
    retrato: str  # "início da leitura | SHA-256 das leituras" usadas pelo guardião
    selo: str


def item_aprovado(aprovacao, indice: int) -> Escrita:
    """A escrita `indice` de uma aprovação válida, ou EscritaNaoAprovada."""
    if not isinstance(aprovacao, Aprovacao):
        raise EscritaNaoAprovada("escrita recusada: falta a aprovação do guardião")
    esperado = _assinatura(aprovacao.escritas, aprovacao.retrato)
    if not hmac.compare_digest(esperado, str(aprovacao.selo)):
        raise EscritaNaoAprovada("escrita recusada: a aprovação não é do guardião ou foi alterada")
    if isinstance(indice, bool) or not isinstance(indice, int) \
            or not 0 <= indice < len(aprovacao.escritas):
        raise EscritaNaoAprovada("escrita recusada: item fora da aprovação")
    return aprovacao.escritas[indice]


@dataclass
class Decisao:
    aprovado: bool
    aprovacao: Aprovacao | None = None
    recusas: list[Recusa] = field(default_factory=list)
    alertas: list[str] = field(default_factory=list)
    sem_mudanca: list[str] = field(default_factory=list)
    espelhados: list[str] = field(default_factory=list)

    @property
    def ordem(self) -> list[str]:
        return [e.texto() for e in self.aprovacao.escritas] if self.aprovacao else []

    def como_dict(self) -> dict:
        return {
            "aprovado": self.aprovado,
            "ordem": self.ordem,
            "recusas": [r.como_dict() for r in self.recusas],
            "alertas": self.alertas,
            "sem_mudanca": self.sem_mudanca,
            "espelhados": self.espelhados,
        }


# --------------------------------------------------------------------------- avaliação


def _aplicar(estado: dict, escritas) -> dict:
    novo = dict(estado)
    for e in escritas:
        novo[e.chave] = e.valor
    return novo


class _Avaliador:
    """Compara estados simulados com o estado lido, pelas regras do guardião."""

    def __init__(self, lido: dict, cfg: Config):
        self.lido = lido
        self.cfg = cfg
        self.base = checar(lido, cfg)

    def problemas(self, escritas: list[Escrita], final: bool) -> list[Checagem]:
        """Checagens que barram o estado com as escritas aplicadas.

        Intermediário: nenhuma violação nova nem piora. Final: também resolve toda
        violação já existente em que o lote mexe (princípio 6)."""
        chk = checar(_aplicar(self.lido, escritas), self.cfg)
        ruins = []
        for id_, novo in chk.items():
            lido = self.base[id_]
            if _piorou(novo, lido):
                ruins.append(novo)
            elif final and _viola(lido) and _viola(novo) \
                    and any(toca(id_, e.chave, e.valor) for e in escritas):
                ruins.append(novo)
        return ruins


def _grupo(escritas: list[Escrita], e: Escrita) -> list[int]:
    """Índices das escritas que andam junto com `e` (o par da ponte)."""
    p = pt.POR_CODIGO[e.codigo]
    if p.escopo == pt.ESCOPO_CANAL and e.canal in PONTE:
        return [i for i, x in enumerate(escritas) if x.codigo == e.codigo and x.canal in PONTE]
    return [i for i, x in enumerate(escritas) if x.chave == e.chave]


def _sugerir(av: _Avaliador, escritas: list[Escrita], indice: int) -> str | None:
    """Valor seguro para a escrita `indice`, mantendo as outras. None se não houver."""
    e = escritas[indice]
    p = pt.POR_CODIGO[e.codigo]
    grupo = _grupo(escritas, e)
    atual = av.lido[e.chave]
    ids = [i for i in av.base if toca(i, e.chave, e.valor) or toca(i, e.chave, atual)]

    def passa(v) -> bool:
        try:
            v = pt.normalizar_escrita(e.codigo, e.canal, v)
        except pt.EscritaInvalida:
            return False
        trocadas = [Escrita(x.codigo, x.canal, v) if i in grupo else x
                    for i, x in enumerate(escritas)]
        ruins = {c.id for c in av.problemas(trocadas, final=True)}
        return not ruins.intersection(ids)  # só as regras em que este ajuste mexe

    onde = f"{e.codigo}:{e.canal}" if e.canal is not None else e.codigo
    if p.opcoes is not None or p.tipo in ("bool", "texto"):
        opcoes = sorted(p.opcoes) if p.opcoes is not None else [True, False]
        bons = [o for o in opcoes if o != e.valor and passa(o)]
        if not bons:
            return None
        return ", ".join(f"{onde}={pt.valor_no_fio(e.codigo, o)}" for o in bons)

    passo = 0.1 if p.casas == 1 else 1
    lo, hi = round(p.minimo / passo), round(p.maximo / passo)
    def valor(u: int):
        return round(u * passo, 1) + 0.0 if p.casas == 1 else u
    pedido = min(max(round(float(e.valor) / passo), lo), hi)
    origem = round(float(atual) / passo)
    achado = None
    if passa(valor(pedido)):
        achado = pedido
    elif lo <= origem <= hi and passa(valor(origem)):
        achado = _bisseccao(pedido, origem, lambda u: passa(valor(u)))  # o seguro mais perto
    else:
        candidatos = []
        for sentido in (1, -1):  # varredura grossa para os dois lados, depois bissecção
            u = ant = pedido
            for _ in range(60):
                if p.tipo == "hz":
                    u = round(u * 2 ** (sentido / 6))
                else:
                    u = u + sentido * round(1.0 / passo)
                if not lo <= u <= hi or u == ant:
                    break
                if passa(valor(u)):
                    candidatos.append(_bisseccao(ant, u, lambda x: passa(valor(x))))
                    break
                ant = u
        if candidatos:
            achado = min(candidatos, key=lambda u: abs(u - pedido))
    if achado is None:
        return None
    return f"{onde}={pt.valor_no_fio(e.codigo, valor(achado))}"


def _bisseccao(ruim: int, bom: int, passa: Callable[[int], bool]) -> int:
    """O valor que passa mais perto de `ruim`, entre `ruim` (não passa) e `bom` (passa)."""
    while abs(bom - ruim) > 1:
        meio = (bom + ruim) // 2
        if passa(meio):
            bom = meio
        else:
            ruim = meio
    return bom


def _caminho_seguro(c: Checagem, cfg: Config) -> str:
    """Quando nenhum valor do próprio ajuste resolve: o que fazer antes."""
    tipo, alvo = c.id.split(":")
    if tipo == "trava":
        s = cfg.saidas[int(alvo) - 1]
        return (f"antes, corrija o {s.trava} da saída {s.numero}: 042:{s.canal} = BT{s.inclinacao} "
                f"ou mais íngreme, 040:{s.canal} ≥ {s.f_min:g} Hz e nenhum reforço de EQ abaixo disso")
    if tipo == "limiter":
        s = cfg.saidas[int(alvo) - 1]
        return f"071:{s.canal}={s.teto_limiter:.1f} ou menos"
    if tipo == "ponte":
        return f"iguale as saídas 3 e 4 em {alvo} ({alvo}:2=v espelha na saída 4)"
    if tipo == "nivel":
        return "não suba master, ganho nem reforço de EQ acima da linha de base"
    return "baixe ganho, master ou reforço de EQ dessa saída"


def avaliar_lote(retrato: dict, pedidos: list[Pedido], cfg: Config) -> Decisao:
    """Decide o lote contra o retrato de uma leitura nova. Só aqui nasce uma Aprovacao."""
    if not pedidos:
        return Decisao(False, recusas=[Recusa(0, "lote vazio")])
    recusas: list[Recusa] = []
    escritas: list[Escrita] = []
    for pedido in pedidos:
        e, r = checar_regra5(pedido)
        if r is not None:
            recusas.append(r)
        if e is not None:
            escritas.append(e)
    if recusas:
        return Decisao(False, recusas=recusas)
    try:
        lido = estado_do_retrato(retrato)
    except EstadoInvalido as erro:
        return Decisao(False, recusas=recusas + [Recusa(0, f"{erro}; leia de novo")])

    escritas, conflitos, espelhados = _espelhar(escritas)
    if conflitos:
        return Decisao(False, recusas=recusas + conflitos, espelhados=espelhados)

    av = _Avaliador(lido, cfg)
    alertas = [f"{ROTULOS[c.regra]}: {c.motivo}" for c in av.base.values() if _viola(c)]
    mudancas = [e for e in escritas if not _iguais(e.codigo, lido[e.chave], e.valor)]
    sem_mudanca = [e.texto() for e in escritas if e not in mudancas]
    decisao = Decisao(False, alertas=alertas, sem_mudanca=sem_mudanca, espelhados=espelhados)
    if not mudancas:
        decisao.aprovado = True
        decisao.aprovacao = _aprovar((), retrato)
        return decisao

    por_chave: dict[tuple, tuple[Recusa, list[int]]] = {}
    for c in av.problemas(mudancas, final=True):
        # Culpados: os ajustes que mexem nesta regra; o par da ponte aparece pela saída 3.
        culpados = list(dict.fromkeys(min(_grupo(mudancas, e)) for e in mudancas
                                      if toca(c.id, e.chave, e.valor)))
        itens = ", ".join(dict.fromkeys(mudancas[i].texto() for i in culpados)) or None
        chave = (c.regra, c.id.split(":")[0], itens)
        if chave in por_chave:  # mesma regra, mesmos ajustes, outra saída: uma linha só
            por_chave[chave][1].append(c.saida)
            continue
        ja_estava = _viola(av.base[c.id]) and not _piorou(c, av.base[c.id])
        motivo = c.motivo + ("; a saída já estava em violação e o lote mexe nela sem resolver"
                             if ja_estava else "")
        seguro = None
        for i in culpados[:2]:
            seguro = _sugerir(av, mudancas, i)
            if seguro:
                break
        recusa = Recusa(c.regra, motivo, itens, seguro or _caminho_seguro(c, cfg))
        por_chave[chave] = (recusa, [])
        recusas.append(recusa)
    for recusa, outras in por_chave.values():
        if outras:
            nums = [str(n) for n in outras]
            lista = nums[0] if len(nums) == 1 else ", ".join(nums[:-1]) + " e " + nums[-1]
            recusa.motivo += f" (também na saída {lista})" if len(nums) == 1 \
                else f" (também nas saídas {lista})"
    if recusas:
        decisao.recusas = recusas
        return decisao

    passos = _passos(mudancas)
    try:
        ordem = procurar_ordem(
            len(passos),
            lambda feitos: not av.problemas([e for i in sorted(feitos) for e in passos[i]], False),
        )
    except LimiteDaOrdem:
        decisao.recusas = [Recusa(8, "lote grande demais para achar uma ordem segura",
                                  seguro="divida o lote em partes menores")]
        return decisao
    if ordem is None:
        decisao.recusas = [Recusa(8, "nenhuma ordem deixa todos os passos intermediários seguros",
                                  seguro="mande os ajustes em lotes separados, um por vez")]
        return decisao
    decisao.aprovado = True
    decisao.aprovacao = _aprovar(tuple(e for i in ordem for e in passos[i]), retrato)
    return decisao


def _passos(mudancas: list[Escrita]) -> list[tuple[Escrita, ...]]:
    """Agrupa as escritas em passos: o par da ponte (saídas 3 e 4) é um passo só."""
    passos: list[tuple[Escrita, ...]] = []
    usados: set[int] = set()
    for i, e in enumerate(mudancas):
        if i in usados:
            continue
        grupo = _grupo(mudancas, e)
        usados.update(grupo)
        passos.append(tuple(mudancas[j] for j in sorted(grupo, key=lambda j: mudancas[j].canal)))
    return passos


def _aprovar(escritas: tuple[Escrita, ...], retrato: dict) -> Aprovacao:
    """O selo amarra as escritas ao início e ao conteúdo (canônico) das leituras."""
    leituras = sorted(([x.get("codigo"), x.get("canal"), type(x.get("valor")).__name__,
                        repr(x.get("valor"))] for x in retrato.get("leituras", [])),
                      key=lambda x: (str(x[0]), -1 if x[1] is None else x[1]))
    resumo = hashlib.sha256(json.dumps(leituras, ensure_ascii=True).encode("ascii")).hexdigest()
    marca = f"{retrato.get('inicio') or ''}|{resumo}"
    return Aprovacao(escritas, marca, _assinatura(escritas, marca))


class LimiteDaOrdem(RuntimeError):
    """A procura da ordem passou de MAX_ESTADOS estados."""


def procurar_ordem(n: int, seguro: Callable[[frozenset], bool],
                   limite: int = MAX_ESTADOS) -> list[int] | None:
    """Ordem dos n passos em que todo estado intermediário é seguro, ou None.

    Busca em profundidade sobre os subconjuntos já aplicados, tentando primeiro a ordem
    pedida. Cada subconjunto é avaliado no máximo uma vez. O estado final (todos os passos)
    já foi validado antes e não entra aqui."""
    completo = frozenset(range(n))
    mortos: set[frozenset] = set()
    avaliados = 0

    def dfs(feitos: frozenset, caminho: list[int]) -> list[int] | None:
        nonlocal avaliados
        if feitos == completo:
            return caminho
        for i in range(n):
            if i in feitos:
                continue
            prox = feitos | {i}
            if prox in mortos:
                continue
            if prox != completo:
                avaliados += 1
                if avaliados > limite:
                    raise LimiteDaOrdem()
                if not seguro(prox):
                    mortos.add(prox)
                    continue
            achado = dfs(prox, caminho + [i])
            if achado is not None:
                return achado
            mortos.add(prox)
        return None

    return dfs(frozenset(), [])
