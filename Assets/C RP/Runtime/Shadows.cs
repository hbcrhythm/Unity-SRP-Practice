using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";

    static int 
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");

    //可投射的定向光数量
    const int maxShadowDirectionalLightCount = 4, maxCascades = 4;
    
    //没个级联都需要自己的阴影转换矩阵
    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowDirectionalLightCount * maxCascades];

    struct ShadowedDirectionalLight {
        public int visiableLightIndex;
    }

    //存储可投射阴影的可见光源的索引
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowDirectionalLightCount];

    int shadowedDirLightCount;

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings) {

        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        shadowedDirLightCount = 0;
    }

    public void Render() {
        if (shadowedDirLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    //渲染方向光阴影
    void RenderDirectionalShadows() {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);

        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        Debug.Log("tiles: " + tiles + " split: " + split + " tileSize: " + tileSize);
        for (int i = 0; i < shadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

        buffer.EndSample(bufferName);

        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize) {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, light.visiableLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visiableLightIndex, i, cascadeCount, ratios, tileSize, 0f, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            shadowDrawingSettings.splitData = splitData;

            int tileIndex = tileOffset + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, split);

            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);

            ExecuteBuffer();

            context.DrawShadows(ref shadowDrawingSettings);
        }
    }

    //从世界空间转换到阴影纹理空间矩阵，这里其实可以用矩阵乘法来做，但会导致大量和0之间的乘法和不必要的加法。
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    //设置渲染窗口偏移 0 0 256 256,256 0 256 256, 0 256 256 256, 256 256 256 256
    Vector2 SetTileViewport(int index, int split, float tileSize) {
        Vector2 offset = new Vector2(index % split, index / split);
        Rect rect = new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize);
        buffer.SetViewport(rect);
        return offset;
    }

    public void Cleanup() {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    void ExecuteBuffer() {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector2 ReserveDirectionalShadows(Light light, int visiableLightIndex) {
        if (shadowedDirLightCount < maxShadowDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0f && cullingResults.GetShadowCasterBounds(visiableLightIndex, out Bounds b)) {
            shadowedDirectionalLights[shadowedDirLightCount] =
            new ShadowedDirectionalLight { 
                visiableLightIndex = visiableLightIndex
            };
            return new Vector2(light.shadowStrength, settings.directional.cascadeCount * shadowedDirLightCount++);
        }
        return Vector2.zero;
    }
}
