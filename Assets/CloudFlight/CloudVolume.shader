Shader "CloudFlight/Cloud sea"
{
    Properties
    {
        [NoScaleOffset] _MatCap("Cloud scattering MatCap",2D)="white"{}
        [NoScaleOffset] _Noise("Baked cloud density and light",2D)="white"{}
        [Toggle] _Core("Cloud core",Float)=0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=10
        [Toggle] _DepthWrite("Write depth",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZTest LEqual ZWrite [_DepthWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MatCap); SAMPLER(sampler_MatCap);
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
                float4 _Pulse;
                float _Core;
            CBUFFER_END
            float Matcap(float3 n)
            {
                float2 uv=TransformWorldToViewDir(n).xy*.475+.5;
                return SAMPLE_TEXTURE2D(_MatCap,sampler_MatCap,uv).r;
            }
            #define CLOUD_CAP(n) Matcap(n)
            #define CLOUD_DETAIL(uv) SAMPLE_TEXTURE2D(_MatCap,sampler_MatCap,uv)
            #include "WeChatMiniGame/shaders/volume.txt"
            struct Attributes {float3 p:POSITION; float2 uv:TEXCOORD0; float4 baked:COLOR;};
            struct Varyings {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float4 baked:TEXCOORD2;float depth:TEXCOORD3;};
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world=TransformObjectToWorld(input.p);
                o.pos=TransformWorldToHClip(o.world);
                o.depth=-TransformWorldToView(o.world).z;
                o.baked=input.baked;
                o.uv=input.uv;
                return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float4 field=i.baked;
                if(_Core<.5) field=SAMPLE_TEXTURE2D(_Noise,sampler_Noise,i.uv);
                return SeaCloud(field,i.world,i.depth,_Core,_Pulse);
            }
            ENDHLSL
        }
    }
}
