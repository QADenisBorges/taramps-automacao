import sys, numpy as np, av, soundfile as sf
c = av.open(sys.argv[1]); st = c.streams.audio[0]
print("codec", st.codec_context.name, "fs", st.codec_context.sample_rate, "canais", st.codec_context.channels)
blocos=[]
for fr in c.decode(st):
    a = fr.to_ndarray()
    if a.ndim==1: a=a[None,:]
    if fr.format.is_planar: a=a.T
    else: a=a.reshape(-1, fr.layout.nb_channels if hasattr(fr.layout,'nb_channels') else len(fr.layout.channels))
    blocos.append(a.astype(np.float32))
x=np.concatenate(blocos); fs=st.codec_context.sample_rate
sf.write(sys.argv[2], x, fs, subtype='FLOAT'); print("ok", x.shape, fs)
