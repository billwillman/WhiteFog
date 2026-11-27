"""Bake the three temporary companion skins from the 1.3.0 pet prefabs for the H5 actor renderer."""
import base64
import gzip
import io
import json
import zlib
from pathlib import Path

import numpy as np
from PIL import Image
from UnityPy.helpers.MeshHelper import MeshHandler

from animation_curves import read_curves
from export_models import DEFAULT_PACKAGE, Library, matrices, trs

OUTPUT = Path(__file__).resolve().parents[3] / 'LoginDemo' / 'battle-art'
PETS = {'adept': 'pet_yyxm_01', 'blade': 'pet_xylh_01', 'ward': 'pet_qxql_01'}


def encoded(data):
    return base64.b64encode(data).decode('ascii')


def bake(db, pet):
    bundle = f'resourcesab/prefabs/charactor/pet/{pet}.ab'
    objects = db.load(bundle)
    renderer = next(r for o in objects.values() if o.type.name == 'SkinnedMeshRenderer'
                    for r in [o.read()] if r.m_Mesh.read().m_Name == pet + '_lod2'
                    and 'yise' not in r.m_Materials[0].read().m_Name)
    root = renderer.m_Bones[0].read()
    while root.m_GameObject.read().m_Name != 'root':
        root = root.m_Father.read()
    paths, parents, transforms, indices = [], [], [], {}

    def visit(node, path, parent):
        index = len(paths)
        indices[node.object_reader.path_id] = index
        paths.append(path)
        parents.append(parent)
        transforms.append(trs(node))
        for child in node.m_Children:
            t = child.read()
            visit(t, path + '/' + t.m_GameObject.read().m_Name, index)

    visit(root, 'root', -1)
    # Bindings are relative to the Animator. Some LODs omit animated secondary bones.
    lookup = {zlib.crc32(path.encode()): i for i, path in enumerate(paths)}
    rest = np.array(transforms)
    mesh = renderer.m_Mesh.read()
    h = MeshHandler(mesh)
    h.process()
    positions = np.array(h.m_Vertices)
    joints, weights = np.array(h.m_BoneIndices), np.array(h.m_BoneWeights)
    bones = [indices[b.path_id] for b in renderer.m_Bones]
    bind = np.array([[[getattr(m, f'e{i}{j}') for j in range(4)] for i in range(4)] for m in mesh.m_BindPose])
    material = renderer.m_Materials[0].read()
    texture = dict(material.m_SavedProperties.m_TexEnvs)['_BaseMap'].m_Texture
    image = db.resolve(bundle, texture)[1].read().image.convert('RGBA')
    image.thumbnail((384, 384), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    image.save(buf, format='WEBP', quality=88)
    uv = np.rint(np.array(h.m_UV0) * 4096).astype('<i2')
    triangles, = h.get_triangles()
    part = dict(texture='data:image/webp;base64,' + encoded(buf.getvalue()), cutoff=.1,
                uv=encoded(uv.tobytes()), indices=encoded(np.asarray(triangles, dtype='<u2').tobytes()))
    clips = []
    for obj in db.load('resourcesab/animation/pet/' + pet + '.ab').values():
        if obj.type.name != 'AnimationClip':
            continue
        name = obj.peek_name().split('@')[-1]
        if name != 'attidle' and not name.startswith('attack_01'):
            continue
        clip = obj.read_typetree()
        duration = clip['m_MuscleClip']['m_StopTime']
        times = np.linspace(0, duration, round(duration * 15) + 1)
        values = np.tile(rest, (len(times), 1, 1))
        samples = read_curves(clip, times)
        offset = 0
        matched = 0
        for binding in clip['m_ClipBindingConstant']['genericBindings']:
            start, width = {1: (0, 3), 2: (3, 4), 3: (7, 3)}[binding['attribute']]
            if binding['path'] in lookup:
                values[:, lookup[binding['path']], start:start+width] = samples[:, offset:offset+width]
                matched += 1
            offset += width
        assert matched > 5 and offset == samples.shape[1], (pet, name, matched)
        quat = values[:, :, 3:7]
        quat /= np.maximum(1e-10, np.linalg.norm(quat, axis=-1, keepdims=True))
        world = matrices(values)
        for i, parent in enumerate(parents):
            if parent >= 0:
                world[:, i] = world[:, parent] @ world[:, i]
        skin = world[:, bones] @ bind
        frames = np.zeros((len(times), len(positions), 3))
        for j in range(joints.shape[1]):
            m = skin[:, joints[:, j]]
            frames += (np.einsum('fvij,vj->fvi', m[:, :, :3, :3], positions) + m[:, :, :3, 3]) * weights[:, j, None]
        assert np.ptp(frames, axis=0).max() > .01 and np.abs(frames).max() < 15, (pet, name)
        clips.append(dict(name='idle' if name == 'attidle' else 'attack', frames=len(times), duration=duration,
                          vertices=[encoded(np.rint(frames*2048).astype('<i2').tobytes())]))
    assert {c['name'] for c in clips} == {'idle', 'attack'}
    return dict(parts=[part], clips=sorted(clips, key=lambda c: c['name'] != 'idle'))


if __name__ == '__main__':
    db = Library(DEFAULT_PACKAGE)
    for ally, pet in PETS.items():
        heads = db.load('resourcesab/ui_new/uiimage/icon/pet/head.ab')
        head = next(o for o in heads.values() if o.type.name == 'Sprite' and o.peek_name() == pet)
        portrait = head.read().image.convert('RGBA')
        portrait.thumbnail((128, 128), Image.Resampling.LANCZOS)
        portrait.save(OUTPUT / ('pet-' + ally + '.webp'), quality=90)
        data = json.dumps(bake(db, pet), separators=(',', ':')).encode()
        (OUTPUT / ('pet-' + ally + '.js')).write_text(
            'window.localCompanionSkin=' + json.dumps(encoded(gzip.compress(data, mtime=0))) + ';\n', encoding='utf-8')
