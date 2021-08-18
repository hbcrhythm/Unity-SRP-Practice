#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

#define MIN_REFLECTIVITY 0.04

//实际上，非金属也会有一点点的反射率，平均约为0.04
float OneMinusReflectivity(float metallic){
	float range = 1.0 - MIN_REFLECTIVITY; //金属通过镜面反射反射所有光，并且漫反射为零
	return range - metallic * range;
}

BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false){
	BRDF brdf;

	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	brdf.diffuse = surface.color * oneMinusReflectivity;
	
	if(applyAlphaToDiffuse){
		brdf.diffuse *= surface.alpha;
	}

	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);//金属影响镜面反射的颜色，非金属镜面反射应该是白色

	//感知粗糙度，更直观的视角效果，方便美工和开发人员的理解 α = perceptualRoughness ^ 2
	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);

	return brdf;
}

//使用URP中相同的公式, Minimalist CookTorrance BRDF
//GGX 分布乘以可见性和菲涅耳的组合近似值
//请参阅 Siggraph 2015 移动移动图形课程的"优化移动PBR"
//https://community.arm.com/events/1155
//镜面反射的强度
float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.0001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}

//直接光 = 直接光的漫反射+直接光的镜面反射
float3 DirectBRDF(Surface surface, BRDF brdf, Light light){
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular){
	float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
	// float3 reflection = specular * brdf.specular;
	reflection /= brdf.roughness * brdf.roughness + 1;
	return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif
