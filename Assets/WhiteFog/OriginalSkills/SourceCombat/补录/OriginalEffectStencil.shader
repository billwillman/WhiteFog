Shader "WhiteFog/Original Effect Stencil"
{
    SubShader
    {
        Tags { "Queue"="Geometry-10" "RenderType"="Transparent" }
        Pass
        {
            ColorMask 0
            Cull Back
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 10 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 position : POSITION) : SV_POSITION { return UnityObjectToClipPos(position); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
