Shader "WhiteFog/Original Skill Particle" {
Properties {
    _Texture ("主纹理", 2D) = "white" {}
    [HDR] _Color ("正面颜色", Color) = (1,1,1,1)
    [HDR] _Color1 ("副色", Color) = (1,1,1,1)
    [HDR] _BackColor ("背面颜色", Color) = (1,1,1,1)
    [HDR] _BackColor1 ("背面副色", Color) = (1,1,1,1)
    _DifferentFaceColor ("独立背面色", Float) = 0
    _ColorControlTex ("颜色控制", 2D) = "black" {}
    _ColorControlTexIndependentTiling ("独立色图 UV", Float) = 0
    _Mask ("遮罩", 2D) = "white" {}
    _MaskRGBA ("遮罩通道", Vector) = (0,0,0,1)
    _MaskU ("U 遮罩起点", Float) = 0
    _MaskV ("V 遮罩起点", Float) = 0
    _MaskSpeed ("遮罩速度", Vector) = (0,0,0,0)
    _MaskTexAngle ("遮罩角度", Float) = 0
    _MaskParticleSystemControl ("粒子遮罩偏移", Float) = 0
    _MainTexStrength ("主纹理强度", Float) = 1
    _MainTexAngle ("主纹理旋转圈数", Float) = 0
    _MainTexAngleSpeed ("主纹理角速度", Float) = 0
    _Uscroll ("U 滚动", Float) = 0
    _Vscroll ("V 滚动", Float) = 0
    _U_Mirror ("水平镜像", Float) = 0
    _V_Mirror ("垂直镜像", Float) = 0
    _UVParticleControl ("粒子 UV 偏移", Float) = 0
    _DissolveTex ("溶解", 2D) = "white" {}
    _DissolveTexDetail ("细节溶解", 2D) = "white" {}
    _Dissolve ("溶解阈值", Float) = 0
    _DissolveEdageWidth ("溶解边缘宽度", Float) = .01
    _DissipateSoftCutout ("边缘软化", Float) = 0
    [HDR] _EdageColor ("溶解边缘色", Color) = (1,1,1,1)
    _Gradian ("溶解过渡", Float) = .02
    _GradianPow ("过渡指数", Float) = 1
    _ParticleControl ("逐粒子溶解", Float) = 0
    _DissolveTexLerp ("细节权重", Float) = 0
    _DissolveTexUscroll ("溶解横移", Float) = 0
    _DissolveTexVscroll ("溶解纵移", Float) = 0
    _DissolveTexDetailUscroll ("细节横移", Float) = 0
    _DissolveTexDetailVscroll ("细节纵移", Float) = 0
    _DistortTexture ("扰动纹理", 2D) = "gray" {}
    _DistRGBA ("扰动通道", Vector) = (1,0,0,0)
    _DistForceU ("横向扰动", Float) = 0
    _DistForceV ("纵向扰动", Float) = 0
    _DistTime ("扰动速度", Float) = 0
    _DistTime1 ("次层扰动速度", Float) = 0
    _DistortParticleSystemControl ("粒子扰动系数", Float) = 0
    _Brightness ("亮度", Float) = 1
    _AlphaAdjust ("透明度", Float) = 1
    _AlphaClip ("裁剪阈值", Float) = 0
    _FxSrcBlend ("源混合", Float) = 5
    _FxDstBlend ("目标混合", Float) = 10
    _ZTest ("深度比较", Float) = 4
    _ZWrite ("写入深度", Float) = 0
    _Cull ("剔除", Float) = 0
}
SubShader {
    Tags {"Queue"="Transparent" "RenderType"="Transparent"}
    Pass {
        Blend [_FxSrcBlend] [_FxDstBlend], One OneMinusSrcAlpha
        ZWrite [_ZWrite] ZTest [_ZTest] Cull [_Cull]
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma shader_feature_local _ H5_PARTICLE_DEPTH_CAPTURE
        #pragma shader_feature_local _ _USE_MASK_ON
        #pragma shader_feature_local _ _USE_COLORCONTROL
        #pragma shader_feature_local _ _USE_CUTOUT
        #pragma shader_feature_local _ _USE_CUTOUT_DETAILTEX
        #pragma shader_feature_local _ _USE_DISTORT_ON
        #pragma shader_feature_local _ _USE_ALPHACLIP
        #include "UnityCG.cginc"
        sampler2D _Texture,_Mask,_ColorControlTex,_DissolveTex,_DissolveTexDetail,_DistortTexture;
        float4 _Texture_ST,_Mask_ST,_ColorControlTex_ST,_DissolveTex_ST,_DissolveTexDetail_ST,_DistortTexture_ST;
        float4 _Color,_Color1,_BackColor,_BackColor1,_EdageColor,_MaskRGBA,_MaskSpeed,_DistRGBA;
        float _MainTexAngle,_MainTexAngleSpeed,_Uscroll,_Vscroll,_U_Mirror,_V_Mirror,_UVParticleControl;
        float _Dissolve,_DissolveEdageWidth,_DissolveTexLerp,_DissolveTexUscroll,_DissolveTexVscroll;
        float _DissolveTexDetailUscroll,_DissolveTexDetailVscroll,_DissipateSoftCutout,_Gradian,_GradianPow,_ParticleControl;
        float _DistTime,_DistTime1,_DistForceU,_DistForceV,_DistortParticleSystemControl,_MaskTexAngle,_MaskParticleSystemControl;
        float _ColorControlTexIndependentTiling,_DifferentFaceColor,_MainTexStrength,_Brightness,_AlphaAdjust,_AlphaClip;
        float _MaskU,_MaskV;
        #ifdef H5_PARTICLE_DEPTH_CAPTURE
        float4 _H5DepthCapture; // Camera-space origin, inverse capture width, additive flag, ignore-depth flag.
        #endif
        struct Input { float4 pos:POSITION; float4 uv:TEXCOORD0; float4 custom:TEXCOORD1; float4 color:COLOR; };
        struct Vary { float4 pos:SV_POSITION; float4 raw:TEXCOORD0; float2 mainUV:TEXCOORD1; float2 maskUV:TEXCOORD2; float4 color:COLOR;
            #ifdef H5_PARTICLE_DEPTH_CAPTURE
            float eyeDepth:TEXCOORD3;
            #endif
        };

        float2 RotateUV(float2 uv, float radians) {
            float s,c; sincos(radians,s,c); uv-=.5;
            return float2(c*uv.x+s*uv.y,-s*uv.x+c*uv.y)+.5;
        }
        Vary vert(Input v) {
            Vary o; o.pos=UnityObjectToClipPos(v.pos); o.raw=v.uv; o.color=v.color;
            #ifdef H5_PARTICLE_DEPTH_CAPTURE
            o.eyeDepth = -UnityObjectToViewPos(v.pos).z;
            #endif
            float2 uv=lerp(v.uv.xy,1-v.uv.xy,float2(_U_Mirror,_V_Mirror));
            float angle=(_MainTexAngle-_Time.y*_MainTexAngleSpeed*.1)*6.28308;
            o.mainUV=RotateUV(TRANSFORM_TEX(uv,_Texture),angle)
                +lerp(frac(_Time.y*float2(_Uscroll,_Vscroll)),v.custom.xy,_UVParticleControl);
            o.maskUV=RotateUV(TRANSFORM_TEX(uv,_Mask),radians(_MaskTexAngle))
                +lerp(_Time.y*_MaskSpeed.xy,v.custom.zw,_MaskParticleSystemControl);
            return o;
        }
        float4 frag(Vary i, float facing:VFACE):SV_Target {
            float2 uv=i.mainUV;
            #if defined(_USE_DISTORT_ON)
                float2 mirrored=lerp(i.raw.xy,1-i.raw.xy,float2(_U_Mirror,_V_Mirror));
                float2 noiseUV=TRANSFORM_TEX(mirrored,_DistortTexture);
                float4 scroll=frac(_Time.xzyx*float4(_DistTime,_DistTime1,_DistTime,_DistTime1));
                float force=dot(tex2D(_DistortTexture,noiseUV+scroll.xy)+tex2D(_DistortTexture,noiseUV+scroll.zw)-1,_DistRGBA);
                uv+=force*float2(_DistForceU,_DistForceV)*lerp(1,i.raw.z,_DistortParticleSystemControl);
            #endif
            float4 sampled=tex2D(_Texture,uv);
            float3 front=_Color.rgb, back=_BackColor.rgb;
            #if defined(_USE_COLORCONTROL)
                float2 rampUV=lerp(i.mainUV,TRANSFORM_TEX(i.raw.xy,_ColorControlTex),_ColorControlTexIndependentTiling);
                float ramp=tex2D(_ColorControlTex,rampUV).r;
                front=lerp(front,_Color1.rgb,ramp); back=lerp(back,_BackColor1.rgb,ramp);
            #endif
            float3 color=sampled.rgb*i.color.rgb*lerp(front,back,facing<0?_DifferentFaceColor:0);
            float alpha=sampled.a*_MainTexStrength*i.color.a*_Color.a;
            #if defined(_USE_MASK_ON)
                alpha*=dot(tex2D(_Mask,i.maskUV),_MaskRGBA) * (i.raw.x > _MaskU && i.raw.y > _MaskV);
            #endif
            alpha=saturate(alpha);
            #if defined(_USE_CUTOUT)
                float2 dissolveUV=TRANSFORM_TEX(i.raw.xy,_DissolveTex)+frac(_Time.y*float2(_DissolveTexUscroll,_DissolveTexVscroll));
                float field=max(.001,tex2D(_DissolveTex,dissolveUV).r);
                #if defined(_USE_CUTOUT_DETAILTEX)
                    float2 detailUV=TRANSFORM_TEX(i.raw.xy,_DissolveTexDetail)+frac(_Time.y*float2(_DissolveTexDetailUscroll,_DissolveTexDetailVscroll));
                    field=lerp(field,max(.001,tex2D(_DissolveTexDetail,detailUV).r),_DissolveTexLerp);
                #endif
                float threshold=clamp(lerp(_Dissolve,i.raw.w,_ParticleControl),.001,.999);
                float slope=pow(max(.02,saturate(_Gradian)),max(1,_GradianPow));
                alpha*=lerp(smoothstep(threshold*(1-slope),threshold,field),
                    smoothstep(threshold,threshold+(1-threshold)*slope,field),threshold);
                float edge=threshold+(1-threshold)*max(.01,_DissolveEdageWidth);
                float start=lerp(edge,threshold,saturate(_DissipateSoftCutout));
                float fade=smoothstep(start, max(start+.00001,edge),field);
                if (_Dissolve>0) color=lerp(_EdageColor.rgb,color,fade);
            #endif
            #if defined(_USE_ALPHACLIP)
                clip(alpha-_AlphaClip);
            #endif
            float4 result = float4(color*_Brightness,alpha*_AlphaAdjust);
            #ifdef H5_PARTICLE_DEPTH_CAPTURE
                float peak = max(result.r, max(result.g, result.b));
                clip(result.a * lerp(1, peak, _H5DepthCapture.z) - 1.0/255.0);
                float testDepth = 1-_H5DepthCapture.w;
                return float4(testDepth*saturate(0.5 + (i.eyeDepth-_H5DepthCapture.x)*_H5DepthCapture.y), testDepth, 0, 1);
            #endif
            return result;
        }
        ENDCG
    }
} }
