#ifndef CUSTOM_LIGHT_INCLUED
#define CUSTOM_LIGHT_INCLUED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float _DirTest;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light{
	float3 color;
	float3 direction;
	float attenuation;
};

int GetDirectionLightCount(){
	return _DirectionalLightCount;
}

int GetOtherLightCount(){
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData){
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;

	return data;	
}

OtherShadowData GetOtherShadowData(int lightIndex, ShadowData shadowData){
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;

	return data;
}

Light GetDirectionLight(int index, Surface surfaceWS, ShadowData shadowData){
	Light light;

	light.color = _DirectionalLightColors[index].xyz;
	light.direction = _DirectionalLightDirections[index].xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	// light.attenuation = shadowData.cascadeIndex * 0.25;
	return light;
}

Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData){
	Light light;

	light.color = _OtherLightColors[index].xyz;
	float3 ray = _OtherLightPositions[index].xyz - surfaceWS.position;
	light.direction = normalize(ray); //注意点光源的光照方向

	// 1/d^2 光照随着距离衰减
	float distanceSqr = max(dot(ray, ray), 0.00001);

	//限制光照范围 max(0, 1-(d^2 / r^2)^2)^2
	float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));

	float4 spotAngles = _OtherLightAngles[index];

	//聚光角度 saturate(da + b)^2
	float spotAttenuation = saturate(dot(_OtherLightDirections[index].xyz, light.direction) * spotAngles.x + spotAngles.y);

	OtherShadowData otherShadowData = GetOtherShadowData(index, shadowData);

	light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) * spotAttenuation * rangeAttenuation / distanceSqr;

	return light;
}

#endif

