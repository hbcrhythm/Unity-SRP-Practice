using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting {
    
    const string bufferName = "Lighting";

    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    static string lightsPerObjectKeyworld = "_LIGHT_PER_OBJECT";

    static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData"),
        dirTest = Shader.PropertyToID("_DirTest");

    static int
        otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightAnglesId = Shader.PropertyToID("_OtherLightAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");


    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    static Vector4[]
        otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount],
        otherLightAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject) {
        this.cullingResults = cullingResults;

        buffer.BeginSample(bufferName);

        shadows.Setup(context, cullingResults, shadowSettings);

        SetupLights(useLightsPerObject);

        shadows.Render();

        buffer.EndSample(bufferName);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights(bool useLightsPerObject) {

        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;


        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++) {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType) {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionLight(dirLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount) 
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, ref visibleLight);
                    }
                    break;
            }
            if (useLightsPerObject) {
                indexMap[i] = newIndex;
            }
        }

        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyworld);
        }
        else {
            Shader.DisableKeyword(lightsPerObjectKeyworld);
        }

     
        buffer.SetGlobalFloat(dirTest, 10f);
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0){
            buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

        buffer.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0) {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            //这里是spot需要
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightAnglesId, otherLightAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }

    }

    /// <summary>
    /// visibleLight很大，这里改为引用传递而不是值传递，这样不会复制它
    /// </summary>
    /// <param name="index"></param>
    /// <param name="visibleLight"></param>
    void SetupDirectionLight(int index, ref VisibleLight visibleLight) {
        //buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
        //buffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }


    void SetupPointLight(int index, ref VisibleLight visibleLight) {
        otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightAngles[index] = new Vector4(0f, 1f);
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);
    }

    void SetupSpotLight(int index, ref VisibleLight visiblelight) {
        otherLightColors[index] = visiblelight.finalColor;
        Vector4 position = visiblelight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visiblelight.range * visiblelight.range, 0.00001f);
        otherLightPositions[index] = position;
        otherLightDirections[index] = -visiblelight.localToWorldMatrix.GetColumn(2);

        // a = 1 / cos(ri/2) - cos(ro/2)
        // b = -cos(ro/2)*a
        // saturate(da + b)^2

        Light light = visiblelight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visiblelight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos);
        otherLightAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, index);

    }

    public void Cleanup() {
        shadows.Cleanup();
    }
}
