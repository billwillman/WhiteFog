from pathlib import Path
import argparse,json
from export_models import LIB
def source_map(out):
    entries=[]
    for file in (LIB/'Library/Effects').glob('*/source.json'):
        info=json.loads(file.read_text('utf8'));materials={m['pathID']:m for m in info['materials']}
        for prefab in info['prefabs']:
            for renderer in prefab['renderers']:
                for index,mat in enumerate(renderer['materials']):
                    m=materials[mat];textures=m['textures']
                    def slot(key):
                        t=textures.get(key,{})
                        return str(LIB/'Library'/t['asset']['file']) if t.get('asset') else ''
                    main=next((slot(k) for k in ('_Texture','_MainTex','_BaseMap') if slot(k)),'')
                    usable=bool(main)
                    entries.append(dict(key=prefab['name']+'/'+renderer['node']+'/'+str(index),main=main,mask=slot('_Mask') if '_USE_MASK_ON' in m['keywords'] else '',ramp=slot('_ColorControlTex') if '_USE_COLORCONTROL' in m['keywords'] else '',cut=slot('_DissolveTex') if '_USE_CUTOUT' in m['keywords'] else '',threshold=m['floats'].get('_Dissolve',0),tint=m['colors'].get('_Color',[1,1,1,1]),second=m['colors'].get('_Color1',m['colors'].get('_Color',[1,1,1,1])),maskRGBA=m['colors'].get('_MaskRGBA',[0,0,0,1]),blend=m['floats'].get('_FxDstBlend',10),uv=textures.get('_Texture',{}).get('scale',[1,1])+textures.get('_Texture',{}).get('offset',[0,0]),usable=usable))
    out.write_text(json.dumps({'entries':entries}),encoding='utf8')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('output',type=Path)
    source_map(parser.parse_args().output)

