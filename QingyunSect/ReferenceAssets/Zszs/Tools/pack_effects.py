"""Pack native particle captures at 24 fps. Every frame uses the same crop."""
from export_models import LIB,dump
from pathlib import Path
from PIL import Image
import argparse,math,numpy as np

def pack(folder):
    rgba=np.array([np.asarray(Image.open(p).convert('RGBA')) for p in sorted(folder.glob('*.png'))])
    # A luminous hit contributes its emitted colour. The native alpha channel
    # also covers the unlit area of its billboard when captured over black.
    if folder.name in ('vfx_liuxingjianyu_hit','vfx_pokongjianfa_hit'):
        emission=rgba[:,:,:,:3].max(axis=3)
        rgba[:,:,:,3]=np.minimum(rgba[:,:,:,3],emission)
    visible=rgba[:,:,:,3]>3
    if not visible.any():raise ValueError('Empty particle review '+folder.name)
    last=int(np.where(visible.any(axis=(1,2)))[0][-1])+1
    rgba=rgba[:last];y,x=np.where(visible.any(axis=0));radius=int(max(np.ptp(x),np.ptp(y))/2)+4
    cx,cy=int((x.min()+x.max())/2),int((y.min()+y.max())/2)
    box=(cx-radius,cy-radius,cx+radius,cy+radius);cell=160;cols=min(6,last);rows=math.ceil(last/cols)
    atlas=Image.new('RGBA',(cols*cell,rows*cell));frames=[]
    for i,frame in enumerate(rgba):
        alpha=frame[:,:,3:].astype(np.float32)
        rgb=frame[:,:,:3].astype(np.float32)*255/np.maximum(alpha,1)
        frame[:,:,:3]=rgb.clip(0,255).astype(np.uint8)
        im=Image.fromarray(frame).crop(box).resize((cell,cell),Image.Resampling.LANCZOS)
        atlas.paste(im,((i%cols)*cell,(i//cols)*cell));frames.append(im)
    dest=LIB/'Library/Effects'/folder.parent.name
    atlas.save(dest/(folder.name+'.webp'),quality=94,method=6)
    durations=[42,41,42]*(last//3+1);durations=durations[:last];durations[-1]+=520
    frames[0].save(dest/(folder.name+'-preview.webp'),save_all=True,append_images=frames[1:],duration=durations,loop=0,quality=90)
    dump(dest/(folder.name+'.json'),dict(rate=24,count=last,cols=cols,rows=rows,cell=cell,captureBounds=box))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('input',type=Path);args=parser.parse_args()
    for path in sorted(args.input.glob('*/*')):pack(path)

