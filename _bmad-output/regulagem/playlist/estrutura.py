import sys, numpy as np, soundfile as sf
from scipy import signal
x, fs = sf.read(sys.argv[1], always_2d=True); m=x.mean(axis=1)
sos=signal.butter(4,[40,100],'bandpass',fs=fs,output='sos'); g=signal.sosfilt(sos,m)
sosv=signal.butter(4,[300,3000],'bandpass',fs=fs,output='sos'); v=signal.sosfilt(sosv,m)
db=lambda a: 20*np.log10(np.sqrt(np.mean(a**2))+1e-9)
print(" s   total grave(40-100) medio(300-3k)")
for t in range(0,int(len(m)/fs)-2,4):
    s=slice(t*fs,(t+4)*fs); print(f"{t//60}:{t%60:02d} {db(m[s]):6.1f} {db(g[s]):6.1f} {db(v[s]):6.1f}")
