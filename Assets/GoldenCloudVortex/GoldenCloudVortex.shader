Shader "GoldenCloudVortex/Rolling painted cloud"
{
    Properties
    {
        [NoScaleOffset] _Painting("Painted cloud",2D)="white"{}
        [NoScaleOffset] _Flow("Painted cloud flow and lightning MatCap",2D)="gray"{}
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Painting); SAMPLER(sampler_Painting);
            TEXTURE2D(_Flow); SAMPLER(sampler_Flow);
            CBUFFER_START(UnityPerMaterial)
                float _Cycle;
                float4 _Storm;
            CBUFFER_END
            #define VORTEX_ART(uv) SAMPLE_TEXTURE2D(_Painting,sampler_Painting,uv).rgb
            #define VORTEX_FLOW(uv) SAMPLE_TEXTURE2D(_Flow,sampler_Flow,uv)
            #define VORTEX_FRAC(x) frac(x)
            #define VORTEX_MIX(a,b,t) lerp(a,b,t)
            #include "WeChatMiniGame/rolling.txt"
            struct Attributes {float3 pos:POSITION;float2 uv:TEXCOORD0;float2 relief:TEXCOORD2;};
            struct Varyings {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 relief:TEXCOORD1;};
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.pos=TransformObjectToHClip(ShapeCloudBank(i.pos,i.uv));
                o.uv=i.uv;o.relief=i.relief;
                return o;
            }
            half4 Frag(Varyings i):SV_Target {return half4(RolledCloud(i.uv,i.relief,_Cycle,_Storm),1);}
            ENDHLSL
        }
    }
}
