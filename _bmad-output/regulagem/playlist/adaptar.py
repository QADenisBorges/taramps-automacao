import sys, numpy as np, soundfile as sf, pyloudnorm as pyln
from scipy import signal
from scipy.ndimage import maximum_filter1d
ent, saida, shelf_db = sys.argv[1], sys.argv[2], float(sys.argv[3])
x, fs = sf.read(ent, always_2d=True)
x = x[:int(118*fs)].copy()
# subsonico no arquivo: LR4 em 40 Hz (o processador ja corta em 40 Hz BW24; aqui evita gastar teto no que nao toca)
sos = signal.butter(2, 40, 'highpass', fs=fs, output='sos'); x = signal.sosfilt(sos, signal.sosfilt(sos, x, axis=0), axis=0)
# prateleira de grave (versao 200 W): shelf de 2a ordem em 100 Hz
if shelf_db != 0:
    A=10**(shelf_db/40); w0=2*np.pi*100/fs; al=np.sin(w0)/2*np.sqrt(2); c=np.cos(w0)
    b=[A*((A+1)-(A-1)*c+2*np.sqrt(A)*al), 2*A*((A-1)-(A+1)*c), A*((A+1)-(A-1)*c-2*np.sqrt(A)*al)]
    a=[(A+1)+(A-1)*c+2*np.sqrt(A)*al, -2*((A-1)+(A+1)*c), (A+1)+(A-1)*c-2*np.sqrt(A)*al]
    x = signal.lfilter(np.array(b)/a[0], np.array(a)/a[0], x, axis=0)
# fade-out de 2 s
n=int(2*fs); x[-n:] *= np.linspace(1,0,n)[:,None]
# limitador de pico verdadeiro (4x), lookahead 1,5 ms, liberacao 80 ms, teto -1 dBTP
teto = 10**(-1/20)
def tp_env(y):
    up = signal.resample_poly(y, 4, 1, axis=0); e = np.abs(up).max(axis=1)
    return e.reshape(-1,4).max(axis=1)[:len(y)]
# ganho de entrada: leva as pancadas a ~-7,5 LUFS de curto prazo (como no original)
env = tp_env(x)
g_need = np.minimum(1.0, teto/np.maximum(env,1e-9))
la = int(0.0015*fs); g_need = -maximum_filter1d(-g_need, size=2*la+1)
rel = np.exp(-1/(0.080*fs)); g=np.empty_like(g_need); cur=1.0
for i,v in enumerate(g_need):
    cur = v if v < cur else v + (cur - v)*rel
    g[i]=cur
x = x*g[:,None]
x *= teto/max(tp_env(x).max(), teto)  # garantia final
sf.write(saida, x, fs, subtype='PCM_16')
m=pyln.Meter(fs)
print(f"{saida.split('/')[-1]}: {len(x)/fs:.0f} s, {m.integrated_loudness(x):.1f} LUFS, TP {20*np.log10(tp_env(x).max()):.2f} dBTP, reducao max {-20*np.log10(g.min()):.1f} dB")
