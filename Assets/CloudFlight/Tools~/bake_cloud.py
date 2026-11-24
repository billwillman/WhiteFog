"""Bake the looping cloud sea, its solid isosurface and translucent density layers.

Coordinates are metres, positive Z is the flight direction. The 192 m field is
periodic in Z. Both renderers consume this binary mesh and the same linear atlas.
Requires NumPy and Pillow.
"""
from pathlib import Path
import struct
import json
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "WeChatMiniGame" / "art"
NX, NY, NZ = 128, 80, 192
PERIOD, STEP, CHUNKS = 192, 6, 8
TILE, COLS = 128, 8
rng = np.random.default_rng(1387)
lattice = rng.uniform(-1, 1, (64, 64, 64)).astype("f4")
xs = np.linspace(-72, 72, NX, dtype="f4")
ys = np.linspace(-36, 36, NY, dtype="f4")
x,y,z = np.meshgrid(xs,ys,np.arange(NZ,dtype="f4"),indexing="ij")


def noise(x,y,z,periods,offset=0):
    # The Z lattice has exactly 'periods' knots per loop.
    c = [x*periods/PERIOD+offset, y*periods/PERIOD-offset*.31, z*periods/PERIOD]
    base = [np.floor(t).astype("i4") for t in c]
    f = [t-i for t,i in zip(c,base)]
    w = [a*a*(3-2*a) for a in f]
    result = np.zeros(np.broadcast_shapes(x.shape,y.shape,z.shape),dtype="f4")
    for i,j,k in np.ndindex(2,2,2):
        g = lattice[(base[0]+i)%64,(base[1]+j)%64,((base[2]+k)%periods+offset)%64]
        result += g*(w[0] if i else 1-w[0])*(w[1] if j else 1-w[1])*(w[2] if k else 1-w[2])
    return result


def center(z):
    a=z*(2*np.pi/PERIOD)
    return 11*np.sin(a+.25)+5*np.sin(2*a-.6),1.6+3.4*np.sin(a-.6)+1.4*np.sin(2*a+.9)


cx,cy=center(z)
a=z*(2*np.pi/PERIOD)
large=noise(x,y,z,5,12)
wx=x+5*large
wy=y+2.8*noise(x,y,z,7,5)
wz=z+4*noise(x,y,z,6,19)
form=noise(wx,wy,wz,12,9)
medium=noise(wx,wy,wz,25,18)
fine=noise(wx,wy,wz,53,29)
ripples=3.5*form+1.65*medium+.76*fine
floor=cy-9+2.7*np.sin(.076*x+a*2)+1.8*np.cos(.12*x-a*3)+4.5*large
roof=cy+14+5.8*np.sin(.067*x+a*2+.8)+4.5*np.cos(.095*x-a)+5.4*large
lower=floor-y+ripples
upper=y-roof+1.35*ripples
bank=9.0*large+4.5*form+medium*2.1+fine + 1.6*np.sin(x*.21-a*2)-.8
# Break the enclosing sheet in two places. From these open bays the next
# bridge is visible across a dark channel before the flight re-enters it.
bay=np.maximum(np.exp(-((np.sin((a-.26)/2)*64)/22)**2),
               .93*np.exp(-((np.sin((a-3.71)/2)*64)/16)**2))
crosswind=np.exp(-((x-cx-9)/36)**2)
upper-=48*bay*crosswind
bank-=15*bay*np.exp(-((x-cx-3)/33)**2)
lower-=bay*(3+9*np.exp(-((x-cx+12)/8)**2))
field=np.maximum(np.maximum(lower,upper),bank)
# Cut a changing passage and broad side openings through connected noise masses.
width=11+3*np.sin(2*a+.3)+3*np.cos(3*a)
height=7.6+2.8*np.cos(2*a+.4)+1.5*np.sin(3*a-.8)
tunnel=(np.sqrt(((x-cx)/width)**2+((y-cy)/height)**2)-1)*8
field=np.minimum(field,tunnel+.95*ripples)
# Keep the bays open laterally. A continuous, noise-shaped layer above the
# route joins the cloud banks before the upper edge of the baked volume.
canopy_shape = 1.4*np.sin(.065*x+a) + 1.6*np.cos(.08*x-2*a) + 1.8*large + .65*ripples
# The outer shoulders dip back into the side banks at wide phone aspect ratios.
shoulder = np.clip((np.abs(x-cx)-18)/24,0,1)
canopy = y - (cy+14.5+np.clip(canopy_shape,-4,4) - 6*shoulder*shoulder*(3-2*shoulder))
blend = np.maximum(0, 2.0-np.abs(field-canopy))
field = np.maximum(field,canopy) + blend*blend/8.0
# Join the outer banks all the way around each cross-section. The wider bays
# keep their interior volume, and the centreline stays open along the flight.
outer_width = 16 + 4.5*bay + 1.5*np.sin(2*a+.7)
outer_height = np.where(y>cy,16+1.6*np.cos(a),13+1.7*np.sin(2*a))
elliptic_radius = np.sqrt(((x-cx)/outer_width)**2 + ((y-cy)/outer_height)**2)
outer_bank = 14*(elliptic_radius-1) + 1.1*large + .55*ripples
join = np.maximum(0, 2.4-np.abs(field-outer_bank))
field = np.maximum(field,outer_bank) + join*join/9.6
density=np.clip(.30+field*.155,0,1)
density=density*density*(3-2*density)

# Optical depth is integrated offline for both the sunlight and skylight.
def incoming(direction,distances):
    result=np.zeros_like(density)
    prev=0
    for distance in distances:
        ix,iy,iz=np.rint(np.asarray(direction)*distance/[xs[1]-xs[0],ys[1]-ys[0],1]).astype(int)
        shifted=np.roll(density,(-ix,-iy,-iz),(0,1,2))
        if ix: shifted[slice(-ix,None) if ix>0 else slice(None,-ix),:,:]=0
        if iy: shifted[:,slice(-iy,None) if iy>0 else slice(None,-iy),:]=0
        result+=shifted*(distance-prev)
        prev=distance
    return result

sun=incoming((-.68,.52,.50),(1,2,4,6,9,13,18,25,34))
sky=incoming((-.15,.94,-.30),(2,5,9,15,24))
grad=np.stack(np.gradient(field,xs,ys,np.arange(NZ)),axis=-1)
grad[:,:,0,2]=.5*(field[:,:,1]-field[:,:,-1])
grad[:,:,-1,2]=.5*(field[:,:,0]-field[:,:,-2])
normal=-grad/np.maximum(.0001,np.linalg.norm(grad,axis=-1,keepdims=True))
wrap=np.clip(.48+.52*(normal[...,1]*.65-normal[...,0]*.63+normal[...,2]*.42),0,1)
light=np.clip(.095+.48*np.exp(-sun*.085)+.18*np.exp(-sky*.082)+.24*wrap+.055*medium,0,1)


def pack_oct(n):
    q=n/np.maximum(.0001,np.sum(np.abs(n),axis=-1,keepdims=True))
    xy=q[...,:2].copy()
    return .5+.5*np.where(q[...,2,None]<0,(1-np.abs(xy[...,::-1]))*np.where(xy>=0,1,-1),xy)


# Surface nets: one vertex per crossed cell, one quad per sign-changing edge.
def isosurface():
    coarse=field[::2,::2,::2]
    nx,ny,nz=coarse.shape
    solid=coarse>5.3
    q=np.concatenate((coarse-5.3,coarse[:,:,:1]-5.3),axis=2)
    corners=np.array(list(np.ndindex(2,2,2)))
    vals=np.stack([q[i:i+nx-1,j:j+ny-1,k:k+nz] for i,j,k in corners],axis=-1)
    active=(vals.min(-1)<=0)&(vals.max(-1)>0)
    cells=np.stack(np.where(active),axis=-1)
    v=vals[active]; offsets=np.zeros_like(cells,dtype=float); count=np.zeros(len(v))
    for i,a in enumerate(corners):
        for j,b in enumerate(corners[i+1:],i+1):
            if np.abs(b-a).sum()!=1: continue
            crossings=(v[:,i]>0)!=(v[:,j]>0)
            t=v[crossings,i]/(v[crossings,i]-v[crossings,j])
            offsets[crossings]+=a+(b-a)*t[:,None]; count[crossings]+=1
    sample=cells+offsets/count[:,None]
    positions=(sample*2)*[xs[1]-xs[0],ys[1]-ys[0],1]+[xs[0],ys[0],0]
    nearest=np.rint(sample*2).astype(int); nearest[:,2]%=NZ
    coord=tuple(nearest.T)
    colors=np.column_stack([np.ones(len(v)),light[coord],pack_oct(normal[coord])])
    ids=np.full(active.shape,-1);ids[active]=np.arange(len(v))
    faces=[]
    for axis in range(3):
        # Connectivity between last and first slices is emitted with a translated seam vertex below.
        edge=np.roll(solid,-1,axis=axis)!=solid
        grid=np.stack(np.where(edge),axis=1)
        grid=grid[(grid[:,0]>0)&(grid[:,0]<nx-1)&(grid[:,1]>0)&(grid[:,1]<ny-1)]
        u=np.eye(3,dtype=int)[(axis+1)%3];w=np.eye(3,dtype=int)[(axis+2)%3]
        ring=[]
        for delta in (0*u,u,u+w,w):
            loc=grid-delta;loc[:,2]%=nz
            ring.append(ids[tuple(loc.T)])
        quads=np.stack(ring,axis=1)
        quads=quads[np.all(quads>=0,axis=1)]
        faces.extend([quads[:,[0,1,2]],quads[:,[0,2,3]]])
    faces=np.concatenate(faces)
    # Crossing the periodic seam requires positions in the same world-space period.
    seam=np.ptp(positions[faces,2],axis=1)>PERIOD/2
    crosses=faces[seam].copy();moved=crosses[positions[crosses,2]<PERIOD/2]
    uniq=np.unique(moved); remap=np.arange(len(positions)); remap[uniq]=len(positions)+np.arange(len(uniq))
    crosses=np.where(positions[crosses,2]<PERIOD/2,remap[crosses],crosses);faces[seam]=crosses
    positions=np.vstack([positions,positions[uniq]+[0,0,PERIOD]])
    return positions,np.vstack([colors,colors[uniq]]),faces


def mesh_record(file,pos,uv,col,faces):
    used,local=np.unique(faces,return_inverse=True)
    if len(used)>65535: raise ValueError("16-bit chunk overflow")
    # Planar buffers can be uploaded directly by WebGL.
    file.write(struct.pack("<II",len(used),local.size))
    for arr,kind in [(pos[used],"<f4"),(uv[used],"<f4"),(np.clip(col[used],0,1)*255+.5,"u1"),(local,"<u2")]:
        file.write(arr.astype(kind).tobytes())


def bake_flash_sites():
    # Select illuminated positions on actual inner cloud faces, including at the open bays.
    points=[]
    for depth in range(0,PERIOD,STEP):
        px,py=center(float(depth))
        candidates=[]
        for theta in np.arange(24)*np.pi/12:
            radius=np.arange(4,35,.65)
            sx=px+radius*np.cos(theta)
            sy=py+.80*radius*np.sin(theta)
            ix=np.clip(np.searchsorted(xs,sx),0,NX-1)
            iy=np.clip(np.searchsorted(ys,sy),0,NY-1)
            hits=np.flatnonzero(density[ix,iy,depth]>.48)
            if len(hits):
                k=hits[0]
                candidates.append((radius[k],theta,float(xs[ix[k]]),float(ys[iy[k]])))
        eligible=[v for v in candidates if v[0]<20]
        eligible=sorted(eligible or candidates,key=lambda v:v[1])
        for k in range(8):
            _,_,xx,yy=eligible[k*len(eligible)//8]
            points.append(dict(x=round(xx,3),y=round(yy,3),z=depth))
    (OUT/'FlashSites.json').write_text(json.dumps(dict(points=points),separators=(',',':')),encoding='utf8')


def bake():
    bake_flash_sites()
    p,col,tri=isosurface()
    owners=np.minimum((p[tri,2].mean(axis=1)/(PERIOD/CHUNKS)).astype(int),CHUNKS-1)
    atlas=np.zeros((512,1024,4),dtype='u1')
    with (OUT/'CloudSea.cloudmesh').open('wb') as f:
        f.write(b'CSM1'+struct.pack('<II',CHUNKS,PERIOD))
        for chunk in range(CHUNKS):
            mesh_record(f,p,np.zeros((len(p),2)),col,tri[owners==chunk])
            all_p=[];all_uv=[];all_c=[];all_tri=[]
            for slot in reversed(range(4*chunk,4*chunk+4)):
                start=slot*STEP; part=density[:,:,start:start+STEP]
                mass=part.sum(2);div=np.maximum(.0001,mass)
                depth=(part*(np.arange(STEP)+.5)).sum(2)/div
                brightness=(light[:,:,start:start+STEP]*part).sum(2)/div
                n=(normal[:,:,start:start+STEP]*part[...,None]).sum(2)/div[...,None]
                tex=np.dstack((np.sqrt(mass/STEP),brightness,pack_oct(n)))
                ix,iy=slot%COLS*TILE,slot//COLS*TILE
                atlas[iy:iy+NY,ix:ix+NX]=np.uint8(np.clip(tex.transpose(1,0,2),0,1)*255+.5)
                gx=np.r_[np.arange(0,NX-1,5),NX-1];gy=np.r_[np.arange(0,NY-1,4),NY-1]
                xx,yy=np.meshgrid(gx,gy,indexing='ij')
                # Ribbons track the mass centroid in 3D, with empty cells removed.
                zz=start+np.where(mass[xx,yy]>.04,depth[xx,yy],STEP*.5)
                pos=np.stack([xs[xx],ys[yy],zz],-1).reshape(-1,3)
                uv=np.stack([(ix+xx+.5)/1024,(iy+yy+.5)/512],-1).reshape(-1,2)
                grid=np.arange(xx.size).reshape(xx.shape)+sum(len(v) for v in all_p)
                quads=np.stack([grid[:-1,:-1],grid[1:,:-1],grid[1:,1:],grid[:-1,1:]],-1)
                occupied=np.maximum.reduceat(np.maximum.reduceat(mass,gx,axis=0),gy,axis=1)[:-1,:-1]>.003
                q=quads[occupied]
                all_tri.extend([q[:,[0,1,2]],q[:,[0,2,3]]])
                all_p.append(pos);all_uv.append(uv);all_c.append(np.zeros((len(pos),4)))
            mesh_record(f,np.vstack(all_p),np.vstack(all_uv),np.vstack(all_c),np.vstack(all_tri))
    Image.fromarray(atlas[::-1]).save(OUT/'CloudNoise.png')
    print("Cloud core triangles",len(tri),"mesh bytes",(OUT/'CloudSea.cloudmesh').stat().st_size)

if __name__=='__main__': bake()
