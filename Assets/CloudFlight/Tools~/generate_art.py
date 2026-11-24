"""Generate the two shader lookup textures and the aircraft mesh. Requires Pillow and numpy."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

out = Path(__file__).resolve().parents[1] / "WeChatMiniGame" / "art"
out.mkdir(parents=True, exist_ok=True)


# Orthographic projection of a softly lit hemisphere. The rim is continuous outside the disk.
y, x = np.mgrid[-1:1:128j, -1:1:128j]
y = -y
length = np.maximum(1, np.hypot(x, y))
x, y = x / length, y / length
z = np.sqrt(np.maximum(0, 1 - x*x - y*y))
broad = np.clip((-.23*x + .62*y + .75*z + .4) / 1.4, 0, 1)
value = .27 + .54*broad + .13*(1-z)**1.8 + .05*z
# Pack tileable fine density and its slopes alongside the authored MatCap.
rng=np.random.default_rng(1731)
iy,ix=np.mgrid[:128,:128]
detail=np.zeros_like(value)
for rate,weight in [(4,.52),(9,.29),(18,.13),(38,.06)]:
    knots=rng.random((rate,rate))
    tx,ty=ix*rate/128,iy*rate/128
    a,b=tx.astype(int),ty.astype(int)
    fx,fy=tx-a,ty-b
    fx,fy=fx*fx*(3-2*fx),fy*fy*(3-2*fy)
    left=knots[b,a]*(1-fy)+knots[(b+1)%rate,a]*fy
    right=knots[b,(a+1)%rate]*(1-fy)+knots[(b+1)%rate,(a+1)%rate]*fy
    detail+=weight*(left*(1-fx)+right*fx)
dx=(np.roll(detail,-1,1)-np.roll(detail,1,1))*4
dy=(np.roll(detail,1,0)-np.roll(detail,-1,0))*4
channels=np.stack((value,detail,.5+dx,.5+dy),-1)
Image.fromarray(np.uint8(np.clip(channels,0,1)*255+.5)).save(out/'CloudMatCap.png')

# Bake the volume atlas used by both renderers.
import runpy
runpy.run_path(str(Path(__file__).with_name("bake_cloud.py")), run_name="__main__")

def mesh():
    return dict(positions=[], normals=[], colors=[], indices=[])

def face(m, points, color):
    pts = np.array(points, dtype=float)
    normal = np.cross(pts[1]-pts[0], pts[2]-pts[0])
    normal /= max(1e-10,np.linalg.norm(normal))
    base = len(m['positions'])//3
    m['positions'].extend(np.round(pts.flatten(),3).tolist())
    m['normals'].extend(np.round(normal,3).tolist()*len(pts))
    m['colors'].extend(list(color)*len(pts))
    for k in range(1,len(pts)-1):
        m['indices'].extend([base,base+k,base+k+1])

def loft(m, sections, top, bottom):
    rings=[]
    for z,rx,ry,cy in sections:
        rings.append([(rx*np.cos(a),cy+ry*np.sin(a),z) for a in np.arange(10)*np.pi/5])
    for a,b in zip(rings,rings[1:]):
        for i in range(10):
            j=(i+1)%10
            face(m,[a[i],a[j],b[j],b[i]],top if (a[i][1]+a[j][1])*.5>-.08 else bottom)
    face(m,list(reversed(rings[0])),bottom)
    face(m,rings[-1],top)

def wing(m, outline, thickness, color):
    top=[(x,y+thickness,z) for x,y,z in outline]
    if np.cross(np.subtract(top[1],top[0]),np.subtract(top[2],top[0]))[1]<0:
        outline=outline[::-1]; top=top[::-1]
    face(m,top,color); face(m,outline[::-1],(47,85,100))
    for i in range(len(top)):
        j=(i+1)%len(top)
        face(m,[top[j],top[i],outline[i],outline[j]],(69,112,124))

plane=mesh()
loft(plane,[(-3.4,.08,.09,.13),(-2.2,.28,.31,.05),(-.65,.55,.48,0),
            (.95,.53,.49,0),(2.15,.37,.34,-.04),(2.78,.04,.07,-.06)],
     (231,192,106),(52,103,112))
loft(plane,[(-.9,.13,.12,.37),(-.2,.40,.37,.41),(.65,.36,.28,.42),(1.14,.07,.035,.38)],
     (34,72,88),(48,89,97))
for side in (-1,1):
    def mirror(points): return [(side*x,y,z) for x,y,z in points]
    wing(plane,mirror([(.35,.03,1),(4.4,.29,-.25),(4.8,.33,-.69),(4.35,.32,-.95),(.4,.03,-.86)]),.105,(219,226,209))
    wing(plane,mirror([(3.88,.405,-.17),(4.4,.405,-.32),(4.59,.432,-.57),(3.97,.415,-.48)]),.012,(210,101,45))
    wing(plane,mirror([(.12,.18,-2.12),(1.84,.25,-2.86),(1.98,.25,-3.16),(.1,.19,-3.04)]),.07,(208,186,117))
fin=[(-.05,.2,-1.97),(-.05,1.38,-2.87),(-.05,1.43,-3.2),(-.05,.18,-3.08)]
face(plane,fin,(224,146,64)); face(plane,[(.05,y,z) for _,y,z in fin[::-1]],(224,146,64))
for i in range(4):
    a,b=fin[i],fin[(i+1)%4]
    face(plane,[a,b,(.05,b[1],b[2]),(.05,a[1],a[2])],(205,177,127))
(out/'Aircraft.json').write_text(json.dumps(plane,separators=(',',':')),'utf-8')
