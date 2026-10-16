"""Testes do btsnoop_att com capturas sintéticas (nenhum dado real do processador)."""

from __future__ import annotations

import contextlib
import datetime as dt
import io
import struct
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import btsnoop_att as b  # noqa: E402

BASE = dt.datetime(2026, 10, 17, 14, 0, 0)
UNIX_BASE_US = int((BASE - dt.datetime(1970, 1, 1)).total_seconds() * 1_000_000)
CONN = 0x0040


def rec(data: bytes, sent: bool, t_s: float, cmd_evt: bool = False) -> bytes:
    flags = (0 if sent else 1) | (2 if cmd_evt else 0)
    ts = UNIX_BASE_US + int(t_s * 1_000_000) + b.EPOCH_DELTA_US
    return struct.pack(">IIIIq", len(data), len(data), flags, 0, ts) + data


def snoop(records: list[bytes], datalink: int = b.DLT_H4) -> bytes:
    return b.BTSNOOP_MAGIC + struct.pack(">II", 1, datalink) + b"".join(records)


def acl(payload: bytes, pb: int = 0b10, conn: int = CONN, h4: bool = True) -> bytes:
    body = struct.pack("<HH", conn | (pb << 12), len(payload)) + payload
    return (b"\x02" + body) if h4 else body


def l2cap(pdu: bytes, cid: int = b.ATT_CID) -> bytes:
    return struct.pack("<HH", len(pdu), cid) + pdu


def att_write_req(handle: int, value: bytes) -> bytes:
    return bytes([0x12]) + struct.pack("<H", handle) + value


def att_write_cmd(handle: int, value: bytes) -> bytes:
    return bytes([0x52]) + struct.pack("<H", handle) + value


def att_notify(handle: int, value: bytes) -> bytes:
    return bytes([0x1B]) + struct.pack("<H", handle) + value


def le_conn_complete(conn: int, addr: str) -> bytes:
    a = bytes.fromhex(addr.replace(":", ""))[::-1]
    params = bytes([0x01, 0x00]) + struct.pack("<H", conn) + bytes([0x00, 0x00]) + a + b"\x00" * 7
    return bytes([0x04, 0x3E, len(params)]) + params


def trace_of(records: list[bytes], datalink: int = b.DLT_H4) -> b.AttTrace:
    dl, recs = b.parse_btsnoop(snoop(records, datalink))
    return b.extract_att(dl, recs)


def ann(step, start, kind="passo", end=None, param="ganho", channel="S1", old="0", new="1"):
    t = lambda s: dt.datetime.strptime(s, "%H:%M:%S").time()  # noqa: E731
    return b.Annotation(step, kind, t(start), t(end) if end else None, param, channel, old, new, "")


class HeaderTests(unittest.TestCase):
    def test_rejects_wrong_magic(self):
        with self.assertRaises(b.BtsnoopError):
            b.parse_btsnoop(b"notsnoop" + b"\x00" * 8)

    def test_rejects_unknown_datalink(self):
        with self.assertRaises(b.BtsnoopError):
            b.parse_btsnoop(b.BTSNOOP_MAGIC + struct.pack(">II", 1, 2001))

    def test_reads_header_and_frames(self):
        dl, recs = b.parse_btsnoop(snoop([rec(acl(l2cap(att_write_cmd(0x10, b"\x01"))), True, 0.0)] * 3))
        self.assertEqual(dl, b.DLT_H4)
        self.assertEqual([r.frame for r in recs], [1, 2, 3])
        self.assertTrue(recs[0].sent)

    def test_truncated_last_record_is_dropped(self):
        raw = snoop([rec(acl(l2cap(att_write_cmd(0x10, b"\x01"))), True, 0.0)])
        _, recs = b.parse_btsnoop(raw[:-2])
        self.assertEqual(recs, [])

    def test_timestamp_conversion(self):
        t = trace_of([rec(acl(l2cap(att_write_cmd(0x10, b"\x01"))), True, 1.5)])
        self.assertEqual(b.us_to_datetime(t.ops[0].timestamp_us, 0), BASE + dt.timedelta(seconds=1.5))
        self.assertEqual(b.us_to_datetime(t.ops[0].timestamp_us, -3).hour, 11)


class AttTests(unittest.TestCase):
    def test_write_request_and_command(self):
        t = trace_of([
            rec(acl(l2cap(att_write_req(0x0012, bytes.fromhex("a501ff")))), True, 0.0),
            rec(acl(l2cap(bytes([0x13]))), False, 0.05),
            rec(acl(l2cap(att_write_cmd(0x0015, b"\x07"))), True, 1.0),
        ])
        w1, rsp, w2 = t.ops
        self.assertEqual((w1.opcode, w1.handle, w1.value, w1.frame, w1.sent), (0x12, 0x12, bytes.fromhex("a501ff"), 1, True))
        self.assertEqual((rsp.name, rsp.handle, rsp.sent), ("Write Response", 0x12, False))
        self.assertEqual((w2.name, w2.handle, w2.value), ("Write Command", 0x15, b"\x07"))
        self.assertTrue(w1.is_write and w2.is_write and not rsp.is_write)

    def test_fragmented_acl_is_reassembled(self):
        value = bytes(range(40))
        pdu = l2cap(att_write_req(0x0020, value))
        frags = [pdu[:10], pdu[10:30], pdu[30:]]
        t = trace_of([
            rec(acl(frags[0], pb=0b00), True, 0.0),
            rec(acl(l2cap(att_notify(0x0030, b"\x01")), pb=0b10), False, 0.01),  # outro sentido no meio
            rec(acl(frags[1], pb=0b01), True, 0.02),
            rec(acl(frags[2], pb=0b01), True, 0.03),
        ])
        notif, write = t.ops
        self.assertEqual((notif.name, notif.frame), ("Handle Value Notification", 2))
        self.assertEqual((write.handle, write.value, write.frame), (0x20, value, 4))
        self.assertEqual(t.orphan_fragments, 0)

    def test_orphan_continuation_is_counted(self):
        t = trace_of([rec(acl(b"\x01\x02\x03", pb=0b01), True, 0.0)])
        self.assertEqual((t.ops, t.orphan_fragments), ([], 1))

    def test_non_att_cid_is_ignored(self):
        t = trace_of([rec(acl(l2cap(b"\x01\x02", cid=0x0005)), True, 0.0)])
        self.assertEqual(t.ops, [])

    def test_notification(self):
        t = trace_of([rec(acl(l2cap(att_notify(0x002A, bytes.fromhex("0102030405")))), False, 0.0)])
        (n,) = t.ops
        self.assertEqual((n.category, n.handle, n.value, n.sent), (b.CAT_NOTIFY, 0x2A, bytes.fromhex("0102030405"), False))

    def test_read_response_gets_handle_from_request(self):
        t = trace_of([
            rec(acl(l2cap(bytes([0x0A]) + struct.pack("<H", 0x0033))), True, 0.0),
            rec(acl(l2cap(bytes([0x0B]) + b"\xaa\xbb")), False, 0.1),
        ])
        self.assertEqual([(o.name, o.handle) for o in t.ops], [("Read Request", 0x33), ("Read Response", 0x33)])
        self.assertEqual(t.ops[1].value, b"\xaa\xbb")

    def test_h1_datalink(self):
        t = trace_of([rec(acl(l2cap(att_write_cmd(0x10, b"\x09")), h4=False), True, 0.0)], datalink=b.DLT_H1)
        self.assertEqual((t.ops[0].handle, t.ops[0].value), (0x10, b"\x09"))

    def test_le_connection_address(self):
        t = trace_of([rec(le_conn_complete(CONN, "AA:BB:CC:DD:EE:FF"), False, 0.0, cmd_evt=True)])
        self.assertEqual(t.connections, {CONN: "AA:BB:CC:DD:EE:FF"})

    def test_gatt_discovery(self):
        svc_uuid = bytes(range(16))
        t = trace_of([
            rec(acl(l2cap(bytes([0x10]) + struct.pack("<HHH", 1, 0xFFFF, 0x2800))), True, 0.0),
            rec(acl(l2cap(bytes([0x11, 20]) + struct.pack("<HH", 0x000C, 0x0015) + svc_uuid)), False, 0.1),
            rec(acl(l2cap(bytes([0x08]) + struct.pack("<HHH", 0x000C, 0x0015, 0x2803))), True, 0.2),
            rec(acl(l2cap(bytes([0x09, 7]) + struct.pack("<HBH", 0x000D, 0x0C, 0x000E) + struct.pack("<H", 0xFFE1))), False, 0.3),
        ])
        self.assertEqual(t.gatt[0x0C].kind, "servico")
        self.assertEqual(t.gatt[0x0C].end_handle, 0x15)
        self.assertEqual(t.gatt[0x0C].uuid, b.format_uuid(svc_uuid))
        self.assertEqual((t.gatt[0x0E].kind, t.gatt[0x0E].uuid, t.gatt[0x0E].properties), ("caracteristica", "0xFFE1", 0x0C))


class MaskTests(unittest.TestCase):
    def test_masks_obvious_encodings(self):
        pats = b.password_patterns("1234")
        self.assertEqual(b.masked_hex(b"\x55" + b"1234" + b"\x66", pats), "55 ** ** ** ** 66")
        self.assertEqual(b.masked_hex(b"\x01\x02\x03\x04", pats), "** ** ** **")
        self.assertEqual(b.masked_hex(b"\xa0\x12\x34", pats), "a0 ** **")
        self.assertEqual(b.masked_hex(b"\xd2\x04", pats), "** **")
        self.assertEqual(b.masked_hex(b"\x10\x20", pats), "10 20")

    def test_no_password_no_mask(self):
        self.assertEqual(b.masked_hex(b"1234", b.password_patterns(None)), "31 32 33 34")


class AnnotationTests(unittest.TestCase):
    def test_reads_semicolon_csv_with_bom(self):
        text = "﻿passo;tipo;inicio;fim;parametro;canal;antigo;novo;obs\n1;passo;14:00:10;;ganho;S1;0,0;0,5;\n\n2;posicionamento;14:01:00;14:01:30;ganho;S1;0,5;-44,5;arrastei\n"
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "a.csv"
            p.write_text(text, encoding="utf-8")
            rows = b.read_annotations(p)
        self.assertEqual(len(rows), 2)
        self.assertEqual((rows[0].step, rows[0].kind, rows[0].old, rows[0].new), ("1", "passo", "0,0", "0,5"))
        self.assertEqual((rows[1].kind, rows[1].end), ("posicionamento", dt.time(14, 1, 30)))

    def test_rejects_missing_columns(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "a.csv"
            p.write_text("parametro,novo\nganho,1\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                b.read_annotations(p)


def _writes(times_handles_values):
    return [rec(acl(l2cap(att_write_req(h, v))), True, t) for t, h, v in times_handles_values]


class CrossCheckTests(unittest.TestCase):
    def check(self, records, annotations, **kw):
        t = trace_of(records)
        b.mark_polling(t.ops, kw.pop("polling", 5), set())
        return b.cross_check(t.ops, annotations, 0, **kw)

    def test_valid_capture(self):
        c = self.check(_writes([(10, 0x12, b"\x01"), (40, 0x12, b"\x02")]),
                       [ann("1", "14:00:11"), ann("2", "14:00:38")])
        self.assertTrue(c.valid, c.redo)
        self.assertEqual(c.status, ["ok", "ok"])

    def test_burst_of_one_press_counts_once(self):
        c = self.check(_writes([(10, 0x12, b"\x01"), (10.2, 0x14, b"\x01")]), [ann("1", "14:00:10")])
        self.assertTrue(c.valid)
        self.assertIn("2 escritas", c.status[0])

    def test_ambiguous_step(self):
        c = self.check(_writes([(10, 0x12, b"\x01"), (14, 0x12, b"\x02")]), [ann("1", "14:00:12")])
        self.assertFalse(c.valid)
        self.assertTrue(c.status[0].startswith("AMBIGUA"))
        self.assertIn("passo 1", c.redo[0])

    def test_unannotated_write_invalidates(self):
        c = self.check(_writes([(10, 0x12, b"\x01"), (100, 0x12, b"\x05")]), [ann("1", "14:00:10")])
        self.assertFalse(c.valid)
        self.assertEqual(len(c.orphans), 1)
        self.assertIn("sem anotação", c.redo[0])

    def test_step_without_write(self):
        c = self.check(_writes([(10, 0x12, b"\x01")]), [ann("1", "14:00:10"), ann("2", "14:05:00")])
        self.assertEqual(c.status[1], "SEM ESCRITA")
        self.assertFalse(c.valid)

    def test_positioning_interval_absorbs_many_bursts(self):
        recs = _writes([(60 + 3 * i, 0x12, bytes([i])) for i in range(8)] + [(120, 0x12, b"\xff")])
        c = self.check(recs, [ann("1", "14:01:00", kind="posicionamento", end="14:01:25"), ann("2", "14:02:00")])
        self.assertTrue(c.valid, c.redo)

    def test_polling_is_excluded(self):
        recs = _writes([(i * 2.0, 0x30, b"\x00") for i in range(6)] + [(50, 0x12, b"\x01")])
        c = self.check(recs, [ann("1", "14:00:50")])
        self.assertTrue(c.valid, c.redo)


class CliTests(unittest.TestCase):
    def run_cli(self, args):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
            code = b.main(args)
        return code, out.getvalue()

    def test_zip_and_annotation_end_to_end(self):
        raw = snoop(_writes([(10, 0x12, b"1234")]) + [rec(acl(l2cap(att_notify(0x14, b"\x07"))), False, 10.1)])
        with tempfile.TemporaryDirectory() as d:
            z = Path(d) / "bugreport.zip"
            with zipfile.ZipFile(z, "w") as zf:
                zf.writestr("FS/data/misc/bluetooth/logs/btsnoop_hci.log.last", snoop([]))
                zf.writestr("FS/data/misc/bluetooth/logs/btsnoop_hci.log", raw)
            a = Path(d) / "a.csv"
            a.write_text("passo,inicio,parametro,antigo,novo\n1,14:00:10,senha,,\n", encoding="utf-8")
            code, text = self.run_cli([str(z), "--anotacao", str(a), "--fuso", "0", "--senha", "1234"])
        self.assertEqual(code, 0, text)
        self.assertIn("btsnoop_hci.log", text)
        self.assertNotIn(".last", text)
        self.assertIn("** ** ** **", text)
        self.assertNotIn("31 32 33 34", text)
        self.assertIn("CAPTURA VALIDA", text)

    def test_invalid_capture_exit_code(self):
        with tempfile.TemporaryDirectory() as d:
            s = Path(d) / "btsnoop_hci.log"
            s.write_bytes(snoop(_writes([(10, 0x12, b"\x01"), (90, 0x12, b"\x02")])))
            a = Path(d) / "a.csv"
            a.write_text("passo,inicio\n1,14:00:10\n", encoding="utf-8")
            code, text = self.run_cli([str(s), "--anotacao", str(a), "--fuso", "0"])
        self.assertEqual(code, 1)
        self.assertIn("CAPTURA INVALIDA", text)

    def test_csv_output(self):
        with tempfile.TemporaryDirectory() as d:
            s = Path(d) / "btsnoop_hci.log"
            s.write_bytes(snoop(_writes([(10, 0x12, b"\x01")])))
            code, text = self.run_cli([str(s), "--csv", "--fuso", "0"])
        self.assertEqual(code, 0)
        self.assertEqual(text.splitlines()[1].split(",")[:7], ["1", "14:00:10.000", "0x0040", "cel->mod", "0x12", "Write Request", "0x0012"])

    def test_not_a_capture(self):
        with tempfile.TemporaryDirectory() as d:
            s = Path(d) / "x.txt"
            s.write_text("oi", encoding="utf-8")
            err = io.StringIO()
            with contextlib.redirect_stderr(err):
                code, _ = self.run_cli([str(s)])
        self.assertEqual(code, 2)


REPO = Path(__file__).resolve().parents[2]


class ReviewFixTests(unittest.TestCase):
    def run_cli(self, args):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = b.main(args)
        return code, out.getvalue(), err.getvalue()

    def capture(self, d, records, annotation=None):
        s = Path(d) / "btsnoop_hci.log"
        s.write_bytes(snoop(records))
        args = [str(s), "--fuso", "0"]
        if annotation is not None:
            a = Path(d) / "a.csv"
            a.write_text(annotation, encoding="utf-8")
            args += ["--anotacao", str(a)]
        return args

    def test_csv_with_password_masks_hex_column(self):
        with tempfile.TemporaryDirectory() as d:
            args = self.capture(d, _writes([(10, 0x12, b"\xaa1234")]))
            code, out, err = self.run_cli(args + ["--csv", "--senha", " 1234 "])
        row = out.splitlines()[1].split(",")
        self.assertEqual(code, 0)
        self.assertEqual(row[8], "aa ** ** ** **")
        self.assertEqual(row[10], "1")
        self.assertNotIn("sem senha", err)

    def test_warns_without_password(self):
        with tempfile.TemporaryDirectory() as d:
            args = self.capture(d, _writes([(10, 0x12, b"\x01")]))
            _, _, err = self.run_cli(args)
        self.assertIn("sem senha", err)

    def test_report_lists_masked_frames(self):
        with tempfile.TemporaryDirectory() as d:
            args = self.capture(d, _writes([(10, 0x12, b"\x01"), (20, 0x12, b"\xd2\x04")]))
            _, out, _ = self.run_cli(args + ["--senha", "1234"])
        self.assertIn("Quadros com máscara de senha (conferir sem máscara, localmente, antes de citar): 2", out)

    def test_ajuste_s_aligns_clocks(self):
        with tempfile.TemporaryDirectory() as d:
            args = self.capture(d, _writes([(20, 0x12, b"\x01")]), "passo,inicio\n1,14:00:10\n")
            code_without, _, _ = self.run_cli(args)
            code_with, out, _ = self.run_cli(args + ["--ajuste-s", "-10"])
        self.assertEqual(code_without, 1)
        self.assertEqual(code_with, 0, out)

    def test_ignorar_handle_removes_orphan(self):
        recs = _writes([(10, 0x12, b"\x01"), (100, 0x30, b"\x09")])
        with tempfile.TemporaryDirectory() as d:
            args = self.capture(d, recs, "passo,inicio\n1,14:00:10\n")
            code_without, _, _ = self.run_cli(args)
            code_with, out, _ = self.run_cli(args + ["--ignorar-handle", "0x0030"])
        self.assertEqual(code_without, 1)
        self.assertEqual(code_with, 0, out)

    def test_decodes_error_prepare_write_and_multi_notification(self):
        t = trace_of([
            rec(acl(l2cap(bytes([0x01, 0x12]) + struct.pack("<H", 0x0021) + b"\x03")), False, 0.0),
            rec(acl(l2cap(bytes([0x16]) + struct.pack("<HH", 0x0022, 5) + b"\xab\xcd")), True, 1.0),
            rec(acl(l2cap(bytes([0x17]) + struct.pack("<HH", 0x0022, 5) + b"\xab\xcd")), False, 1.1),
            rec(acl(l2cap(bytes([0x23]) + struct.pack("<HH", 0x40, 1) + b"\x01" + struct.pack("<HH", 0x41, 2) + b"\x02\x03")), False, 2.0),
        ])
        err, prep, prep_rsp, n1, n2 = t.ops
        self.assertEqual((err.name, err.handle, err.value), ("Error Response", 0x21, b"\x12\x03"))
        self.assertEqual((prep.handle, prep.offset, prep.value, prep.is_write), (0x22, 5, b"\xab\xcd", True))
        self.assertEqual((prep_rsp.name, prep_rsp.offset, prep_rsp.is_write), ("Prepare Write Response", 5, False))
        self.assertEqual([(n.handle, n.value) for n in (n1, n2)], [(0x40, b"\x01"), (0x41, b"\x02\x03")])

    def test_find_information_labels_declarations(self):
        t = trace_of([
            rec(acl(l2cap(bytes([0x04]) + struct.pack("<HH", 1, 0xFFFF))), True, 0.0),
            rec(acl(l2cap(bytes([0x05, 1]) + struct.pack("<HHHHHH", 1, 0x2800, 2, 0x2803, 4, 0x2902))), False, 0.1),
        ])
        self.assertEqual([t.gatt[h].kind for h in (1, 2, 4)], ["declaracao", "declaracao", "descritor"])

    def test_conexao_row_absorbs_connection_writes(self):
        recs = _writes([(5, 0x12, b"\x01"), (12, 0x14, b"\x02"), (20, 0x12, b"\x03"), (60, 0x12, b"\x04")])
        c = b.cross_check(trace_of(recs).ops, [ann("C1.0", "14:00:03", kind="conexao", end="14:00:25"), ann("C1.1", "14:01:00")], 0)
        self.assertTrue(c.valid, c.redo)

    def test_truncated_record_warns(self):
        data = acl(l2cap(att_write_cmd(0x10, b"\x01")))
        ts = UNIX_BASE_US + b.EPOCH_DELTA_US
        raw = snoop([struct.pack(">IIIIq", len(data) + 20, len(data), 0, 0, ts) + data])
        with tempfile.TemporaryDirectory() as d:
            s = Path(d) / "btsnoop_hci.log"
            s.write_bytes(raw)
            _, out, _ = self.run_cli([str(s), "--fuso", "0"])
            _, _, err = self.run_cli([str(s), "--fuso", "0", "--csv"])
        self.assertIn("1 registro(s) cortado(s)", out)
        self.assertIn("1 registro(s) cortado(s)", err)

    def test_reads_model_annotation(self):
        rows = b.read_annotations(REPO / "protocolo" / "capturas" / "MODELO-anotacao.csv")
        self.assertEqual(rows[0].kind, "conexao")
        self.assertTrue(all(r.kind in b.ANNOTATION_KINDS for r in rows))

    def write_annotation(self, d, data: bytes) -> Path:
        p = Path(d) / "a.csv"
        p.write_bytes(data)
        return p

    def test_cp1252_annotation(self):
        with tempfile.TemporaryDirectory() as d:
            p = self.write_annotation(d, "passo;inicio;parametro;obs\n1;14:00:10;ganho;mínimo\n".encode("cp1252"))
            (row,) = b.read_annotations(p)
        self.assertEqual(row.note, "mínimo")

    def test_extra_fields_raise_clear_error(self):
        with tempfile.TemporaryDirectory() as d:
            p = self.write_annotation(d, b"passo;inicio;novo\n1;14:00:10;0,5;dB;sobrando\n")
            with self.assertRaisesRegex(ValueError, "campos a mais"):
                b.read_annotations(p)

    def test_end_before_start_raises(self):
        with tempfile.TemporaryDirectory() as d:
            p = self.write_annotation(d, b"passo;tipo;inicio;fim\n1;posicionamento;14:10:00;14:09:00\n")
            with self.assertRaisesRegex(ValueError, "antes do inicio"):
                b.read_annotations(p)
            p = self.write_annotation(d, b"passo;tipo;inicio;fim\n1;posicionamento;23:59:50;00:00:20\n")
            self.assertEqual(len(b.read_annotations(p)), 1)


if __name__ == "__main__":
    unittest.main()
