"""Fit the sampled original sword arm trajectories to the existing NP0023 skin.
Output five affine matrices per pose, with fixed byte weights per vertex."""
from pathlib import Path
import argparse,re,base64,json
import numpy as np

def unit(x):return x/np.maximum(np.linalg.norm(x,axis=-1,keepdims=True),1e-8)
def align(a,b):
    a,b=unit(a),unit(b);v=np.cross(a,b);c=np.dot(a,b)
    if c<-.9999:
        axis=unit(np.cross(a,[0,1,0]) if abs(a[1])<.9 else np.cross(a,[1,0,0]))
        return 2*np.outer(axis,axis)-np.eye(3)
    skew=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
    return np.eye(3)+skew+skew@skew/max(1e-8,1+c)
def frame(x,z):
    x=unit(x);y=unit(np.cross(z,x));return np.stack([x,y,np.cross(x,y)],axis=1)
def affine(rot,src,dest):return np.column_stack([rot,dest-rot@src])
def encode(a):return base64.b64encode(a.tobytes()).decode()
def run(source,samples,dest):
    prefab=(source/'PackRes/CinemaDirector/shoot-LianQiTuPo.prefab').read_text('utf-8-sig')
    docs={int(i):(t,b) for t,i,b in re.findall(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- |\Z)',prefab)}
    parents={};names={};parts=[];rest={}
    for i,(ty,v) in docs.items():
        if ty=='4':
            parents[i]=int(re.search(r'm_Father: \{fileID: (\d+)',v)[1])
            names[i]=re.search(r'(?m)^  m_Name: (.*)$',docs[int(re.search(r'm_GameObject: \{fileID: (\d+)',v)[1])][1])[1]
    def group(id):
        while id in names:
            n=names[id].removeprefix('Bip001 ')
            for side,index in [('L ',1),('R ',3)]:
                if n==side+'UpperArm':return index
                if n in (side+'Forearm',side+'Hand'):return index+1
            if n in ('Spine','Spine1','Spine2','Neck','Head'):return 0
            id=parents[id]
        return -1
    for f in sorted((source/'Mesh').glob('NP*.asset')):
        mesh=f.read_text();guid=re.search(r'guid: (\w+)',Path(str(f)+'.meta').read_text())[1]
        ren=next(b for t,b in docs.values() if t=='137' and 'guid: '+guid in b)
        ids=[int(i) for i in re.findall(r'fileID: (\d+)',re.search(r'  m_Bones:\n((?:  - [^\n]+\n)+)',ren)[1])]
        inv=np.array([float(x) for x in re.findall(r'e\d\d: ([^\n]+)',mesh.split('  m_BindPose:',1)[1].split('  m_BoneNameHashes:',1)[0])]).reshape((-1,4,4))
        for i,b in zip(ids,np.linalg.inv(inv)):rest[names[i].removeprefix('Bip001 ')]=b[:3,3]
        count=int(re.search(r'm_VertexCount: (\d+)',mesh)[1]);data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',mesh)[1])
        channels=[tuple(map(int,v)) for v in re.findall(r'stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)',mesh.split('m_Channels:',1)[1].split('m_DataSize:',1)[0])]
        def read(c):
            stream,offset,fmt,dim=channels[c];base=0
            for j in range(stream+1):
                stride=max(off+d*4 for st,off,_,d in channels if st==j and d)
                if j==stream:return np.ndarray((count,dim),'<i4' if fmt==10 else '<f4',buffer=data,offset=base+offset,strides=(stride,4))
                base=(base+stride*count+15)//16*16
        weights=read(12);joints=read(13);mapped=np.array([group(i) for i in ids])[joints.clip(0,len(ids)-1)]
        influences=np.column_stack([(weights*(mapped==k)).sum(axis=1) for k in range(5)])
        parts.append(dict(vertices=count,positions=encode(np.rint(read(0)*4096).astype('<i2')),weights=encode(np.rint(influences.clip(0,1)*255).astype('uint8'))))
    lookup=['Pelvis','Spine1','Head','L UpperArm','L Forearm','L Hand','R UpperArm','R Forearm','R Hand']
    bind=np.array([rest[n] for n in lookup]);base=frame(bind[6]-bind[3],bind[2]-bind[0]);motions={}
    for clip in json.loads(samples.read_text())['clips']:
        raw=np.array(clip['positions']).reshape(-1,9,3)
        poses=raw[:,:,[0,2,1]].copy();poses[:,:,1]*=-1
        all_matrices=[];hands=[]
        for pose in poses:
            torso=frame(pose[6]-pose[3],pose[2]-pose[0])@base.T
            matrices=[affine(torso,bind[0],bind[0])];wrists=[]
            for a,b,c in [(3,4,5),(6,7,8)]:
                shoulder=torso@(bind[a]-bind[0])+bind[0]
                arm=align(torso@(bind[b]-bind[a]),pose[b]-pose[a])@torso
                elbow=arm@(bind[b]-bind[a])+shoulder
                forearm=align(arm@(bind[c]-bind[b]),pose[c]-pose[b])@arm
                wrists.append(forearm@(bind[c]-bind[b])+elbow)
                matrices.extend([affine(arm,bind[a],shoulder),affine(forearm,bind[b],elbow)])
            all_matrices.append(matrices);hands.append(wrists)
        id=clip['clip'].split('@')[1];hand=0 if 'liuxing' in id else 1
        release=int(np.argmax((raw[:,[5,8]]-raw[:,[0]])[:,hand,2]))
        motions[id]=dict(rate=clip['rate'],frames=clip['frames'],rows=encode(np.array(all_matrices,'<f4')),castingHand=encode(np.array(hands,'<f4')[:,hand].copy()),releaseFrame=release,source=clip['clip'])
    dest.write_text(json.dumps(dict(parts=parts,clips=motions),separators=(',',':')),'utf8')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('samples',type=Path);p.add_argument('dest',type=Path)
    a=p.parse_args();run(a.source,a.samples,a.dest)

