Shader "Death Derby/Training Sand"
{
    Properties
    {
        _BaseColor("Sunlit Sand", Color) = (0.78, 0.55, 0.29, 1)
        _LowColor("Compacted Sand", Color) = (0.56, 0.35, 0.16, 1)
        _PatchScale("Colour Patch Size (metres)", Range(2, 80)) = 24
        _RippleScale("Wind Ripples Per Metre", Range(0.5, 5)) = 1.8
        _RippleHeight("Ripple Relief (metres)", Range(0, 0.04)) = 0.012
        _GrainStrength("Fine Grain", Range(0, 1)) = 0.35
        _Smoothness("Smoothness", Range(0, 0.4)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "UniversalMaterialType" = "Lit" "TerrainCompatible" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _LowColor;
            float _PatchScale;
            float _RippleScale;
            float _RippleHeight;
            half _GrainStrength;
            half _Smoothness;
        CBUFFER_END

        float SandHash(float2 p)
        {
            float3 q = frac(float3(p.xyx) * 0.1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }

        float SandNoise(float2 p)
        {
            float2 cell = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(SandHash(cell), SandHash(cell + float2(1, 0)), f.x),
                        lerp(SandHash(cell + float2(0, 1)), SandHash(cell + 1), f.x), f.y);
        }

        // World-space patterns do not stretch with the terrain UVs or repeat in tiles.
        void SandSurface(float3 positionWS, half3 geometricNormal, out half3 albedo, out half3 normalWS)
        {
            float2 p = positionWS.xz;
            float broad = SandNoise(p / _PatchScale);
            float mottling = SandNoise(p * 0.29 + 17.3);
            float patch = saturate(0.2 + broad * 0.65 + (mottling - 0.5) * 0.22);

            float warp = SandNoise(p * 0.18 + 8.7);
            float phase = (dot(p, float2(0.857, 0.515)) * _RippleScale + warp * 1.8) * TWO_PI;
            // Suppress sub-pixel detail at distance instead of allowing it to shimmer.
            float rippleFade = 1.0 - smoothstep(0.8, 3.0, fwidth(phase));
            float ripple = (sin(phase) + 0.22 * sin(phase * 2.0 + 0.6)) * rippleFade;
            float rippleMask = smoothstep(0.18, 0.8, mottling) * 0.7 + 0.3;
            float2 grainUV = p * 38.0;
            float grainFade = 1.0 - smoothstep(0.25, 0.8, max(fwidth(grainUV.x), fwidth(grainUV.y)));
            float grain = (SandNoise(grainUV) - 0.5) * grainFade * _GrainStrength;

            albedo = lerp(_LowColor.rgb, _BaseColor.rgb, patch);
            albedo *= 1.0 + ripple * rippleMask * 0.045 + grain * 0.2;

            // Surface-gradient bump mapping needs no mesh tangents (Unity terrains have none).
            // This changes lighting only: wheel contact and terrain geometry stay untouched.
            float height = ripple * rippleMask * _RippleHeight + grain * 0.002;
            float3 n = normalize(geometricNormal);
            float3 dx = ddx(positionWS);
            float3 dy = ddy(positionWS);
            float3 r1 = cross(dy, n);
            float3 r2 = cross(n, dx);
            float determinant = dot(dx, r1);
            float3 gradient = (ddx(height) * r1 + ddy(height) * r2) * sign(determinant);
            normalWS = normalize(max(abs(determinant), 0.000001) * n - gradient);
        }

        struct SandAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            // Terrain uses its primary UVs for both static and dynamic lightmaps.
            float2 terrainUV : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct SandVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 fogAndVertexLight : TEXCOORD2;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD4;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        SandVaryings SandVertex(SandAttributes input)
        {
            SandVaryings output = (SandVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = vertex.positionCS;
            output.positionWS = vertex.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fogAndVertexLight = half4(ComputeFogFactor(vertex.positionCS.z),
                VertexLighting(vertex.positionWS, output.normalWS));
            OUTPUT_LIGHTMAP_UV(input.terrainUV, unity_LightmapST, output.lightmapUV);
            OUTPUT_SH(output.normalWS, output.vertexSH);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.terrainUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertex);
            #endif
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            // Also works with the PC renderer's deferred path.
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SandVertex
            #pragma fragment SandFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            half4 SandFragment(SandVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo, normalWS;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    lighting.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.fogCoord = input.fogAndVertexLight.x;
                lighting.vertexLighting = input.fogAndVertexLight.yzw;
                #ifdef DYNAMICLIGHTMAP_ON
                    lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, normalWS);
                #else
                    lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                #endif
                lighting.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surface, lighting);
                #endif
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SandVertex
            #pragma fragment SandDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            half4 SandDepthNormals(SandVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo, normalWS;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex SandVertex
            #pragma fragment SandDepth
            #pragma multi_compile_instancing
            half SandDepth(SandVaryings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex SandMetaVertex
            #pragma fragment SandMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            SandVaryings SandMetaVertex(SandAttributes input)
            {
                SandVaryings output = SandVertex(input);
                output.positionCS = MetaVertexPosition(input.positionOS, input.terrainUV, input.terrainUV,
                    unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }

            half4 SandMetaFragment(SandVaryings input) : SV_Target
            {
                half3 albedo, normalWS;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = albedo;
                return MetaFragment(meta);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
