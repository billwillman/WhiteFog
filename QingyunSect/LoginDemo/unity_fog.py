"""Translate the project's mobile fog functions, keeping the shader and material as sources."""
import math
import re


def shader_parts(root):
    source = (root/'FogOfWar.shader').read_text(encoding='utf-8-sig')
    material = (root/'Mobile/FogOfWar_WeChat.mat').read_text(encoding='utf-8-sig')
    preset = (root/'OriginalFogPreset.asset').read_text(encoding='utf-8-sig')
    density = source[source.index('float Remap('):source.index('float3 MaskBands(')]
    light = source[source.index('float fresnel ='):source.index('float reveal =')]
    light = light.replace('input.normal', 'vec3(0.,1.,0.)')
    def linear(x):
        return x/12.92 if x <= .04045 else ((x+.055)/1.055)**2.4
    def gradient(name):
        block = preset.split('  '+name+':',1)[1].split('    m_Mode:',1)[0]
        points = []
        for i, rgb in re.findall(r'key(\d+): \{r: ([^}]+)\}', block):
            pos = int(re.search(r'ctime'+i+r': (\d+)',block)[1])/65535
            vals = [float(x.split(':')[-1]) for x in rgb.split(',')[:3]]
            points.append((pos,vals))
        for (lo,a),(hi,b) in zip(points,points[1:]):
            if lo<=.5<=hi:
                return [x+(y-x)*(.5-lo)/(hi-lo) for x,y in zip(a,b)]
    overrides={'_Color':gradient('tint'),'_DirectionColor':gradient('directionalColor')}
    # Same midday orbit as FogOfWar.Tick on the mobile scene's default time of day.
    angle=math.radians(115+140*(.5-.2)/.8)
    x,y,z=math.sin(angle),-math.sin(math.radians(37.5))*math.cos(angle),math.cos(math.radians(37.5))*math.cos(angle)
    overrides['_Direction']=[.5*x+math.sin(math.pi/3)*z,y,-math.sin(math.pi/3)*x+.5*z]
    def number(value):
        s=format(float(value),'.9g')
        return s if '.' in s or 'e' in s else s+'.0'
    defines=[]
    used=density+light+' _DisplacementHeight _OffsetOutside _OffsetEdge _OffsetHalf _AlphaHalf _CloudsOpacityHalf _AlphaOuter _CloudsOpacityOuter _AlphaPower _AlphaMultiply '
    for name,value in re.findall(r'^    - (_\w+): (.+)$',material,re.M):
        if name not in used or 'Texture' in value:continue
        if value.startswith('{'):
            vals=[float(v) for v in re.findall(r': ([-.\dEe+]+)',value)]
            if name in overrides:vals=overrides[name]+[1]
            if name in ('_Color','_DirectionColor','_ColorValleys','_ColorPeaks','_DarkTint'):
                vals=[linear(c) for c in vals[:3]]+vals[3:]
            expr='vec4('+','.join(map(number,vals))+')'
        else:
            expr=number(1 if name=='_DirectionFalloff' else value)
        defines.append('#define '+name+' '+expr)
    def translate(s):
        s=s.replace('_Time.y','time').replace('float2','vec2').replace('float3','vec3').replace('float4','vec4').replace('lerp(','mix(')
        # Both Unity R8 textures are packed losslessly into RG for the web.
        for tex,ch in [('_BaseClouds','r'),('_CloudsA','g'),('_CloudsB','g')]:
            s=re.sub(r'SAMPLE_TEXTURE2D(?:_LOD)?\('+tex+r',\s*\w+,\s*(\w+)(?:, 0)?\)\.r',r'texture2D(clouds,\1).'+ch,s)
        return re.sub(r'(?<![\w.])(\d+)(?![\w.])',r'\1.0',s)
    return dict(common='#define FOG_MOBILE\n#define saturate(x) clamp(x,0.0,1.0)\n'+'\n'.join(defines)+'\n'+translate(density), light=translate(light))
