Shader "DOD/VFX/FogOfWar"
{
    Properties
    {
        _BaseSpeed ("Base Speed", Float) = 0.0199999996
        _BaseClouds ("Base Clouds", 2D) = "white" {}
        _BaseCloudsScale ("Base Clouds Scale", Float) = 0.800000012
        _BaseCloudsIntensity ("Base Clouds Intensity", Float) = 1.5
        _BaseCloudsSpeed ("Base Clouds Speed", Vector) = (-0.5, -0.800000012, 0, 0)
        _BaseCloudsRemap ("Base Clouds Remap", Vector) = (-0.400000006, 1.71000004, 0.0799999982, 0.699999988)
        _CloudsA ("Clouds A", 2D) = "white" {}
        _CloudsAScale ("Clouds A Scale", Float) = 0.5
        _CloudsASpeed ("Clouds A Speed", Vector) = (0.200000003, 0.100000001, 0, 0)
        _CloudsB ("Clouds B", 2D) = "white" {}
        _CloudsBScale ("Clouds B Scale", Float) = 1
        _CloudsBSpeed ("Clouds B Speed", Vector) = (-0.150000006, -1, 0, 0)
        _DetailCloudsPower ("Detail Clouds Power", Float) = 1
        _DetailCloudsRemap ("Detail Clouds Remap", Vector) = (0.159999996, 2, -0.200000003, 1)
        _SmoothstepMin ("Smoothstep Min", Float) = 0
        _SmoothstepMax ("Smoothstep Max", Float) = 0.600000024
        _ColorValleys ("Color Valleys", Color) = (0.484607875, 0.554144025, 0.630999923, 0)
        _ColorPeaks ("Color Peaks", Color) = (0.899999976, 0.941666722, 1, 0)
        _Color ("Tint", Color) = (0.885319173, 0.849174976, 0.812995434, 1)
        _EmissionStrength ("Emission Strength", Float) = 0.25
        _FresnelPower ("Fresnel Power", Float) = 2.20000005
        _FresnelScale ("Fresnel Scale", Float) = 2
        _FresnelOpacity ("Fresnel Opacity", Float) = 0.349999994
        _DisplacementHeight ("Displacement Height", Float) = 1.5
        _FadePower ("Fade Power", Float) = 2
        _BaseScale ("Base Scale", Float) = 3
        _FadeDistanceOutside ("Fade Distance Outside", Float) = 0
        _FadeDistanceEdge ("Fade Distance Edge", Float) = 3
        _FadeDistanceHalf ("Fade Distance Half", Float) = 3
        _FadeDistanceFull ("Fade Distance Full", Float) = 6
        _OffsetOutside ("Offset Outside", Float) = 0.5
        _OffsetEdge ("Offset Edge", Float) = 1
        _OffsetHalf ("Offset Half", Float) = 3
        _OffsetFull ("Offset Full", Float) = 6
        _AlphaHalf ("Alpha Half", Float) = 1
        _CloudsOpacityHalf ("Clouds Opacity Half", Float) = 0.800000012
        _AlphaOuter ("Alpha Outer", Float) = 0
        _CloudsOpacityOuter ("Clouds Opacity Outer", Float) = 0.800000012
        _AlphaFull ("Alpha Full", Float) = 1
        _DarkTint ("Dark Tint", Color) = (0.34587121, 0.363916665, 0.397000015, 0)
        _AlphaPower ("Alpha Power", Float) = 0.5
        _AlphaMultiply ("Alpha Multiply", Float) = 1
        _DarkTintFalloff ("Dark Tint Falloff", Float) = 0.800000012
        _DirectionalMaskClouds ("Directional Mask Clouds", Float) = 0.349999994
        _DirectionlessMaskIntensity ("DirectionlessMaskIntensity", Float) = 0.5
        _DirectionIntensity ("_DirectionIntensity", Float) = 1
        _DirectionColor ("_DirectionColor", Color) = (1, 1, 1, 1)
        _Direction ("_Direction", Vector) = (1, 1, 1, 0)
        _DirectionFalloff ("_DirectionFalloff", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature_local _ FOG_MOBILE
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseClouds); SAMPLER(sampler_BaseClouds);
            TEXTURE2D(_CloudsA); SAMPLER(sampler_CloudsA);
            TEXTURE2D(_CloudsB); SAMPLER(sampler_CloudsB);
            TEXTURE2D(g_FogOfWar_Mask); SAMPLER(sampler_g_FogOfWar_Mask);
            float2 g_FogOfWar_Origin;
            float g_FogOfWar_Size;

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseCloudsRemap, _DetailCloudsRemap, _BaseCloudsSpeed, _CloudsASpeed, _CloudsBSpeed;
            float4 _ColorValleys, _ColorPeaks, _DarkTint, _Color, _DirectionColor, _Direction;
            float _BaseSpeed, _BaseScale, _BaseCloudsScale, _BaseCloudsIntensity, _CloudsAScale, _CloudsBScale;
            float _DetailCloudsPower, _SmoothstepMin, _SmoothstepMax, _DisplacementHeight;
            float _OffsetOutside, _OffsetEdge, _OffsetHalf, _OffsetFull;
            float _AlphaHalf, _AlphaFull, _AlphaOuter, _CloudsOpacityOuter, _CloudsOpacityHalf;
            float _AlphaMultiply, _AlphaPower, _FadeDistanceOutside, _FadeDistanceEdge, _FadeDistanceHalf, _FadeDistanceFull, _FadePower;
            float _FresnelPower, _FresnelScale, _FresnelOpacity, _DarkTintFalloff, _DirectionIntensity;
            float _EmissionStrength, _DirectionFalloff, _DirectionalMaskClouds, _DirectionlessMaskIntensity;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };

            float Remap(float value, float4 range)
            {
                return (value - range.x) * (range.w - range.z) / (range.y - range.x) + range.z;
            }
            float CloudValue(float2 xz, bool vertexStage)
            {
                float2 uv = xz * (0.01 * _BaseScale);
                float scroll = _Time.y * _BaseSpeed;
                float2 aUV = uv * _CloudsAScale + _CloudsASpeed.xy * scroll;
                float2 bUV = uv * _CloudsBScale + _CloudsBSpeed.xy * scroll;
                float2 baseUV = uv * _BaseCloudsScale + _BaseCloudsSpeed.xy * scroll;
                float a, b, baseCloud;
                #if defined(FOG_MOBILE)
                if (vertexStage)
                {
                    baseCloud = SAMPLE_TEXTURE2D_LOD(_BaseClouds, sampler_BaseClouds, baseUV, 0).r;
                    // Mean detail of the source noise keeps the large-scale displacement level.
                    a = b = 0.64;
                }
                else
                {
                    baseCloud = SAMPLE_TEXTURE2D(_BaseClouds, sampler_BaseClouds, baseUV).r;
                    a = SAMPLE_TEXTURE2D(_CloudsA, sampler_CloudsA, aUV).r;
                    b = 0.64;
                }
                #else
                if (vertexStage)
                {
                    a = SAMPLE_TEXTURE2D_LOD(_CloudsA, sampler_CloudsA, aUV, 0).r;
                    b = SAMPLE_TEXTURE2D_LOD(_CloudsB, sampler_CloudsB, bUV, 0).r;
                    baseCloud = SAMPLE_TEXTURE2D_LOD(_BaseClouds, sampler_BaseClouds, baseUV, 0).r;
                }
                else
                {
                    a = SAMPLE_TEXTURE2D(_CloudsA, sampler_CloudsA, aUV).r;
                    b = SAMPLE_TEXTURE2D(_CloudsB, sampler_CloudsB, bUV).r;
                    baseCloud = SAMPLE_TEXTURE2D(_BaseClouds, sampler_BaseClouds, baseUV).r;
                }
                #endif
                float detail = pow(saturate((a+b)*0.5), _DetailCloudsPower);
                detail = smoothstep(_SmoothstepMin, _SmoothstepMax, abs(Remap(detail, _DetailCloudsRemap)));
                float density = Remap(baseCloud, _BaseCloudsRemap) * _BaseCloudsIntensity + detail;
                return max(0.1, smoothstep(0, _BaseCloudsIntensity, density));
            }
            float3 MaskBands(float2 xz)
            {
                float2 uv = (xz - g_FogOfWar_Origin) / g_FogOfWar_Size + 0.5;
                float mask = SAMPLE_TEXTURE2D_LOD(g_FogOfWar_Mask, sampler_g_FogOfWar_Mask, uv, 0).r;
                #if defined(FOG_MOBILE)
                    mask *= 3.0;
                #endif
                return smoothstep(0, 1, mask - float3(0, 1, 2));
            }
            float Layers(float outside, float edge, float halfVisible, float full, float3 bands)
            {
                return lerp(lerp(lerp(outside, edge, bands.x), halfVisible, bands.y), full, bands.z);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 pos = input.positionOS;
                float2 xz = TransformObjectToWorld(pos).xz;
                float cloud = CloudValue(xz, true);
                float offset = Layers(_OffsetOutside, _OffsetEdge, _OffsetHalf, _OffsetFull, MaskBands(xz));
                pos.y += lerp(pos.y - _DisplacementHeight, pos.y + _DisplacementHeight, cloud) - offset;
                o.world = TransformObjectToWorld(pos);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float cloud = CloudValue(input.world.xz, false);
                float3 bands = MaskBands(input.world.xz);
                float3 cameraRay = normalize(input.world - _WorldSpaceCameraPos);
                float3 view = unity_OrthoParams.w == 0 ? -cameraRay : UNITY_MATRIX_V[2].xyz;
                float fresnel = pow(max(0.0001, 1 - dot(normalize(input.normal), view)), _FresnelPower);
                fresnel = smoothstep(0, 1, fresnel * _FresnelScale) * cloud * _FresnelOpacity;
                float sun = pow(saturate(dot(cameraRay, _Direction.xyz) * 0.5 + 0.5), _DirectionFalloff);
                sun = lerp(_DirectionlessMaskIntensity, sun, _DirectionIntensity);
                sun = lerp(sun, sun * cloud, _DirectionalMaskClouds);
                float3 tint = lerp(_ColorValleys.rgb, _ColorPeaks.rgb, cloud) * _Color.rgb;
                float3 lit = lerp(tint, _DirectionColor.rgb, sun);
                float dark = saturate(pow(abs(1 - bands.y), _DarkTintFalloff));
                float3 darkLit = lit * _DarkTint.rgb;
                float3 rgb = lerp(tint + fresnel * (1 - sun), darkLit, dark)
                           + _EmissionStrength * lerp(lit, darkLit, dark);

                float reveal = Layers(0, _AlphaOuter - cloud * _CloudsOpacityOuter,
                                      _AlphaHalf - cloud * _CloudsOpacityHalf, _AlphaFull, bands);
                float fadeDistance = Layers(_FadeDistanceOutside, _FadeDistanceEdge, _FadeDistanceHalf, _FadeDistanceFull, bands);
                float depth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float gap = LinearEyeDepth(depth, _ZBufferParams) - LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float fade = fadeDistance == 0 ? 1 : saturate(abs(gap / fadeDistance));
                fade = smoothstep(0, 1, pow(fade, _FadePower));
                return float4(rgb, saturate(pow(abs((1 - reveal) * fade), _AlphaPower) * _AlphaMultiply));
            }
            ENDHLSL
        }
    }
}
