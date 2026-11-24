Shader "Echo/Cabin Overcast Sky"
{
    Properties
    {
        [NoScaleOffset] _Clouds("Storm cubemap", Cube) = "gray" {}
        _NightAmount("Night amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            TEXTURECUBE(_Clouds);
            SAMPLER(sampler_Clouds);
            CBUFFER_START(UnityPerMaterial)
                half4 _Clouds_HDR;
                half _NightAmount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.direction = input.positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 direction = normalize(input.direction);
                half4 encoded = SAMPLE_TEXTURECUBE(_Clouds, sampler_Clouds, direction);
                half3 decoded = DecodeHDREnvironment(encoded, _Clouds_HDR);
                half luminance = dot(decoded, half3(0.2126, 0.7152, 0.0722));
                half light = saturate(pow(max(luminance, 0.001), 0.48) * 0.89);
                half3 clouds = lerp(half3(0.135, 0.18, 0.21), half3(0.49, 0.52, 0.53), light);
                half horizon = pow(1 - saturate(direction.y), 3.5);
                half3 daySky = lerp(clouds, half3(0.25, 0.31, 0.33), horizon * 0.83);
                half3 nightClouds = lerp(half3(0.004, 0.008, 0.018), half3(0.025, 0.040, 0.067), light);
                half3 nightSky = lerp(nightClouds, half3(0.016, 0.024, 0.044), horizon * 0.77);
                return half4(lerp(daySky, nightSky, _NightAmount), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
