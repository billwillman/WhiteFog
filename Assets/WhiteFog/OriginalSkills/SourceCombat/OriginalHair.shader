Shader "WhiteFog/Original Hair Preview" {
Properties {
    _BaseMap ("原作分区纹理", 2D) = "white" {}
    _BaseColor ("深色", Color) = (0.17,0.154,0.15,1)
    _BaseColorB ("浅色", Color) = (0,0,0,1)
    _BaseColorControl ("分区范围", Range(0.01,1)) = 0.095
    _ShadowColor ("阴影", Color) = (0.83,0.83,0.83,1)
}
SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
    Pass {
        Cull Back
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _BaseMap;
        float4 _BaseColor, _BaseColorB, _ShadowColor;
        float _BaseColorControl;
        struct Vertex { float4 pos:POSITION; float2 uv:TEXCOORD0; };
        struct Varying { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
        Varying vert(Vertex v) {
            Varying o; o.pos = UnityObjectToClipPos(v.pos); o.uv = v.uv; return o;
        }
        fixed4 frag(Varying i):SV_Target {
            half3 mask = tex2D(_BaseMap,i.uv).rgb;
            half3 hair = lerp(_BaseColor.rgb, _BaseColorB.rgb,saturate(mask.g/max(0.01,_BaseColorControl)));
            return fixed4(hair * lerp(_ShadowColor.rgb,1.0,mask.r),1);
        }
        ENDCG
    }
}
}