Shader "Death Derby/Training Sand"
{
    Properties
    {
        _BaseColor("Sunlit Sand", Color) = (0.78, 0.56, 0.31, 1)
        _LowColor("Compacted Sand", Color) = (0.69, 0.475, 0.25, 1)
        _PatchScale("Colour Patch Size (metres)", Range(2, 80)) = 24
        _RippleScale("Wind Ripples Per Metre", Range(0.5, 5)) = 2.1
        _RippleHeight("Ripple Relief (metres)", Range(0, 0.04)) = 0.0022
        _RippleContrast("Ripple Colour Contrast", Range(0, 0.08)) = 0.009
        _DetailFadeDistance("Fine Detail Fade Distance (metres)", Range(6, 40)) = 16
        _GrainStrength("Fine Grain", Range(0, 1)) = 0.28
        _Smoothness("Smoothness", Range(0, 0.4)) = 0.055
        [HideInInspector] _SandPatchMask("Runtime Sand Patch Mask", 2D) = "black" {}
        [HideInInspector] _SandPatchCoverage("Runtime Sand Deformation Coverage", 2D) = "black" {}
        [HideInInspector] _SandPatchWorld("Runtime Sand Patch Bounds", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SandPatchEnabled("Runtime Sand Patches Enabled", Float) = 0
        [HideInInspector] _SandPatchSurface("Runtime Sand Patch Surface", Float) = 0
        [HideInInspector] _SandBakedGI("Training Baked Indirect Light", 2D) = "black" {}
        [HideInInspector] _SandBakedDirection("Training Baked Light Direction", 2D) = "gray" {}
        [HideInInspector] _SandBakedWorld("Training Baked Ground Bounds", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SandBakedST("Training Baked Lightmap Transform", Vector) = (1, 1, 0, 0)
        [HideInInspector] _SandBakedEnabled("Training Baked Indirect Light Enabled", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "UniversalMaterialType" = "Lit" "TerrainCompatible" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_SandPatchMask); SAMPLER(sampler_SandPatchMask);
        TEXTURE2D(_SandPatchCoverage); SAMPLER(sampler_SandPatchCoverage);
        TEXTURE2D(_SandBakedGI); SAMPLER(sampler_SandBakedGI);
        TEXTURE2D(_SandBakedDirection);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _LowColor;
            float _PatchScale;
            float _RippleScale;
            float _RippleHeight;
            float _RippleContrast;
            float _DetailFadeDistance;
            half _GrainStrength;
            half _Smoothness;
            float4 _SandPatchWorld;
            float4 _SandPatchMask_TexelSize;
            half _SandPatchEnabled;
            half _SandPatchSurface;
            float4 _SandBakedWorld;
            float4 _SandBakedST;
            half _SandBakedEnabled;
        CBUFFER_END

        half SandDeformationCoverage(float2 uv)
        {
            if (any(uv < 0) || any(uv >= 1)) return 0;
            float2 tilePosition = uv * _SandPatchMask_TexelSize.zw;
            float2 tile = floor(tilePosition);
            // Sample texel centres explicitly: hardware point-sampler rounding at
            // a tile edge must not choose a different slot than the fine lookup.
            float2 lookupUV = (tile + 0.5) * _SandPatchMask_TexelSize.xy;
            float slot = floor(SAMPLE_TEXTURE2D_LOD(_SandPatchMask, sampler_SandPatchMask, lookupUV, 0).r * 255.0 + 0.5) - 1;
            if (slot < 0) return 0;
            // Matches the fixed 8 x 4 cache atlas; every slot contains 80 x 80 cells.
            float2 tileUV = (min(floor(frac(tilePosition) * 80.0), 79.0) + 0.5) / 80.0;
            float2 atlasUV = (float2(fmod(slot, 8.0), floor(slot / 8.0)) + tileUV) / float2(8, 4);
            return SAMPLE_TEXTURE2D_LOD(_SandPatchCoverage, sampler_SandPatchCoverage, atlasUV, 0).r;
        }

        void SandClipReplacedGround(float3 positionWS)
        {
            // Only runtime material copies enable this. The dense replacement mesh
            // draws the groove below the original surface instead of covering it up.
            if (_SandPatchEnabled > 0.5)
            {
                float2 uv = (positionWS.xz - _SandPatchWorld.xy) * _SandPatchWorld.zw;
                half replaced = SandDeformationCoverage(uv);
                if (_SandPatchSurface > 0.5)
                {
                    // Untouched portions of the mesh must not render in any pass.
                    clip(replaced - 0.5);
                    return;
                }
                // A clip discards all MSAA samples at once, but the replacement
                // mesh only covers some samples along its outer edge. Preserve
                // the original sand there instead of exposing sky-colored cracks.
                // Adjacent active tiles still form one continuous cutout.
                float2 footprint = max(abs(ddx(uv)) + abs(ddy(uv)), _SandPatchWorld.zw * 0.1);
                if (replaced > 0.5)
                {
                    replaced = min(replaced, SandDeformationCoverage(uv + footprint));
                    replaced = min(replaced, SandDeformationCoverage(uv + float2(footprint.x, -footprint.y)));
                    replaced = min(replaced, SandDeformationCoverage(uv + float2(-footprint.x, footprint.y)));
                    replaced = min(replaced, SandDeformationCoverage(uv - footprint));
                    clip(0.5 - replaced);
                }
            }
        }

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
            // Quintic interpolation keeps both slope and curvature continuous.
            // Grain relief should not reveal the underlying noise grid.
            f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
            return lerp(lerp(SandHash(cell), SandHash(cell + float2(1, 0)), f.x),
                        lerp(SandHash(cell + float2(0, 1)), SandHash(cell + 1), f.x), f.y);
        }

        float SandFilteredNoise(float2 p)
        {
            float footprint = max(length(ddx(p)), length(ddy(p)));
            return lerp(SandNoise(p), 0.5, smoothstep(0.3, 1.4, footprint));
        }

        // World-space patterns do not stretch with the terrain UVs or repeat in tiles.
        void SandSurface(float3 positionWS, half3 geometricNormal, out half3 albedo, out half3 normalWS, out half smoothness)
        {
            float2 p = positionWS.xz;
            float broad = SandFilteredNoise(p / _PatchScale) * 0.7 + SandFilteredNoise(p / (_PatchScale * 0.43) + 41.7) * 0.3;
            float mottling = SandFilteredNoise(p * 0.29 + 17.3);
            float patch = saturate(0.2 + broad * 0.65 + (mottling - 0.5) * 0.14);

            float warp = SandFilteredNoise(p * 0.18 + 8.7);
            float bend = SandFilteredNoise(p * 0.065 + 23.1);
            float phase = (dot(p, float2(0.857, 0.515)) * _RippleScale + (warp - 0.5) * 1.2 + (bend - 0.5) * 2.4) * TWO_PI;
            // Spatial filtering alone does not prevent a striped surface strobing past
            // a fast camera. Keep the ripples local, soft, and absent in the mid-distance.
            float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos);
            float detailFade = 1.0 - smoothstep(_DetailFadeDistance * 0.25, _DetailFadeDistance, distanceToCamera);
            float footprint = fwidth(phase);
            float rippleFade = (1.0 - smoothstep(0.25, 1.25, footprint)) * detailFade;
            float harmonicFade = 1.0 - smoothstep(0.15, 0.65, footprint);
            float ripple = (sin(phase) + 0.15 * sin(phase * 2.0 + 0.6) * harmonicFade) * rippleFade;
            // Irregular quiet patches interrupt the continuous parallel "conveyor belt" lines.
            float rippleMask = smoothstep(0.30, 0.75, mottling) * lerp(0.25, 1.0, bend);
            // Three independently filtered scales give fine sand some body at
            // driving height without turning individual grains into glitter.
            // Rotate the grain basis so its lattice cannot align with the tracks.
            float2 grainUV = float2(dot(p, float2(0.8, 0.6)), dot(p, float2(-0.6, 0.8)));
            float aggregate = (SandFilteredNoise(grainUV * 8.0 + 47.3) - 0.5)
                * (1.0 - smoothstep(5.0, 18.0, distanceToCamera));
            float grain = (SandFilteredNoise(grainUV * 42.0 + 9.2) - 0.5)
                * (1.0 - smoothstep(2.0, 10.0, distanceToCamera));
            float micrograin = (SandFilteredNoise(grainUV * 170.0 + 31.7) - 0.5)
                * (1.0 - smoothstep(1.0, 4.0, distanceToCamera));

            albedo = lerp(_LowColor.rgb, _BaseColor.rgb, patch);
            albedo *= 1.0 + ripple * rippleMask * _RippleContrast
                + (aggregate * 0.06 + grain * 0.16 + micrograin * 0.10) * _GrainStrength;
            smoothness = _Smoothness * lerp(1.0, lerp(0.75, 1.15, saturate(0.5 + aggregate + grain)), _GrainStrength);

            // Surface-gradient bump mapping needs no mesh tangents (Unity terrains have none).
            // This changes lighting only: wheel contact and terrain geometry stay untouched.
            float height = ripple * rippleMask * _RippleHeight
                + (aggregate * 0.0008 + grain * 0.0015 + micrograin * 0.00018) * _GrainStrength;
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
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
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
                SandClipReplacedGround(input.positionWS);
                half3 albedo, normalWS;
                half smoothness;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS, smoothness);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                surface.smoothness = smoothness;
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
                // Terrain and replacement tiles have very different triangle sizes.
                // Interpolated vertex fog exposes their rectangular boundaries;
                // evaluate fog from the fragment's world position on both surfaces.
                lighting.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1), input.fogAndVertexLight.x);
                lighting.vertexLighting = input.fogAndVertexLight.yzw;
                #ifdef DYNAMICLIGHTMAP_ON
                    lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, normalWS);
                #else
                    lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                #endif
                // Unity's terrain baker ignores layerless custom-shader terrains.
                // The editor bake proxy supplies this world-aligned indirect map.
                // Terrain and moving/recycled rut meshes sample the same texels;
                // this replaces ambient GI rather than adding light twice.
                if (_SandBakedEnabled > 0.5)
                {
                    float2 groundUV = (input.positionWS.xz - _SandBakedWorld.xy) * _SandBakedWorld.zw;
                    lighting.bakedGI = SampleDirectionalLightmap(
                        TEXTURE2D_ARGS(_SandBakedGI, sampler_SandBakedGI),
                        TEXTURE2D_ARGS(_SandBakedDirection, sampler_SandBakedGI),
                        saturate(groundUV), _SandBakedST, normalWS, true);
                }
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
                SandClipReplacedGround(input.positionWS);
                half3 albedo, normalWS;
                half smoothness;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS, smoothness);
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
            #pragma target 3.0
            #pragma vertex SandVertex
            #pragma fragment SandDepth
            #pragma multi_compile_instancing
            half SandDepth(SandVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SandClipReplacedGround(input.positionWS);
                return input.positionCS.z;
            }
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
                half smoothness;
                SandSurface(input.positionWS, input.normalWS, albedo, normalWS, smoothness);
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
            #pragma target 3.0
            #pragma vertex SandShadowVertex
            #pragma fragment SandShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float3 _LightPosition;

            struct SandShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            SandShadowVaryings SandShadowVertex(SandAttributes input)
            {
                SandShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - output.positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                    ApplyShadowBias(output.positionWS, normalWS, lightDirection)));
                return output;
            }

            half4 SandShadowFragment(SandShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SandClipReplacedGround(input.positionWS);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
