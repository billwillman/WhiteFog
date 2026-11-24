"""Bake the funnel and its painted cloud flow / MatCap atlas with NumPy, Pillow and OpenCV."""
from pathlib import Path
import struct
import json
import cv2
import numpy as np
from PIL import Image

ART = Path(__file__).resolve().parents[1] / "WeChatMiniGame" / "art"
ASPECT = 16 / 9
CENTER = np.array([0.5, 0.35])  # Painting UV, measured from the bottom.
SECTORS, RINGS = 72, 24
TAU = 2 * np.pi


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t*t*(3-2*t)


def bake_flow():
    w,h = 256,144
    # Work bottom-up so relief agrees with the GPU UV convention.
    rgb = np.asarray(Image.open(ART / "CloudPainting.jpg").convert("RGB"), dtype="f4")[::-1] / 255
    luma = cv2.resize(rgb @ np.array([.2126,.7152,.0722],dtype="f4"), (w,h), interpolation=cv2.INTER_AREA)
    dx = cv2.Sobel(luma,cv2.CV_32F,1,0)
    dy = cv2.Sobel(luma,cv2.CV_32F,0,1)
    blur = lambda v: cv2.GaussianBlur(v,(0,0),2.5)
    v,u = np.mgrid[:h,:w]
    u,v=(u+.5)/w,(v+.5)/h
    x,y=(u-CENTER[0])*ASPECT,v-CENTER[1]
    r=np.hypot(x,y)
    nx,ny=x/np.maximum(r,.0001),y/np.maximum(r,.0001)
    # Orbit centres rise on larger ellipses, keeping the foreground shelf low.
    height_ratio, lift = .72,.36
    scaled_y = y/height_ratio
    ring = (np.sqrt((1-lift*lift)*x*x+scaled_y*scaled_y)-lift*scaled_y)/(1-lift*lift)
    orbit_y = CENTER[1]+lift*height_ratio*ring
    broad = lambda channel: cv2.GaussianBlur(channel,(0,0),5)
    angular_travel = np.minimum(.72,.32/np.maximum(ring,.001))
    angular_travel *= smooth(.065,.23,ring)*(.87+.13*broad(luma))
    border = np.minimum(np.minimum(u,1-u)*ASPECT,np.minimum(v,1-v))
    angular_travel = np.minimum(angular_travel,1.85*border/np.maximum(ring,.001))
    phase = .49+.19*np.sin(5*u+2*v)+.18*np.cos(7*v-3*u)+.10*broad(luma)
    mx,my=2*u-1,2*v-1
    mz=np.sqrt(np.maximum(0,1-mx*mx-my*my))
    cap=np.clip(.31+.63*np.maximum(0,-.36*mx+.54*my+.76*mz)+.11*(1-mz)**2,0,1)
    flow=np.stack((angular_travel,orbit_y,phase,cap),axis=-1)
    Image.fromarray(np.uint8(np.clip(flow[::-1],0,1)*255+.5)).save(ART/"CloudFlow.png")
    # Low-frequency painted relief is baked into the mesh. The flash lookup
    # follows the rounded cloud lips without per-pixel normal reconstruction.
    slope=np.stack((blur(dx),blur(dy)),axis=-1)
    surface=np.stack((-nx*.35,-ny*.30),axis=-1)-slope*1.7
    sites=[]
    for angle in np.arange(20)*TAU/20:
        radii=np.linspace(.27,.70,52)
        su=CENTER[0]+radii*np.cos(angle)/ASPECT
        sv=CENTER[1]+radii*np.sin(angle)
        sx=np.clip((su*w).astype(int),0,w-1);sy=np.clip((sv*h).astype(int),0,h-1)
        score=np.abs(luma[sy,sx]-.63)+.22*np.abs(radii-.46)
        score+=4*((su<.13)|(su>.87)|(sv<.13)|(sv>.88))
        best=score.argmin()
        sites.append(dict(x=round(float(su[best]),5),y=round(float(sv[best]),5)))
    (ART/"FlashSites.json").write_text(json.dumps(dict(points=sites),separators=(',',':')),encoding="utf8")
    return np.clip(surface,-.9,.9)


def vertex(u,v,z,theta,r,surface):
    # Position the ivory throat slightly below frame centre.
    screen_y=v+.048*np.sin(np.pi*np.clip(v,0,1))
    lens=np.tan(np.deg2rad(25))
    p=[(u-.5)*2*ASPECT*lens*z,(screen_y-.5)*2*lens*z,z]
    swell=smooth(.055,.26,r)*(1-smooth(.94,1.46,r))
    # Preserve the GVX1 vertex layout shared with the serialized Unity mesh.
    # The original displacement channels are reserved.
    front=(1-smooth(-.14,.04,v-CENTER[1]+.11*(u-CENTER[0])*ASPECT))*smooth(.18,.46,r)
    exposed=1-.70*smooth(.56,.85,u)*(1-front)
    amplitude=swell*(.035+.044*front+.019*(.5+.5*np.sin(3*theta+9*r)))*exposed
    phase=8.7*r+1.3*np.sin(theta)+2*theta
    direction=theta+1.08+front*.84
    h,w=surface.shape[:2]
    normal=surface[np.clip(int(v*h),0,h-1),np.clip(int(u*w),0,w-1)]
    return p+[u,v,amplitude*np.cos(direction),amplitude*np.sin(direction),phase,.028*swell,*normal]


def bake_mesh(surface):
    vertices=[vertex(*CENTER,78,0,0,surface)]
    indices=[]
    for k in range(1,RINGS+1):
        radius=1.42*(k/RINGS)**1.20
        for j in range(SECTORS):
            theta=TAU*j/SECTORS
            r=radius*(1+.048*np.sin(3*theta+radius*3)+.032*np.cos(2*theta-radius*4))
            u=CENTER[0]+r*np.cos(theta)/ASPECT
            v=CENTER[1]+r*np.sin(theta)
            depth=6+72/(1+5.5*r)*(1+.065*smooth(.01,.3,r)*np.sin(theta*3+13*r))
            vertices.append(vertex(u,v,depth,theta,r,surface))
            i=1+(k-1)*SECTORS+j
            nxt=1+(k-1)*SECTORS+(j+1)%SECTORS
            if k==1:
                indices.extend([0,nxt,i])
            else:
                indices.extend([i-SECTORS,nxt,i, i-SECTORS,nxt-SECTORS,nxt])
    vertices=np.asarray(vertices,dtype="<f4")
    indices=np.asarray(indices,dtype="<u2")
    with (ART/"Funnel.bytes").open("wb") as f:
        f.write(struct.pack("<4sII",b"GVX1",len(vertices),len(indices)))
        f.write(vertices.tobytes())
        f.write(indices.tobytes())
    print(f"Cloud funnel: {len(vertices)} vertices, {len(indices)//3} triangles")


if __name__=="__main__":
    bake_mesh(bake_flow())
