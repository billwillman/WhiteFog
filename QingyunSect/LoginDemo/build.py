"""Bake the portable portrait preview, including the original cloud mesh and GLSL."""
import base64
import io
import json
import math
import random
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter
from unity_fog import shader_parts
import cv2
import numpy as np

HERE = Path(__file__).resolve().parent
CLOUD = HERE.parents[1] / 'Assets/GoldenCloudVortex/WeChatMiniGame'
ART = CLOUD / 'art'
WIDTH, HEIGHT = 1448, 1086  # rig authoring coordinates, in the source sprite


def uri(mime, data):
    return 'data:' + mime + ';base64,' + base64.b64encode(data).decode('ascii')


def battle_art():
    assets_root = HERE.parents[1] / 'Assets'
    noise_maps = []
    for index in (1, 2):
        source = assets_root / f'FogOfWar/Mobile/NoiseClouds{index}_1024.asset'
        pixels = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-fA-F]+)', source.read_text())[1])
        field = np.frombuffer(pixels[:1024**2], np.uint8).reshape(1024, 1024)
        noise_maps.append(field)
    # Opaque RGB preserves both original R8 channels when browsers decode the PNG.
    image = Image.fromarray(np.stack([*noise_maps,np.zeros_like(noise_maps[0])],axis=-1))
    packed = io.BytesIO()
    image.save(packed, format='PNG')
    library=HERE.parent/'ReferenceAssets/Zszs/Library'
    bird=library/'Models/mon_xiaodiao_001/mon_xiaodiao_001.glb'
    effects={}
    for prefix, folder, names in [
        ('', 'pugongjian', ['eff_cha_jianpugong_shifa01','eff_cha_jianpugong_trail01','eff_cha_jianpugong_hit']),
        ('pierce', 'pokongjianfa', ['vfx_pokongjianfa_shifa','vfx_pokongjianfa_trail','vfx_pokongjianfa_hit']),
        ('rain', 'liuxingjianyu', ['vfx_liuxingjianyu_shifa','vfx_liuxingjianyu_trail','vfx_liuxingjianyu_hit'])]:
        fx_root=library/'Effects'/folder
        for stage,name in zip(('cast','trail','hit'),names):
            info=json.loads((fx_root/(name+'.json')).read_text('utf8'))
            effects[prefix+stage]=dict(info,image=uri('image/webp',(fx_root/(name+'.webp')).read_bytes()))
    ward_root=library/'Effects/hudun'
    ward_name='vfx_cha_buff_xinfa_hudun'
    effects['ward']=dict(json.loads((ward_root/(ward_name+'.json')).read_text('utf8')),image=uri('image/webp',(ward_root/(ward_name+'.webp')).read_bytes()))
    ui_dir=HERE/'battle-art'
    sprites=json.loads((ui_dir/'interface-source.json').read_text('utf8'))['sprites']
    ui={key:uri('image/png',(ui_dir/(key+'.png')).read_bytes()) for key in sprites}
    enemy = None
    atlas_path = HERE/'battle-enemy.json'
    if atlas_path.is_file():
        atlas=json.loads(atlas_path.read_text(encoding='utf-8-sig'))
        image_file=HERE/atlas['image']
        with Image.open(image_file) as sprite:
            if sprite.size!=(atlas['width'],atlas['height']):
                raise ValueError('Impostor atlas dimensions do not match the image')
            enemy=dict(atlas=atlas,image=uri(Image.MIME[sprite.format],image_file.read_bytes()))
    return dict(parts=json.loads((HERE/'entry-role.json').read_text(encoding='utf-8')),
                noise=uri('image/png',packed.getvalue()),
                edgeClouds=uri('image/webp',(HERE/'edge-clouds.webp').read_bytes()),
                cap=uri('image/png',(assets_root/'CloudFlight/WeChatMiniGame/art/CloudMatCap.png').read_bytes()),
                fog=shader_parts(assets_root/'FogOfWar'),enemy=enemy,enemyModel=uri('model/gltf-binary',bird.read_bytes()),effects=effects,ui=ui,
                swordPose=json.loads((ui_dir/'rider-sword.json').read_text('utf8')),
                still=uri('image/webp',(HERE/'entry-still.webp').read_bytes()) if (HERE/'entry-still.webp').exists() else '')


def smooth(lo, hi, value):
    t = max(0, min(1, (value - lo) / (hi - lo)))
    return t * t * (3 - 2 * t)


def river_frames(plate):
    # Tangent and channel half-width in pixels on the original painted map.
    channel = [(923,672,14),(951,690,21),(874,722,22),(965,755,37),
               (1035,790,53),(1003,826,74),(875,869,65),(746,910,57),
               (623,943,52),(505,977,42),(415,1010,42),(371,1048,50),
               (377,1070,50),(431,1090,46),(551,1115,49),(685,1144,53),
               (803,1177,57),(925,1220,56),(985,1270,59),(988,1320,66),
               (938,1360,65),(837,1394,54),(717,1422,47),(657,1460,40),
               (622,1494,39),(535,1528,35),(430,1553,31),(367,1591,25),(306,1628,16)]
    x, y, width, height = box = (252, 628, 828, 1044)
    w, h = 276, 348
    pixels = cv2.resize(plate[y:y+height, x:x+width], (w,h), interpolation=cv2.INTER_AREA)
    red, green, blue = pixels.astype(np.float32).transpose(2,0,1)
    pigment = np.clip((green-red-3)/18,0,1) * np.clip((blue-red+1)/14,0,1)
    gy,gx = np.indices((h,w), dtype=np.float32)
    px, py = gx*3+x+1, gy*3+y+1
    best = np.full((h,w), 1e8, np.float32)
    vx,vy,shore = np.zeros_like(best),np.zeros_like(best),np.zeros_like(best)
    for (ax,ay,ar),(bx,by,br) in zip(channel,channel[1:]):
        dx,dy = bx-ax,by-ay
        length = math.hypot(dx,dy)
        u = np.clip(((px-ax)*dx+(py-ay)*dy)/length**2,0,1)
        dist = np.hypot(px-ax-u*dx,py-ay-u*dy)
        closer = dist < best
        vx[closer],vy[closer] = dx/length,dy/length
        shore[closer] = np.clip((ar+(br-ar)*u[closer]-dist[closer])/13,0,1)
        best = np.minimum(best,dist)
    mask = shore*pigment
    vx = cv2.GaussianBlur(vx,(0,0),2)
    vy = cv2.GaussianBlur(vy,(0,0),2)
    # Advection of the original brushwork. The two offset phases crossfade
    # when either sample resets, so the last cell leads into the first.
    tone = cv2.cvtColor(pixels,cv2.COLOR_RGB2GRAY).astype(np.float32)
    frames = 24
    sheet = Image.new('RGBA',(w*6,h*(frames//6)))
    for i in range(frames):
        a = i/frames
        b = (a+.5)%1
        weight = math.sin(math.pi*a)**2
        def transport(phase):
            distance = (phase-.5)*11.
            return cv2.remap(tone,gx-vx*distance,gy-vy*distance,
                             cv2.INTER_LINEAR,borderMode=cv2.BORDER_REPLICATE)
        delta = (weight*transport(a) + (1-weight)*transport(b) - tone)*mask
        rgba = np.empty((h,w,4),np.uint8)
        rgba[:,:,:3] = np.where((delta>0)[:,:,None], [248,250,225], [24,65,70])
        rgba[:,:,3] = np.uint8(np.minimum(abs(delta)*2.65,155))
        sheet.paste(Image.fromarray(rgba),((i%6)*w,(i//6)*h))
    out = io.BytesIO()
    sheet.save(out,'WEBP',quality=77,alpha_quality=90,method=4)
    return dict(src=uri('image/webp',out.getvalue()),box=box,width=w,height=h,
                frames=frames,columns=6)


def story_art():
    def crop_uri(image):
        encoded = io.BytesIO()
        image.save(encoded, 'WEBP', quality=90, method=6)
        return uri('image/webp', encoded.getvalue())

    parts = {}
    with Image.open(HERE.parent/'world-map-region-v2.png') as atlas:
        atlas = atlas.convert('RGB')
        plate = np.array(atlas)
        for name, points, pivot in [
            ('bell', [(704,982),(708,982),(709,988),(714,990),(717,994),(717,1005),(720,1008),(718,1012),(709,1013),(707,1016),(704,1014),(695,1012),(694,1009),(697,1006),(697,995),(700,990),(704,988)], (706,982)),
            ('boat', [(668,1114),(674,1116),(700,1121),(701,1106),(704,1099),(712,1096),(725,1094),(762,1096),(769,1100),(774,1108),(774,1119),(785,1121),(785,1109),(779,1108),(779,1082),(776,1078),(780,1078),(780,1071),(782,1071),(783,1078),(793,1076),(795,1073),(796,1075),(793,1079),(793,1109),(789,1110),(789,1121),(803,1121),(809,1118),(810,1122),(801,1131),(790,1137),(780,1141),(759,1143),(723,1143),(701,1140),(682,1132)], (801,1130)),
            ('sailboat', [(966,865),(979,866),(979,858),(984,854),(996,854),(997,853),(996,824),(995,824),(995,822),(1008,819),(1009,811),(1011,811),(1011,819),(1023,816),(1025,815),(1024,819),(1023,820),(1022,847),(1025,852),(1012,853),(1011,858),(1023,855),(1033,850),(1036,849),(1035,852),(1030,859),(1023,865),(1010,868),(989,872),(978,872),(972,869)], (1000,866)),
            ('bridge-flag', [(371,727),(377,727),(381,729),(381,752),(378,753),(372,757),(370,757),(371,747)], (377,728)),
        ]:
            mask=Image.new('L',atlas.size)
            ImageDraw.Draw(mask).polygon(points,fill=255)
            mask=mask.filter(ImageFilter.GaussianBlur(.55))
            if name=='bridge-flag':
                # The upright is only a few source pixels wide at this scale.
                mask.paste(0,(381,0,atlas.width,atlas.height))
            box=mask.getbbox()
            art=atlas.crop(box).convert('RGBA')
            art.putalpha(mask.crop(box))
            parts[name]=dict(src=crop_uri(art),box=[box[0],box[1]-150,art.width,art.height],
                             pivot=[(pivot[0]-box[0])/art.width,(pivot[1]-box[1])/art.height])
            plate=cv2.inpaint(plate,np.array(mask),4,cv2.INPAINT_TELEA)
        # Restore the water at each original berth. Reflections are redrawn at
        # the moving hulls; a stationary dark hull on the river spoils the sail.
        for polygon,offset in [
            ([(963,870),(978,870),(1030,860),(1035,863),(1022,876),(985,887),(965,884)],(0,22)),
            ([(672,1142),(689,1137),(729,1143),(799,1137),(809,1144),(790,1159),(750,1175),(711,1167),(677,1156)],(-45,22)),
        ]:
            alpha=Image.new('L',atlas.size)
            ImageDraw.Draw(alpha).polygon(polygon,fill=255)
            weight=np.asarray(alpha.filter(ImageFilter.GaussianBlur(2.2)),dtype=np.float32)[:,:,None]/255
            donor=np.roll(plate,(-offset[1],-offset[0]),axis=(0,1))
            plate=np.uint8(plate*(1-weight)+donor*weight)
        rope=np.zeros(plate.shape[:2],np.uint8)
        cv2.polylines(rope,[np.array([(648,1135),(657,1130),(670,1119)])],False,255,4)
        plate=cv2.inpaint(plate,rope,3,cv2.INPAINT_TELEA)
        # The riverside pine overlaps the sailing lane. Preserve its painted
        # branches over the travelling sail as it emerges from behind the tree.
        tree_box=(813,846,965,948)
        tree=atlas.crop(tree_box).convert('RGBA')
        tr,tg,tb=np.asarray(tree,dtype=np.float32)[:,:,:3].transpose(2,0,1)
        foliage=255*np.clip((tg-tb-9)/13,0,1)*np.clip((231-tg)/30,0,1)
        tree.putalpha(Image.fromarray(np.uint8(foliage)))
        parts['river-pine']=dict(src=crop_uri(tree),box=[813,696,tree.width,tree.height],pivot=[0,0])
        region = crop_uri(Image.fromarray(plate).crop((0,150,1080,1797)))
        current = river_frames(plate)
        sample=np.asarray(Image.fromarray(plate).crop((0,150,1080,1797)).resize((270,412))).astype(float)
        red,green,blue=sample.transpose(2,0,1)
        alpha=np.uint8(255*np.clip((green-red-2)/18,0,1)*np.clip((blue-red+3)/12,0,1))
        water=Image.new('RGBA',(270,412),'white')
        water.putalpha(Image.fromarray(alpha).filter(ImageFilter.GaussianBlur(.5)))
        mask=io.BytesIO()
        water.save(mask,format='PNG',optimize=True)
        face = crop_uri(atlas.crop((11,6,158,153)).resize((128,128), Image.Resampling.LANCZOS))
    with Image.open(HERE.parent/'world-map-concept-v1.png') as city:
        court=crop_uri(city.crop((433,715,1043,1798)))
    # Bake the cover front, river-first reveal and painted mist into three channels.
    width, height = 126, 224
    rng = random.Random(11)
    layers = []
    for n, gain in [(4,.47),(9,.25),(22,.12),(50,.048)]:
        noise = Image.frombytes('L', (n,n*2), bytes(rng.randrange(256) for _ in range(n*n*2)))
        layers.append((noise.resize((width,height), Image.Resampling.BICUBIC).tobytes(), gain))
    with Image.open(HERE/'portrait-clouds.jpg') as clouds:
        w,h = clouds.size
        tone = clouds.crop((int(.29*w),int(.20*h),int(.67*w),int(.58*h))).convert('L').resize((width,height)).tobytes()
    packed = bytearray()
    for i,gray in enumerate(tone):
        x,y = i%width/(width-1), i//width/(height-1)
        grain = sum((texture[i]/128-1)*gain for texture,gain in layers)
        cover = (1-x)*.44 + (1-y)*.53 + .16*grain
        reveal = math.hypot((x-.62)*.68,y-.48) + .09*grain + (.13 if y<.08 or y>.937 else 0)
        packed.extend((round(max(0,min(1,(cover+.2)/1.4))*255),
                       round(max(0,min(1,(reveal+.12)/1.2))*255),gray))
    portrait_sheet=Image.new('RGBA',(1536,1024))
    for column, filename in enumerate(('dialogue-portraits.webp','ferry-expressions.webp')):
        with Image.open(HERE/filename) as half:
            portrait_sheet.paste(half,(column*768,0))
    return dict(region=region,face=face,court=court,parts=parts,current=current,water=uri('image/png',mask.getvalue()),
                ferry=uri('image/webp',(HERE/'old-ferry.webp').read_bytes()),portraits=crop_uri(portrait_sheet),
                mist=base64.b64encode(packed).decode(), mistWidth=width, mistHeight=height)


def skeleton():
    scale = Image.open(HERE / 'rider.png').width / WIDTH
    bones, pivots, names, timelines = [], [], {}, {}

    def bone(name, parent, x, y):
        idx = len(bones)
        px, py = pivots[names[parent]] if parent else (850, 710)
        item = dict(name=name, x=round((x-px)*scale, 3), y=round((py-y)*scale, 3))
        if parent:
            item['parent'] = parent
        bones.append(item)
        names[name] = idx
        pivots.append((x, y))
        return idx

    def keys(amplitude, cycles, phase, key='angle', flutter=0):
        frames = []
        for i in range(65):
            t = 2*math.pi*i/64
            swing = math.sin(cycles*t+phase)
            ripple = flutter*math.sin((2*cycles+1)*t+phase*1.4)
            frames.append(dict(time=i/8, **{key: round(amplitude*(swing+ripple), 3)}))
        return frames

    root = bone('flight', None, 850, 710)
    timelines['flight'] = dict(rotate=keys(1.35, 2, .1),
                               translate=keys(17*scale, 2, 1.0, 'y'))
    torso = bone('rider', 'flight', 850, 710)
    timelines['rider'] = dict(rotate=keys(.85, 2, 1.2))
    chains = {}
    for name, parent, points, amplitudes, cycles, phase in [
        ('hair', 'rider', [(816,143),(718,196),(612,215)], [8,13,18], 3, .3),
        ('headband', 'rider', [(815,76),(756,108),(690,109)], [13,21,28], 4, 1.4),
        ('robe', 'rider', [(835,359),(620,470),(425,488)], [2.9,4.7,5.4], 2, 2.1),
        ('sword-streamer', 'flight', [(453,868),(385,920),(230,945)], [5.4,8.2,11.2], 3, 2.4),
    ]:
        ids = []
        for k, ((x,y), amp) in enumerate(zip(points, amplitudes)):
            joint = name + '-' + str(k+1)
            ids.append(bone(joint, parent, x, y))
            flutter = (.09*k if name in ('hair', 'headband') else 0)
            timelines[joint] = dict(rotate=keys(amp, cycles, phase-.82*k, flutter=flutter))
            parent = joint
        chains[name] = (points, ids)

    def weights(x,y):
        # The chain envelopes follow the contours of this particular painted attachment.
        blade = 906-.243*x
        # Pin the support leg and both boots to the same transform as the blade.
        # Feather into the hip and the free cloth to the left of the calf.
        planted = smooth(500,577,y)*smooth(635,710,x)
        body = smooth(15,110,blade-y)*(1-planted)
        base = {root:1-body,torso:body}
        strand = None
        if y < 310 and x < 824:
            band_height = 80 + max(0, 815-x)*.22
            strand = 'headband' if y < band_height+23 else 'hair'
        elif y > 844 and x < 480:
            strand = 'sword-streamer'
        elif 318 < y < 696 and x < 835:
            strand = 'robe'
        if strand:
            points, ids = chains[strand]
            influence = smooth(0, 65, points[0][0]-x)
            if strand == 'sword-streamer':
                influence *= smooth(845,897,y)
            if strand == 'robe':
                influence *= smooth(318,370,y)*(1-smooth(650,696,y))*(1-planted)
            mix1 = smooth(points[0][0]-points[1][0]-40, points[0][0]-points[1][0]+40, points[0][0]-x)
            mix2 = smooth(points[0][0]-points[2][0]-45, points[0][0]-points[2][0]+45, points[0][0]-x)
            base = {i:w*(1-influence) for i,w in base.items()}
            for i,w in zip(ids,(1-mix1,mix1*(1-mix2),mix1*mix2)):
                base[i] = w*influence
        return [(i,w) for i,w in base.items() if w > .00001]

    cols, rows = 42, 38
    corners = [(0,0),(cols,0),(cols,rows),(0,rows)]
    points = corners + [(x,y) for y in range(rows+1) for x in range(cols+1) if (x,y) not in corners]
    indices = {p:i for i,p in enumerate(points)}
    uv, vertices, triangles = [], [], []
    for ix,iy in points:
        u,v = ix/cols,iy/rows
        x,y = u*WIDTH,v*HEIGHT
        uv.extend((round(u,6),round(v,6)))
        ws = weights(x,y)
        vertices.append(len(ws))
        for i,w in ws:
            bx,by = pivots[i]
            vertices.extend([i,round((x-bx)*scale,3),round((by-y)*scale,3),round(w,6)])
    for y in range(rows):
        for x in range(cols):
            a,b,c,d = [indices[p] for p in [(x,y),(x+1,y),(x,y+1),(x+1,y+1)]]
            triangles.extend((a,c,b,b,c,d))
    mesh = dict(type='mesh',uvs=uv,vertices=vertices,triangles=triangles,hull=4,
                width=WIDTH*scale,height=HEIGHT*scale)
    return dict(skeleton=dict(spine='3.8.99',width=WIDTH*scale,height=HEIGHT*scale,images='./'),
                bones=bones, slots=[dict(name='disciple',bone='flight',attachment='rider')],
                skins=[dict(name='default',attachments={'disciple':{'rider':mesh}})],
                animations={'hover':{'bones':timelines}})


def main():
    rig = skeleton()
    compact = lambda value: json.dumps(value,ensure_ascii=False,separators=(',',':'))
    (HERE/'rider.json').write_text(compact(rig),encoding='utf-8')
    w,h = Image.open(HERE/'rider.png').size
    (HERE/'rider.atlas').write_text(
        f'rider.png\nsize: {w}, {h}\nformat: RGBA8888\nfilter: Linear, Linear\nrepeat: none\n'
        f'rider\n  rotate: false\n  xy: 0, 0\n  size: {w}, {h}\n  orig: {w}, {h}\n  offset: 0, 0\n  index: -1\n',
        encoding='utf-8')
    # Pack just the title artwork, retaining the approved brush lettering. The UI
    # uses this plate as an ink mask; it is never used as the cloud background.
    with Image.open(HERE.parent/'login-concept-v1.png') as concept:
        clip = concept.crop((144,242,843,560))
        plate = io.BytesIO()
        clip.convert('RGB').save(plate,format='WEBP',quality=94)
        stamp = io.BytesIO()
        concept.crop((250,1364,685,1515)).convert('RGB').save(stamp,format='WEBP',quality=96)
    data = dict(mesh=base64.b64encode((ART/'Funnel.bytes').read_bytes()).decode(),
                shader=(CLOUD/'rolling.txt').read_text(encoding='utf-8-sig'),
                painting=uri('image/jpeg',(HERE/'portrait-clouds.jpg').read_bytes()),
                flow=uri('image/png',(ART/'CloudFlow.png').read_bytes()),
                rider=uri('image/png',(HERE/'rider.png').read_bytes()),rig=rig,story=story_art(),
                battle=battle_art())
    html=(HERE/'screen.html.in').read_text(encoding='utf-8')
    html=html.replace('TITLE_PLATE',uri('image/webp',plate.getvalue()))
    html=html.replace('BUTTON_PLATE',uri('image/webp',stamp.getvalue()))
    html=html.replace('/* PACKED_ASSETS */', 'const assets='+compact(data)+';')
    html=html.replace('/* SCENE_CODE */',(HERE/'scene.js').read_text(encoding='utf-8'))
    html=html.replace('/* STORY_CODE */',(HERE/'story.js').read_text(encoding='utf-8'))
    html=html.replace('/* BATTLE_ENTRY */','\n'.join(src.read_text(encoding='utf-8') for src in (
        HERE.parent/'Impostor/atlas.js', HERE/'battle-clouds.js', HERE/'battle-skills.js', HERE/'battle-enemy.js', HERE/'battle-entry.js')))
    html=html.replace('/* DIALOGUE_CODE */',(HERE/'dialogue.js').read_text(encoding='utf-8'))
    html=html.replace('/* MAP_MOTION */',(HERE/'map-motion.js').read_text(encoding='utf-8'))
    html=html.replace('/* STORY_STYLE */',(HERE/'story.css').read_text(encoding='utf-8'))
    (HERE/'index.html').write_text(html,encoding='utf-8')


if __name__=='__main__':
    main()
