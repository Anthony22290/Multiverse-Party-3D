import numpy as np, wave
SR=44100
rng=np.random.default_rng(7)
def save(name,x):
    x=x/np.max(np.abs(x))*.85
    w=wave.open(name,'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes()); w.close()
def midi(n): return 440*2**((n-69)/12)
def osc(f,n,kind):
    t=np.arange(n)/SR; ph=(f*t)%1
    if kind=='tri': return 4*np.abs(ph-.5)-1
    if kind=='sq': return np.where(ph<.5,1.,-1.)*.55
    if kind=='pulse': return np.where(ph<.25,1.,-1.)*.45
    if kind=='sine': return np.sin(2*np.pi*f*t)
def env(n,a=.005,d=.08,s=.6,r=.05):
    e=np.ones(n)*s; A=int(a*SR); D=int(d*SR); R=int(r*SR)
    A=min(A,n); e[:A]=np.linspace(0,1,A)
    if A+D<n: e[A:A+D]=np.linspace(1,s,D)
    if R<n: e[-R:]*=np.linspace(1,0,R)
    return e
def lp(x,a=.15):
    y=np.zeros_like(x); acc=0.
    for i in range(len(x)): acc+=a*(x[i]-acc); y[i]=acc
    return y
def song(name,bpm,key,prog,bars_per_chord,melody_seed,swing=0.):
    beat=60/bpm; bar=4*beat
    total_bars=len(prog)*bars_per_chord*4
    N=int(round(total_bars*bar*SR)); out=np.zeros(N)
    def add(sig,start):
        i=int(start*SR)%N; end=i+len(sig)
        if end<=N: out[i:end]+=sig
        else: k=N-i; out[i:]+=sig[:k]; out[:end-N]+=sig[k:]
    r=np.random.default_rng(melody_seed)
    # chords as scale degrees in major
    scale=[0,2,4,5,7,9,11]
    def chord(deg): return [key+scale[(deg+k)%7]+12*((deg+k)//7) for k in (0,2,4)]
    for b in range(total_bars):
        deg=prog[(b//bars_per_chord)%len(prog)]
        ch=chord(deg); t0=b*bar
        # bass: root on 1 and 3, fifth on offbeats
        for q in range(8):
            n=ch[0]-24 if q%4 in (0,) else (ch[2]-24 if q%2 else ch[0]-24)
            dur=beat*.45; s=osc(midi(n),int(dur*SR),'tri')*env(int(dur*SR),.003,.05,.7,.04)*.55
            add(s,t0+q*beat/2)
        # arpeggio 16ths
        for q in range(16):
            n=ch[q%3]+12+(12 if q%8>=6 else 0)
            dur=beat/4*.9; s=osc(midi(n),int(dur*SR),'pulse')*env(int(dur*SR),.002,.04,.3,.02)*.16
            add(s,t0+q*beat/4+(swing*beat/4 if q%2 else 0))
        # pad (soft triad)
        dur=bar; n=int(dur*SR)
        pad=sum(osc(midi(c),n,'tri') for c in ch)*env(n,.15,.3,.6,.3)*.07
        add(pad,t0)
        # drums
        for q in range(4):
            k=int(.18*SR); tt=np.arange(k)/SR
            if q in (0,2): add(np.sin(2*np.pi*(55+90*np.exp(-tt*35))*tt)*np.exp(-tt*16)*.9, t0+q*beat)
            if q in (1,3): add(rng.standard_normal(int(.14*SR))*np.exp(-np.arange(int(.14*SR))/SR*28)*.32, t0+q*beat)
            for h in range(2):
                hh=rng.standard_normal(int(.04*SR)); hh=hh-lp(hh,.5); add(hh*np.exp(-np.arange(len(hh))/SR*90)*.18, t0+q*beat+h*beat/2+(swing*beat/2 if h else 0))
    # melody: second half of loop only (first half breathes), chord-tone based phrases
    half=total_bars//2
    for b in range(half,total_bars):
        deg=prog[(b//bars_per_chord)%len(prog)]; ch=chord(deg); t0=b*bar
        rhythms=[[0,1,1.5,2,3],[0,.5,1,2,2.5,3],[0,1,2,2.5,3,3.5],[0,1.5,2,3]]; rhythm=rhythms[r.integers(len(rhythms))]
        for i,pos in enumerate(rhythm):
            nxt=rhythm[i+1] if i+1<len(rhythm) else 4
            pool=ch+[key+scale[(deg+1)%7]] if i%2 else ch
            n=pool[r.integers(len(pool))]+24
            dur=(nxt-pos)*beat*.92; s=osc(midi(n),int(dur*SR),'sq')
            vib=1+.004*np.sin(2*np.pi*5.5*np.arange(len(s))/SR)
            s=osc(midi(n),int(dur*SR),'sq')*env(int(dur*SR),.01,.1,.55,.06)*.2
            add(lp(s,.35),t0+pos*beat)
    # wrap-around echo for a seamless loop
    d=int(beat*.75*SR); out=out+np.roll(out,d)*.22
    save(name,out)
song('menu.wav',104,57,[0,5,3,4],1,3,swing=.1)     # A major-ish: I vi IV V
song('plaza.wav',124,60,[0,4,5,3],1,11,swing=.0)   # C major: I V vi IV
print("ok")
