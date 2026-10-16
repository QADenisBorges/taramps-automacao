#!/usr/bin/env python3
"""Lista as operações ATT de um HCI snoop log (btsnoop) e cruza com a tabela de anotação.

Uso típico (ver protocolo/ROTEIRO-CAPTURA.md):

    python -I ferramentas/btsnoop_att.py protocolo/capturas/bruto/C1-bugreport.zip \
        --anotacao protocolo/capturas/C1-anotacao.csv

A entrada pode ser o arquivo btsnoop ou o .zip do `adb bugreport` (o btsnoop é
procurado dentro dele). Só usa a biblioteca padrão.

O número do quadro é o índice do registro no btsnoop, a partir de 1, igual ao do
Wireshark. Quando a PDU ATT vem fragmentada, o quadro citado é o do último fragmento.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import io
import os
import struct
import sys
import zipfile
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

BTSNOOP_MAGIC = b"btsnoop\x00"
# Microssegundos entre 0000-01-01 (época do btsnoop) e 1970-01-01.
EPOCH_DELTA_US = 0x00DCDDB30F2F8000

DLT_H1 = 1001  # HCI sem encapsulamento: o tipo vem das flags do registro
DLT_H4 = 1002  # HCI UART (H4): o 1º byte é o tipo do pacote (Android)

H4_CMD, H4_ACL, H4_SCO, H4_EVT, H4_ISO = 1, 2, 3, 4, 5

ATT_CID = 0x0004

SENT = "cel->mod"  # host -> controlador: o celular (central) envia ao processador
RECV = "mod->cel"

ATT_OPCODES = {
    0x01: "Error Response",
    0x02: "Exchange MTU Request",
    0x03: "Exchange MTU Response",
    0x04: "Find Information Request",
    0x05: "Find Information Response",
    0x06: "Find By Type Value Request",
    0x07: "Find By Type Value Response",
    0x08: "Read By Type Request",
    0x09: "Read By Type Response",
    0x0A: "Read Request",
    0x0B: "Read Response",
    0x0C: "Read Blob Request",
    0x0D: "Read Blob Response",
    0x0E: "Read Multiple Request",
    0x0F: "Read Multiple Response",
    0x10: "Read By Group Type Request",
    0x11: "Read By Group Type Response",
    0x12: "Write Request",
    0x13: "Write Response",
    0x16: "Prepare Write Request",
    0x17: "Prepare Write Response",
    0x18: "Execute Write Request",
    0x19: "Execute Write Response",
    0x1B: "Handle Value Notification",
    0x1D: "Handle Value Indication",
    0x1E: "Handle Value Confirmation",
    0x20: "Read Multiple Variable Request",
    0x21: "Read Multiple Variable Response",
    0x23: "Multiple Handle Value Notification",
    0x52: "Write Command",
    0xD2: "Signed Write Command",
}

# Categorias exibidas por padrão. O resto (descoberta, MTU, confirmações) só com --tudo.
CAT_WRITE = "escrita"
CAT_NOTIFY = "notificacao"
CAT_READ = "leitura"
CAT_ERROR = "erro"
CAT_OTHER = "outro"

_CATEGORY = {
    0x12: CAT_WRITE, 0x52: CAT_WRITE, 0xD2: CAT_WRITE, 0x16: CAT_WRITE, 0x18: CAT_WRITE,
    0x1B: CAT_NOTIFY, 0x1D: CAT_NOTIFY, 0x23: CAT_NOTIFY,
    0x0A: CAT_READ, 0x0B: CAT_READ, 0x0C: CAT_READ, 0x0D: CAT_READ,
    0x0E: CAT_READ, 0x0F: CAT_READ, 0x20: CAT_READ, 0x21: CAT_READ,
    0x01: CAT_ERROR,
}
DEFAULT_CATEGORIES = {CAT_WRITE, CAT_NOTIFY, CAT_READ, CAT_ERROR}

# Escritas que contam no cruzamento com a anotação (Execute Write só confirma a fila).
_WRITE_OPCODES_WITH_VALUE = {0x12, 0x52, 0xD2, 0x16}

UUID_PRIMARY_SERVICE = 0x2800
UUID_CHARACTERISTIC = 0x2803
_DECLARATION_UUIDS = {"0x2800", "0x2801", "0x2803"}


class BtsnoopError(ValueError):
    """Arquivo que não é btsnoop ou está corrompido."""


# --------------------------------------------------------------------------- leitura


@dataclass
class Record:
    frame: int
    timestamp_us: int  # desde 0000-01-01, como gravado no arquivo
    sent: bool
    ptype: int  # H4_*
    data: bytes  # sem o byte de tipo H4
    truncated: bool = False  # orig_len > incl_len: log filtrado ou cortado


def parse_btsnoop(raw: bytes) -> tuple[int, list[Record]]:
    """Lê um btsnoop v1 e devolve (datalink, registros)."""
    if len(raw) < 16 or raw[:8] != BTSNOOP_MAGIC:
        raise BtsnoopError("não é um arquivo btsnoop (cabeçalho 'btsnoop\\0' ausente)")
    version, datalink = struct.unpack(">II", raw[8:16])
    if version != 1:
        raise BtsnoopError(f"versão btsnoop {version} não suportada")
    if datalink not in (DLT_H1, DLT_H4):
        raise BtsnoopError(f"datalink {datalink} não suportado (esperado 1001 ou 1002)")

    records: list[Record] = []
    pos = 16
    frame = 0
    while pos + 24 <= len(raw):
        orig_len, incl_len, flags, _drops, ts = struct.unpack(">IIIIq", raw[pos:pos + 24])
        pos += 24
        payload = raw[pos:pos + incl_len]
        pos += incl_len
        frame += 1
        if len(payload) < incl_len:
            break  # registro truncado no fim do arquivo (log ainda sendo gravado)
        sent = (flags & 0x01) == 0
        if datalink == DLT_H4:
            if not payload:
                continue
            ptype, data = payload[0], payload[1:]
        else:
            if flags & 0x02:
                ptype = H4_CMD if sent else H4_EVT
            else:
                ptype = H4_ACL
            data = payload
        records.append(Record(frame, ts, sent, ptype, data, truncated=orig_len > incl_len))
    return datalink, records


def load_capture(path: Path, entry: str | None = None) -> tuple[bytes, str]:
    """Devolve (bytes do btsnoop, descrição da origem). Aceita btsnoop ou .zip do bugreport."""
    raw = path.read_bytes()
    if raw[:8] == BTSNOOP_MAGIC:
        return raw, str(path)
    if not zipfile.is_zipfile(io.BytesIO(raw)):
        raise BtsnoopError(f"{path}: nem btsnoop nem .zip")
    with zipfile.ZipFile(io.BytesIO(raw)) as zf:
        names = zf.namelist()
        if entry is not None:
            if entry not in names:
                raise BtsnoopError(f"entrada '{entry}' não existe no zip")
            chosen = entry
        else:
            candidates = [n for n in names if "btsnoop" in n.lower() and not n.endswith("/")]
            if not candidates:
                raise BtsnoopError(
                    "nenhum btsnoop dentro do zip; confira se o snoop log estava 'Ativado'"
                )
            # Prefere o log da sessão atual (btsnoop_hci.log) ao da anterior (.last).
            candidates.sort(key=lambda n: (n.endswith(".last"), "filtered" in n.lower(), len(n)))
            chosen = candidates[0]
        data = zf.read(chosen)
    if data[:8] != BTSNOOP_MAGIC:
        raise BtsnoopError(f"'{chosen}' dentro do zip não é btsnoop")
    return data, f"{path} :: {chosen}"


# --------------------------------------------------------------------------- ATT


@dataclass
class AttOp:
    frame: int
    timestamp_us: int
    sent: bool
    conn: int
    opcode: int
    handle: int | None
    value: bytes
    offset: int | None = None
    ignored: bool = False  # marcada como polling / ignorada no cruzamento

    @property
    def name(self) -> str:
        return ATT_OPCODES.get(self.opcode, f"Opcode 0x{self.opcode:02X}")

    @property
    def category(self) -> str:
        return _CATEGORY.get(self.opcode, CAT_OTHER)

    @property
    def is_write(self) -> bool:
        return self.sent and self.opcode in _WRITE_OPCODES_WITH_VALUE


@dataclass
class GattAttribute:
    handle: int
    uuid: str
    kind: str  # "servico", "caracteristica", "declaracao", "descritor"
    end_handle: int | None = None
    properties: int | None = None


@dataclass
class AttTrace:
    datalink: int
    records: int
    first_us: int | None
    last_us: int | None
    ops: list[AttOp] = field(default_factory=list)
    connections: dict[int, str] = field(default_factory=dict)
    gatt: dict[int, GattAttribute] = field(default_factory=dict)
    orphan_fragments: int = 0
    truncated_records: int = 0


def format_uuid(b: bytes) -> str:
    if len(b) == 2:
        return f"0x{struct.unpack('<H', b)[0]:04X}"
    if len(b) == 16:
        h = b[::-1].hex()
        return f"{h[:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:]}"
    return b.hex()


def _props_text(p: int) -> str:
    names = [(0x02, "read"), (0x04, "write-no-rsp"), (0x08, "write"), (0x10, "notify"),
             (0x20, "indicate"), (0x40, "signed-write"), (0x01, "broadcast"), (0x80, "ext")]
    return ",".join(n for bit, n in names if p & bit) or "-"


_REQUEST_OPS = {0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x12, 0x16, 0x18, 0x20}
_RESPONSE_OPS = {0x01, 0x03, 0x05, 0x07, 0x09, 0x0B, 0x0D, 0x0F, 0x11, 0x13, 0x17, 0x19, 0x21}
_REQUESTS_WITH_HANDLE = {0x0A, 0x0C, 0x12}


class _AttDecoder:
    def __init__(self, trace: AttTrace):
        self.trace = trace
        # Uma requisição pendente por (conexão, lado que pediu): o ATT é sequencial por bearer.
        self.pending: dict[tuple[int, bool], tuple[int, bytes]] = {}

    def feed(self, frame: int, ts: int, sent: bool, conn: int, pdu: bytes) -> None:
        if not pdu:
            return
        op, p = pdu[0], pdu[1:]
        ops = self.trace.ops
        req = None
        if op in _REQUEST_OPS:
            self.pending[(conn, sent)] = (op, p)
        elif op in _RESPONSE_OPS:
            req = self.pending.pop((conn, not sent), None)

        def add(handle, value, offset=None, opcode=op):
            ops.append(AttOp(frame, ts, sent, conn, opcode, handle, value, offset))

        if op in (0x12, 0x52, 0x1B, 0x1D, 0x0A) and len(p) >= 2:
            add(struct.unpack("<H", p[:2])[0], p[2:])
        elif op == 0xD2 and len(p) >= 2:
            add(struct.unpack("<H", p[:2])[0], p[2:-12] if len(p) >= 14 else p[2:])
        elif op in (0x16, 0x17) and len(p) >= 4:
            h, off = struct.unpack("<HH", p[:4])
            add(h, p[4:], off)
        elif op == 0x0C and len(p) >= 4:
            h, off = struct.unpack("<HH", p[:4])
            add(h, b"", off)
        elif op in (0x0B, 0x0D, 0x13):
            ok = req is not None and req[0] in _REQUESTS_WITH_HANDLE and len(req[1]) >= 2
            add(struct.unpack("<H", req[1][:2])[0] if ok else None, p)
        elif op == 0x01 and len(p) >= 4:
            req_op, h, err = p[0], struct.unpack("<H", p[1:3])[0], p[3]
            add(h, bytes([req_op, err]))
        elif op == 0x23:
            i = 0
            while i + 4 <= len(p):
                h, ln = struct.unpack("<HH", p[i:i + 4])
                add(h, p[i + 4:i + 4 + ln])
                i += 4 + ln
        else:
            add(None, p)

        # Descoberta do GATT do processador: o celular pede, o processador responde.
        if req is not None and not sent:
            self._discovery(req, op, p)

    def _discovery(self, req: tuple[int, bytes], op: int, p: bytes) -> None:
        req_op, rp = req
        gatt = self.trace.gatt
        if req_op == 0x10 and op == 0x11 and p:
            req_uuid = rp[4:]
            if req_uuid != struct.pack("<H", UUID_PRIMARY_SERVICE):
                return
            ln = p[0]
            for i in range(1, len(p) - ln + 1, ln):
                e = p[i:i + ln]
                start, end = struct.unpack("<HH", e[:4])
                gatt[start] = GattAttribute(start, format_uuid(e[4:]), "servico", end_handle=end)
        elif req_op == 0x08 and op == 0x09 and p:
            if rp[4:] != struct.pack("<H", UUID_CHARACTERISTIC):
                return
            ln = p[0]
            for i in range(1, len(p) - ln + 1, ln):
                e = p[i:i + ln]
                if len(e) < 7:
                    continue
                props = e[2]
                value_handle = struct.unpack("<H", e[3:5])[0]
                gatt[value_handle] = GattAttribute(
                    value_handle, format_uuid(e[5:]), "caracteristica", properties=props
                )
        elif req_op == 0x04 and op == 0x05 and p:
            fmt, step = p[0], (4 if p[0] == 1 else 18)
            if fmt not in (1, 2):
                return
            for i in range(1, len(p) - step + 1, step):
                h = struct.unpack("<H", p[i:i + 2])[0]
                if h not in gatt:
                    uuid = format_uuid(p[i + 2:i + step])
                    kind = "declaracao" if uuid in _DECLARATION_UUIDS else "descritor"
                    gatt[h] = GattAttribute(h, uuid, kind)


def extract_att(datalink: int, records: list[Record]) -> AttTrace:
    trace = AttTrace(
        datalink=datalink,
        records=len(records),
        first_us=min(r.timestamp_us for r in records) if records else None,
        last_us=max(r.timestamp_us for r in records) if records else None,
        truncated_records=sum(r.truncated for r in records),
    )
    decoder = _AttDecoder(trace)
    buffers: dict[tuple[int, bool], bytearray] = {}

    for r in records:
        if r.ptype == H4_EVT:
            _handle_event(trace, buffers, r.data)
            continue
        if r.ptype != H4_ACL or len(r.data) < 4:
            continue
        hf, dlen = struct.unpack("<HH", r.data[:4])
        conn, pb = hf & 0x0FFF, (hf >> 12) & 0x3
        frag = r.data[4:4 + dlen]
        key = (conn, r.sent)
        if pb == 0b01:  # continuação
            if key not in buffers:
                trace.orphan_fragments += 1
                continue
            buffers[key].extend(frag)
        else:  # início (00, 10) ou PDU completa (11)
            buffers[key] = bytearray(frag)
        buf = buffers[key]
        if len(buf) < 4:
            continue
        l2len, cid = struct.unpack("<HH", buf[:4])
        if len(buf) < l2len + 4:
            continue
        del buffers[key]
        if cid == ATT_CID:
            decoder.feed(r.frame, r.timestamp_us, r.sent, conn, bytes(buf[4:4 + l2len]))
    return trace


def _handle_event(trace: AttTrace, buffers: dict, data: bytes) -> None:
    if len(data) < 2:
        return
    code, params = data[0], data[2:]
    if code == 0x3E and params and params[0] in (0x01, 0x0A, 0x29) and len(params) >= 12:
        status = params[1]
        if status == 0:
            conn = struct.unpack("<H", params[2:4])[0] & 0x0FFF
            addr = params[6:12][::-1]
            trace.connections[conn] = ":".join(f"{b:02X}" for b in addr)
    elif code == 0x05 and len(params) >= 3:
        conn = struct.unpack("<H", params[1:3])[0] & 0x0FFF
        buffers.pop((conn, True), None)
        buffers.pop((conn, False), None)


# --------------------------------------------------------------------------- polling e senha


def mark_polling(ops: list[AttOp], min_repeats: int, ignore_handles: set[int]) -> list[tuple[int, bytes, int]]:
    """Marca como ignoradas as escritas repetidas idênticas (polling) e as dos handles ignorados.

    Devolve [(handle, valor, repetições)] das escritas consideradas polling.
    """
    counts = Counter((o.handle, o.value) for o in ops if o.is_write)
    polling = [(h, v, n) for (h, v), n in counts.items() if min_repeats > 0 and n >= min_repeats]
    poll_keys = {(h, v) for h, v, _ in polling}
    for o in ops:
        if o.is_write and ((o.handle, o.value) in poll_keys or o.handle in ignore_handles):
            o.ignored = True
    return sorted(polling, key=lambda t: -t[2])


def password_patterns(password: str | None) -> list[bytes]:
    """Codificações óbvias da senha de 4 dígitos. A máscara não cobre codificações ofuscadas."""
    if not password:
        return []
    pats = [password.encode("ascii")]
    if password.isdigit():
        n = int(password)
        pats.append(bytes(int(c) for c in password))
        if len(password) % 2 == 0:
            pats.append(bytes(int(password[i:i + 2], 16) for i in range(0, len(password), 2)))  # BCD
        if n <= 0xFFFF:
            pats += [struct.pack("<H", n), struct.pack(">H", n)]
    return [p for p in dict.fromkeys(pats) if p]


TRUNCATED_WARNING = (
    "AVISO: {n} registro(s) cortado(s) no log (tamanho original maior que o gravado). "
    "Use o modo 'Ativado' do registro HCI, não o filtrado, e refaça a sessão."
)
NO_PASSWORD_WARNING = (
    "aviso: sem senha (TARAMPS_SENHA), o hex sai sem máscara; não versione este resultado."
)


def _hidden_mask(value: bytes, patterns: list[bytes]) -> list[bool]:
    hidden = [False] * len(value)
    for pat in patterns:
        start = value.find(pat)
        while start != -1:
            for i in range(start, start + len(pat)):
                hidden[i] = True
            start = value.find(pat, start + 1)
    return hidden


def is_masked(value: bytes, patterns: list[bytes]) -> bool:
    return any(_hidden_mask(value, patterns))


def masked_hex(value: bytes, patterns: list[bytes]) -> str:
    hidden = _hidden_mask(value, patterns)
    return " ".join("**" if hidden[i] else f"{b:02x}" for i, b in enumerate(value))


# --------------------------------------------------------------------------- anotação


@dataclass
class Annotation:
    step: str
    kind: str  # passo | posicionamento | conexao
    start: dt.time
    end: dt.time | None
    parameter: str
    channel: str
    old: str
    new: str
    note: str


ANNOTATION_COLUMNS = ["passo", "tipo", "inicio", "fim", "parametro", "canal", "antigo", "novo", "obs"]
ANNOTATION_KINDS = {"passo", "posicionamento", "conexao"}


def _parse_time(text: str) -> dt.time:
    text = text.strip()
    for fmt in ("%H:%M:%S.%f", "%H:%M:%S", "%H:%M"):
        try:
            return dt.datetime.strptime(text, fmt).time()
        except ValueError:
            pass
    raise ValueError(f"horário inválido: '{text}' (use HH:MM:SS)")


def read_annotations(path: Path) -> list[Annotation]:
    data = path.read_bytes()
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError:
        text = data.decode("cp1252")  # CSV comum do Excel pt-BR
    first = text.splitlines()[0] if text.strip() else ""
    delim = ";" if first.count(";") > first.count(",") else ","
    reader = csv.DictReader(io.StringIO(text), delimiter=delim)
    if reader.fieldnames is None:
        raise ValueError(f"{path}: anotação vazia")
    fields = [f.strip().lower() for f in reader.fieldnames]
    missing = {"passo", "inicio"} - set(fields)
    if missing:
        raise ValueError(f"{path}: faltam colunas {sorted(missing)}; esperado {ANNOTATION_COLUMNS}")
    out = []
    for row in reader:
        if None in row:
            raise ValueError(
                f"{path}: linha {reader.line_num} tem campos a mais; confira o separador ';' "
                "e se algum valor tem ';' ou ',' sobrando"
            )
        row = {(k or "").strip().lower(): (v or "").strip() for k, v in row.items()}
        if not any(row.values()):
            continue
        kind = (row.get("tipo") or "passo").lower()
        if kind not in ANNOTATION_KINDS:
            raise ValueError(f"{path}: tipo '{kind}' inválido no passo {row.get('passo')}")
        start = _parse_time(row["inicio"])
        end = _parse_time(row["fim"]) if row.get("fim") else None
        if end is not None and end < start:
            gap = (dt.datetime.combine(dt.date.min, start) - dt.datetime.combine(dt.date.min, end)).total_seconds()
            if gap < 12 * 3600:
                raise ValueError(f"{path}: passo {row.get('passo')}: fim {end} antes do inicio {start}")
        out.append(Annotation(
            step=row.get("passo", ""),
            kind=kind,
            start=start,
            end=end,
            parameter=row.get("parametro", ""),
            channel=row.get("canal", ""),
            old=row.get("antigo", ""),
            new=row.get("novo", ""),
            note=row.get("obs", ""),
        ))
    return out


# --------------------------------------------------------------------------- relógio


def us_to_datetime(timestamp_us: int, utc_offset_h: float) -> dt.datetime:
    unix_us = timestamp_us - EPOCH_DELTA_US
    base = dt.datetime(1970, 1, 1) + dt.timedelta(microseconds=unix_us)
    return base + dt.timedelta(hours=utc_offset_h)


def local_utc_offset_h() -> float:
    off = dt.datetime.now().astimezone().utcoffset()
    return off.total_seconds() / 3600 if off else 0.0


def _anchor(t: dt.time, span: tuple[dt.datetime, dt.datetime]) -> dt.datetime:
    """Põe o horário anotado na data da captura (trata captura que passa da meia-noite)."""
    lo, hi = span
    best = None
    for day in (lo.date() - dt.timedelta(days=1), lo.date(), hi.date(), hi.date() + dt.timedelta(days=1)):
        cand = dt.datetime.combine(day, t)
        dist = 0.0 if lo <= cand <= hi else min(abs((cand - lo).total_seconds()), abs((cand - hi).total_seconds()))
        if best is None or dist < best[0]:
            best = (dist, cand)
    return best[1]


# --------------------------------------------------------------------------- cruzamento


@dataclass
class Burst:
    ops: list[AttOp]
    start: dt.datetime
    end: dt.datetime
    annotation: int | None = None  # índice em annotations

    @property
    def handles(self) -> list[int | None]:
        return list(dict.fromkeys(o.handle for o in self.ops))


@dataclass
class CrossCheck:
    bursts: list[Burst]
    annotations: list[Annotation]
    status: list[str]  # por anotação
    orphans: list[Burst]
    redo: list[str]

    @property
    def valid(self) -> bool:
        return not self.redo


def cross_check(
    ops: list[AttOp],
    annotations: list[Annotation],
    utc_offset_h: float,
    window_s: float = 8.0,
    burst_gap_s: float = 1.0,
) -> CrossCheck:
    writes = [o for o in ops if o.is_write and not o.ignored]
    bursts: list[Burst] = []
    for o in writes:
        t = us_to_datetime(o.timestamp_us, utc_offset_h)
        if bursts and (t - bursts[-1].end).total_seconds() <= burst_gap_s:
            bursts[-1].ops.append(o)
            bursts[-1].end = t
        else:
            bursts.append(Burst([o], t, t))

    all_times = [us_to_datetime(o.timestamp_us, utc_offset_h) for o in ops] or [dt.datetime(1970, 1, 1)]
    span = (min(all_times), max(all_times))
    windows = []
    for a in annotations:
        s = _anchor(a.start, span)
        e = _anchor(a.end, span) if a.end else s
        if e < s:
            e += dt.timedelta(days=1)
        windows.append((s, e))

    win = dt.timedelta(seconds=window_s)
    for b in bursts:
        best = None
        for i, (s, e) in enumerate(windows):
            if s - win <= b.start <= e + win:
                dist = 0.0 if s <= b.start <= e else min(abs((b.start - s).total_seconds()), abs((b.start - e).total_seconds()))
                if best is None or dist < best[0]:
                    best = (dist, i)
        b.annotation = best[1] if best else None

    status, redo = [], []
    for i, a in enumerate(annotations):
        mine = [b for b in bursts if b.annotation == i]
        if a.kind != "passo":
            status.append(f"ok ({len(mine)} rajada(s), {a.kind})")
            continue
        if len(mine) == 1:
            n_ops, n_h = len(mine[0].ops), len(mine[0].handles)
            status.append("ok" if n_ops == 1 else f"ok ({n_ops} escritas em {n_h} handle(s))")
        elif not mine:
            status.append("SEM ESCRITA")
            redo.append(f"passo {a.step}: nenhuma escrita perto de {a.start} (confira o horário ou --ajuste-s)")
        else:
            status.append(f"AMBIGUA ({len(mine)} rajadas)")
            redo.append(f"passo {a.step}: {len(mine)} rajadas de escrita perto de {a.start}")

    orphans = [b for b in bursts if b.annotation is None]
    for b in orphans:
        redo.append(f"escrita sem anotação às {b.start.time()} (handle(s) {_fmt_handles(b.handles)})")
    return CrossCheck(bursts, annotations, status, orphans, redo)


# --------------------------------------------------------------------------- saída


def _fmt_handle(h: int | None) -> str:
    return "-" if h is None else f"0x{h:04X}"


def _fmt_handles(hs) -> str:
    return ",".join(_fmt_handle(h) for h in hs)


def _fmt_time(t: dt.datetime) -> str:
    return t.strftime("%H:%M:%S.") + f"{t.microsecond // 1000:03d}"


def render_report(
    trace: AttTrace,
    source: str,
    utc_offset_h: float,
    categories: set[str],
    patterns: list[bytes],
    polling: list[tuple[int, bytes, int]],
    check: CrossCheck | None,
) -> str:
    out: list[str] = []
    w = out.append
    w(f"Arquivo: {source}")
    dl = {DLT_H1: "H1", DLT_H4: "H4"}.get(trace.datalink, str(trace.datalink))
    if trace.first_us is not None:
        a = us_to_datetime(trace.first_us, utc_offset_h)
        b = us_to_datetime(trace.last_us, utc_offset_h)
        w(f"Formato: btsnoop v1 {dl}; {trace.records} quadros; de {a:%Y-%m-%d %H:%M:%S} a {b:%H:%M:%S} (UTC{utc_offset_h:+g}h)")
    else:
        w(f"Formato: btsnoop v1 {dl}; nenhum quadro")
    w(f"Operações ATT: {len(trace.ops)}" + (f"; fragmentos órfãos: {trace.orphan_fragments}" if trace.orphan_fragments else ""))
    if trace.truncated_records:
        w(TRUNCATED_WARNING.format(n=trace.truncated_records))
    if trace.connections:
        w("Conexões LE: " + "; ".join(f"0x{c:04X} = {addr}" for c, addr in sorted(trace.connections.items())))

    if trace.gatt:
        w("")
        w("GATT descoberto na captura:")
        for h in sorted(trace.gatt):
            g = trace.gatt[h]
            if g.kind == "servico":
                w(f"  servico        0x{g.handle:04X}-0x{g.end_handle:04X}  {g.uuid}")
            elif g.kind == "caracteristica":
                w(f"  caracteristica 0x{g.handle:04X}  {g.uuid}  [{_props_text(g.properties or 0)}]")
            else:
                w(f"  {g.kind:14} 0x{g.handle:04X}  {g.uuid}")
    else:
        w("GATT: nenhuma descoberta de serviços na captura (o Android pode ter usado o cache).")

    w("")
    w(f"{'quadro':>7}  {'horario':12}  {'conexao':7}  {'sentido':8}  {'operacao':34}  {'handle':6}  hex")
    for o in trace.ops:
        if o.category not in categories:
            continue
        t = _fmt_time(us_to_datetime(o.timestamp_us, utc_offset_h))
        name = o.name + (f" @{o.offset}" if o.offset else "") + (" (polling)" if o.ignored else "")
        w(f"{o.frame:>7}  {t:12}  0x{o.conn:04X}   {SENT if o.sent else RECV:8}  {name:34}  {_fmt_handle(o.handle):6}  {masked_hex(o.value, patterns)}")

    masked_frames = sorted({o.frame for o in trace.ops if o.category in categories and is_masked(o.value, patterns)})
    if masked_frames:
        w("")
        w("Quadros com máscara de senha (conferir sem máscara, localmente, antes de citar): "
          + ", ".join(str(f) for f in masked_frames))

    if polling:
        w("")
        w("Escritas repetidas tratadas como polling (fora do cruzamento):")
        for h, v, n in polling:
            w(f"  {_fmt_handle(h)}  {masked_hex(v, patterns)}  x{n}")

    if check is not None:
        w("")
        w("Cruzamento com a anotação:")
        for i, (a, st) in enumerate(zip(check.annotations, check.status)):
            w(f"  passo {a.step:>3}  {a.start}  {a.parameter} {a.channel} {a.old} -> {a.new}  [{st}]")
            for b in check.bursts:
                if b.annotation == i:
                    for o in b.ops:
                        t = _fmt_time(us_to_datetime(o.timestamp_us, utc_offset_h))
                        w(f"      quadro {o.frame}  {t}  {o.name}  {_fmt_handle(o.handle)}  {masked_hex(o.value, patterns)}")
        if check.orphans:
            w("  Escritas sem anotação:")
            for b in check.orphans:
                for o in b.ops:
                    t = _fmt_time(us_to_datetime(o.timestamp_us, utc_offset_h))
                    w(f"      quadro {o.frame}  {t}  {o.name}  {_fmt_handle(o.handle)}  {masked_hex(o.value, patterns)}")
        w("")
        if check.valid:
            w("Veredito: CAPTURA VALIDA")
        else:
            w("Veredito: CAPTURA INVALIDA - refazer só estes passos:")
            for r in check.redo:
                w(f"  - {r}")
    return "\n".join(out) + "\n"


def render_csv(trace: AttTrace, utc_offset_h: float, categories: set[str], patterns: list[bytes]) -> str:
    buf = io.StringIO()
    wr = csv.writer(buf, lineterminator="\n")
    wr.writerow(["quadro", "horario", "conexao", "sentido", "opcode", "operacao", "handle", "offset", "hex", "polling", "mascarado"])
    for o in trace.ops:
        if o.category not in categories:
            continue
        wr.writerow([
            o.frame, _fmt_time(us_to_datetime(o.timestamp_us, utc_offset_h)), f"0x{o.conn:04X}",
            SENT if o.sent else RECV, f"0x{o.opcode:02X}", o.name, _fmt_handle(o.handle),
            "" if o.offset is None else o.offset, masked_hex(o.value, patterns), int(o.ignored),
            int(is_masked(o.value, patterns)),
        ])
    return buf.getvalue()


# --------------------------------------------------------------------------- CLI


def _parse_handle(text: str) -> int:
    return int(text, 0)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        description="Lista as operações ATT de um btsnoop (ou do .zip do adb bugreport) e cruza com a anotação.",
    )
    ap.add_argument("captura", type=Path, help="btsnoop_hci.log ou bugreport .zip")
    ap.add_argument("--entrada", help="caminho do btsnoop dentro do zip (padrão: o btsnoop_hci.log mais recente)")
    ap.add_argument("--anotacao", type=Path, help="CSV de anotação (colunas: " + ",".join(ANNOTATION_COLUMNS) + ")")
    ap.add_argument("--fuso", type=float, default=None,
                    help="horas somadas ao horário do btsnoop, lido como UTC (padrão: fuso deste computador). "
                         "Use 0 se o celular gravar hora local.")
    ap.add_argument("--ajuste-s", type=float, default=0.0,
                    help="segundos somados ao horário da captura, para alinhar com o relógio usado na anotação")
    ap.add_argument("--janela-s", type=float, default=8.0, help="tolerância entre horário anotado e escrita (s)")
    ap.add_argument("--rajada-s", type=float, default=1.0, help="intervalo máximo entre escritas da mesma rajada (s)")
    ap.add_argument("--polling", type=int, default=5,
                    help="escrita idêntica repetida N vezes vira polling e sai do cruzamento (0 desliga)")
    ap.add_argument("--ignorar-handle", type=_parse_handle, action="append", default=[],
                    help="handle cujas escritas ficam fora do cruzamento (ex.: 0x0012); pode repetir")
    ap.add_argument("--conexao", type=_parse_handle, help="só esta conexão HCI (ex.: 0x0040)")
    ap.add_argument("--tudo", action="store_true", help="inclui descoberta, MTU e confirmações")
    ap.add_argument("--csv", action="store_true", help="lista as operações em CSV (sem relatório)")
    ap.add_argument("--senha", default=os.environ.get("TARAMPS_SENHA"),
                    help="mascara a senha BT no hex (padrão: variável TARAMPS_SENHA)")
    args = ap.parse_args(argv)

    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    try:
        raw, source = load_capture(args.captura, args.entrada)
        datalink, records = parse_btsnoop(raw)
        annotations = read_annotations(args.anotacao) if args.anotacao else None
    except (OSError, BtsnoopError, ValueError) as e:
        print(f"erro: {e}", file=sys.stderr)
        return 2

    trace = extract_att(datalink, records)
    if args.conexao is not None:
        trace.ops = [o for o in trace.ops if o.conn == args.conexao]
    offset_h = (local_utc_offset_h() if args.fuso is None else args.fuso) + args.ajuste_s / 3600
    polling = mark_polling(trace.ops, args.polling, set(args.ignorar_handle))
    categories = set(_CATEGORY.values()) | {CAT_OTHER} if args.tudo else set(DEFAULT_CATEGORIES)
    password = (args.senha or "").strip()
    patterns = password_patterns(password)
    if not password:
        print(NO_PASSWORD_WARNING, file=sys.stderr)

    if args.csv:
        if trace.truncated_records:
            print(TRUNCATED_WARNING.format(n=trace.truncated_records), file=sys.stderr)
        sys.stdout.write(render_csv(trace, offset_h, categories, patterns))
        return 0

    check = None
    if annotations is not None:
        check = cross_check(trace.ops, annotations, offset_h, args.janela_s, args.rajada_s)
    sys.stdout.write(render_report(trace, source, offset_h, categories, patterns, polling, check))
    return 0 if check is None or check.valid else 1


if __name__ == "__main__":
    sys.exit(main())
