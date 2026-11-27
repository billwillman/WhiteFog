Shader "WhiteFog/Original Mesh Particle"
{
    Properties
    {
        _BaseMap ("主纹理", 2D) = "white" {}
        _SLCol ("暗部颜色", Color) = (1,1,1,1)
        _SFCol ("阴影颜色", Color) = (1,1,1,1)
        _CelShade ("明暗分区", Float) = 1
        _CelShadeMidPoint ("明暗分界", Float) = 0.5
        _CelShadeSmoothness ("分界平滑度", Float) = 0.2
        [HDR] _StateFalloffColor ("边缘颜色", Color) = (1,1,1,1)
        _StateFalloffPow ("边缘指数", Float) = 1
        _StateFalloffAlpha ("边缘强度", Float) = 0
        _OutlineColor ("描边颜色", Color) = (0,0,0,1)
        _OutlineColorLerp ("纹理描边权重", Float) = 0
        _normalizeViewNormalXYScale ("描边宽度", Float) = 1
        _RenderMode ("描边方向来源", Float) = 0
        _Alpha ("透明度", Float) = 1
        _AlphaAdjust ("透明度系数", Float) = 1
        _AlphaRange ("渐隐起点", Float) = 0
        _AlphaReverseDir ("反转渐隐", Float) = 0
        _AlphaScale ("描边透明度系数", Float) = 1
        _Cull ("剔除", Float) = 2
        _ZWrite ("写入深度", Float) = 1
        _ZTest ("深度比较", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite [_ZWrite]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _SLCol, _SFCol, _StateFalloffColor, _OutlineColor;
            float _CelShade, _CelShadeMidPoint, _CelShadeSmoothness;
            float _StateFalloffPow, _StateFalloffAlpha, _OutlineColorLerp;
            float _normalizeViewNormalXYScale, _RenderMode;
            float _Alpha, _AlphaAdjust, _AlphaRange, _AlphaReverseDir, _AlphaScale;
        CBUFFER_END
        struct Attributes
        {
            float4 position : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };
        struct Varyings
        {
            float4 position : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 world : TEXCOORD1;
            half3 normal : TEXCOORD2;
            half4 color : COLOR;
        };
        Varyings Vertex(Attributes v)
        {
            Varyings o;
            o.world = TransformObjectToWorld(v.position.xyz);
            o.position = TransformWorldToHClip(o.world);
            o.normal = TransformObjectToWorldNormal(v.normal);
            o.color = v.color;
            o.uv = v.uv;
            return o;
        }
        half Opacity(Varyings i, float scale)
        {
            float height = saturate(i.world.y - GetObjectToWorldMatrix()._m13 - _AlphaRange);
            return lerp(height, 1-height, _AlphaReverseDir) * saturate(i.color.a * _Alpha * scale) * _AlphaAdjust;
        }
        ENDHLSL
        Pass
        {
            Name "BaseCel"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZTest [_ZTest]
            Stencil { Ref 2 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Surface
            half4 Surface(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 normal = NormalizeNormalPerPixel(i.normal);
                Light light = GetMainLight();
                float diffuse = saturate(dot(normal, light.direction) * 0.5 + 0.5);
                float mid = max(0.0001, _CelShadeMidPoint);
                float ramp = smoothstep(0, mid, pow(max(0, diffuse-mid), _CelShadeSmoothness));
                float lit = pow(smoothstep(0, max(0.0001, _CelShade), ramp), 4);
                half3 shadow = lerp(1, _SLCol.rgb, i.color.b);
                half3 tint = lerp(_SFCol.rgb, ramp * lerp(shadow, 1, lit), ramp);
                half3 rgb = albedo * (SampleSH(normal) + light.color * diffuse * tint);
                half rim = 1 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(i.world)));
                rgb += pow(rim, _StateFalloffPow) * _StateFalloffColor.rgb * _StateFalloffAlpha;
                return half4(rgb * i.color.rgb * i.color.rgb, Opacity(i, 1));
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZTest LEqual
            Stencil { Ref 2 Comp NotEqual }
            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment Outline
            Varyings OutlineVertex(Attributes v)
            {
                Varyings o = Vertex(v);
                float3 dir = _RenderMode != 0 ? o.normal : TransformObjectToWorldDir(v.tangent.xyz);
                float distanceToEye = max(0.001, distance(o.world, GetCameraPositionWS()));
                o.position.xy += mul((float3x3)UNITY_MATRIX_VP, dir).xy
                    * (0.001 * _normalizeViewNormalXYScale / distanceToEye) * o.position.w;
                return o;
            }
            half4 Outline(Varyings i) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half peak = max(color.r, max(color.g, color.b));
                half3 dominant = color * step(peak-0.004, color);
                return half4(_OutlineColor.rgb * lerp(1, 0.8 * dominant, _OutlineColorLerp), Opacity(i, _AlphaScale));
            }
            ENDHLSL
        }
    }
}
