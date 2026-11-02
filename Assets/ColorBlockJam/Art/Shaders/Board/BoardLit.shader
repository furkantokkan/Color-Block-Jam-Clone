Shader "ColorBlockJam/Board Lit"
{
    Properties
    {
        [NoScaleOffset] _BaseMap("Shared Color Atlas", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = 0.28
        _Specular("Specular", Range(0,1)) = 0.3
        [HideInInspector] _PaletteUV("Atlas UV Scale / Offset", Vector) = (1,0.5,0,0)
        [HideInInspector] _ClipPlane("Clip Plane", Vector) = (0,0,0,0)
        [HideInInspector] _ClipEnabled("Clip Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define _SPECULAR_COLOR 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            half _Smoothness;
            half _Specular;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Board)
            UNITY_DEFINE_INSTANCED_PROP(float4, _PaletteUV)
            UNITY_DEFINE_INSTANCED_PROP(float4, _ClipPlane)
            UNITY_DEFINE_INSTANCED_PROP(float, _ClipEnabled)
        UNITY_INSTANCING_BUFFER_END(Board)
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 fogAndVertexLight:TEXCOORD2;
            float2 uv:TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            float4 paletteUV = UNITY_ACCESS_INSTANCED_PROP(Board, _PaletteUV);
            output.uv = input.uv * paletteUV.xy + paletteUV.zw;
            output.fogAndVertexLight = half4(ComputeFogFactor(output.positionCS.z),VertexLighting(output.positionWS,output.normalWS));
            return output;
        }
        void ClipBoard(float3 positionWS)
        {
            float enabled = UNITY_ACCESS_INSTANCED_PROP(Board, _ClipEnabled);
            float4 plane = UNITY_ACCESS_INSTANCED_PROP(Board, _ClipPlane);
            clip(lerp(1.0, dot(float4(positionWS, 1), plane), enabled));
        }
        half4 ColorFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ClipBoard(input.positionWS);
            InputData data = (InputData)0;
            data.positionWS = input.positionWS;
            data.normalWS = NormalizeNormalPerPixel(input.normalWS);
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            data.bakedGI = SampleSH(data.normalWS);
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            data.shadowMask = half4(1,1,1,1);
            data.vertexLighting = input.fogAndVertexLight.yzw;
            SurfaceData surface = (SurfaceData)0;
            // Every colour is an atlas cell that _PaletteUV selects (DEC-202); there is no tint property.
            surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
            surface.specular = _Specular.xxx;
            surface.smoothness = _Smoothness;
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            surface.alpha = 1;
            half4 color = UniversalFragmentBlinnPhong(data, surface);
            color.rgb = MixFog(color.rgb, input.fogAndVertexLight.x);
            return color;
        }
        half4 DepthFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            ClipBoard(input.positionWS);
            return 0;
        }
        half4 NormalFragment(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            ClipBoard(input.positionWS);
            float3 normal = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(octNormal * .5 + .5)),0);
            #else
                return half4(normal,0);
            #endif
        }
        float3 _LightDirection;
        float3 _LightPosition;
        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction = normalize(_LightPosition-output.positionWS);
            #else
                float3 direction = _LightDirection;
            #endif
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
            #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #endif
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment ColorFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
