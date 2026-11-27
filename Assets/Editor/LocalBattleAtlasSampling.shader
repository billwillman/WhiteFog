Shader "Hidden/WhiteFog/H5 Battle Atlas Sampling"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZTest Always ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _InnerFraction;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 coord = i.uv * 2 - 1;
                float2 a = abs(coord);
                // Reserve the middle three quarters of a cell for the space occupied by the fighters.
                float2 world = min(a / .75, 1) * _InnerFraction
                    + max(a - .75, 0) * (1 - _InnerFraction) / .25;
                float2 uv = .5 + .5 * sign(coord) * world;
                float2 halfStep = .25 * (abs(ddx(uv)) + abs(ddy(uv)));
                return .25 * (tex2D(_MainTex,uv-halfStep) + tex2D(_MainTex,uv+halfStep)
                    + tex2D(_MainTex,uv+float2(halfStep.x,-halfStep.y)) + tex2D(_MainTex,uv+float2(-halfStep.x,halfStep.y)));
            }
            ENDCG
        }
    }
}
