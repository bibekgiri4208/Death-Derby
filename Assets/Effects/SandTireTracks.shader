Shader "Death Derby/Sand Tyre Tracks"
{
    Properties
    {
        _BaseColor("Compaction Multiplier", Color) = (0.48, 0.43, 0.35, 1)
        _TreadSpacing("Tread Spacing (metres)", Float) = 0.15
        _TrackLifetime("Lifetime (seconds)", Float) = 75
        [HideInInspector] _TrackTime("Current Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Pass
        {
            Name "Tyre Imprints"
            Tags { "LightMode" = "UniversalForwardOnly" }
            // Darken the existing lit ground, including its shadows, rather than painting black stripes.
            Blend DstColor Zero
            ZWrite Off
            ColorMask RGB
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrackVertex
            #pragma fragment TrackFragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _TreadSpacing;
                float _TrackLifetime;
                float _TrackTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 timeAndSlip : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float2 timeAndSlip : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings TrackVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.timeAndSlip = input.timeAndSlip;
                output.fog = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }
            half4 TrackFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float u = input.uv.x;
                float phase = input.uv.y / max(_TreadSpacing, 0.02) + abs(u - 0.5) * 1.2;
                float aa = max(fwidth(phase), 0.025);
                float tread = 1 - smoothstep(0.45 - aa, 0.62 + aa, frac(phase));
                tread = lerp(tread, 0.65, saturate(aa * 2));
                float grooves = smoothstep(0.035, 0.1, abs(frac(u * 4 + 0.5) - 0.5));
                float imprint = lerp(0.22, 1, tread) * lerp(0.35, 1, grooves);
                // Sliding scrubs out individual tread blocks into a wider disturbed ribbon.
                imprint = lerp(imprint, 0.85, input.timeAndSlip.y);
                float edgeNoise = sin(input.uv.y * 37) * sin(input.uv.y * 13) * 0.018;
                float edge = smoothstep(0, 0.14, u + edgeNoise) * (1 - smoothstep(0.86, 1, u + edgeNoise));
                float age = saturate((_TrackTime - input.timeAndSlip.x) / max(_TrackLifetime, 1));
                float fade = 1 - smoothstep(0.65, 1, age);
                float opacity = input.color.a * imprint * edge * fade;
                half3 multiplier = lerp(half3(1, 1, 1), _BaseColor.rgb, opacity);
                return half4(MixFogColor(multiplier, half3(1, 1, 1), input.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
