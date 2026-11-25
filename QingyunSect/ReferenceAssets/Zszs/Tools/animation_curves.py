"""Sample the source generic Transform curves at the clip's authored sample rate."""
import struct
import zlib
import numpy as np

def read_curves(clip, times):
    data=clip['m_MuscleClip']['m_Clip']['data']
    if data['m_ACLClip']['m_CurveCount']:
        raise ValueError('Unsupported ACL curve channel')
    stream=data['m_StreamedClip']
    raw=np.array(stream['data'],'<u4').tobytes()
    curves=[[] for _ in range(stream['curveCount'])]
    offset=0
    while offset<len(raw):
        time,count=struct.unpack_from('<fI',raw,offset);offset+=8
        for _ in range(count):
            i,a,b,c,d=struct.unpack_from('<I4f',raw,offset);offset+=20
            curves[i].append((time,a,b,c,d))
    sampled=[]
    for keys in curves:
        stamps=np.array([key[0] for key in keys])
        values=[]
        for t in times:
            i=max(0,int(np.searchsorted(stamps,t+1e-7,side='right'))-1)
            key,a,b,c,d=keys[i]
            dt=max(0,t-key) if np.isfinite(key) else 0
            values.append(((a*dt+b)*dt+c)*dt+d)
        sampled.append(values)
    dense=data['m_DenseClip']
    if dense['m_CurveCount']:
        frames=np.asarray(dense['m_SampleArray']).reshape(dense['m_FrameCount'],-1)
        sample=(times-dense['m_BeginTime'])*dense['m_SampleRate']
        index=np.rint(sample).astype(int).clip(0,len(frames)-1)
        sampled.extend(frames[index].T)
    sampled.extend(np.tile(data['m_ConstantClip']['data'],(len(times),1)).T)
    return np.array(sampled).T

def decode_clips(objects,paths,unity_rest):
    lookup={zlib.crc32(p.encode()):i for i,p in enumerate(paths)}
    result=[]
    for obj in sorted((o for o in objects.values() if o.type.name=='AnimationClip'),key=lambda o:o.peek_name()):
        clip=obj.read_typetree(); rate=clip['m_SampleRate']
        frames=round(clip['m_MuscleClip']['m_StopTime']*rate)+1
        samples=read_curves(clip,np.arange(frames)/rate)
        trs=np.tile(unity_rest,(frames,1,1));offset=0
        for binding in clip['m_ClipBindingConstant']['genericBindings']:
            if binding['typeID']!=4:raise ValueError('Expected a transform')
            start,width={1:(0,3),2:(3,4),3:(7,3)}[binding['attribute']]
            trs[:,lookup[binding['path']],start:start+width]=samples[:,offset:offset+width];offset+=width
        if offset!=samples.shape[1] or not np.isfinite(trs).all() or np.ptp(trs,axis=0).max()<.001:
            raise ValueError('Incomplete or static decoded motion: '+clip['m_Name'])
        result.append(dict(name=clip['m_Name'],frames=frames,rate=rate,tracks=[dict(path=p,trs=trs[:,i].reshape(-1)) for i,p in enumerate(paths)]))
    return dict(clips=result)

