Shader "Echo/Cabin Terrain"
{
    Properties
    {
        _GrassColor("Grass color", 2D) = "white" {}
        [Normal] _GrassNormal("Grass normal", 2D) = "bump" {}
        _PathColor("Trail color", 2D) = "white" {}
        [Normal] _PathNormal("Trail normal", 2D) = "bump" {}
        _GravelColor("Gravel color", 2D) = "white" {}
        [Normal] _GravelNormal("Gravel normal", 2D) = "bump" {}
        _RockColor("Rock color", 2D) = "white" {}
        [Normal] _RockNormal("Rock normal", 2D) = "bump" {}
        _NoiseMap("Texture variation", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassColor);  SAMPLER(sampler_GrassColor);
            TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_PathColor);   SAMPLER(sampler_PathColor);
            TEXTURE2D(_PathNormal);  SAMPLER(sampler_PathNormal);
            TEXTURE2D(_GravelColor); SAMPLER(sampler_GravelColor);
            TEXTURE2D(_GravelNormal);SAMPLER(sampler_GravelNormal);
            TEXTURE2D(_RockColor);   SAMPLER(sampler_RockColor);
            TEXTURE2D(_RockNormal);  SAMPLER(sampler_RockNormal);
            TEXTURE2D(_NoiseMap);    SAMPLER(sampler_NoiseMap);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 weights : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 weights : TEXCOORD2;
                half fog : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.weights = input.weights;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }

            half3 GroundColor(TEXTURE2D_PARAM(map, texSampler), float2 uv, half blend)
            {
                const float2x2 rotation = float2x2(0.61, -0.79, 0.79, 0.61);
                float2 second = mul(rotation, uv) * 0.72 + float2(6.49, 3.37);
                return lerp(SAMPLE_TEXTURE2D(map, texSampler, uv).rgb,
                    SAMPLE_TEXTURE2D(map, texSampler, second).rgb, blend);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float2 xz = input.positionWS.xz;
                half3 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, xz * 0.095).rgb;
                half broad = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, xz * 0.024 + 0.3).b;
                float2 uv = xz + (noise.rg - 0.5) * 0.61;
                half variant = smoothstep(0.20, 0.8, noise.b);
                half4 weight = max(input.weights, 0.0);
                weight /= max(dot(weight, half4(1,1,1,1)), 0.001);

                float2 rockUV = uv + normalize(normal.xz + 0.0001) * input.positionWS.y * 0.90;
                half3 turf = GroundColor(TEXTURE2D_ARGS(_GrassColor, sampler_GrassColor), uv * 0.58, variant);
                half3 trail = GroundColor(TEXTURE2D_ARGS(_PathColor, sampler_PathColor), uv * 0.74, variant);
                half3 gravel = GroundColor(TEXTURE2D_ARGS(_GravelColor, sampler_GravelColor), uv * 0.88, variant);
                half3 rock = GroundColor(TEXTURE2D_ARGS(_RockColor, sampler_RockColor), rockUV * 0.57, variant);
                half3 albedo = turf * weight.x * half3(0.56, 0.64, 0.51) +
                    trail * weight.y * half3(0.70, 0.63, 0.55) +
                    gravel * weight.z * half3(0.63, 0.59, 0.55) +
                    rock * weight.w * half3(0.70, 0.72, 0.69);
                albedo *= lerp(0.59, 1.12, saturate(broad * 0.7 + noise.b * 0.3));

                half2 bump =
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, uv * 0.58), 0.69).xy * weight.x +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_PathNormal, sampler_PathNormal, uv * 0.74), 0.78).xy * weight.y +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_GravelNormal, sampler_GravelNormal, uv * 0.88), 0.8).xy * weight.z +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, rockUV * 0.57), 0.7).xy * weight.w;
                float3 tangent = normalize(float3(normal.y, -normal.x, 0.0));
                float3 bitangent = normalize(cross(tangent, normal));
                normal = normalize(normal + tangent * bump.x + bitangent * bump.y);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = dot(weight, half4(0.17, 0.31, 0.23, 0.25));
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 0.87;
                surface.alpha = 1;

                InputData light = (InputData)0;
                light.positionWS = input.positionWS;
                light.normalWS = normal;
                light.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    light.shadowCoord = input.shadowCoord;
                #else
                    light.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                light.fogCoord = input.fog;
                light.vertexLighting = VertexLighting(input.positionWS, normal);
                light.bakedGI = SampleSH(normal);
                light.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                light.shadowMask = half4(1, 1, 1, 1);
                half4 color = UniversalFragmentPBR(light, surface);
                color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack Off
}
