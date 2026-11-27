"""Pack WebGL textures and the rigged bird as local data URLs (also usable over file://)."""
import base64
import io
import json
import re
from pathlib import Path

from PIL import Image
from unity_fog import shader_parts

here = Path(__file__).resolve().parent
assets = here.parents[1] / 'Assets'
library = here.parent / 'ReferenceAssets/Zszs/Library'


def uri(mime, data):
    return 'data:' + mime + ';base64,' + base64.b64encode(data).decode()


def pack():
    fog = assets / 'FogOfWar'
    noise = []
    for n in (1, 2):
        source = (fog / f'Mobile/NoiseClouds{n}_1024.asset').read_text('utf-8-sig')
        pixels = bytes.fromhex(re.search(r'_typelessdata: ([a-fA-F0-9]+)', source)[1])
        noise.append(Image.frombytes('L', (1024, 1024), pixels[:1024**2]))
    png = io.BytesIO()
    Image.merge('RGB', (*noise, Image.new('L', (1024,1024)))).save(png, 'PNG')
    art = dict(noise=uri('image/png', png.getvalue()), fog=shader_parts(fog),
               cap=uri('image/png', (assets/'CloudFlight/WeChatMiniGame/art/CloudMatCap.png').read_bytes()),
               enemy=uri('model/gltf-binary', (library/'Models/mon_xiaodiao_001/mon_xiaodiao_001.glb').read_bytes()),
               edgeClouds=uri('image/webp', (here/'edge-clouds.webp').read_bytes()))
    (here/'battle-art/local-field.js').write_text('window.localBattleArt = '+json.dumps(art,separators=(',',':'))+';\n',encoding='utf8')


if __name__ == '__main__':
    pack()
