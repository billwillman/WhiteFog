"""Export the world-duel HUD from the 1.3.0 APK UI prefabs for the Qingyun H5 runtime."""
import argparse
from pathlib import Path
from types import SimpleNamespace

from PIL import Image, ImageChops
from export_models import DEFAULT_PACKAGE, LIB, Library

WORLD = 'resourcesab/prefabs_ui/worldcombat.ab'
RESULT = 'resourcesab/prefabs_ui/result.ab'
OUTPUT = LIB.parents[1] / 'LoginDemo/battle-art/duel-ui'


def prefab_images(db, bundle, name):
    objects = db.load(bundle)
    assets = next(o.read() for o in objects.values() if o.type.name == 'AssetBundle')
    root = next(v.asset.path_id for k, v in assets.m_Container if k.endswith('/' + name.lower() + '.prefab'))

    def walk(go, path):
        components = [objects[c.component.path_id] for c in go.m_Component]
        rect = components[0].read()
        for comp in components[1:]:
            if comp.type.name != 'MonoBehaviour':
                continue
            data = comp.read_typetree()
            ref = data.get('m_Sprite')
            if ref and ref['m_PathID']:
                ptr = SimpleNamespace(file_id=ref['m_FileID'], path_id=ref['m_PathID'], assetsfile=comp.assets_file)
                sprite = db.resolve(bundle, ptr)[1].read()
                color = tuple(round(255 * data['m_Color'][k]) for k in 'rgba')
                yield path, (sprite, rect, color, data['m_Type'])
        for ptr in rect.m_Children:
            child = objects[objects[ptr.path_id].read().m_GameObject.path_id].read()
            if child.m_IsActive:
                yield from walk(child, path + '/' + child.m_Name)
    return dict(walk(objects[root].read(), ''))


def render(node, size=None):
    sprite, rect, color, kind = node
    image = sprite.image.convert('RGBA')
    if color != (255, 255, 255, 255):
        image = ImageChops.multiply(image, Image.new('RGBA', image.size, color))
    if size is None:
        size = tuple(round(abs(getattr(rect.m_SizeDelta, k) * getattr(rect.m_LocalScale, k))) for k in 'xy')
    border = sprite.m_Border
    # These sliced HUD sprites all use horizontal caps. Unity also retains the
    # center texel when trimmed atlas padding makes the two caps meet.
    if kind == 1 and (border.x or border.z):
        left = min(round(border.x), (image.width-1)//2)
        right = min(round(border.z), image.width-left-1)
        if border.x + border.z < image.width:
            left, right = round(border.x), round(border.z)
        out = Image.new('RGBA', size)
        sx = (0, left, image.width-right, image.width)
        dx = (0, left, size[0]-right, size[0])
        for i in range(3):
            width = dx[i+1]-dx[i]
            if width > 0 and sx[i+1] > sx[i]:
                strip = image.crop((sx[i], 0, sx[i+1], image.height))
                out.paste(strip.resize((width, size[1]), Image.Resampling.LANCZOS), (dx[i], 0))
        image = out
    else:
        image = image.resize(size, Image.Resampling.LANCZOS)
    return image.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if rect.m_LocalScale.x < 0 else image


def put(canvas, image, x, y):
    canvas.alpha_composite(image, (round(x-image.width/2), round(y-image.height/2)))


def save(name, image):
    image.save(OUTPUT / (name+'.webp'), lossless=True, method=6)


def victory_aura(db):
    # The mobile win prefab uses a UI mesh over the two Image layers. Bake its
    # vertex alpha, authored UVs and scrolling color-control texture once.
    from UnityPy.helpers.MeshHelper import MeshHandler
    import numpy as np

    objects=db.load(RESULT)
    root=next(o.read() for o in objects.values() if o.type.name=='AssetBundle')
    go_id=next(v.asset.path_id for k,v in root.m_Container if k.endswith('/resultimgwinworld_lod3.prefab'))
    root_rect=objects[objects[go_id].read().m_Component[0].component.path_id].read()
    children=[objects[objects[child.path_id].read().m_GameObject.path_id].read() for child in root_rect.m_Children]
    ring=next(child for child in children if child.m_Name=='kuosan_changzhu')
    components=[objects[c.component.path_id] for c in ring.m_Component]
    material,settings=next((o,a) for o in components if o.type.name=='MonoBehaviour'
                           for a in [o.read_typetree()] if '_ColorControlTex' in a)
    mesh_ref=next(o.read().m_Mesh for o in components if o.type.name=='MeshFilter')
    mesh=MeshHandler(db.resolve(RESULT,mesh_ref)[1].read())
    mesh.process()
    side=240
    xy=np.asarray(mesh.m_Vertices)[:,:2]
    xy=(xy/np.max(abs(xy))*[.5,-.5]+.5)*(side-1)
    attributes=np.column_stack((np.asarray(mesh.m_UV0)[:,:2],np.asarray(mesh.m_Colors)[:,3]))
    samples=np.zeros((side,side,3),dtype=np.float32)
    for a,b,c in mesh.get_triangles()[0]:
        points=xy[[a,b,c]]
        left,top=np.maximum(0,np.floor(points.min(axis=0)).astype(int))
        right,bottom=np.minimum(side-1,np.ceil(points.max(axis=0)).astype(int))
        y,x=np.mgrid[top:bottom+1,left:right+1]
        axes=(points[1:]-points[0]).T
        if abs(np.linalg.det(axes))<1e-7:
            continue
        weights=(np.stack((x+.5,y+.5),axis=-1)-points[0])@np.linalg.inv(axes).T
        bary=np.dstack((1-weights.sum(axis=2),weights))
        inside=bary.min(axis=2)>=0
        samples[top:bottom+1,left:right+1][inside]=bary[inside]@attributes[[a,b,c]]
    u,v,alpha=samples.transpose(2,0,1)

    def texture(key):
        ref=settings[key]
        ptr=SimpleNamespace(file_id=ref['m_FileID'],path_id=ref['m_PathID'],assetsfile=material.assets_file)
        return np.asarray(db.resolve(RESULT,ptr)[1].read().image.convert('RGBA'),dtype=np.float32)/255

    def sample(tex,u,v):
        return tex[((1-v)*tex.shape[0]).astype(int)%tex.shape[0],(u*tex.shape[1]).astype(int)%tex.shape[1]]

    main=sample(texture('_MainTex'),u,v)
    mask=sample(texture('_Mask'),1-v,u)
    control=texture('_ColorControlTex')
    cold,hot=(np.array([settings[key][ch] for ch in 'rgb']) for key in ('_Color','_Color1'))
    channels = np.asarray([settings['_MaskRGBA'][k] for k in 'xyzw'])
    opacity = alpha * settings['_Color']['a'] * (mask @ channels) * main[:, :, 3]
    frames=[]
    scroll=settings['_ColorControlTexVscroll']
    for frame in range(30):
        t=frame/(30*abs(scroll))
        ramp=sample(control,u,v+t*scroll)[:,:,0,None]
        rgb=(cold*(1-ramp)+hot*ramp)*main[:,:,:3]
        rgba=np.dstack((np.clip(rgb,0,1),np.clip(opacity,0,1)))
        frames.append(Image.fromarray(np.uint8(255*rgba)))
    frames[0].save(OUTPUT/'win-aura.webp',save_all=True,append_images=frames[1:],duration=round(1000/(len(frames)*abs(scroll))),loop=0,quality=85,alpha_quality=92)


def extract(package):
    db = Library(package)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    player = prefab_images(db, WORLD, 'WorldCombatPlayerBarHead')
    boss = prefab_images(db, WORLD, 'WorldCombatBossBarHead')
    panel = '/PanelBoss/PanelBossName'
    save('player-bar', render(player['/BarImg/ImgBg'], (135,32)))
    save('player-fill', render(player['/BarImg/ProgressBarHp']))
    save('delay-fill', render(player['/BarImg/ProgressBarHpDelay']))
    save('boss-fill', render(boss[panel+'/BarImg/ProgressBarHp'], (280,20)))
    plate = Image.new('RGBA', (404,96))
    for path in ['/ImgBg','/BarImg/ImgBg','/ImgIconBg','/ImgIconBg/ImgIcon']:
        node = boss[panel+path]
        # The icon is parented to ImgIconBg. BarImg has a left-hand pivot.
        rect = node[1]
        if path == '/BarImg/ImgBg':
            image = render(node, (284,24))
            x,y = -124.3+142,-2.2
        else:
            image = render(node)
            x,y = rect.m_AnchoredPosition.x,rect.m_AnchoredPosition.y
            if path.endswith('/ImgIcon'):
                parent = boss[panel+'/ImgIconBg'][1].m_AnchoredPosition
                x,y = x+parent.x,y+parent.y
        put(plate,image,202+x,65-y)
    save('boss-bar',plate)
    for name,asset in [('win','ResultImgWinWorld_LOD3'),('lose','ResultImgLoseWorld')]:
        nodes = prefab_images(db,RESULT,asset)
        title = Image.new('RGBA',(1080,1000))
        for node in nodes.values():
            pos = node[1].m_AnchoredPosition
            put(title, render(node), 540+pos.x, 500-pos.y)
        save(name,title)
    victory_aura(db)
    tip = prefab_images(db,WORLD,'WorldCombatSkillTipNode')
    save('skill-tip',render(tip['/ImgBg']))
    hurt = prefab_images(db,RESULT,'ResultPanelHurtOnly')
    info = '/HurtView/Viewport/Content/PanelHurt1/PanelInfo/'
    icon = render(hurt[info+'ImgBg'])
    put(icon, render(hurt[info+'ImgHurt']), icon.width/2,icon.height/2)
    save('damage-icon',icon)
    for key,path,size in [('divider','/PanelTitle/ImgLineLeft',(377,22)),
                          ('damage-track',info+'ProgressBar/ImgBg',(442,24)),
                          ('damage-fill',info+'ProgressBar/ImgFill',(438,20)),
                          ('details-arrow',info+'BtnArrow/ImgArrow',(58,58))]:
        save(key,render(hurt[path],size))
    loss = prefab_images(db,RESULT,'ResultPanelImproveNone')
    save('lose-advice',render(loss['/ImgNone']))
    back = next(o.read() for o in db.load('atlas/com.ab').values()
                if o.type.name=='Sprite' and o.peek_name()=='com_btn_back_01')
    save('back',back.image)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path,default=DEFAULT_PACKAGE)
    extract(parser.parse_args().package)
