using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    CullingResults cullingResults;

    Lighting lighting = new Lighting();

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings) {
        this.context = context;
        this.camera = camera;

        
        PrepareBuffer();
        PrepareForSceneWindow();

        if (!cull(shadowSettings.maxDistance)) {
            return;
        }

        buffer.BeginSample(SampleName);
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);

        Setup();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();

        submit();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>是否进行了有效的剔除</returns>
    bool cull(float maxShadowDistance) {
        //Get The Culling parameters from desired Camera
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
            // Schedule the cull operation
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);// 最大阴影距离
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Setup() {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear) ;
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void submit() {
        buffer.EndSample(SampleName);

        ExecuteBuffer();
        context.Submit();

    }

    /// <summary>
    /// ExecuteCommandBuffer 执行缓存命令，本操作会复制缓冲区，但不会清楚缓冲区，如果后面需要重用缓冲区，那么需要执行clear()命令
    /// ScriptableRenderContext 会将 commandBuffer 参数注册到自己要执行的命令内部列表中
    /// </summary>
    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 绘制顺序 不透明物件 -> 天空盒 -> 透明物件
    /// 减少绘制天空盒时候绘制像素的数量
    /// DrawRenderers 取决于CommandBuffer 中指定的管线状态，所以需要确保在DrawRenderers 之前调用ExecuteCommandBuffer
    /// </summary>
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings) { 
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
}
