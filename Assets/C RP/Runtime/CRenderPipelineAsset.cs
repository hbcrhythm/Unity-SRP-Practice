using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/CRP Render Pipeline")]
public class CRenderPipelineAsset : RenderPipelineAsset { 
    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useLightsPerObject = true, useSRPBatcher = true;

    [SerializeField]
    ShadowSettings shadow = default;

    protected override RenderPipeline CreatePipeline() {
        return new CustomRenderPipeline(
            useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, shadow
        );
    }
}
