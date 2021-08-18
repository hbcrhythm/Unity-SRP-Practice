#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

// #include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

// TEXTURE2D(_BaseMap);
// SAMPLER(sampler_BaseMap);

// UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
// 	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
// 	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
// 	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
// 	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
// 	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
// UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	float4 tangentOS : TANGENT;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV;
	#if defined(_DETAIL_MAP)
	float2 detailUV : VAR_DETAIL_UV;
	#endif
	#if defined(_NORMAL_MAP)
	float4 tangentWS : VAR_TANGENT;
	#endif
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID		
};

Varyings LitPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	
	#if defined(_NORMAL_MAP)
	output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	#endif

	#if defined(_DETAIL_MAP)
	output.detailUV = TransformDetailUV(input.baseUV);
	#endif

	output.baseUV = TransformBaseUV(input.baseUV);

	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);

	ClipLOD(input.positionCS.xy, unity_LODFade.x);
	// #if defined(LOD_FADE_CROSSFADE)
	// 	return unity_LODFade.x;
	// #endif

	InputConfig config = GetInputConfig(input.baseUV);

	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
		
	#if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif

	float4 base = GetBase(config);

	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	Surface surface;
	surface.position = input.positionWS;
	// surface.normal = normalize(input.normalWS);

	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
		surface.interpolatedNormal = input.normalWS; //通常不需要在这里进行归一化了,大多数mesh顶点没有法线,如果三角形的"弯曲"?太大的话，可能会对阴影bias产生负面影响
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif

	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.xyz;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.occlusion = GetOcclusion(config);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif

 	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(config);

	return float4(color, surface.alpha);
}

#endif
