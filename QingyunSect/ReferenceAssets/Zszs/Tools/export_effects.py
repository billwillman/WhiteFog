"""Collect complete source VFX bundles, individual prefab stages and referenced materials."""
from export_models import Library, DEFAULT_PACKAGE, LIB, dump, normalized
from pathlib import Path
from collections import defaultdict
import argparse, hashlib, shutil, gzip, json

GROUPS={'pugongjian':'剑普攻','pokongjianfa':'破空剑法','liuxingjianyu':'流星剑雨'}
def collect(db,temp):
    root=LIB/'Library';effects=[];packages={}
    for code,title in GROUPS.items():
        source='resourcesab/prefabs/effect/character/'+code+'.ab'
        for dep in db.dependencies(source):
            if dep in packages:continue
            dest=root/'Bundles'/dep;dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(db.root/dep,dest)
            packages[dep]=hashlib.sha256(dest.read_bytes()).hexdigest()
            if temp:
                path=temp/dep;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(normalized(dest))
        objects=db.load(source);bundle=next(o.read() for o in objects.values() if o.type.name=='AssetBundle')
        components=defaultdict(list);children=defaultdict(list);transforms={}
        for o in objects.values():
            if o.type.name not in ('Transform','ParticleSystem','ParticleSystemRenderer','MeshRenderer','TrailRenderer','LineRenderer','Animator','Animation','MeshFilter'):continue
            if o.type.name=='Transform':
                t=o.read();transforms[o.path_id]=t.m_GameObject.path_id;children[t.m_Father.path_id].append(o.path_id)
            else:
                t=o.read_typetree();components[t['m_GameObject']['m_PathID']].append((o,t))
        roots={v:k for k,v in transforms.items()};mats={};prefabs=[];archive={}
        def visit(k):
            yield transforms[k]
            for c in children[k]:yield from visit(c)
        for path,entry in bundle.m_Container:
            if not path.endswith('.prefab') or entry.asset.path_id not in roots:continue
            layers=[];renderers=[];extra=[]
            for go in visit(roots[entry.asset.path_id]):
                name=objects[go].peek_name()
                for obj,t in components[go]:
                    kind=obj.type.name
                    if kind=='ParticleSystem':
                        layers.append(dict(node=name,id=str(obj.path_id),duration=t['lengthInSec'],loop=t['looping'],modules=[k for k,v in t.items() if isinstance(v,dict) and v.get('enabled')]))
                        archive[str(obj.path_id)]=t
                    elif kind.endswith('Renderer'):
                        ids=[]
                        for ptr in obj.read().m_Materials:
                            res=db.resolve(source,ptr)
                            if res:
                                b,o=res;s=str(o.path_id);mats[s]=db.material(b,o.path_id);ids.append(s)
                        renderers.append(dict(node=name,type=kind,mode=t.get('m_RenderMode'),materials=ids))
                    else:extra.append(dict(node=name,type=kind))
            prefabs.append(dict(name=path.split('/')[-1][:-7],path=path,layers=layers,renderers=renderers,components=extra))
        out=root/'Effects'/code;out.mkdir(parents=True,exist_ok=True)
        with gzip.open(out/'particle-modules.json.gz','wt',encoding='utf8') as f:json.dump(archive,f,separators=(',',':'))
        record=dict(id=code,title=title,source=source,prefabs=prefabs,materials=list(mats.values()),dependencies=db.dependencies(source))
        dump(out/'source.json',record);effects.append(dict(id=code,title=title,prefabs=len(prefabs),layers=len(archive),materials=len(mats)))
        print(code,effects[-1],flush=True)
    dump(root/'effects.json',effects);dump(root/'Bundles/checksums.json',packages)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,default=DEFAULT_PACKAGE);parser.add_argument('--normalized-dir',type=Path)
    args=parser.parse_args();collect(Library(args.package),args.normalized_dir)

