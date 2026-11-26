"""Pack a gzip Unity WebGL build for lazy loading from HTTP or a local HTML file."""
import argparse
import base64
import json
from pathlib import Path


def pack(source, destination):
    loader, = source.glob('*.loader.js')
    files = {}
    for key, suffix, mime in [
        ('dataUrl', '.data.unityweb', 'application/octet-stream'),
        ('frameworkUrl', '.framework.js.unityweb', 'application/javascript'),
        ('codeUrl', '.wasm.unityweb', 'application/wasm'),
    ]:
        path, = source.glob('*' + suffix)
        data = path.read_bytes()
        files[key] = dict(type=mime, gzip=data.startswith(b'\x1f\x8b'),
                          bytes=base64.b64encode(data).decode('ascii'))
    destination.write_text(loader.read_text(encoding='utf-8') +
                           '\nwindow.qingyunPlayerFiles=' +
                           json.dumps(files, separators=(',', ':')) + ';\n', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('build', type=Path)
    args = parser.parse_args()
    pack(args.build, Path(__file__).resolve().parent / 'battle-art/unity-player.js')
