#!/usr/bin/env python3
"""Protocolo BLE do PRO 2.4BT + AUDIO (Marco 1), sem depender do aparelho.

Fonte de tudo aqui: protocolo/MAPA.md (§2 formato, §3 autenticação, §4 parâmetros).

- Catálogo dos parâmetros do Marco 1, com código, escopo (global, canal ou banda),
  tipo, unidade e faixa.
- Montagem das consultas (`#CCC#`, `#CCC#c#` e `020#b`).
- Trava de envio: só passam consultas e a autenticação `*000*SENHA*`. Qualquer
  escrita (`*CCC*...*`) é recusada antes de chegar ao BLE.
- Montagem e validação de escrita (`*CCC*v*`, `*CCC*c*v*`, `020*b*v`), num caminho
  separado da trava acima. Quem decide se a escrita sai é o guardião (guardiao.py).
- Montador de respostas: aceita `#` ou `*`, junta fragmentos de notificação e casa
  a resposta com a consulta pelo código e pelo canal. O que não casa vira "órfão".
- Log bruto com hex e ASCII de cada quadro, com a senha mascarada.
- Amostra de conferência com o app (`--conferir`).

Só usa a biblioteca padrão. Os testes estão em ferramentas/tests/test_protocolo_taramps.py.
"""

from __future__ import annotations

import datetime as dt
import re
from dataclasses import dataclass, field
from typing import IO

# --------------------------------------------------------------------------- GATT (MAPA §1)

UUID_ANUNCIO = "00002055-0000-1000-8000-00805f9b34fb"  # UUID de 16 bits 0x2055 no anúncio
UUID_SERVICO = "00000000-cc7a-482a-984a-7f2ed5b3e58f"
UUID_ESCRITA = "00001122-8e22-4541-9d4c-21edae82ed19"
UUID_RESPOSTA = "00001123-8e22-4541-9d4c-21edae82ed19"

# --------------------------------------------------------------------------- catálogo (MAPA §4)

FILTROS = {
    0: "OFF", 1: "LR12", 2: "LR18", 3: "LR24", 4: "LR30", 5: "LR36", 6: "LR42", 7: "LR48",
    8: "BT6", 9: "BT12", 10: "BT18", 11: "BT24", 12: "BT30", 13: "BT36", 14: "BT42", 15: "BT48",
}
PRESETS_EQ = {
    0: "Flat", 1: "Loudness", 2: "Bass Boost", 3: "Mid-Bass Boost", 4: "Treble Boost",
    5: "Powerful", 6: "Electronic", 7: "Rock Style", 8: "Hip-Hop Style", 9: "Pop Music",
    10: "Vocal", 11: "Competition", 255: "Custom",
}
BANDAS_HZ = (25, 40, 63, 100, 160, 250, 400, 630, 1000, 1600, 2500, 4000, 6300, 10000, 16000)
ROTEAMENTOS = {"a": "A", "b": "B", "a+b": "A+B"}

CANAIS = (0, 1, 2, 3)  # saída 1 = canal 0 (MAPA §2)

ESCOPO_GLOBAL = "global"
ESCOPO_CANAL = "canal"
ESCOPO_BANDA = "banda"


@dataclass(frozen=True)
class Parametro:
    codigo: str
    nome: str
    rotulo: str  # nome curto em português, para as mensagens
    escopo: str  # global | canal | banda
    tipo: str  # db | hz | q | cm | ms | indice | bool | texto | graus
    unidade: str | None
    tela: str  # tela do app onde o Denis confere o valor
    minimo: float | None = None
    maximo: float | None = None
    casas: int = 0  # 0 = inteiro no fio; 1 = uma casa decimal
    opcoes: dict | None = None  # índice -> texto
    especiais: dict | None = None  # valor fora da faixa com significado (ex.: 0.5 = OFF)
    critico: bool = False  # entra em todos os canais da conferência


def _p(codigo, nome, rotulo, escopo, tipo, unidade, tela, **kw) -> Parametro:
    return Parametro(codigo, nome, rotulo, escopo, tipo, unidade, tela, **kw)


CATALOGO: tuple[Parametro, ...] = (
    _p("080", "master", "Master", ESCOPO_GLOBAL, "db", "dB", "Nível das Saídas",
       minimo=-80.0, maximo=0.0, casas=1),
    _p("081", "ganho", "Ganho da saída", ESCOPO_CANAL, "db", "dB", "Nível das Saídas",
       minimo=-45.0, maximo=15.0, casas=1, critico=True),
    _p("100", "mute", "Mute", ESCOPO_CANAL, "bool", None, "Home"),
    _p("010", "roteamento", "Roteamento", ESCOPO_CANAL, "texto", None, "Roteam. Ent/Saída",
       opcoes=ROTEAMENTOS),
    _p("060", "fase", "Fase", ESCOPO_CANAL, "graus", "graus", "Fase", opcoes={0: "0°", 180: "180°"}),
    _p("040", "hpf_freq", "HPF frequência", ESCOPO_CANAL, "hz", "Hz", "Crossover",
       minimo=10, maximo=22000, critico=True),
    _p("041", "lpf_freq", "LPF frequência", ESCOPO_CANAL, "hz", "Hz", "Crossover",
       minimo=10, maximo=22000, critico=True),
    _p("042", "hpf_filtro", "HPF filtro", ESCOPO_CANAL, "indice", None, "Crossover",
       opcoes=FILTROS, critico=True),
    _p("043", "lpf_filtro", "LPF filtro", ESCOPO_CANAL, "indice", None, "Crossover",
       opcoes=FILTROS, critico=True),
    _p("050", "delay", "Delay", ESCOPO_CANAL, "cm", "cm", "Alinhamento",
       minimo=0.0, maximo=275.0, casas=1),
    _p("070", "limiter_auto", "Limiter auto", ESCOPO_CANAL, "bool", None, "Limiter", critico=True),
    _p("071", "limiter_threshold", "Limiter threshold", ESCOPO_CANAL, "db", "dB", "Limiter",
       minimo=-48.0, maximo=0.0, casas=1, especiais={0.5: "OFF"}, critico=True),
    # No modo Auto o app manda 0.05 ou 0.01 (MAPA §8, lacuna 6).
    _p("072", "limiter_attack", "Limiter attack", ESCOPO_CANAL, "ms", "ms", "Limiter",
       minimo=0.1, maximo=100.0, casas=1, especiais={0.01: "auto", 0.05: "auto"}),
    _p("073", "limiter_release", "Limiter release", ESCOPO_CANAL, "ms", "ms", "Limiter",
       minimo=1, maximo=1600),
    _p("090", "eq_saida_freq", "EQ de saída, frequência", ESCOPO_CANAL, "hz", "Hz", "EQ Param Saída",
       minimo=10, maximo=22000),
    _p("091", "eq_saida_ganho", "EQ de saída, ganho", ESCOPO_CANAL, "db", "dB", "EQ Param Saída",
       minimo=-12.0, maximo=12.0, casas=1),
    _p("092", "eq_saida_q", "EQ de saída, Q", ESCOPO_CANAL, "q", None, "EQ Param Saída",
       minimo=0.1, maximo=10.0, casas=1),
    _p("030", "eq_entrada_ganho", "EQ de entrada, ganho", ESCOPO_GLOBAL, "db", "dB", "EQ Param Entr",
       minimo=-12.0, maximo=12.0, casas=1),
    _p("031", "eq_entrada_freq", "EQ de entrada, frequência", ESCOPO_GLOBAL, "hz", "Hz",
       "EQ Param Entr", minimo=10, maximo=22000),
    _p("032", "eq_entrada_q", "EQ de entrada, Q", ESCOPO_GLOBAL, "q", None, "EQ Param Entr",
       minimo=0.4, maximo=10.0, casas=1),
    _p("020", "eq_grafico", "EQ 15 bandas", ESCOPO_BANDA, "db", "dB", "EQ 15 Bandas",
       minimo=-12.0, maximo=12.0, casas=1),
    _p("220", "preset_eq", "Preset de EQ", ESCOPO_GLOBAL, "indice", None, "EQ 15 Bandas",
       opcoes=PRESETS_EQ),
)

POR_CODIGO = {p.codigo: p for p in CATALOGO}

AUTENTICACAO = Parametro("000", "autenticacao", "Senha", ESCOPO_GLOBAL, "texto", None, "")


@dataclass(frozen=True)
class Consulta:
    parametro: Parametro
    canal: int | None = None  # canal 0 a 3, ou banda 0 a 14

    @property
    def codigo(self) -> str:
        return self.parametro.codigo

    @property
    def texto(self) -> str:
        p = self.parametro
        if p.escopo == ESCOPO_BANDA:
            return f"{p.codigo}#{self.canal}"  # sem '#' inicial (MAPA §4)
        if p.escopo == ESCOPO_CANAL:
            return f"#{p.codigo}#{self.canal}#"
        return f"#{p.codigo}#"

    def descricao(self) -> str:
        p = self.parametro
        if p.escopo == ESCOPO_CANAL:
            return f"{p.codigo} {p.rotulo}, saída {self.canal + 1}"
        if p.escopo == ESCOPO_BANDA:
            return f"{p.codigo} {p.rotulo}, banda {_hz(BANDAS_HZ[self.canal])}"
        return f"{p.codigo} {p.rotulo}"


def _hz(hz: int) -> str:
    return f"{hz / 1000:g}k" if hz >= 1000 else f"{hz} Hz"


def consultas_marco1() -> list[Consulta]:
    """As 84 consultas do Marco 1: master, 16 parâmetros × 4 canais, EQ de entrada,
    15 bandas e preset de EQ."""
    lista: list[Consulta] = []
    for p in CATALOGO:
        if p.escopo == ESCOPO_CANAL:
            lista += [Consulta(p, c) for c in CANAIS]
        elif p.escopo == ESCOPO_BANDA:
            lista += [Consulta(p, b) for b in range(len(BANDAS_HZ))]
        else:
            lista.append(Consulta(p))
    return lista


# --------------------------------------------------------------------------- trava de envio


class EnvioRecusado(ValueError):
    """Tentativa de enviar algo que não é consulta nem autenticação."""


_CONSULTA_RE = re.compile(r"#(\d{3})#(?:(\d{1,2})#)?")
_GRAFICO_RE = re.compile(r"020#(\d{1,2})")
_AUTENTICACAO_RE = re.compile(r"\*000\*[0-9A-Z]{4}\*")

# Lista de permissão: só códigos conhecidos de leitura. Os do catálogo (o 020 tem forma
# própria) mais voltímetro (005) e clip da entrada e das saídas (111, 150), todos globais.
# Qualquer outro código, inclusive os que mexem no aparelho (MAPA §5), é recusado.
CODIGOS_GLOBAIS_PERMITIDOS = frozenset(
    {p.codigo for p in CATALOGO if p.escopo == ESCOPO_GLOBAL} | {"005", "111", "150"}
)
CODIGOS_CANAL_PERMITIDOS = frozenset(p.codigo for p in CATALOGO if p.escopo == ESCOPO_CANAL)


def verificar_envio(texto: str) -> bytes:
    """Devolve os bytes a enviar, ou levanta EnvioRecusado.

    Passam só: consulta `#CCC#` / `#CCC#c#`, consulta do EQ gráfico `020#b` e a
    autenticação `*000*SENHA*`."""
    if not isinstance(texto, str) or not texto.isascii():
        raise EnvioRecusado("envio recusado: só texto ASCII")
    if _AUTENTICACAO_RE.fullmatch(texto):
        return texto.encode("ascii")
    m = _CONSULTA_RE.fullmatch(texto)
    if m:
        codigo, canal = m.group(1), m.group(2)
        if canal is None and codigo in CODIGOS_GLOBAIS_PERMITIDOS:
            return texto.encode("ascii")
        if canal is not None and codigo in CODIGOS_CANAL_PERMITIDOS and int(canal) in CANAIS:
            return texto.encode("ascii")
        raise EnvioRecusado(f"envio recusado: {texto!r} não é uma consulta conhecida (só leitura)")
    m = _GRAFICO_RE.fullmatch(texto)
    if m and int(m.group(1)) < len(BANDAS_HZ):
        return texto.encode("ascii")
    raise EnvioRecusado(f"envio recusado: {mascarar_texto(texto)!r} não é consulta (só leitura)")


def eh_autenticacao(texto: str) -> bool:
    return bool(_AUTENTICACAO_RE.fullmatch(texto))


_SENHA_RE = re.compile(r"[0-9A-Za-z]{4}")


def montar_autenticacao(senha: str) -> str:
    """`*000*SENHA*`, com a senha em maiúsculas como o app faz (MAPA §3)."""
    if not _SENHA_RE.fullmatch(senha or ""):
        raise ValueError("a senha tem 4 letras ou números")
    return f"*000*{senha.upper()}*"


# --------------------------------------------------------------------------- escrita (MAPA §2 e §4)
#
# Caminho separado da trava de leitura acima, que continua recusando toda escrita. Aqui só
# se monta e valida o quadro; quem decide se ele pode sair é o guardião (guardiao.py), e o
# único método que o envia é ClienteTaramps.escrever, que exige a aprovação do guardião.


class EscritaInvalida(ValueError):
    """Quadro de escrita fora do formato, do catálogo ou da faixa."""


# Os códigos do catálogo, menos o preset de EQ (220): ele troca as 15 bandas por valores
# que o guardião não conhece, então o estado resultante é imprevisível (princípio 5).
CODIGOS_ESCRITA = frozenset(p.codigo for p in CATALOGO) - {"220"}

_ESCRITA_RE = re.compile(r"\*(\d{3})\*(?:(\d{1,2})\*)?([^*#]+)\*")
_ESCRITA_GRAFICO_RE = re.compile(r"020\*(\d{1,2})\*([^*#]+)")


def ler_numero(valor) -> float:
    if isinstance(valor, bool):
        raise EscritaInvalida("valor booleano onde se espera número")
    if isinstance(valor, str):
        valor = valor.strip().replace(",", ".")
    try:
        numero = float(valor)
    except (TypeError, ValueError):
        raise EscritaInvalida(f"valor {valor!r} não é número") from None
    if numero != numero or numero in (float("inf"), float("-inf")):
        raise EscritaInvalida("valor não é número")
    return numero


def normalizar_escrita(codigo: str, canal: int | None, valor):
    """Valor pronto para escrever (float, int, bool ou texto), ou EscritaInvalida.

    Confere o código (catálogo, sem o 220), o canal ou a banda, o tipo, as casas
    decimais e a faixa do app. O threshold aceita `0.5` (OFF): barrar o OFF é papel do
    guardião (trava 3), não do formato."""
    p = POR_CODIGO.get(codigo)
    if p is None or codigo not in CODIGOS_ESCRITA:
        raise EscritaInvalida(f"{codigo} não é um código de escrita do Marco 1")
    if p.escopo == ESCOPO_GLOBAL:
        if canal is not None:
            raise EscritaInvalida(f"{codigo} não tem canal")
    elif p.escopo == ESCOPO_CANAL:
        if isinstance(canal, bool) or canal not in CANAIS:
            raise EscritaInvalida(f"{codigo} precisa de canal 0 a 3")
    elif isinstance(canal, bool) or canal not in range(len(BANDAS_HZ)):
        raise EscritaInvalida(f"{codigo} precisa de banda 0 a 14")

    if p.tipo == "texto":
        v = str(valor).strip().lower()
        v = "a+b" if v == "ab" else v
        if v not in ROTEAMENTOS:
            raise EscritaInvalida("roteamento é a, b ou a+b")
        return v
    if p.tipo == "bool":
        if isinstance(valor, bool):
            return valor
        n = ler_numero(valor)
        if n not in (0, 1):
            raise EscritaInvalida(f"{p.rotulo} é 1 ou 0")
        return n == 1
    n = ler_numero(valor)
    if abs(n) > 1e6:  # bem fora de qualquer faixa; evita estouro nas contas abaixo
        raise EscritaInvalida(f"{p.rotulo} vai de {p.minimo:g} a {p.maximo:g}"
                              if p.minimo is not None else f"{p.rotulo}: valor fora da faixa")
    if p.casas == 1:
        if abs(n * 10 - round(n * 10)) > 1e-6:
            raise EscritaInvalida(f"{p.rotulo} tem no máximo uma casa decimal")
        v = round(n, 1) + 0.0  # + 0.0 tira o -0.0
        if p.especiais and v in p.especiais:
            return v
        if (p.minimo is not None and v < p.minimo) or (p.maximo is not None and v > p.maximo):
            raise EscritaInvalida(f"{p.rotulo} vai de {p.minimo:g} a {p.maximo:g}")
        return v
    if n != int(n):
        raise EscritaInvalida(f"{p.rotulo} é número inteiro")
    v = int(n)
    if p.opcoes is not None:
        if v not in p.opcoes:
            raise EscritaInvalida(f"{p.rotulo}: valor {v} não existe")
        return v
    if (p.minimo is not None and v < p.minimo) or (p.maximo is not None and v > p.maximo):
        raise EscritaInvalida(f"{p.rotulo} vai de {p.minimo:g} a {p.maximo:g}")
    return v


def valor_no_fio(codigo: str, valor) -> str:
    """Texto do valor no quadro: 1 casa (ponto) para dB, Q, cm e attack; inteiro para Hz,
    release, índice e fase; 1/0 para booleano; a, b ou a+b para o roteamento."""
    p = POR_CODIGO[codigo]
    if p.tipo == "texto":
        return str(valor)
    if p.tipo == "bool":
        return "1" if valor else "0"
    if p.casas == 1:
        return f"{float(valor) + 0.0:.1f}"
    return str(int(valor))


def montar_escrita(codigo: str, canal: int | None, valor) -> str:
    """`*CCC*v*`, `*CCC*c*v*` ou `020*b*v` (sem `*` nas pontas), com o valor validado."""
    v = valor_no_fio(codigo, normalizar_escrita(codigo, canal, valor))
    p = POR_CODIGO[codigo]
    if p.escopo == ESCOPO_BANDA:
        return f"{codigo}*{canal}*{v}"
    if p.escopo == ESCOPO_CANAL:
        return f"*{codigo}*{canal}*{v}*"
    return f"*{codigo}*{v}*"


def verificar_escrita(texto: str) -> bytes:
    """Bytes de um quadro de escrita, ou EscritaInvalida.

    O quadro precisa ser exatamente o que `montar_escrita` produz para um código e um
    valor válidos. Senha, reset, memórias, gerador, nome e remote out nunca passam."""
    if not isinstance(texto, str) or not texto.isascii():
        raise EscritaInvalida("escrita recusada: só texto ASCII")
    m = _ESCRITA_GRAFICO_RE.fullmatch(texto)
    if m:
        codigo, canal, bruto = "020", int(m.group(1)), m.group(2)
    else:
        m = _ESCRITA_RE.fullmatch(texto)
        if not m or m.group(1) == "020":
            raise EscritaInvalida(f"escrita recusada: {mascarar_texto(texto)!r} fora do formato")
        codigo, bruto = m.group(1), m.group(3)
        canal = int(m.group(2)) if m.group(2) is not None else None
    if montar_escrita(codigo, canal, bruto) != texto:
        raise EscritaInvalida(f"escrita recusada: {texto!r} fora do formato do app")
    return texto.encode("ascii")


def mesmo_valor(codigo: str, a, b) -> bool:
    """Compara o valor pedido com o relido, na resolução do protocolo."""
    p = POR_CODIGO[codigo]
    if a is None or b is None:
        return False
    if p.tipo == "texto":
        return str(a).lower() == str(b).lower()
    if p.tipo == "bool":
        return bool(a) == bool(b)
    try:
        if p.casas == 0:  # inteiro no fio: relido não inteiro (11.4) é outro valor
            return float(a) == float(b)
        return round(float(a), p.casas) == round(float(b), p.casas)
    except (TypeError, ValueError):
        return False


# --------------------------------------------------------------------------- máscara da senha

TEXTO_SENHA_MASCARADA = "*000*****"
_PREFIXO_SENHA_RE = re.compile(rb"\*00([01])\*")
_QUADRO_SENHA_RE = re.compile(rb"\*000\*[^*#]*\*?")


def _mascara(dados: bytes, senha: str | None) -> list[bool]:
    oculto = [False] * len(dados)
    # Senha no próprio protocolo: o segmento depois de *000* e os dois depois de *001*
    # (senha antiga e nova), em toda ocorrência, com ou sem saber a senha.
    for m in _PREFIXO_SENHA_RE.finditer(dados):
        faltam = 2 if m.group(1) == b"1" else 1
        for i in range(m.end(), len(dados)):
            if dados[i] in b"*#":
                faltam -= 1
                if faltam == 0:
                    break
            else:
                oculto[i] = True
    # Qualquer ocorrência da senha, em qualquer quadro, sem diferenciar maiúsculas.
    if senha:
        alvo = senha.upper().encode("ascii", "replace")
        norm = dados.upper()
        pos = norm.find(alvo)
        while pos != -1:
            for i in range(pos, pos + len(alvo)):
                oculto[i] = True
            pos = norm.find(alvo, pos + 1)
    return oculto


def mascarar(dados: bytes, senha: str | None = None, contexto: bytes = b"") -> tuple[str, str]:
    """(hex, ascii) do quadro com a senha escondida.

    `contexto` = final do quadro anterior na mesma direção, para pegar a senha quando ela
    vem partida entre dois fragmentos."""
    oculto = _mascara(contexto + dados, senha)[len(contexto):]
    hexa = " ".join("**" if oculto[i] else f"{b:02x}" for i, b in enumerate(dados))
    if any(oculto) and _QUADRO_SENHA_RE.fullmatch(dados):
        texto = TEXTO_SENHA_MASCARADA
    else:
        texto = "".join(
            "*" if oculto[i] else (chr(b) if 32 <= b < 127 else ".") for i, b in enumerate(dados)
        )
    return hexa, texto


def mascarar_texto(texto: str, senha: str | None = None) -> str:
    return mascarar(texto.encode("utf-8", "replace"), senha)[1]


# --------------------------------------------------------------------------- log bruto

TX = "TX"  # notebook -> processador
RX = "RX"  # processador -> notebook


class LogBruto:
    """Uma linha por quadro: horário, direção, hex e ASCII. A senha nunca entra."""

    def __init__(self, destino: IO[str], senha: str | None = None):
        self._destino = destino
        self._senha = senha
        self._anterior: dict[str, bytes] = {}  # fim do último quadro de cada direção

    def registrar(self, direcao: str, dados: bytes, nota: str = "") -> None:
        dados = bytes(dados)
        hexa, texto = mascarar(dados, self._senha, self._anterior.get(direcao, b""))
        self._anterior[direcao] = dados[-16:]
        hora = dt.datetime.now().isoformat(timespec="milliseconds")
        linha = f"{hora}  {direcao:<5} {hexa}  |{texto}|"
        if nota:
            linha += f"  {nota}"
        self._destino.write(linha + "\n")
        self._destino.flush()

    def ocultar(self, texto: str) -> str:
        """O texto como ele entra no log (senha mascarada)."""
        return mascarar_texto(texto, self._senha)

    def anotar(self, texto: str) -> None:
        hora = dt.datetime.now().isoformat(timespec="milliseconds")
        self._destino.write(f"{hora}  INFO  {mascarar_texto(texto, self._senha)}\n")
        self._destino.flush()


# --------------------------------------------------------------------------- respostas


@dataclass(frozen=True)
class Quadro:
    codigo: str
    segmentos: tuple[str, ...]
    cru: str


def analisar(texto: str) -> Quadro | None:
    """Quebra uma resposta como o app: troca '*' por '#', tira os separadores das
    pontas, divide em segmentos e exige 3 dígitos no primeiro (MAPA §2)."""
    t = texto.strip().replace("*", "#").strip("#")
    segs = t.split("#")
    if not segs or not re.fullmatch(r"\d{3}", segs[0]):
        return None
    return Quadro(segs[0], tuple(s.strip() for s in segs[1:]), texto)


_INICIO_QUADRO_RE = re.compile(r"(?<=[#*])(?=[#*]\d{3}[#*])")


def _pedacos(buffer: str) -> list[tuple[str, bool]]:
    """Divide o buffer em quadros. Devolve (texto, fechado). Só o último pode estar aberto."""
    linhas = re.split(r"[\r\n]+", buffer)
    aberto_no_fim = not re.search(r"[\r\n]$", buffer)
    saida: list[tuple[str, bool]] = []
    for i, linha in enumerate(linhas):
        partes = [p for p in _INICIO_QUADRO_RE.split(linha) if p]
        for j, parte in enumerate(partes):
            ultima = i == len(linhas) - 1 and j == len(partes) - 1
            saida.append((parte, not (ultima and aberto_no_fim)))
    return [(t, f) for t, f in saida if t.strip()]


@dataclass
class Casamento:
    cru: str | None  # quadro que respondeu à consulta (None = não veio)
    valor: str | None  # texto do valor dentro do quadro
    nota: str | None = None
    orfaos: list[str] = field(default_factory=list)


_PARCIAL = object()


def _extrair(consulta: Consulta, q: Quadro, termina_sep: bool, fechado: bool):
    """Valor (str, nota) se o quadro responde à consulta, _PARCIAL se pode ser o começo
    da resposta, ou None se não é dela."""
    if q.codigo != consulta.codigo:
        return None
    segs = q.segmentos
    p = consulta.parametro
    if p.codigo == "000":
        carga = "#".join(segs)
        if carga.upper() == "ERRO" or fechado:
            return carga, None
        return _PARCIAL
    pronto = fechado or termina_sep
    if p.escopo == ESCOPO_GLOBAL:
        if not segs or not pronto:
            return None if fechado and not segs else _PARCIAL
        nota = "segmentos a mais na resposta" if len(segs) > 1 else None
        return segs[-1], nota
    # canal ou banda
    if p.codigo == "020" and len(segs) == len(BANDAS_HZ):
        if not pronto:
            return _PARCIAL
        return segs[consulta.canal], "a resposta trouxe as 15 bandas"
    if len(segs) >= 2:
        if not _mesmo_canal(segs[0], consulta.canal):
            return None
        if not pronto:
            return _PARCIAL
        nota = "segmentos a mais na resposta" if len(segs) > 2 else None
        return segs[1], nota
    if len(segs) == 1:
        if not fechado:
            return _PARCIAL  # pode ser só "#081#0#" com o valor ainda a caminho
        return segs[0], "resposta sem canal"
    return None if fechado else _PARCIAL


def _mesmo_canal(seg: str, canal: int | None) -> bool:
    try:
        return int(seg) == canal
    except ValueError:
        return False


class Montador:
    """Junta os fragmentos de notificação e casa a resposta com a consulta pendente."""

    def __init__(self) -> None:
        self._buffer = ""

    def alimentar(self, dados: bytes) -> None:
        self._buffer += bytes(dados).decode("utf-8", errors="replace")

    @property
    def vazio(self) -> bool:
        return not self._buffer.strip()

    def descartar(self) -> list[str]:
        """Esvazia o buffer e devolve o que havia nele como órfãos."""
        orfaos = [t for t, _ in _pedacos(self._buffer)]
        self._buffer = ""
        return orfaos

    def casar(self, consulta: Consulta, final: bool = False,
              enviado: str | None = None, sem_asterisco: bool = False) -> Casamento | None:
        """Procura a resposta no buffer.

        Devolve None quando ainda pode chegar mais (nada ou só o começo da resposta).
        Com `final=True` (fim da espera), devolve o que houver, mesmo sem resposta.
        Um eco do que foi enviado (`enviado`, por padrão o texto da consulta) nunca
        conta como resposta: vira órfão. Com `sem_asterisco` (releitura depois de uma
        escrita), um quadro com `*` (eco no formato de escrita, como `*081*0*-12.0*` ou
        `020*3*2.5`) nunca conta como resposta: vira órfão."""
        eco = analisar(enviado if enviado is not None else consulta.texto)
        pedacos = _pedacos(self._buffer)
        achado: tuple[int, str, str | None] | None = None
        esperar = False
        for i, (texto, fechado) in enumerate(pedacos):
            if sem_asterisco and "*" in texto:
                continue
            q = analisar(texto)
            if q is None:
                continue
            if (fechado or final) and eco is not None and                     (q.codigo, q.segmentos) == (eco.codigo, eco.segmentos):
                continue  # eco do envio
            r = _extrair(consulta, q, texto.rstrip().endswith(("#", "*")), fechado or final)
            if r is _PARCIAL:
                esperar = True
            elif r is not None:
                achado = (i, r[0], r[1])
                break
        if achado is None and (esperar or not final):
            return None
        if achado is None:
            return Casamento(None, None, None, self.descartar())
        i, valor, nota = achado
        orfaos = [t for j, (t, _) in enumerate(pedacos) if j != i]
        self._buffer = ""
        return Casamento(pedacos[i][0], valor, nota, orfaos)


# --------------------------------------------------------------------------- conversão


def converter(p: Parametro, texto: str) -> dict:
    """Converte o texto do valor. Devolve {valor, rotulo?, fora_da_faixa?, motivo?}."""
    s = texto.strip()
    try:
        if p.tipo == "texto":
            v = s.lower()
            if v == "ab":
                v = "a+b"  # a leitura aceita também "ab" (MAPA §4)
            if p.opcoes is not None:
                if v not in p.opcoes:
                    return {"valor": v, "fora_da_faixa": True}
                return {"valor": v, "rotulo": p.opcoes[v]}
            return {"valor": s}
        numero = float(s)
    except ValueError:
        return {"valor": None, "motivo": "valor ilegível"}
    if numero != numero or numero in (float("inf"), float("-inf")):
        return {"valor": None, "motivo": "valor ilegível"}

    if p.tipo == "bool":
        return {"valor": numero != 0}  # na leitura, ≠0 = ligado/mutado (MAPA §4)
    if p.tipo in ("indice", "graus") or p.casas == 0:
        if numero != int(numero):
            return {"valor": numero, "fora_da_faixa": True}
        valor = int(numero)
    else:
        valor = round(numero, 3)

    saida: dict = {"valor": valor}
    if p.especiais and valor in p.especiais:
        saida["rotulo"] = p.especiais[valor]
        return saida
    if p.opcoes is not None:
        if valor in p.opcoes:
            saida["rotulo"] = p.opcoes[valor]
        else:
            saida["fora_da_faixa"] = True
        return saida
    if (p.minimo is not None and valor < p.minimo) or (p.maximo is not None and valor > p.maximo):
        saida["fora_da_faixa"] = True
    return saida


def leitura(consulta: Consulta, casamento: Casamento | None, motivo: str | None = None) -> dict:
    """Uma entrada do retrato: código, canal, nome, valor convertido, unidade e resposta crua."""
    p = consulta.parametro
    item: dict = {"codigo": p.codigo, "canal": consulta.canal, "nome": p.nome}
    if p.escopo == ESCOPO_BANDA:
        item["banda_hz"] = BANDAS_HZ[consulta.canal]
    if casamento is None or casamento.cru is None:
        item.update({"valor": None, "unidade": p.unidade, "cru": None,
                     "motivo": motivo or "timeout"})
        return item
    conv = converter(p, casamento.valor or "")
    item["valor"] = conv["valor"]
    item["unidade"] = p.unidade
    item["cru"] = casamento.cru
    for chave in ("rotulo", "fora_da_faixa", "motivo"):
        if chave in conv:
            item[chave] = conv[chave]
    if casamento.nota:
        item["nota"] = casamento.nota
    return item


def firmware_de(carga: str) -> str:
    """O firmware são os últimos 4 caracteres da resposta 000 (MAPA §3)."""
    return carga.strip()[-4:]


def formatar_valor(item: dict) -> str:
    """Valor como o Denis vê no app: rótulo quando há, senão número e unidade."""
    if item.get("valor") is None:
        return "sem leitura"
    if item.get("rotulo"):
        return str(item["rotulo"])
    v = item["valor"]
    p = POR_CODIGO.get(item.get("codigo", ""))
    if isinstance(v, bool):
        if p is not None and p.nome == "mute":
            return "mutado" if v else "sem mute"
        return "ligado" if v else "desligado"
    if isinstance(v, float) and p is not None and p.casas == 1:
        texto = f"{v:.1f}"
    else:
        texto = f"{v}"
    unidade = item.get("unidade")
    texto = f"{texto} {unidade}" if unidade else texto
    if p is not None and p.nome == "limiter_threshold":
        # O app mostra o threshold em volts: V = 2,0 × 10^(dB/20) (MAPA §7).
        texto += f" (no app: {2.0 * 10 ** (v / 20):.2f}V)"
    return texto


# --------------------------------------------------------------------------- conferência

# Ordem das telas no app, para o Denis andar sempre para a frente na conferência.
ORDEM_TELAS = (
    "Nível das Saídas", "Home", "Roteam. Ent/Saída", "Fase", "Alinhamento", "EQ Param Entr",
    "Crossover", "Limiter", "EQ Param Saída", "EQ 15 Bandas",
)

# Ordem de prioridade para completar os tipos que os críticos não cobrem.
_EXTRAS_POR_TIPO = ("010", "060", "050", "072", "092", "073", "032")


def amostra_conferencia(retrato: dict) -> list[dict]:
    """Amostra guiada para conferir um retrato (uma memória) com o app.

    - Cada parâmetro crítico (ganho, HPF/LPF frequência e filtro, limiter threshold e
      auto) entra nos 4 canais: são os que protegem os falantes.
    - Cada tipo de parâmetro do catálogo aparece pelo menos uma vez.
    - O EQ de 15 bandas entra com a banda de maior ganho, porque o formato dele é diferente.
    Resultado: ~34 itens, em ordem de tela do app, conferidos um por vez.
    """
    leituras = retrato.get("leituras", [])
    por_chave = {(x["codigo"], x.get("canal")): x for x in leituras}

    def pegar(codigo: str, canal_preferido: int | None) -> dict | None:
        p = POR_CODIGO[codigo]
        if p.escopo == ESCOPO_GLOBAL:
            candidatos = [None]
        elif p.escopo == ESCOPO_CANAL:
            candidatos = [(canal_preferido + k) % 4 for k in range(4)]
        else:
            candidatos = list(range(len(BANDAS_HZ)))
        lidos = [por_chave.get((codigo, c)) for c in candidatos]
        lidos = [x for x in lidos if x is not None and x.get("valor") is not None]
        return lidos[0] if lidos else None

    itens: list[dict] = []
    rodizio = 0
    for p in CATALOGO:
        if p.critico:
            for c in CANAIS:
                x = por_chave.get((p.codigo, c))
                if x is not None and x.get("valor") is not None:
                    itens.append(x)
    tipos_cobertos = {POR_CODIGO[x["codigo"]].tipo for x in itens}
    for codigo in _EXTRAS_POR_TIPO + tuple(p.codigo for p in CATALOGO):
        tipo = POR_CODIGO[codigo].tipo
        if tipo in tipos_cobertos:
            continue
        x = pegar(codigo, rodizio % 4)
        if x is not None:
            itens.append(x)
            tipos_cobertos.add(tipo)
            rodizio += 1
    bandas = [x for x in leituras if x["codigo"] == "020" and x.get("valor") is not None]
    if bandas:
        itens.append(max(bandas, key=lambda x: (abs(x["valor"]), -x["canal"])))

    itens.sort(key=lambda x: ORDEM_TELAS.index(POR_CODIGO[x["codigo"]].tela))
    saida = []
    for x in itens:
        p = POR_CODIGO[x["codigo"]]
        if p.escopo == ESCOPO_CANAL:
            onde = f"saída {x['canal'] + 1}"
        elif p.escopo == ESCOPO_BANDA:
            onde = f"banda {_hz(BANDAS_HZ[x['canal']])}"
        else:
            onde = ""
        saida.append({
            "codigo": x["codigo"], "canal": x.get("canal"), "nome": x["nome"],
            "tipo": p.tipo, "critico": p.critico, "tela": p.tela, "rotulo": p.rotulo,
            "onde": onde, "valor": x["valor"], "mostrado": formatar_valor(x), "cru": x.get("cru"),
        })
    return saida
