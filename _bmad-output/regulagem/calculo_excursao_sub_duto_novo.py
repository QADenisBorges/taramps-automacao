"""Protech Light 8" na caixa com o duto novo (Fb medida 34,5 Hz em 09/11/2026).

Uso: uv run --no-project --with numpy python calculo_excursao_sub_duto_novo.py

Mesmo modelo de calculo_excursao_sub.py (Small, Ql = 7, ficha da Protech de 27/10/2026), com a
caixa de ~27 L líquidos, Fb 34,5 Hz e o duto de PVC 75 mm (Ø int. 7,1 cm → 39,6 cm²). Para cada
subsônico LR24 dá a tensão na ponte em que o cone chega ao Xmax (tom senoidal, pior frequência
de 20 a 150 Hz) e a velocidade do ar no duto nessa tensão. Sem ganho da sala nem compressão.
"""
import numpy as np

RHO, C = 1.18, 343.0
FS, QMS, QES, VAS, BL, RE, LE, SD = 46, 3.90, 0.61, 10e-3, 13.0, 3.7, 1.63e-3, 240e-4
XMAX = 8e-3
VB, FB, QL, SP = 27e-3, 34.5, 7.0, 39.6e-4

MMS = QES * BL**2 / (2 * np.pi * FS * RE)
CMS = 1 / ((2 * np.pi * FS) ** 2 * MMS)
RMS = 2 * np.pi * FS * MMS / QMS
CAB = VB / (RHO * C**2)
MAP = 1 / ((2 * np.pi * FB) ** 2 * CAB)
RAL = QL / (2 * np.pi * FB * CAB)
F = np.arange(20, 150.5, 0.5)


def por_volt(pa, pb=150):
    s = 1j * 2 * np.pi * F
    zb = 1 / (s * CAB + 1 / (s * MAP) + 1 / RAL)
    zm = s * MMS + RMS + 1 / (s * CMS) + SD**2 * zb
    lr = lambda fc, alta: ((1j * F / fc) ** 2 if alta else 1) / ((1j * F / fc) ** 2 + np.sqrt(2) * 1j * F / fc + 1)
    h = lr(pa, True) ** 2 * lr(pb, False) ** 2
    u = BL * h / ((RE + s * LE) * zm + BL**2)
    x = np.sqrt(2) * abs(u) / (2 * np.pi * F)
    vd = np.sqrt(2) * abs(SD * u * zb / (s * MAP)) / SP
    return x, vd


if __name__ == "__main__":
    for pa in (35, 30, 28, 25, 22):
        x, vd = por_volt(pa)
        i = int(np.argmax(x))
        v = XMAX / x[i]
        print(f"subsônico LR24 {pa} Hz: Xmax com {v:4.1f} V (pior em {F[i]:.1f} Hz)"
              f" | ar no duto nessa tensão: até {vd.max() * v:3.0f} m/s"
              f" | com 25 V: {x[i] * 25 / XMAX * 100:3.0f} % do Xmax")
