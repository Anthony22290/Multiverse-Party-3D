from pathlib import Path
import math
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon, Circle, FancyArrowPatch
from matplotlib.lines import Line2D

root=Path(__file__).resolve().parents[1]
s=44/15.4329567
z=lambda x:(x-.35)*s
w=3.76*s*1.2
fig=plt.figure(figsize=(13,12),facecolor="#071d2a")
ax=fig.add_axes([.06,.10,.56,.78],facecolor="#123e50")
notes=fig.add_axes([.66,.1,.31,.78],facecolor="#071d2a")
notes.axis("off")
fig.text(.06,.95,"ARENA DEL CAOS",color="#f5bd62",fontsize=27,weight="bold")
fig.text(.06,.913,"Plano de distribución · Barco pirata de 44 m",color="#bcdadf",fontsize=14)
ax.set_aspect("equal")
# Stylized footprint, aligned with +Z. The playing decks below use actual implementation coordinates.
hull=[(-w*.45,z(-5.95)),(w*.45,z(-5.95)),(w*.61,z(-3)),(w*.62,z(2)),(w*.44,z(6.5)),(0,z(9.52)),(-w*.44,z(6.5)),(-w*.62,z(2)),(-w*.61,z(-3))]
ax.add_patch(Polygon(hull,facecolor="#3a2119",edgecolor="#ce9250",linewidth=2))
zones=[("POPA",z(-5.85),z(-3.28),"#9c6334"),("PATIO CENTRAL",z(-3.28),z(2.31),"#b88043"),("PROA",z(2.31),z(6.4),"#a46a39")]
for label,a,b,color in zones:
    ax.add_patch(Rectangle((-w/2,a),w,b-a,facecolor=color,edgecolor="#e5b866",linewidth=1.4))
    for x in range(-6,7):
        ax.plot([x,x],[a,b],color="#674425",lw=.4,alpha=.65)
    for zz in [a+i*2.2 for i in range(1,int((b-a)/2.2)+1)]:
        ax.plot([-w/2,w/2],[zz,zz],color="#5b3a25",lw=.45,alpha=.6)
    ax.text(0,b-.8,label,ha="center",va="center",color="#fff1c9",fontsize=10,weight="bold")
for side in [-1,1]:
    for a,b in [(z(.7),z(2.31)),(z(-1.8),z(-3.28))]:
        x=side*w*.32
        ax.add_patch(Rectangle((x-1.2,min(a,b)),2.4,abs(b-a),facecolor="#d2a268",edgecolor="#fce2a0",linewidth=1.2))
        ax.add_patch(FancyArrowPatch((x,a),(x,b),arrowstyle="-|>",mutation_scale=14,color="#493320"))
    for zz in [z(-.78)-3+i*3.1 for i in range(3)]:
        ax.add_patch(Circle((side*(w*.5-1.5),zz),.42,facecolor="#543a28",edgecolor="#e2af60",linewidth=1))
    ax.add_patch(Circle((side*(w*.5-1.7),z(4.2)),.63,facecolor="#18242a",edgecolor="#efc06d",linewidth=1.6))
    ax.add_patch(Rectangle((side*(w*.5-1.1)-.5,z(-4.9)-.35),1,.7,facecolor="#56352a",edgecolor="#f2bd50",linewidth=1))
    for zz in [z(-5.6),z(6.1)]:
        ax.plot(side*(w*.5-1.1),zz,"*",color="#ffe49b",markersize=11)
    # Actual rail gaps: center court and the outer section of the bow.
    for zz in [z(-.78),z(4.8)]:
        ax.plot([side*w/2,side*w/2],[zz-1.2,zz+1.2],color="#52c5cb",lw=5)
ax.add_patch(Rectangle((-1.3,z(-.78)-1.6),2.6,3.2,facecolor="#293439",edgecolor="#e3ad56"))
for x in [-.9,-.45,0,.45,.9]:ax.plot([x,x],[z(-.78)-1.4,z(-.78)+1.4],color="#899193",lw=.7)
for zz in [z(-1.9),z(3.4)]:
    ax.add_patch(Rectangle((-.425,zz-.425),.85,.85,facecolor="#402d23",edgecolor="#e0af65"))
ax.add_patch(Circle((0,z(-5.1)),.65,facecolor="none",edgecolor="#f5cb70",linewidth=2))
for i in range(6):
    a=i*math.pi/3
    x=math.cos(a)*w*.26; zz=z(-.78)+math.sin(a)*3.6
    ax.add_patch(Circle((x,zz),.37,facecolor="#d9f5ed" if i==0 else "#bb4e47",edgecolor="#fff4cb",linewidth=1))
    ax.text(x,zz,str(i+1),ha="center",va="center",fontsize=7,color="#173442",weight="bold")
ax.annotate("",(-w/2,-20),(w/2,-20),arrowprops=dict(arrowstyle="<->",color="#9fd4d8"))
ax.text(0,-21.3,"12,9 m de cubierta",ha="center",color="#bfe4e4",fontsize=10)
ax.text(0,27,"PROA / +Z",ha="center",color="#e6e7ce",fontsize=10)
ax.set_xlim(-11,11);ax.set_ylim(-23,29)
ax.set_xticks([]);ax.set_yticks([])
for spine in ax.spines.values():spine.set_visible(False)

items=[
("01","PATIO CENTRAL","Altura: 3 m sobre el agua.\nEscotilla a ras, seis barriles,\ndos cubos y seis tripulantes."),
("02","CASTILLO DE PROA","Altura: 4,51 m. Dos cañones,\nanclas y faroles. Dos rampas\nconectan con el patio."),
("03","ALCÁZAR DE POPA","Altura: 5,65 m. Timón, cofres\ny faroles. Acceso por dos\nrampas de 2,4 m de ancho."),
("04","BORDAS ABIERTAS","Barandas bajas y huecos\nlaterales para empujar, lanzar\ny agarrarse antes de caer."),
("05","MAR Y ENTORNO","Agua turquesa animada, espuma\ny tres islotes con palmeras.\nEl tiburón ataca desde el borde.")]
for i,(number,title,body) in enumerate(items):
    y=.95-i*.18
    notes.text(0,y,number,color="#efb962",fontsize=22,weight="bold")
    notes.text(.16,y+.007,title,color="#e7eee4",fontsize=12,weight="bold")
    notes.text(.16,y-.034,body,color="#a9c7cf",fontsize=11,linespacing=1.55,va="top")
notes.text(0,.015,"Cubierta útil: ~35 × 12,9 m\nEl casco y el bauprés suman 44 m.",color="#e6c995",fontsize=11,linespacing=1.5)
fig.text(.06,.042,"Esquema de distribución, no captura de Unity. El mapa se genera al entrar en Play.",color="#82abb6",fontsize=10)
(root/"Docs").mkdir(exist_ok=True)
fig.savefig(root/"Docs/ArenaDelCaos_Mapa.png",dpi=150,facecolor=fig.get_facecolor())
plt.close(fig)
print("Saved Docs/ArenaDelCaos_Mapa.png")
