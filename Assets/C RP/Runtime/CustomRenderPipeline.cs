using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    ShadowSettings shadowSettings;

    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstaning, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings) {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstaning;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;


        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;

        GraphicsSettings.lightsUseLinearIntensity = true; //将光照强度转换为线性空间，unity默认情况下不会将其转换为线性空间

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {

        foreach (Camera camera in cameras) {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings);
        }
    }
}