"""Archive the original model, material and animation files used by the collection."""
from export_models import LIB,DEFAULT_PACKAGE,Library,dump
import shutil,hashlib,json
from pathlib import Path

ANIMATIONS={
    'mon_xiaodiao_001':['monster/mon_xiaodiao'],
    'jy_shendiao_001':['monster/jy_shendiao_001'],
    'mon_yegou001':['monster/mon_yegou001'],
    'mon_jinlianyt_001':['monster/mon_jinlianyt_001'],
    'jy_xianbaihu_001':['monster/jy_xianbaihu_001','monster/jy_xianbaihu_001/boss_xianbaihu_001'],
    'chr_male_000':['character/chr_male_001/common/chr_male_000_lod1@qinggong_end_map5.anim'],
    'chr_male_001':['character/chr_male_001/jian','character/chr_male_001/common/chr_male_001@attack_front_yj.anim'],
}
def archive():
    db=Library(DEFAULT_PACKAGE);root=LIB/'Library';checksums=root/'Bundles/checksums.json'
    hashes=json.loads(checksums.read_text('utf8'))
    for file in sorted((root/'Models').glob('*/source.json')):
        model=json.loads(file.read_text('utf8'));names={model['source'],model['prefab']};clips=[]
        for m in model['materials']:
            names.update((m['bundle'],m['shaderBundle']))
            for t in m['textures'].values():
                if t['asset']:names.add(t['asset']['bundle'])
        for path in ANIMATIONS.get(model['id'],[]):
            bundle='resourcesab/animation/'+path+'.ab';names.add(bundle)
            clips.extend(dict(name=o.peek_name(),bundle=bundle) for o in db.load(bundle).values() if o.type.name=='AnimationClip')
        for name in sorted(names):
            if name in hashes:continue
            p=root/'Bundles'/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(db.root/name,p)
            hashes[name]=hashlib.sha256(p.read_bytes()).hexdigest()
        model['archivedBundles']=sorted(names);model['sourceClips']=clips;dump(file,model)
        print(model['id'],len(clips),'archived animation clips')
    dump(checksums,hashes)
if __name__=='__main__':archive()


