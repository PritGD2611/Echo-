// URP triplanar terrain/bark shader with anti-tiling measures:
//  - world-space triplanar projection (no UV seams, no stretching)
//  - per-instance projection rotation (each tree/rock gets different tiling)
//  - low-frequency domain warp (bends straight tile edges)
//  - two-scale detail blend + macro noise variation (breaks visible repeats)
//  - height-map driven vertex displacement (real geometry relief)
//  - parallax occlusion mapping for per-pixel depth (fake geometry)
Shader "Echo/Forest Triplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [Normal] _NormalMap("Normal", 2D) = "bump" {}
        _ArmMap("ARM (AO / Roughness / Metallic)", 2D) = "white" {}
        _DispMap("Height / Displacement", 2D) = "gray" {}
        _NoiseMap("Macro Noise (RG = warp, B = macro)", 2D) = "gray" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)

        _BaseScale("Texture Scale (1 / meters)", Float) = 0.2
        _DetailScale("Detail Scale Multiplier", Range(0.05, 1)) = 0.37
        _DetailBlend("Detail Blend", Range(0, 1)) = 0.45
        _NormalScale("Normal Strength", Range(0, 3)) = 1

        _Smoothness("Smoothness Multiplier", Range(0, 1)) = 1
        _Metallic("Metallic Multiplier", Range(0, 1)) = 1
        _AoStrength("AO Strength", Range(0, 1)) = 1

        _MacroScale("Macro Noise Scale", Float) = 0.05
        _MacroStrength("Macro Variation", Range(0, 0.6)) = 0.25
        _Warp("Domain Warp (meters)", Range(0, 2)) = 0.4
        _InstanceVariation("Per-Instance Variation", Range(0, 1)) = 1

        _DispHeight("Vertex Displacement (meters)", Range(0, 1)) = 0
        _Parallax("Parallax Depth (meters)", Range(0, 0.6)) = 0
        [Toggle(_POM)] _PomOn("Parallax Occlusion Mapping", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _BaseScale;
            float _DetailScale;
            float _DetailBlend;
            float _NormalScale;
            half _Smoothness;
            half _Metallic;
            half _AoStrength;
            float _MacroScale;
            float _MacroStrength;
            float _Warp;
            float _InstanceVariation;
            float _DispHeight;
            float _Parallax;
        CBUFFER_END

        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
        TEXTURE2D(_ArmMap);     SAMPLER(sampler_ArmMap);
        TEXTURE2D(_DispMap);    SAMPLER(sampler_DispMap);
        TEXTURE2D(_NoiseMap);   SAMPLER(sampler_NoiseMap);

        // ------------------------------------------------------------ noise

        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        // --------------------------------------------------------- sampling

        float3 InstanceOrigin()
        {
            return float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
        }

        float3 RotateY(float3 p, float angle)
        {
            float s = sin(angle);
            float c = cos(angle);
            return float3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
        }

        half3 SampleBase(float3 p, float3 weights, float scale)
        {
            half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.yz * scale).rgb;
            half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz * scale).rgb;
            half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy * scale).rgb;
            return x * weights.x + y * weights.y + z * weights.z;
        }

        half3 SampleArm(float3 p, float3 weights, float scale)
        {
            half3 x = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, p.yz * scale).rgb;
            half3 y = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, p.xz * scale).rgb;
            half3 z = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, p.xy * scale).rgb;
            return x * weights.x + y * weights.y + z * weights.z;
        }

        void SampleNormal(float3 p, float3 weights, float scale, inout float3 normalWS)
        {
            half3 nx = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.yz * scale), _NormalScale);
            half3 ny = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xz * scale), _NormalScale);
            half3 nz = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xy * scale), _NormalScale);

            float3 tangentPerturb =
                weights.x * float3(0, nx.y, nx.x) +
                weights.y * float3(ny.x, 0, ny.y) +
                weights.z * float3(nz.x, nz.y, 0);

            normalWS = normalize(normalWS + tangentPerturb);
        }

        // ------------------------------------------------- height / relief

        // Vertex shaders have no derivatives, so the height lookup needs an
        // explicit mip level there.
        float SampleHeightVertex(float3 p, float3 weights, float scale)
        {
            float x = SAMPLE_TEXTURE2D_LOD(_DispMap, sampler_DispMap, p.yz * scale, 0).r;
            float y = SAMPLE_TEXTURE2D_LOD(_DispMap, sampler_DispMap, p.xz * scale, 0).r;
            float z = SAMPLE_TEXTURE2D_LOD(_DispMap, sampler_DispMap, p.xy * scale, 0).r;
            return x * weights.x + y * weights.y + z * weights.z;
        }

        float3 HeightWeights(float3 normalWS)
        {
            float3 w = pow(abs(normalWS), 4.0);
            return w / max(w.x + w.y + w.z, 1e-4);
        }

        // Real geometry: push the mesh vertices along the world normal so the
        // terrain silhouette follows the height map. Applied in every pass
        // (lit, shadow, depth) so lighting and shadows stay consistent.
        float3 ApplyVertexDisplacement(float3 positionWS, float3 normalWS)
        {
            if (_DispHeight <= 0.0001) return positionWS;

            float angle = Hash13(floor(InstanceOrigin() * 0.731)) * 6.2831853 * _InstanceVariation;
            float3 p = RotateY(positionWS, angle);
            float3 weights = HeightWeights(RotateY(normalWS, angle));

            float height = SampleHeightVertex(p, weights, _BaseScale);
            return positionWS + normalWS * ((height - 0.5) * _DispHeight);
        }

        // Fake geometry: parallax occlusion march on the dominant projection
        // axis, returning the world-space offset to apply before texturing.
        float3 ParallaxOffset(float3 p, float3 viewDirRotated, float3 weights, float scale)
        {
            int axis = 1;
            if (weights.x > weights.y && weights.x > weights.z) axis = 0;
            else if (weights.z > weights.y && weights.z > weights.x) axis = 2;

            float2 uv = axis == 1 ? p.xz : (axis == 0 ? p.yz : p.xy);
            float2 dir = axis == 1 ? viewDirRotated.xz : (axis == 0 ? viewDirRotated.yz : viewDirRotated.xy);
            float vAxis = axis == 1 ? viewDirRotated.y : (axis == 0 ? viewDirRotated.x : viewDirRotated.z);

            // Ray length through the height range, clamped at grazing angles.
            float2 marchDir = -dir / max(abs(vAxis), 0.2) * _Parallax;

            const int steps = 8;
            float layerDepth = 1.0 / steps;
            float2 delta = marchDir / steps;

            float depth = 0.0;
            float height = SAMPLE_TEXTURE2D(_DispMap, sampler_DispMap, uv * scale).r;

            [unroll]
            for (int i = 0; i < steps; i++)
            {
                if (depth >= height) break;
                uv += delta;
                depth += layerDepth;
                height = SAMPLE_TEXTURE2D(_DispMap, sampler_DispMap, uv * scale).r;
            }

            // One refinement step against the previous layer.
            float2 previous = uv - delta;
            float h1 = SAMPLE_TEXTURE2D(_DispMap, sampler_DispMap, previous * scale).r;
            float after = (depth - h1) / max(depth - height + layerDepth, 1e-4);
            uv = lerp(previous, uv, saturate(after));

            float2 offset = uv - (axis == 1 ? p.xz : (axis == 0 ? p.yz : p.xy));

            float3 result = 0;
            if (axis == 1) result.xz = offset;
            else if (axis == 0) result.yz = offset;
            else result.xy = offset;
            return result;
        }
        ENDHLSL

        // ------------------------------------------------------------- lit

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _TWO_SCALE
            #pragma shader_feature_local _POM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);

                float3 positionWS = ApplyVertexDisplacement(pos.positionWS, nrm.normalWS);

                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = nrm.normalWS;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 n = normalize(input.normalWS);

                // Per-instance rotation so every tree/rock projects differently.
                float3 origin = InstanceOrigin();
                float angle = Hash13(floor(origin * 0.731)) * 6.2831853 * _InstanceVariation;
                float3 p = RotateY(input.positionWS, angle);
                float3 nr = RotateY(n, angle);

                float3 weights = pow(abs(nr), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 1e-4);

                // Low-frequency warp: no straight tile seams on the ground.
                // Sampled from a small RGBA noise texture (RG = warp, B = macro),
                // which is far cheaper than evaluating fBm per pixel.
                half4 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.xz * _MacroScale);
                float2 warp = (noise.rg - 0.5) * 2.0;
                p.xz += warp * _Warp;
                p.y += (noise.b - 0.5) * _Warp * 0.35;

                half macroBig = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.xz * _MacroScale * 0.17 + 0.53).r;
                float macro = saturate(noise.b * 0.65 + macroBig * 0.35);

                float scaleA = _BaseScale;

                #ifdef _POM
                    // Per-pixel depth from the height map: offsets every texture
                    // lookup so grooves and cracks read as real geometry.
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float3 viewRotated = RotateY(viewDirWS, angle);
                    p += ParallaxOffset(p, viewRotated, weights, scaleA);
                #endif

                #ifdef _TWO_SCALE
                    half3 baseA = SampleBase(p, weights, scaleA);
                    half3 baseB = SampleBase(p + 41.7, weights, scaleA * _DetailScale);
                    float blend = saturate(_DetailBlend * (0.35 + 0.65 * macro));
                    half3 albedo = lerp(baseA, baseB, blend);
                #else
                    half3 albedo = SampleBase(p, weights, scaleA);
                #endif

                half3 arm = SampleArm(p, weights, scaleA);

                albedo *= _BaseColor.rgb * (1.0 + (macro - 0.5) * 2.0 * _MacroStrength);

                half occlusion = lerp(1.0, arm.r, _AoStrength);
                half smoothness = saturate((1.0 - arm.g) * _Smoothness);
                half metallic = saturate(arm.b * _Metallic);

                SampleNormal(p, weights, scaleA, n);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = metallic;
                surface.specular = 0;
                surface.smoothness = smoothness;
                surface.normalTS = float3(0, 0, 1);
                surface.emission = 0;
                surface.occlusion = occlusion;
                surface.alpha = 1;
                surface.clearCoatMask = 0;
                surface.clearCoatSmoothness = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.vertexLighting = 0;
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // --------------------------------------------------------- shadows

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
            {
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS = ApplyVertexDisplacement(positionWS, normalWS);
                o.positionCS = GetShadowPositionHClip(positionWS, normalWS);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------- depth only

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS = ApplyVertexDisplacement(positionWS, normalWS);
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
