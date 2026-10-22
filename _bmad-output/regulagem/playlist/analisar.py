import sys, numpy as np, soundfile as sf, pyloudnorm as pyln
from scipy import signal
x, fs = sf.read(sys.argv[1], always_2d=True)
print(f"fs={fs} canais={x.shape[1]} duracao={len(x)/fs:.1f}s")
m = pyln.Meter(fs)
print(f"LUFS integrado={m.integrated_loudness(x):.1f}")
up = signal.resample_poly(x, 4, 1, axis=0)
print(f"pico amostra={20*np.log10(np.max(np.abs(x))):.2f} dBFS  true peak~{20*np.log10(np.max(np.abs(up))):.2f} dBTP")
clip = np.mean(np.abs(x) > 0.999)*100; print(f"amostras >=0,999: {clip:.3f}%")
mono = x.mean(axis=1)
f, P = signal.welch(mono, fs, nperseg=fs)
tot = P.sum()
for a,b in [(0,30),(30,40),(40,50),(50,60),(60,80),(80,100),(100,200),(200,1000),(1000,4000),(4000,20000)]:
    s=P[(f>=a)&(f<b)].sum(); print(f"  {a:>5}-{b:<5} Hz: {10*np.log10(s/tot):6.1f} dB  ({100*s/tot:5.1f}%)")
print(f"pico espectral graves: {f[(f>20)&(f<200)][np.argmax(P[(f>20)&(f<200)])]:.0f} Hz")
# loudness em janelas de 3 s
w=3*fs; L=[]
for i in range(0, len(x)-w, fs):
    seg=x[i:i+w]
    try: L.append((i/fs, m.integrated_loudness(seg)))
    except: pass
print("loudness a cada 10 s (3 s de janela):")
for t,l in L[::10]: print(f"  {t:5.0f}s {l:6.1f}")
