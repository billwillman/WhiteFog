"""Export the verified model prefabs from the 1.3.0 asset set to glTF 2.0."""
from pathlib import Path
from collections import Counter
from functools import lru_cache
import argparse, base64, hashlib, io, json, re, struct
import numpy as np
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

LIB=Path(__file__).resolve().parents[1]
DEFAULT_PACKAGE=Path('E:/zszs_1.3.0_20261118_043440_4d006/assets')
SAMPLES=[
 ('小雕','怪物','monster/mon_xiaodiao_001','resourcesab/prefabs/effect/monster/shendiao.ab'),
 ('神雕','怪物','jy/jy_shendiao_001','resourcesab/prefabs/charactor/monster.ab'),
 ('猎犬','怪物','monster/mon_liequan_001','resourcesab/prefabs/charactor/monster.ab'),
 ('野狗','怪物','monster/mon_yegou001','resourcesab/prefabs/charactor/monster.ab'),
 ('金莲玉兔','怪物','monster/mon_jinlianyt_001','resourcesab/prefabs/charactor/monster/xian.ab'),
 ('仙白虎','怪物','jy/jy_xianbaihu_001','resourcesab/prefabs/charactor/boss.ab'),
 ('男角色 000','人物','character/chr_male_000','artres/charactor/character/backup.ab'),
 ('男角色 001','人物','character/chr_male_001','artres/charactor/character/backup.ab'),
]

def dump(path,obj):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(obj,ensure_ascii=False,indent=2,allow_nan=False),encoding='utf8')

def normalized(path):
    b=bytearray(path.read_bytes())
    if b[:8]!=b'UnityFS\0':raise ValueError(path)
    if b[8:12]==b'\0\0\0\6':
        # This release writes the version-6 tag, but places the block directory
        # and block data at the version-7 16-byte boundaries.
        header=b.index(0,b.index(0,12)+1)+1
        end=header+20
        if int.from_bytes(b[header:header+8],'big')!=len(b) or any(b[end:(end+15)//16*16]):
            raise ValueError(f'Unexpected header: {path}')
        b[11]=7
    return bytes(b)

class Library:
    def __init__(self,root):
        self.root=root
        self.items={i['assetBundleName']:i for i in json.loads((root/'_ab_info.json').read_text())['items']}
        self.envs={};self.objects={};self.cab_names={}
    def load(self,name):
        if name not in self.envs:
            e=UnityPy.load(normalized(self.root/name))
            self.envs[name]=e
            objects=list(e.objects)
            if not objects:raise ValueError('Empty bundle '+name)
            sf=objects[0].assets_file
            self.cab_names[sf.name.lower()]=name
            self.objects[name]={o.path_id:o for o in objects}
        return self.objects[name]
    def dependencies(self,name):
        seen=set();queue=[name]
        while queue:
            n=queue.pop()
            if n not in seen:
                seen.add(n);queue.extend(self.items[n]['depAbs'])
        return sorted(seen)
    def resolve(self,owner,pointer):
        if not pointer.path_id:return None
        name=owner
        if pointer.file_id:
            cab=pointer.assetsfile.externals[pointer.file_id-1].name.lower()
            if cab not in self.cab_names:
                for dep in self.items[owner]['depAbs']:
                    self.load(dep)
                    if cab in self.cab_names:break
            name=self.cab_names.get(cab)
        if name is None:return None
        o=self.load(name).get(pointer.path_id)
        return (name,o) if o else None
    @lru_cache(maxsize=None)
    def texture(self,name,oid):
        o=self.load(name)[oid];tex=o.read()
        if o.type.name!='Texture2D':
            return dict(name=tex.m_Name,bundle=name,pathID=str(oid),type=o.type.name,size=[tex.m_Width,tex.m_Height],unityFormat=tex.m_TextureFormat,faces=getattr(tex,'m_ImageCount',1))
        filename=re.sub(r'[^-\w.]+','_',tex.m_Name)+'_'+str(oid)+'.png'
        dst=LIB/'Library'/'Textures'/filename
        dst.parent.mkdir(parents=True,exist_ok=True)
        tex.image.save(dst)
        return dict(name=tex.m_Name,file='Textures/'+filename,size=[tex.m_Width,tex.m_Height],unityFormat=tex.m_TextureFormat,bundle=name,pathID=str(oid))
    @lru_cache(maxsize=None)
    def material(self,name,oid):
        mat=self.load(name)[oid].read();props=mat.m_SavedProperties
        shader=self.resolve(name,mat.m_Shader)
        shader_name=None
        if shader:
            shader_name=shader[1].peek_name()
            if not shader_name:
                sh=shader[1].read()
                shader_name=getattr(getattr(sh,'m_ParsedForm',None),'m_Name',None) or sh.m_Name
        tex={}
        for slot,item in props.m_TexEnvs:
            if not item.m_Texture.path_id:continue
            resolved=self.resolve(name,item.m_Texture)
            asset=self.texture(resolved[0],resolved[1].path_id) if resolved else None
            tex[slot]=dict(asset=asset,pathID=str(item.m_Texture.path_id),scale=[item.m_Scale.x,item.m_Scale.y],offset=[item.m_Offset.x,item.m_Offset.y])
        return dict(name=mat.m_Name,bundle=name,pathID=str(oid),shader=shader_name,shaderPathID=str(mat.m_Shader.path_id),
                    shaderBundle=shader[0] if shader else None,renderQueue=mat.m_CustomRenderQueue,keywords=mat.m_ValidKeywords or [],invalidKeywords=mat.m_InvalidKeywords or [],
                    floats=dict(props.m_Floats),integers=dict(props.m_Ints or []),
                    colors={k:[v.r,v.g,v.b,v.a] for k,v in props.m_Colors},textures=tex,
                    instancing=mat.m_EnableInstancingVariants,disabledPasses=mat.disabledShaderPasses or [])
    @lru_cache(maxsize=None)
    def bindings(self,prefab):
        result={}
        for o in self.load(prefab).values():
            if o.type.name!='SkinnedMeshRenderer':continue
            r=o.read()
            # Key against the source serialized file ID as well as the mesh ID.
            ref=r.m_Mesh
            if not ref.path_id:continue
            cab=(ref.assetsfile.externals[ref.file_id-1].name if ref.file_id else o.assets_file.name).lower()
            key=(cab,ref.path_id)
            if key in result:continue
            slots=[]
            for ptr in r.m_Materials:
                if not ptr.path_id:continue
                slots.append(ptr)
            if slots:result[key]=slots
        return result

class GLB:
    def __init__(self):
        self.data=bytearray()
        self.j=dict(asset={'version':'2.0','generator':'Qingyun native model export'},scene=0,scenes=[{'nodes':[]}],nodes=[],meshes=[],skins=[],materials=[],images=[],textures=[],bufferViews=[],accessors=[],extensionsUsed=['KHR_materials_unlit'])
    def view(self,data):
        self.data.extend(bytes(-len(self.data)%4));view=len(self.j['bufferViews'])
        self.j['bufferViews'].append(dict(buffer=0,byteOffset=len(self.data),byteLength=len(data)))
        self.data.extend(data);return view
    def accessor(self,array,kind='SCALAR',bounds=False):
        a=np.ascontiguousarray(array)
        desc=dict(bufferView=self.view(a.tobytes()),componentType={'f':5126,'B':5121,'H':5123,'I':5125}[a.dtype.char],count=len(a),type=kind)
        if bounds:
            desc.update(min=np.atleast_1d(a.min(axis=0)).tolist(),max=np.atleast_1d(a.max(axis=0)).tolist())
        i=len(self.j['accessors']);self.j['accessors'].append(desc);return i
    def write(self,dst):
        self.data.extend(bytes(-len(self.data)%4));self.j['buffers']=[{'byteLength':len(self.data)}]
        raw=json.dumps(self.j,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode();raw+=b' '*(-len(raw)%4)
        dst.write_bytes(struct.pack('<4sIIII',b'glTF',2,len(raw)+len(self.data)+28,len(raw),0x4e4f534a)+raw+struct.pack('<II',len(self.data),0x004e4942)+self.data)

def xyz(v):return [v.x,v.y,v.z]
def trs(t):return xyz(t.m_LocalPosition)+xyz(t.m_LocalRotation)+[t.m_LocalRotation.w]+xyz(t.m_LocalScale)
def gltf_trs(t):
    t=np.array(t,np.float64);t[...,0]*=-1;t[...,4:6]*=-1
    q=t[...,3:7];q/=np.maximum(1e-10,np.linalg.norm(q,axis=-1,keepdims=True));return t
def matrices(values):
    p=values[...,:3];s=values[...,7:10]
    x,y,z,w=np.moveaxis(values[...,3:7],-1,0)
    m=np.zeros((*values.shape[:-1],4,4))
    for i,row in enumerate([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                             [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                             [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]):
        for j,v in enumerate(row):m[...,i,j]=v*s[...,j]
    m[...,:3,3]=p;m[...,3,3]=1
    return m

def export_model(db,spec,tracks=None):
    title,group,stem,prefab=spec;name=stem.rsplit('/',1)[1];source='artres/model/'+stem+'.ab'
    objs=db.load(source);cab=next(iter(objs.values())).assets_file.name.lower()
    transforms={o.path_id:o.read() for o in objs.values() if o.type.name=='Transform'}
    def depth(k):return 1+depth(transforms[k].m_Father.path_id) if transforms[k].m_Father.path_id else 0
    order=sorted(transforms,key=depth);idx={k:i for i,k in enumerate(order)}
    go_index={transforms[k].m_GameObject.path_id:idx[k] for k in order}
    rest=np.array([gltf_trs(trs(transforms[k])) for k in order]);paths=[];parents=[]
    g=GLB();j=g.j
    for i,k in enumerate(order):
        t=transforms[k];par=idx.get(t.m_Father.path_id,-1);parents.append(par);n=t.m_GameObject.read().m_Name.strip()
        paths.append('/'.join(filter(None,[paths[par],n])) if par>=0 else '')
        node=dict(name=n,translation=rest[i,:3].tolist(),rotation=rest[i,3:7].tolist(),scale=rest[i,7:].tolist())
        j['nodes'].append(node)
        if par<0:j['scenes'][0]['nodes'].append(i)
        else:j['nodes'][par].setdefault('children',[]).append(i)
    mats=[];mat_ids={};tex_ids={};total_v=total_t=0
    def material(owner,ptr):
        ref=db.resolve(owner,ptr)
        if not ref:raise ValueError('Unresolved material')
        key=(ref[0],ref[1].path_id)
        if key in mat_ids:return mat_ids[key]
        m=db.material(*key);mats.append(m)
        base=next((m['textures'][s]['asset'] for s in ('_BaseMap','_MainTex','_Texture') if s in m['textures'] and m['textures'][s]['asset']),None)
        if not base:raise ValueError('Missing base texture on '+m['name'])
        f=base['file']
        if f not in tex_ids:
            index=len(j['images']);tex_ids[f]=index
            encoded=(LIB/'Library'/f).read_bytes()
            if '/Hair_' in (m['shader'] or ''):
                # Hair RGB/A stores dye and highlight controls. Preserve the original
                # packed map in the library and flatten its source dye for glTF.
                from PIL import Image
                controls=np.asarray(Image.open(io.BytesIO(encoded)).convert('RGBA'))/255
                base_color=np.array(m['colors']['_BaseColor'][:3]);highlight=np.array(m['colors']['_HighLightColor'][:3])
                rgb=controls[:,:,:1]*base_color+controls[:,:,3:4]*highlight
                portable=io.BytesIO();Image.fromarray((np.clip(rgb,0,1)*255).astype('uint8')).save(portable,format='PNG')
                encoded=portable.getvalue()
            j['images'].append(dict(name=base['name'],mimeType='image/png',bufferView=g.view(encoded)))
            j['textures'].append(dict(source=index))
        mat_ids[key]=len(j['materials'])
        j['materials'].append(dict(name=m['name'],pbrMetallicRoughness=dict(baseColorTexture={'index':tex_ids[f]},metallicFactor=0,roughnessFactor=1),extensions={'KHR_materials_unlit':{}},doubleSided=True,alphaMode='MASK',alphaCutoff=float(m['floats'].get('_Cutoff',.45))))
        return mat_ids[key]
    binding=db.bindings(prefab)
    for o in objs.values():
        if o.type.name!='SkinnedMeshRenderer':continue
        r=o.read();mp=r.m_Mesh;mesh=mp.read();h=MeshHandler(mesh);h.process()
        slots=binding.get((cab,mp.path_id))
        if not slots:raise ValueError('Missing original prefab material slots: '+name+' '+mesh.m_Name)
        pos=np.array(h.m_Vertices,'<f4');pos[:,0]*=-1
        uv=np.array(h.m_UV0,'<f4');uv[:,1]=1-uv[:,1]
        normals=np.array(h.m_Normals,'<f4');normals[:,0]*=-1
        joints=np.array(h.m_BoneIndices,'uint16');weights=np.array(h.m_BoneWeights,'<f4')
        if h.m_BoneWeights is None and joints.shape[1]==1:weights=np.ones_like(joints,dtype='<f4')
        joints=np.pad(joints,((0,0),(0,4-joints.shape[1])));weights=np.pad(weights,((0,0),(0,4-weights.shape[1])))
        weights/=np.maximum(1e-8,weights.sum(axis=1,keepdims=True))
        aa=dict(POSITION=g.accessor(pos,'VEC3',True),NORMAL=g.accessor(normals,'VEC3'),TEXCOORD_0=g.accessor(uv,'VEC2'),JOINTS_0=g.accessor(joints,'VEC4'),WEIGHTS_0=g.accessor(weights,'VEC4'))
        prim=[]
        for n,ts in enumerate(h.get_triangles()):
            tri=np.array(ts,'uint16').reshape(-1,3)
            # Choose front winding from the exported normals after changing handedness.
            cross=np.cross(pos[tri[:,1]]-pos[tri[:,0]],pos[tri[:,2]]-pos[tri[:,0]])
            if (cross*normals[tri[:,0]]).sum()<0:tri=tri[:,[0,2,1]]
            prim.append(dict(attributes=aa,indices=g.accessor(tri.ravel()),material=material(prefab,slots[n])))
            total_t+=len(tri)
        total_v+=len(pos)
        node=j['nodes'][go_index[r.m_GameObject.path_id]]
        node['mesh']=len(j['meshes']);j['meshes'].append(dict(name=mesh.m_Name,primitives=prim))
        co=np.diag([-1,1,1,1])
        bind=co@np.array([[[getattr(b,f'e{x}{y}') for y in range(4)] for x in range(4)] for b in mesh.m_BindPose])@co
        bone_ids=[idx[b.path_id] for b in r.m_Bones]
        if not bone_ids:
            if 'avatar_joint_indices' not in locals():
                avatar=next(o.read_typetree() for o in objs.values() if o.type.name=='Avatar')
                dictionary=dict(avatar['m_TOS']);av=avatar['m_Avatar'];skeleton=av['m_AvatarSkeleton']['data']
                poses=av['m_DefaultPose']['data']['m_X'];avatar_joint_indices={};existing=dict(zip(paths,range(len(paths))))
                for h,hierarchy,pose in zip(skeleton['m_ID'],skeleton['m_Node'],poses):
                    path=dictionary[h]
                    if path in existing:
                        avatar_joint_indices[h]=existing[path];continue
                    p,q,scale=pose['t'],pose['q'],pose['s']
                    local=gltf_trs([p['x'],p['y'],p['z'],q['x'],q['y'],q['z'],q['w'],scale['x'],scale['y'],scale['z']])
                    idx_parent=avatar_joint_indices[skeleton['m_ID'][hierarchy['m_ParentId']]]
                    idx_new=len(j['nodes']);avatar_joint_indices[h]=idx_new
                    j['nodes'].append(dict(name=path.rsplit('/',1)[-1],translation=local[:3].tolist(),rotation=local[3:7].tolist(),scale=local[7:].tolist()))
                    j['nodes'][idx_parent].setdefault('children',[]).append(idx_new)
            bone_ids=[avatar_joint_indices[h] for h in mesh.m_BoneNameHashes]
        node['skin']=len(j['skins']);j['skins'].append(dict(joints=bone_ids,inverseBindMatrices=g.accessor(bind.transpose(0,2,1).reshape(-1,16).astype('<f4'),'MAT4')))
    if any(not skin['joints'] for skin in j['skins']):raise ValueError('Model has optimized skeleton; export from its avatar first: '+name)
    if total_v==0:raise ValueError('No skinned mesh '+name)
    clip_names=[]
    if tracks is True:
        from animation_curves import decode_clips
        tracks=decode_clips(db.load('resourcesab/animation/monster/mon_xiaodiao.ab'),paths,np.array([trs(transforms[k]) for k in order]))
    if tracks:
        j['animations']=[]
        for clip in tracks['clips']:
            n=clip['frames'];rate=clip['rate']; cname=clip['name'];clip_names.append(cname)
            values=np.broadcast_to(rest,(n,*rest.shape)).copy()
            for tr in clip['tracks']:values[:,paths.index(tr['path'])]=gltf_trs(np.array(tr['trs']).reshape(n,10))
            times=g.accessor((np.arange(n)/rate).astype('<f4'),bounds=True)
            animation=dict(name=cname,samplers=[],channels=[])
            for k in range(len(order)):
                for sl,kind,target in [(slice(0,3),'VEC3','translation'),(slice(3,7),'VEC4','rotation'),(slice(7,10),'VEC3','scale')]:
                    arr=values[:,k,sl].copy()
                    if target=='rotation':
                        for fi in range(1,n):
                            if np.dot(arr[fi-1],arr[fi])<0:arr[fi]*=-1
                    if np.allclose(arr,rest[k,sl],atol=1e-6):continue
                    sampler=len(animation['samplers'])
                    animation['samplers'].append(dict(input=times,output=g.accessor(arr.astype('<f4'),kind),interpolation='LINEAR'))
                    animation['channels'].append(dict(sampler=sampler,target=dict(node=k,path=target)))
            if cname.endswith(('@fly','@fly_idle01')):
                world=matrices(values)
                for i,par in enumerate(parents):
                    if par>=0:world[:,i]=world[:,par]@world[:,i]
                palette=world[:,bone_ids]@bind
                skinned=np.zeros((n,len(pos),3))
                for i in range(4):
                    m=palette[:,joints[:,i]]
                    skinned+=(np.einsum('fvij,vj->fvi',m[:,:,:3,:3],pos)+m[:,:,:3,3])*weights[:,i,None]
                if np.ptp(skinned,axis=0).max()<.02:raise ValueError('Skinned pose stayed still: '+cname)
                animation['extras']=dict(rate=rate,frames=n,skinRows=g.accessor(palette[:,:,:3,:].reshape(-1,4).astype('<f4'),'VEC4'),
                    minimum=skinned.min(axis=(0,1)).tolist(),maximum=skinned.max(axis=(0,1)).tolist())
            if animation['channels']:j['animations'].append(animation)
    out=LIB/'Library'/'Models'/name;out.mkdir(parents=True,exist_ok=True);g.write(out/(name+'.glb'))
    record=dict(id=name,title=title,group=group,source=source,prefab=prefab,vertices=total_v,triangles=total_t,skins=len(j['skins']),materials=mats,animations=clip_names)
    dump(out/'source.json',record);print(name,total_v,total_t,'materials',len(mats),'clips',len(clip_names),flush=True)
    return record

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,default=DEFAULT_PACKAGE);parser.add_argument('--only',nargs='*')
    args=parser.parse_args(); db=Library(args.package)
    records=[]
    for spec in SAMPLES:
        bird=spec[2].endswith('/mon_xiaodiao_001')
        if args.only and spec[2].split('/')[-1] not in args.only:continue
        records.append(export_model(db,spec,True if bird else None))
    if not args.only:dump(LIB/'Library'/'models.json',[{k:v for k,v in r.items() if k!='materials'} for r in records])


