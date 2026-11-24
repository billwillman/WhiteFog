Shader "GoldenCloudVortex/Rolling painted cloud"
{
    Properties
    {
        [NoScaleOffset] _Painting("Painted cloud",2D)="white"{}
        [NoScaleOffset] _Flow("Painted cloud flow and lightning MatCap",2D)="gray"{}
        [HideInInspector] _PassageMode("Passage mode",Float)=0
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
                float _PassageMode;
                float4 _Storm;
            CBUFFER_END
            #define VORTEX_ART(uv) SAMPLE_TEXTURE2D(_Painting,sampler_Painting,uv).rgb
            #define VORTEX_FLOW(uv) SAMPLE_TEXTURE2D(_Flow,sampler_Flow,uv)
            #define VORTEX_FRAC(x) frac(x)
            #define VORTEX_MIX(a,b,t) lerp(a,b,t)
            // BEGIN SHARED CLOUD CODE
            // Lower the foreground shelf in screen space. Its silhouette stays fixed in time.
            float3 ShapeCloudBank(float3 localPos, float2 uv)
            {
                float sides=smoothstep(.055,.46,abs(uv.x-.5));
                float shelf=smoothstep(-.15,.14,uv.y)*(1.0-smoothstep(.22,.62,uv.y));
                localPos.y-=localPos.z*.092*sides*shelf;
                return localPos;
            }

            #ifndef VORTEX_VERTEX
            float2 SampleClockwiseOrbit(float2 uv, float2 radius, float angle)
            {
                // Backtrace counterclockwise in y-up painting UV. Both taps stay on
                // the same elliptical band. The trace remains below 0.56 radians.
                float squared=angle*angle;
                float sine=angle*(1.0-squared/6.0);
                float cosineMinusOne=squared*(-.5+squared/24.0);
                const float axisRatio=1.28; // .72 screen-space height times 16:9
                return uv+float2(radius.x*cosineMinusOne-radius.y*sine/axisRatio,
                                 radius.x*sine*axisRatio+radius.y*cosineMinusOne);
            }

            // Each axial section projects the same painted banks at its own apparent
            // distance. Its painted highlights and shadows are transported together.
            float2 PassageSectionUV(float2 uv, float2 orbitRadius, float turn, float wall, float offset, float variant)
            {
                float angle=turn*wall*(1.35*offset+.09*variant);
                float2 rolled=SampleClockwiseOrbit(uv,orbitRadius,angle);
                float2 throat=float2(.5,.35);
                float scale=exp2(-.70*wall*offset);
                float2 source=throat+(rolled-throat)*scale;
                // Reflect the narrow overscan at the frame edges to avoid stretched texels.
                return 1.0-abs(1.0-abs(source));
            }

            float3 RolledCloud(float2 uv, float2 normalXY, float depth, float cycle, float passageMode, float4 storm)
            {
                float3 flow=VORTEX_FLOW(uv).rgb;
                float2 radius=uv-float2(.5,flow.g);
                float distanceToOpening=length((uv-float2(.5,.35))*float2(1.7777778,1.0));
                float2 firstUV,secondUV;
                float weight;
                if(passageMode>.5)
                {
                    float wall=smoothstep(.08,.40,distanceToOpening);
                    float phase=VORTEX_FRAC(depth/32.0+3.0*cycle+.22*flow.b);
                    float next=VORTEX_FRAC(phase+.5);
                    firstUV=PassageSectionUV(uv,radius,flow.r,wall,phase-.5,0.0);
                    secondUV=PassageSectionUV(uv,radius,flow.r,wall,next-.5,1.0);
                    weight=smoothstep(.20,.80,abs(2.0*phase-1.0));
                }
                else
                {
                    float phase=VORTEX_FRAC(6.0*cycle+flow.b);
                    float next=VORTEX_FRAC(phase+.5);
                    firstUV=SampleClockwiseOrbit(uv,radius,(phase-.5)*flow.r);
                    secondUV=SampleClockwiseOrbit(uv,radius,(next-.5)*flow.r);
                    weight=smoothstep(.18,.82,abs(2.0*phase-1.0));
                }
                float3 color=VORTEX_MIX(VORTEX_ART(firstUV),VORTEX_ART(secondUV),weight);
                if(passageMode>.5)
                {
                    color*=float3(.80,.76,.74);
                    color+=float3(0.0,.019,.052)*(1.0-smoothstep(.08,.54,distanceToOpening));
                }
                else
                {
                    float ivory=1.0-smoothstep(.08,.71,distanceToOpening);
                    color+=float3(.003,.071,.153)*ivory*(.40+.60*color.g);
                    color*=1.0-.15*smoothstep(.57,.87,uv.x)*smoothstep(.52,.84,uv.y);
                }
                if(storm.w>0.0)
                {
                    // Deform the pocket along the painted folds. Retain the relief's
                    // original luminance variations when a warm bank changes to violet.
                    float2 d=(uv-storm.xy)*float2(1.7777778,1.0)/storm.z;
                    d+=.23*normalXY;
                    float halo=max(0.0,(exp2(-1.55*dot(d,d))-.045)/.955);
                    float cap=VORTEX_FLOW(normalXY*.47+.5).a;
                    float luma=dot(color,float3(.2126,.7152,.0722));
                    float pocket=halo*storm.w*(1.0-.78*smoothstep(.56,.94,luma));
                    float crest=smoothstep(.35,.95,cap+.16*normalXY.y);
                    float strength=pocket*(.54+.50*crest);
                    float3 violet=VORTEX_MIX(float3(.54,.23,.88),float3(.84,.55,1.0),crest);
                    violet/=dot(violet,float3(.2126,.7152,.0722));
                    float chroma=.88*smoothstep(.015,.80,strength);
                    color=VORTEX_MIX(color,luma*violet,chroma);
                    color+=violet*(.55+.21*crest)*strength*(1.0-.45*luma);
                }
                return color;
            }

            // Retain the fixed-camera and passage entry points used by older wrappers.
            float3 RolledCloud(float2 uv, float2 normalXY, float cycle, float4 storm)
            {
                return RolledCloud(uv,normalXY,0.0,cycle,0.0,storm);
            }

            float3 RolledCloud(float2 uv, float2 normalXY, float depth, float cycle, float4 storm)
            {
                return RolledCloud(uv,normalXY,depth,cycle,1.0,storm);
            }
            #endif
            // END SHARED CLOUD CODE
            struct Attributes {float3 pos:POSITION;float2 uv:TEXCOORD0;float2 relief:TEXCOORD2;};
            struct Varyings {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 relief:TEXCOORD1;float depth:TEXCOORD2;};
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.pos=TransformObjectToHClip(ShapeCloudBank(i.pos,i.uv));
                o.uv=i.uv;o.relief=i.relief;o.depth=i.pos.z;
                return o;
            }
            half4 Frag(Varyings i):SV_Target {return half4(RolledCloud(i.uv,i.relief,i.depth,_Cycle,_PassageMode,_Storm),1);}
            ENDHLSL
        }
    }
}
