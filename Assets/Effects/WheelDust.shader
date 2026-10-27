Shader "Death Derby/Wheel Dust"
{
    Properties
    {
        _SoftParticlesEnabled("Soft Ground Intersections", Float) = 1
        _DepthFadeDistance("Intersection Softness", Float) = 0.25
        _Density("Cloud Optical Density", Range(0.5, 4)) = 2.2
        _SandBaseColor("Surface Sand Colour", Color) = (0.78, 0.55, 0.29, 1)
        _SandLowColor("Compacted Sand Colour", Color) = (0.65, 0.435, 0.225, 1)
        _SandPatchScale("Sand Patch Size", Float) = 32
        _SandSmoothness("Surface Smoothness", Float) = 0.08
        _ProceduralSand("Use Training Sand Palette", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Dust"
            Tags { "LightMode" = "SandDustVolume" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DustVertex
            #pragma fragment DustFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SoftParticlesEnabled;
                float _DepthFadeDistance;
                float _Density;
                half4 _SandBaseColor, _SandLowColor;
                float _SandPatchScale, _SandSmoothness, _ProceduralSand;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float CloudNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            Varyings DustVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;
                output.positionWS = vertex.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }

            half4 DustFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float billow = CloudNoise(input.uv * 4.3 + 7.1);
                float wisps = CloudNoise(input.uv * 10.7 + 23.6);
                float radius = length(p) + (billow - 0.5) * 0.24;
                float cloud = saturate(1 - smoothstep(0.12, 1, radius));
                float opticalDepth = cloud * sqrt(cloud) * lerp(0.55, 1, billow * 0.65 + wisps * 0.35)
                    * input.color.a * _Density;
                float eyeDepth = -TransformWorldToView(input.positionWS).z;
                if (_SoftParticlesEnabled > 0.5)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    opticalDepth *= saturate((sceneDepth - eyeDepth) / max(_DepthFadeDistance, 0.01));
                }
                opticalDepth *= saturate((eyeDepth - 0.3) / 0.7);
                // The mineral pigment is the terrain's palette, not an unrelated cream
                // particle colour. Match TrainingSand's broad colour variation and PBR
                // lighting, leaving out its microscopic ripple/grain normals.
                float2 ground = input.positionWS.xz;
                float scale = max(_SandPatchScale, 0.01);
                float broad = CloudNoise(ground / scale) * 0.7 + CloudNoise(ground / (scale * 0.43) + 41.7) * 0.3;
                float mottling = CloudNoise(ground * 0.29 + 17.3);
                float patch = saturate(0.2 + broad * 0.65 + (mottling - 0.5) * 0.14);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_SandLowColor.rgb, _SandBaseColor.rgb, lerp(1, patch, _ProceduralSand));
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                surface.smoothness = _SandSmoothness;
                surface.occlusion = 1;
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = half3(0, 1, 0);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.shadowMask = half4(1, 1, 1, 1);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                // A small intensity lift represents fine sand scattering above the ground;
                // unlike a white/cream tint, this does not change the mineral's hue.
                half3 color = MixFog(UniversalFragmentPBR(lighting, surface).rgb * lerp(1.0, 1.28, billow), input.fog);
                // Additive weighted colour + optical depth; colour blending happens only
                // once in SandDustComposite, after all dust layers have been accumulated.
                return half4(color * opticalDepth, opticalDepth);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
