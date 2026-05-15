Shader "Custom/CameraDistortion"
{
    Properties
    {
        _GrainStrength ("Grain Strength", Range(0, 0.3)) = 0.06
        _NoiseStrength ("Shifting Noise Strength", Range(0, 0.2)) = 0.04
        _JitterStrength ("Horizontal Jitter", Range(0, 0.03)) = 0.006
        _ScanlineStrength ("Scanline Strength", Range(0, 0.5)) = 0.12
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "VideoTapeShader"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _GrainStrength;
            float _NoiseStrength;
            float _JitterStrength;
            float _ScanlineStrength;
            float _VignetteStrength;

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Tape wobble
                float lineNoise = rand(float2(floor(uv.y * 240.0), floor(_Time.y * 20.0)));
                float wobble = sin(uv.y * 90.0 + _Time.y * 8.0) * _JitterStrength;
                uv.x += wobble + (lineNoise - 0.5) * _JitterStrength;

                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Grain
                float grain = rand(uv * _ScreenParams.xy + _Time.y * 80.0);
                grain = (grain - 0.5) * _GrainStrength;
                col.rgb += grain;

                // Noise bands
                float noiseBand = rand(float2(floor(uv.y * 60.0), floor(_Time.y * 12.0)));
                col.rgb += (noiseBand - 0.5) * _NoiseStrength;

                // Scanlines
                float scanline = sin(uv.y * _ScreenParams.y * 1.5);
                col.rgb -= scanline * _ScanlineStrength * 0.05;

                // Red/blue separation
                float chromaOffset = 0.002;
                float red = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(chromaOffset, 0)).r;
                float blue = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - float2(chromaOffset, 0)).b;
                col.r = red;
                col.b = blue;

                // Vignette
                float2 centered = input.texcoord - 0.5;
                float vignette = dot(centered, centered);
                col.rgb *= 1.0 - vignette * _VignetteStrength;

                return col;
            }

            ENDHLSL
        }
    }
}