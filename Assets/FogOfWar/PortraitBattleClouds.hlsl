// Same perspective cloud plane as QingyunSect/LoginDemo/battle-clouds.js.
// Actors keep their independent portrait framing.
static const float3 Eye = float3(0, 14, 0);
static const float3 Forward = float3(0, -.6156615, .7880108);
static const float3 Up = float3(0, .7880108, .6156615);
Varyings ProjectBattleClouds(Attributes input)
{
    Varyings o;
    float aspect = _ScreenParams.x / _ScreenParams.y;
    float2 gridXY = input.positionOS.xy;
    float3 ray = Forward + .39 * (gridXY.y * Up + float3(gridXY.x * aspect,0,0));
    o.world = Eye + ray * (-Eye.y / ray.y);
    float3 bands = MaskBands(o.world.xz);
    float offset = Layers(_OffsetOutside, _OffsetEdge, _OffsetHalf, _OffsetFull, bands);
    o.world.y = lerp(-_DisplacementHeight, _DisplacementHeight, CloudValue(o.world.xz, true)) - offset;
    float3 rel = o.world - Eye;
    float depth = dot(rel,Forward);
    o.positionCS = float4(rel.x / (.39 * aspect), dot(rel,Up) * _ProjectionParams.x / .39, .5 * depth, depth);
    o.normal = float3(0,1,0);
    return o;
}


float3 LightBattleClouds(float3 rgb, float2 xz)
{
    float cycle = floor(_Time.y / 3.6);
    float phase = fmod(_Time.y,3.6) - .52;
    float pulse = smoothstep(0,.34,phase) * (1 - smoothstep(.36,1.55,phase));
    float2 flash = float2(fmod(cycle,2) < 1 ? -4 : 4, 14 + 9 * frac(sin(cycle * 17.17) * 437.53));
    float2 delta = (xz - flash) * float2(.16,.19);
    float halo = max(0,(exp2(-1.65 * dot(delta,delta)) - .045) / .955) * pulse;
    if (halo > 0) {
        float2 uv = xz * .01 * _BaseScale * _BaseCloudsScale + _BaseCloudsSpeed.xy * _Time.y * _BaseSpeed;
        float2 dx = float2(2.0 / 1024,0);
        float2 slope = float2(SAMPLE_TEXTURE2D(_BaseClouds,sampler_BaseClouds,uv+dx).r - SAMPLE_TEXTURE2D(_BaseClouds,sampler_BaseClouds,uv-dx).r,
            SAMPLE_TEXTURE2D(_BaseClouds,sampler_BaseClouds,uv+dx.yx).r - SAMPLE_TEXTURE2D(_BaseClouds,sampler_BaseClouds,uv-dx.yx).r) * 4;
        float3 n = normalize(float3(-slope.x,1,-slope.y));
        float cap = SAMPLE_TEXTURE2D(_CloudMatcap,sampler_CloudMatcap,float2(n.x,dot(n,Up))*.475+.5).r;
        rgb += lerp(float3(.12,.29,.38),float3(.28,.16,.37),frac(cycle*.618)) * halo * (.35 + .83 * cap);
    }
    return rgb;
}
