Shader "WhiteFog/Original Weapon Mesh"
{
    Properties
    {
        _BaseMap ("主纹理", 2D) = "white" {}
        _ControlMap ("控制贴图", 2D) = "black" {}
        _BaseColor ("基础色", Color) = (1,1,1,1)
        _SkinColor ("控制图 Alpha 着色", Color) = (1,1,1,1)
        _ShadowColor ("暗部色", Color) = (.5,.5,.5,1)
        _AmbientColor ("环境色", Color) = (1,1,1,1)
        _ToonThesHold ("明暗阈值", Float) = .5
        _ToonHardness ("明暗硬度", Float) = 40
        _IsHit ("着色边缘", Float) = 0
        _HitColor ("边缘色", Color) = (1,0,0,1)
        _HitFresnelPow ("边缘指数", Float) = 1
        _Cull ("剔除", Float) = 2
        _ZWrite ("深度写入", Float) = 1
        _ZTest ("深度比较", Float) = 4
        _OutlineColor ("描边颜色", Color) = (0,0,0,1)
        _OutlineColorLerp ("纹理描边权重", Float) = 0
        _normalizeViewNormalXYScale ("描边宽度", Float) = 1
        _RenderMode ("描边方向来源", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Blend One Zero
        ZWrite [_ZWrite]
        Pass
        {
            Name "BaseCel"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZTest [_ZTest]
            Stencil { Ref 2 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ControlMap); SAMPLER(sampler_ControlMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _SkinColor, _AmbientColor, _ShadowColor, _HitColor;
                half _ToonThesHold, _ToonHardness, _IsHit, _HitFresnelPow;
            CBUFFER_END
            struct Attributes { float4 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.position.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normal);
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 control = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, i.uv);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * lerp(_BaseColor.rgb, _SkinColor.rgb, control.a);
                half3 normal = NormalizeNormalPerPixel(i.normal);
                Light light = GetMainLight();
                half ramp = saturate(((saturate(dot(normal, light.direction)) + 1) * .5h - _ToonThesHold) * _ToonHardness);
                half3 direct = albedo * lerp(_ShadowColor.rgb, max(.1h, 1 - control.b), ramp * (1 - control.g));
                half3 rgb = direct * light.color + albedo * _AmbientColor.rgb;
                half edge = pow(1 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(i.world))), _HitFresnelPow);
                return half4(lerp(rgb, _HitColor.rgb, edge * _IsHit), 1);
            }
            ENDHLSL
        }
        UsePass "WhiteFog/Original Mesh Particle/Outline"
    }
}
