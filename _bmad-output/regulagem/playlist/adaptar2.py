"""Uso: adaptar2.py entrada.wav saida.wav inicio_s fim_s shelf_db bell51_db alvo_lufs_p90"""
import sys, numpy as np, soundfile as sf, pyloudnorm as pyln
from scipy import signal
from scipy.ndimage import maximum_filter1d
ent, sai = sys.argv[1], sys.argv[2]
ini, fim, shelf_db, bell_db, alvo = map(float, sys.argv[3:8])
x, fs = sf.read(ent, always_2d=True); x = x[int(ini*fs):int(fim*fs)].copy()
sos = signal.butter(2, 40, 'highpass', fs=fs, output='sos'); x = signal.sosfilt(sos, signal.sosfilt(sos, x, axis=0), axis=0)
def biquad(tipo, f0, db, q=0.707):
    A=10**(db/40); w0=2*np.pi*f0/fs; c=np.cos(w0); s=np.sin(w0)
    if tipo=='shelf':
        al=s/2*np.sqrt(2)
        b=[A*((A+1)-(A-1)*c+2*np.sqrt(A)*al), 2*A*((A-1)-(A+1)*c), A*((A+1)-(A-1)*c-2*np.sqrt(A)*al)]
        a=[(A+1)+(A-1)*c+2*np.sqrt(A)*al, -2*((A-1)+(A+1)*c), (A+1)+(A-1)*c-2*np.sqrt(A)*al]
    else:
        al=s/(2*q); b=[1+al*A, -2*c, 1-al*A]; a=[1+al/A, -2*c, 1-al/A]
    return np.array(b)/a[0], np.array(a)/a[0]
if shelf_db: b,a=biquad('shelf',100,shelf_db); x=signal.lfilter(b,a,x,axis=0)
if bell_db: b,a=biquad('bell',51,bell_db,2.0); x=signal.lfilter(b,a,x,axis=0)
n=int(0.3*fs); x[:n]*=np.linspace(0,1,n)[:,None]
n=int(2*fs); x[-n:]*=np.linspace(1,0,n)[:,None]
m=pyln.Meter(fs)
def p90(y):
    w=3*fs; L=[m.integrated_loudness(y[i:i+w]) for i in range(0,len(y)-w,fs)]
    return np.percentile([l for l in L if np.isfinite(l)],90)
x *= 10**((alvo-p90(x))/20)
teto=10**(-1/20)
def tp_env(y):
    up=signal.resample_poly(y,4,1,axis=0); return np.abs(up).max(axis=1).reshape(-1,4).max(axis=1)[:len(y)]
gn=np.minimum(1.0, teto/np.maximum(tp_env(x),1e-9)); la=int(0.0015*fs); gn=-maximum_filter1d(-gn,size=2*la+1)
rel=np.exp(-1/(0.080*fs)); g=np.empty_like(gn); cur=1.0
for i,v in enumerate(gn):
    cur = v if v<cur else v+(cur-v)*rel; g[i]=cur
x*=g[:,None]; x*=teto/max(tp_env(x).max(),teto)
sf.write(sai, x, fs, subtype='PCM_16')
print(f"{sai.split('/')[-1]}: {len(x)/fs:.0f} s | {m.integrated_loudness(x):.1f} LUFS | P90 {p90(x):.1f} | TP {20*np.log10(tp_env(x).max()):.2f} | limitador max {-20*np.log10(g.min()):.1f} dB, >1 dB em {100*np.mean(g<10**(-1/20)):.1f}% do tempo")
