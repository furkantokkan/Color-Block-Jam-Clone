Shader "ColorBlockJam/Ice"
{
Properties
{
[NoScaleOffset]_IceTexture("IceTexture", 2D) = "white" {}
_IceTextureScale("IceTextureScale", Float) = 0.45
_IceCracks("IceCracks", Float) = 0
_IceTint("IceTint", Color) = (0.24, 0.72, 0.96, 1)
_IceHighlight("IceHighlight", Color) = (0.82, 0.98, 1, 1)
_IceOpacity("IceOpacity", Float) = 0.9
_ClipPlane("ClipPlane", Vector) = (0, 0, 0, 0)
_ClipEnabled("ClipEnabled", Float) = 0
[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
[HideInInspector]_QueueControl("_QueueControl", Float) = -1
[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader
{
Tags
{
"RenderPipeline"="UniversalPipeline"
"RenderType"="Transparent"
"UniversalMaterialType" = "Lit"
"Queue"="Transparent"
"DisableBatching"="False"
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="UniversalLitSubTarget"
}
Pass
{
    Name "Universal Forward"
    Tags
    {
        "LightMode" = "UniversalForward"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _FORWARD_PLUS
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define _FOG_FRAGMENT 1
#define _SURFACE_TYPE_TRANSPARENT 1
#define _RECEIVE_SHADOWS_OFF 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP3;
#endif
 float4 tangentWS : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float3 positionWS : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4 = _IceTint;
float4 _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4 = _IceHighlight;
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float4 _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4;
Unity_Lerp_float4(_Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4, _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4, (_Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float.xxxx), _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4);
float _Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float = float(0.12);
float _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float;
Unity_Add_float(_Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float);
float4 _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4, (_Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float.xxxx), _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4);
float _Float_be16271c13b2415eadd5bc3bdf72dc30_Out_0_Float = float(0.72);
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.BaseColor = (_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Float_be16271c13b2415eadd5bc3bdf72dc30_Out_0_Float;
surface.Occlusion = float(1);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "GBuffer"
    Tags
    {
        "LightMode" = "UniversalGBuffer"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma exclude_renderers gles gles3 glcore
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
#pragma multi_compile_fragment _ DEBUG_DISPLAY
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_GBUFFER
#define _FOG_FRAGMENT 1
#define _SURFACE_TYPE_TRANSPARENT 1
#define _RECEIVE_SHADOWS_OFF 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP3;
#endif
 float4 tangentWS : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float3 positionWS : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4 = _IceTint;
float4 _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4 = _IceHighlight;
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float4 _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4;
Unity_Lerp_float4(_Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4, _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4, (_Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float.xxxx), _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4);
float _Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float = float(0.12);
float _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float;
Unity_Add_float(_Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float);
float4 _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4, (_Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float.xxxx), _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4);
float _Float_be16271c13b2415eadd5bc3bdf72dc30_Out_0_Float = float(0.72);
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.BaseColor = (_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Float_be16271c13b2415eadd5bc3bdf72dc30_Out_0_Float;
surface.Occlusion = float(1);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthNormals"
    Tags
    {
        "LightMode" = "DepthNormals"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD1
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALS


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float3 positionWS : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 NormalTS;
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ EDITOR_VISUALIZATION
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define _FOG_FRAGMENT 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 texCoord1;
 float4 texCoord2;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 texCoord1 : INTERP1;
 float4 texCoord2 : INTERP2;
 float3 positionWS : INTERP3;
 float3 normalWS : INTERP4;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.texCoord2.xyzw = input.texCoord2;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.texCoord2 = input.texCoord2.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4 = _IceTint;
float4 _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4 = _IceHighlight;
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float4 _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4;
Unity_Lerp_float4(_Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4, _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4, (_Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float.xxxx), _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4);
float _Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float = float(0.12);
float _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float;
Unity_Add_float(_Float_b73a8b7f7a17463cbead2039a86b55a9_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float);
float4 _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4, (_Add_dcb124faf63447b0bae724323ccf661c_Out_2_Float.xxxx), _Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4);
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.BaseColor = (_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4.xyz);
surface.Emission = (_Multiply_db655740109741468f72ba38271bb956_Out_2_Vector4.xyz);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENESELECTIONPASS 1
#define ALPHA_CLIP_THRESHOLD 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS : INTERP0;
 float3 normalWS : INTERP1;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1
#define ALPHA_CLIP_THRESHOLD 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS : INTERP0;
 float3 normalWS : INTERP1;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Universal 2D"
    Tags
    {
        "LightMode" = "Universal2D"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_2D


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS : INTERP0;
 float3 normalWS : INTERP1;
#if UNITY_ANY_INSTANCING_ENABLED
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _IceTexture_TexelSize;
float _IceTextureScale;
float _IceCracks;
float4 _IceTint;
float4 _IceHighlight;
float _IceOpacity;
float4 _ClipPlane;
float _ClipEnabled;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_IceTexture);
SAMPLER(sampler_IceTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
{
    Out = smoothstep(Edge1, Edge2, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 _Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4 = _IceTint;
float4 _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4 = _IceHighlight;
float _Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float = float(0.14);
float _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float = float(0.34);
UnityTexture2D _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_IceTexture);
float2 _Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2 = IN.WorldSpacePosition.xz;
float _Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float = _IceTextureScale;
float2 _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2;
Unity_Multiply_float2_float2(_Swizzle_f813d8b941c74078af0eed167f92de7c_Out_1_Vector2, (_Property_0b33b7db13234091aed4c30b0acc760e_Out_0_Float.xx), _Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2);
float4 _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.tex, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.samplerstate, _Property_6158042d6acf474abc6028b526ca8d1c_Out_0_Texture2D.GetTransformedUV(_Multiply_acc9041816824a04836865024bb17b9d_Out_2_Vector2) );
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.r;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_G_5_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.g;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_B_6_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.b;
float _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_A_7_Float = _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_RGBA_0_Vector4.a;
float _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float;
Unity_Smoothstep_float(_Float_c9e9c6afec0049a38245a2f77af35980_Out_0_Float, _Float_8d63460b943e4ea3aa49140ee6745fb7_Out_0_Float, _SampleTexture2D_4e2ca966b1f947ddbcde031fa90356bf_R_4_Float, _Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float);
float _Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float = float(0.65);
float _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float = float(1);
float _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float = _IceCracks;
float _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float;
Unity_Lerp_float(_Float_894e5d37ff844b7682f7463707737c9a_Out_0_Float, _Float_7c624619fe8647a3a644497a57c28a93_Out_0_Float, _Property_d49620210e2444ab93988375ba617f3c_Out_0_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float);
float _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float;
Unity_Multiply_float_float(_Smoothstep_12b0fb47dbc94bd28160884352aecf48_Out_3_Float, _Lerp_0df9d03520394473904219eaf7f182a8_Out_3_Float, _Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float);
float _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float = float(2.24);
float _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Float_a11a6b27fcdc4f319d72a94dc9ae4dd8_Out_0_Float, _FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float);
float _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float = float(0.7);
float _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float;
Unity_Multiply_float_float(_FresnelEffect_d8f97921a16b4695862a11d4f98bde4a_Out_3_Float, _Float_caa6263c115a439c938bd5ae5fedd7c5_Out_0_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float);
float _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float;
Unity_Add_float(_Multiply_553c95a8f3c1412697a131c29449f784_Out_2_Float, _Multiply_c0fcefa0fd2b46e6ad28086c4a4c3cf9_Out_2_Float, _Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float);
float _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float;
Unity_Saturate_float(_Add_7d163e5108e94eab94db8e41c85c4a19_Out_2_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float);
float4 _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4;
Unity_Lerp_float4(_Property_2e6bb992b8ce466fa8de114725320119_Out_0_Vector4, _Property_fff9e255b598466f87dddfe247959ae0_Out_0_Vector4, (_Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float.xxxx), _Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4);
float _Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float = float(0.88);
float _Float_2826d15ba2764424808f39c196552d83_Out_0_Float = float(1);
float _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float;
Unity_Lerp_float(_Float_110103ecb1e24aabb1e019455b5552c1_Out_0_Float, _Float_2826d15ba2764424808f39c196552d83_Out_0_Float, _Saturate_1b1dec69a014456ebf294ba16d391245_Out_1_Float, _Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float);
float _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float = _IceOpacity;
float _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float;
Unity_Multiply_float_float(_Lerp_5ff1e867468140bcb5dee739184a7205_Out_3_Float, _Property_183293a6f84e46e3945532d7c9161956_Out_0_Float, _Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float);
float _Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float = float(1);
float _Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float = float(0);
float4 _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4 = _ClipPlane;
float3 _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3 = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.xyz;
float _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float;
Unity_DotProduct_float3(IN.WorldSpacePosition, _Swizzle_af876dae9ebf4e82a0e608d49e875110_Out_1_Vector3, _DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float);
float _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float = _Property_a6c7fef8499449c1860e6f045e1b7ebe_Out_0_Vector4.w;
float _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float;
Unity_Add_float(_DotProduct_a54ddf758ddd4a44902ac495cc855b6d_Out_2_Float, _Swizzle_81660c3917854ff785ce1464c4e6b766_Out_1_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float);
float _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float;
Unity_Step_float(_Float_19faea0b7c7a4a16801589367b4e96b9_Out_0_Float, _Add_9738c722b9f743a3a9e672a91cb309b0_Out_2_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float);
float _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float = _ClipEnabled;
float _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float;
Unity_Lerp_float(_Float_1b51c4a8e2094afebd7f8511f4681261_Out_0_Float, _Step_24c9671431284aa7a281fbdd571c221c_Out_2_Float, _Property_29d35f23de214205b22795eacaa364b5_Out_0_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float);
float _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
Unity_Multiply_float_float(_Multiply_a221e68a61e4450798c64a1b9c3dfd6d_Out_2_Float, _Lerp_b34a3328ca134cbabe1810d45080f996_Out_3_Float, _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float);
surface.BaseColor = (_Lerp_48843f2df43f4461bfc496a8d378b081_Out_3_Vector4.xyz);
surface.Alpha = _Multiply_733223a379a04e749f55fb37f3b8b42a_Out_2_Float;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}