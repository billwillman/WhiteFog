Shader "Qingyun/ParticleReview" {
Properties {_MainTex("Texture",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) _Second("Second",Color)=(1,1,1,1) _Cut("Cut",2D)="white"{} _HasCut("Use cut",Float)=0 _Threshold("Threshold",Float)=0 _Mask("Mask",2D)="white"{} _Ramp("Ramp",2D)="white"{} _HasMask("Has mask",Float)=0 _HasRamp("Has ramp",Float)=0 _MaskChannels("Mask Channels",Vector)=(0,0,0,1) _Dst("Destination",Float)=10}
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Pass {
Blend SrcAlpha [_Dst], One OneMinusSrcAlpha
Cull Off ZWrite Off ZTest Always
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_Mask,_Ramp,_Cut;float4 _Second;float _HasCut,_Threshold;float4 _MaskChannels;float _HasRamp,_HasMask;float4 _MainTex_ST,_Tint;
struct input {float4 pos:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;};
struct vary {float4 pos:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
vary vert(input v){vary o;o.pos=UnityObjectToClipPos(v.pos);o.uv=TRANSFORM_TEX(v.uv.xy,_MainTex);o.color=v.color;return o;}
float4 frag(vary i):SV_Target{float4 c=tex2D(_MainTex,i.uv);float intensity=max(c.r,max(c.g,c.b));
float control=tex2D(_Ramp,i.uv).r; c*=lerp(_Tint,_Second,control*_HasRamp); if(_HasCut>.5)c.a*=smoothstep(_Threshold-.055,_Threshold+.055,tex2D(_Cut,i.uv).r);
c.a*=lerp(1,saturate(dot(tex2D(_Mask,i.uv),_MaskChannels)),_HasMask);
return c*i.color;}
ENDCG
}}}
