"""Excursão do cone e velocidade do ar no duto do Protech Light 8" na caixa de 28 L (Fb 51 Hz).

Uso: uv run --no-project --with numpy python calculo_excursao_sub.py

Modelo de parâmetros concentrados no domínio acústico (Small): bobina com Re e Le, falante,
caixa com perda Ql = 7 e duto sintonizado na Fb medida. Ficha do site da Protech (27/10/2026).
A tensão em cada frequência sai da cadeia do processador (subsônico BW4, passa-baixa LR4, EQ
de saída), referida à medida de 20/10 (ganho +2,6 → 27,5 V com 80 Hz a 0 dBFS), e para no teto
do limitador. Sem ganho da sala e sem compressão do duto: a velocidade real no duto é menor.
"""
import numpy as np

RHO, C = 1.18, 343.0
FS, QMS, QES, VAS, BL, RE, LE, SD = 46, 3.90, 0.61, 10e-3, 13.0, 3.7, 1.63e-3, 240e-4
XMAX = 8e-3  # pico, um lado
VB, FB, QL, SP = 28e-3, 51.0, 7.0, 38.5e-4

MMS = QES * BL**2 / (2 * np.pi * FS * RE)
CMS = 1 / ((2 * np.pi * FS) ** 2 * MMS)
RMS = 2 * np.pi * FS * MMS / QMS
CAB = VB / (RHO * C**2)
MAP = 1 / ((2 * np.pi * FB) ** 2 * CAB)
RAL = QL / (2 * np.pi * FB * CAB)

F = np.array([30, 35, 38, 40, 42, 45, 48, 51, 55, 60, 70, 80, 100, 150], dtype=float)


def resposta(v_rms):
    s = 1j * 2 * np.pi * F
    ze = RE + s * LE
    pg = v_rms * BL / (ze * SD)
    zae = BL**2 / (ze * SD**2)
    zd = s * MMS / SD**2 + 1 / (s * CMS * SD**2) + RMS / SD**2
    zb = 1 / (s * CAB + 1 / RAL + 1 / (s * MAP))
    ud = pg / (zae + zd + zb)
    up = ud * zb / (s * MAP)
    x = np.sqrt(2) * abs(ud) / (SD * abs(s))
    v_duto = np.sqrt(2) * abs(up) / SP
    return x, v_duto


def bw4_pa(fc):
    s = 1j * F / fc
    return s**4 / ((s**2 + 0.7654 * s + 1) * (s**2 + 1.8478 * s + 1))


def lr4_pb(fc):
    s = 1j * F / fc
    return (1 / (s**2 + np.sqrt(2) * s + 1)) ** 2


def peq(f0, ganho, q):
    a = 10 ** (ganho / 40)
    s = 1j * F / f0
    return (s**2 + s * a / q + 1) / (s**2 + s / (a * q) + 1)


def cenario(nome, ganho, teto, pb, pa=40, eq=None):
    s80 = 1j * 80 / 40
    ref = 27.5 / abs(s80**4 / ((s80**2 + 0.7654 * s80 + 1) * (s80**2 + 1.8478 * s80 + 1)))
    ref /= abs((1 / ((1j * 80 / 200) ** 2 + np.sqrt(2) * 1j * 80 / 200 + 1)) ** 2)
    h = bw4_pa(pa) * lr4_pb(pb) * (peq(*eq) if eq else 1)
    v = np.minimum(ref * 10 ** ((ganho - 2.6) / 20) * abs(h), teto)
    x, vd = resposta(v)
    i = int(np.argmax(x))
    print(f"{nome:52s} x máx {x[i] * 1e3:4.1f} mm ({x[i] / XMAX * 100:3.0f} % do Xmax) em {F[i]:.0f} Hz"
          f" | ar no duto em 51 Hz {vd[F == 51][0]:4.1f} m/s")


if __name__ == "__main__":
    cenario("SPL hoje: +2,6, teto 28,6 V, sub 40 Hz", 2.6, 28.6, 200)
    cenario("SPL a 34 V, sub 40 Hz", 4.4, 34, 200)
    cenario("SPL a 34 V, sub 43 Hz", 4.4, 34, 200, 43)
    cenario("FILME hoje: +6, EQ 45 Hz +3, teto 20 V", 6, 20, 150, 40, (45, 3, 0.7))
    cenario("FILME hoje, pico no ataque de 50 ms (ponte 40 V)", 6, 40, 150, 40, (45, 3, 0.7))
    cenario("FILME sem o reforço, sub 45 Hz, teto 20 V", 6, 20, 150, 45)
    cenario("FILME sem o reforço, sub 45 Hz, pico (40 V)", 6, 40, 150, 45)
    cenario("Tom puro no ceifamento da ponte (40 V)", 40, 40, 200)
