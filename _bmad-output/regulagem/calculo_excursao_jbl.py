"""Excursão de pico da JBL 4TRMS80 na BS-369 selada, com o passa-alta do processador.

Uso: uv run --with numpy python calculo_excursao_jbl.py

Modelo eletromecânico com fonte de tensão (sem indutância da bobina, sem perdas da caixa):
x = (Bl · V_pico / Re) / (Mms·s² + (Rms + Bl²/Re)·s + 1/Cms + ρc²Sd²/Vb), vezes o filtro.
Cms vem da Vas e Mms vem da Fs. Roda com Bl da ficha (4,1) e Bl calculado pelo Qes (4,38),
porque os dois não fecham entre si, e com a caixa de 3,2 a 3,9 L (faixa do volume medido).
"""
import numpy as np

RHO, C = 1.204, 343.0
FS, VAS, SD, RE, QES, QMS, BL_FICHA = 98, 2.06e-3, 64e-4, 3.4, 0.81, 3.19, 4.1
XMAX = 1.75e-3  # pico, um lado

CMS = VAS / (RHO * C**2 * SD**2)
WS = 2 * np.pi * FS
MMS = 1 / (WS**2 * CMS)
RMS = WS * MMS / QMS
BL_QES = np.sqrt(WS * MMS * RE / QES)
ETA0 = 4 * np.pi**2 / C**3 * FS**3 * VAS / QES

F = np.logspace(np.log10(15), np.log10(2000), 4000)


def filtro(tipo, fc):
    s = 1j * F / fc
    bw2 = s**2 / (s**2 + np.sqrt(2) * s + 1)
    if tipo == "nenhum":
        return np.ones_like(F)
    if tipo == "BW2":
        return bw2
    if tipo == "LR4":
        return bw2**2
    if tipo == "BW4":
        return s**4 / ((s**2 + 0.7654 * s + 1) * (s**2 + 1.8478 * s + 1))
    raise ValueError(tipo)


def excursao_pico(v_rms, bl, vb, tipo, fc):
    s = 1j * 2 * np.pi * F
    kt = 1 / CMS + RHO * C**2 * SD**2 / vb
    x = (bl * v_rms * np.sqrt(2) / RE) / (MMS * s**2 + (RMS + bl**2 / RE) * s + kt)
    return np.abs(x * filtro(tipo, fc))


print(f"Cms {CMS*1e6:.0f} µm/N · Mms {MMS*1e3:.2f} g · Bl pelo Qes {BL_QES:.2f} · "
      f"η0 {ETA0*100:.3f} % → {112.1 + 10*np.log10(ETA0):.1f} dB/1 W/1 m")

for vb in (3.2e-3, 3.5e-3, 3.9e-3):
    for bl in (BL_FICHA, BL_QES):
        kt = 1 / CMS + RHO * C**2 * SD**2 / vb
        fc_caixa = np.sqrt(kt / MMS) / (2 * np.pi)
        qtc = np.sqrt(kt * MMS) / (RMS + bl**2 / RE)
        print(f"\nCaixa {vb*1e3:.1f} L, Bl {bl:.2f}: Fc {fc_caixa:.0f} Hz, Qtc {qtc:.2f}")
        for tipo, fc in [("nenhum", 1), ("BW2", 120), ("LR4", 150), ("LR4", 180),
                         ("LR4", 200), ("BW4", 150), ("BW4", 180), ("LR4", 250)]:
            x = excursao_pico(1.0, bl, vb, tipo, fc)
            i = x.argmax()
            v_xmax = XMAX / x[i]
            print(f"  {tipo:6}{fc if tipo != 'nenhum' else '':>4}: pior {F[i]:4.0f} Hz · "
                  f"Xmax com {v_xmax:5.1f} V ({v_xmax**2/4:5.1f} W em 4 Ω) · "
                  f"com 17,9 V: {x[i]*17.9/XMAX:.2f} × Xmax · com 11 V: {x[i]*11/XMAX:.2f} × Xmax")
