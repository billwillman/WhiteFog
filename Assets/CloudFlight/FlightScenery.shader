Shader "CloudFlight/Flight scenery"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Varyings {float4 pos:SV_POSITION;half3 color:TEXCOORD0;};
            Varyings Vert(float3 positionOS:POSITION,float3 normalOS:NORMAL,half4 tint:COLOR)
            {
                Varyings o; float3 w=TransformObjectToWorld(positionOS); o.pos=TransformWorldToHClip(w);
                half light=0.63+0.38*saturate(dot(TransformObjectToWorldNormal(normalOS),normalize(float3(-0.3,0.8,-0.48))));
                float haze=saturate((distance(w,_WorldSpaceCameraPos)-75)/200);
                o.color=lerp(tint.rgb*tint.rgb*light,float3(.30,.42,.50),haze);
                return o;
            }
            half4 Frag(Varyings i):SV_Target {return half4(i.color,1);}
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="DepthOnly"}
            ColorMask 0 ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float3 p:POSITION):SV_POSITION {return TransformObjectToHClip(p);}
            half4 Frag():SV_Target {return 0;}
            ENDHLSL
        }
    }
}
