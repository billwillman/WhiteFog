Shader "Hidden/WhiteFog/Original Skill Preview Light" {
    Properties { [HideInInspector] _MainTex ("Source", 2D) = "white" {} }
    SubShader {
        Cull Off ZTest Always ZWrite Off
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _SkillPreviewBloom;
        float4 _MainTex_TexelSize;
        float3 Box(float2 uv) {
            float2 d = abs(_MainTex_TexelSize.xy) * .5;
            return .25 * (tex2D(_MainTex,uv+d).rgb + tex2D(_MainTex,uv-d).rgb +
                tex2D(_MainTex,uv+float2(d.x,-d.y)).rgb + tex2D(_MainTex,uv+float2(-d.x,d.y)).rgb);
        }
        float4 Extract(v2f_img i):SV_Target {
            float3 c=Box(i.uv);
            float peak=max(c.r,max(c.g,c.b));
            float threshold=GammaToLinearSpaceExact(.9), knee=threshold*.5;
            float soft=clamp(peak-threshold+knee,0,2*knee);
            soft=soft*soft/(4*knee+.00001);
            return float4(c*max(soft,peak-threshold)/max(peak,.00001),1);
        }
        float4 Reduce(v2f_img i):SV_Target { return float4(Box(i.uv),1); }
        float4 Expand(v2f_img i):SV_Target {
            return float4(lerp(tex2D(_MainTex,i.uv).rgb,tex2D(_SkillPreviewBloom,i.uv).rgb,.7),1);
        }
        float4 Composite(v2f_img i):SV_Target {
            float4 src=tex2D(_MainTex,i.uv);
            float3 halo=tex2D(_SkillPreviewBloom,i.uv).rgb;
            halo=lerp(dot(halo,float3(.2126,.7152,.0722)),halo,1.05);
            float3 x=max(0,(src.rgb+.6*halo)*exp2(.1));
            float3 mapped=saturate(x*(2.51*x+.03)/(x*(2.43*x+.59)+.14));
            return float4(mapped,src.a);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Extract
            ENDCG
        }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Reduce
            ENDCG
        }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Expand
            ENDCG
        }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG
        }
    }
}