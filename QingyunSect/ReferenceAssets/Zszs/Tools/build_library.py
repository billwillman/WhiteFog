"""Build the offline index from the validated sources and native particle captures."""
from export_models import LIB
from pathlib import Path
import html,json

root=LIB/'Library'
models=[json.loads(p.read_text('utf8')) for p in sorted((root/'Models').glob('*/source.json'))]
effects=[json.loads((root/'Effects'/code/'source.json').read_text('utf8')) for code in ['pugongjian','pokongjianfa','liuxingjianyu']]
def esc(s):return html.escape(str(s),quote=True)
def card(m):
    folder='Models/'+m['id']+'/'
    preview=folder+'preview.webp';materials='、'.join(dict.fromkeys(x['shader'] or '未知' for x in m['materials']))
    p=f'<article><img class="portrait" src="{preview}" alt="{esc(m["title"])}原模型预览" loading="lazy"><div><h3>{esc(m["title"])}</h3><p>{m["vertices"]:,} 顶点 · {m["triangles"]:,} 三角形 · {len(m["materials"])} 材质</p>'
    if m['animations']:p+=f'<p>GLB 内含 {len(m["animations"])} 段飞行动作</p>'
    p+=f'<a href="{folder}{m["id"]}.glb" download>下载 GLB</a>　<a href="{folder}source.json">材质与来源</a></div><details><summary>着色器与原动作包</summary><p>{esc(materials)}</p>'
    for src in dict.fromkeys(x['bundle'] for x in m['sourceClips']):
        count=sum(x['bundle']==src for x in m['sourceClips']);p+=f'<p><a href="Bundles/{esc(src)}" download>{esc(Path(src).stem)} · {count} 段</a></p>'
    return p+'</details></article>'
def vfx(e):
    folder='Effects/'+e['id']+'/'
    stages=({'pugongjian':['eff_cha_jianpugong_shifa01','eff_cha_jianpugong_trail01','eff_cha_jianpugong_hit']}.get(e['id']) or ['vfx_'+e['id']+'_'+s for s in ['shifa','trail','hit']])
    standard=sum('_lod' not in p['name'] for p in e['prefabs'])
    p=f'<section class="skill"><h3>{e["title"]}</h3><p>{standard} 个基础预制体 · 含 LOD 共 {len(e["prefabs"])} 个 · {len(e["materials"])} 种材质</p><div class="frames">'
    for name,label in zip(stages,['施法','飞行 / 拖尾','命中']):
        p+=f'<figure><img loading="lazy" src="{folder}{name}-preview.webp" alt="{e["title"]}{label}动画"><figcaption>{label}</figcaption></figure>'
    return p+f'</div><a href="{folder}source.json">预制体和材质参数</a>　<a href="{folder}particle-modules.json.gz" download>完整粒子模块</a>　<a href="Bundles/{esc(e["source"])}" download>原始 AB</a></section>'
style="""*{box-sizing:border-box}html{scroll-behavior:smooth}body{background:#f5f1e7;color:#233b38;margin:0;font:16px/1.65 system-ui,'Microsoft YaHei',sans-serif}main{max-width:1050px;margin:auto;padding:36px clamp(18px,5vw,66px) 70px}h1,h2,h3{font-family:'Songti SC',SimSun,serif}h1{font-size:clamp(34px,6vw,62px);margin:0;letter-spacing:.035em}h2{font-size:28px;border-bottom:1px solid #aea997;padding-bottom:6px;margin-top:34px}h3{font-size:21px;margin:0}p{margin:.4em 0;color:#42534c}a{color:#285e56;text-underline-offset:3px}header{border-top:4px solid #53655c;padding-top:16px}nav{display:flex;gap:18px;flex-wrap:wrap;margin:18px 0}.skill,article{border:1px solid #d1cdbc;background:#fffdf5;padding:16px;margin:14px 0}.frames{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin:12px 0}.frames figure{margin:0;text-align:center}.frames img{display:block;width:100%;object-fit:contain;height:clamp(90px,19vw,164px);background:#424f57}figcaption{color:#424d46;font-size:14px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.grid article{margin:0;display:grid;grid-template-columns:98px 1fr;align-items:center;gap:12px}.portrait{width:100%;aspect-ratio:1;object-fit:contain;background:#e4e5d9}.grid p{font-size:14px}details{grid-column:1/-1;overflow-wrap:anywhere}summary{cursor:pointer;color:#31594a;font-size:14px}@media(max-width:660px){.grid{grid-template-columns:1fr}main{padding-top:20px}.skill{padding:12px}}"""
parts=['<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>宗师之上 · 战斗资源</title><style>'+style+'</style><main><header><h1>战斗资源</h1><p>《宗师之上》1.3.0 · 角色、怪物与剑招</p><nav><a href="#skills">技能表现</a><a href="#monsters">怪物</a><a href="#roles">人物</a><a href="../../../LoginDemo/sword-vs-xiaodiao.mp4">观看御剑对敌录像</a><a href="../../../Index.html#ferry-battle-entry">返回策划书</a></nav></header>',
'<h2 id="skills">技能表现</h2><p>每行从左到右展示同一招的施法、拖尾和命中。团结编辑器按原粒子预制体模拟，透明图集以 24 帧播放。HTML5 预览材质近似还原原贴图、颜色、遮罩和消融，完整着色器与粒子参数随原包归档。</p>']
parts.extend(vfx(e) for e in effects)
for group,anchor in [('怪物','monsters'),('人物','roles')]:
    parts.append(f'<h2 id="{anchor}">{group}</h2><div class="grid">');parts.extend(card(m) for m in models if m['group']==group);parts.append('</div>')
parts.append('<p>小雕 GLB 可在 <a href="../../../Impostor/index.html">HTML5 Impostor 工具</a> 中导入并查看五段动作。其他模型提供默认姿势，已收集的动作保留为原始 AB。GLB 使用轻量预览材质，可在各自的来源记录中查看原着色器、纹理槽、颜色、混合参数和关键字。</p><p><a href="Bundles/checksums.json">原包路径和 SHA-256 校验清单</a></p></main></html>')
(root/'index.html').write_text('\n'.join(parts),'utf8')

