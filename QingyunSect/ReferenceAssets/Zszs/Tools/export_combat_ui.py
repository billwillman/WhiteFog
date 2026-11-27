"""Extract the actual world-combat slot sprites and the selected shield prefab."""
from export_models import Library,DEFAULT_PACKAGE,LIB,dump,normalized
from pathlib import Path
import argparse
from collections import defaultdict

def extract(project):
    db=Library(DEFAULT_PACKAGE);dst=LIB.parents[1]/'LoginDemo/battle-art';dst.mkdir(parents=True,exist_ok=True)
    sources={}
    for name,bundle in {
        'worldcombat_skill_state_bg01':'atlas/worldcombat.ab','worldcombat_bg_skill_group':'atlas/worldcombat.ab',
        'worldcombat_btn_switch':'atlas/worldcombat.ab','worldcombat_btn_combat':'atlas/worldcombat.ab',
        '22020':'resourcesab/ui_new/uiimage/icon/skill.ab','22030':'resourcesab/ui_new/uiimage/icon/skill.ab','quality_skill_new_02':'resourcesab/ui_new/uiimage/icon/quality/idx2.ab',
        '21020':'resourcesab/ui_new/uiimage/icon/skill.ab','25090':'resourcesab/ui_new/uiimage/icon/skill.ab','25120':'resourcesab/ui_new/uiimage/icon/skill.ab',
        'quality_skill_new_01':'resourcesab/ui_new/uiimage/icon/quality/idx1.ab','quality_skill_new_05':'resourcesab/ui_new/uiimage/icon/quality/idx5.ab'
    }.items():
        obj=next(o for o in db.load(bundle).values() if o.type.name=='Sprite' and o.peek_name()==name)
        im=obj.read().image;im.save(dst/(name+'.png'));sources[name]=dict(bundle=bundle,pathId=str(obj.path_id),size=im.size)
    dump(dst/'interface-source.json',dict(sprites=sources,prefab='resourcesab/prefabs_ui/worldcombat.ab',node='WorldCombatSkillNodeNew',slots=6,cell=[134,158],spacing=35,iconNode='SkillItem/SkillImg',cooldownNode='SkillItem/TxtTime'))
    src='resourcesab/prefabs/effect/buff.ab';items=db.load(src);ab=next(o.read() for o in items.values() if o.type.name=='AssetBundle')
    children=defaultdict(list);trans={};components=defaultdict(list)
    name='vfx_cha_buff_xinfa_hudun';root=next(v.asset.path_id for k,v in ab.m_Container if k.endswith('/'+name+'.prefab'))
    for o in items.values():
        if o.type.name=='Transform':
            t=o.read();trans[o.path_id]=t.m_GameObject.path_id;children[t.m_Father.path_id].append(o.path_id)
        elif o.type.name in ('ParticleSystemRenderer','MeshRenderer','TrailRenderer'):components[o.read().m_GameObject.path_id].append(o.read())
    def walk(x):
        yield trans[x]
        for child in children[x]:yield from walk(child)
    renderers=[];materials={};bundles={src}
    for go in walk(next(k for k,v in trans.items() if v==root)):
        for renderer in components[go]:
            ids=[]
            for ptr in renderer.m_Materials:
                ref=db.resolve(src,ptr)
                if ref:
                    bundle,object_=ref;uid=str(object_.path_id);materials[uid]=db.material(bundle,object_.path_id);ids.append(uid);bundles.add(bundle)
            renderers.append(dict(node=items[go].peek_name(),materials=ids))
    for dep in db.items[src]['depAbs']:
        if 'vfx_mesh' in dep:bundles.add(dep)
    for dep in bundles:
        path=project/'fx-bundles'/dep;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(normalized(db.root/dep))
    dump(LIB/'Library/Effects/hudun/source.json',dict(id='hudun',title='护盾',source=src,prefabs=[dict(name=name,renderers=renderers)],materials=list(materials.values())))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('project',type=Path);extract(parser.parse_args().project)

