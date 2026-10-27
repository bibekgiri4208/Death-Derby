Shader "Hidden/Death Derby/Sand Dust Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_SandDustVolume);

            // Perceptual colour interpolation avoids the olive intermediate produced by
            // linear-RGB yellow/blue blending. It is applied once to the combined volume,
            // not once per sprite, and leaves every dust-free pixel unchanged.
            float3 ToLab(float3 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    c = SRGBToLinear(max(c, 0));
                #endif
                float3 lms = mul(float3x3(0.4122214708, 0.5363325363, 0.0514459929,
                    0.2119034982, 0.6806995451, 0.1073969566,
                    0.0883024619, 0.2817188376, 0.6299787005), max(c, 0));
                lms = pow(max(lms, 0), 1.0 / 3.0);
                return mul(float3x3(0.2104542553, 0.7936177850, -0.0040720468,
                    1.9779984951, -2.4285922050, 0.4505937099,
                    0.0259040371, 0.7827717662, -0.8086757660), lms);
            }
            float3 FromLab(float3 c)
            {
                float3 lms = mul(float3x3(1, 0.3963377774, 0.2158037573,
                    1, -0.1055613458, -0.0638541728,
                    1, -0.0894841775, -1.2914855480), c);
                lms = lms * lms * lms;
                float3 colour = max(mul(float3x3(4.0767416621, -3.3077115913, 0.2309699292,
                    -1.2684380046, 2.6097574011, -0.3413193965,
                    -0.0041960863, -0.7034186147, 1.7076147010), lms), 0);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    colour = LinearToSRGB(colour);
                #endif
                return colour;
            }
            half4 Composite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float4 volume = SAMPLE_TEXTURE2D_X(_SandDustVolume, sampler_LinearClamp, uv);
                if (volume.a < 0.0001) return scene;
                float3 dust = max(volume.rgb / volume.a, 0);
                float opacity = 1 - exp2(-min(volume.a, 32) * 1.442695);
                float3 sceneLab = ToLab(scene.rgb);
                float3 dustLab = ToLab(dust);
                // Transmitted scenery loses chroma inside the cloud rather than mixing
                // its saturated blue directly into the mineral pigment as green.
                float chromaProduct = max(length(sceneLab.yz) * length(dustLab.yz), 0.000001);
                float opposedHue = saturate((1 - dot(sceneLab.yz, dustLab.yz) / chromaProduct) * 0.5);
                sceneLab.yz *= 1 - opacity * opposedHue;
                float3 colour = FromLab(lerp(sceneLab, dustLab, opacity));
                return half4(colour, scene.a + (1 - scene.a) * opacity);
            }
            ENDHLSL
        }
    }
}
