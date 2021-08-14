using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool showPresets;

    bool Clipping {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

    bool PremultiplyAlpha {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend {
        set => SetProperty("_DstBlend", (float)value);
    }

    enum ShadowMode { 
        On, Clip, Dither, Off
    }

    ShadowMode Shadows {
        set {
            if (SetProperty("_Shadows", (float)value)) {
                SetKeyworld("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyworld("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    bool ZWrite {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue {
        set {
            foreach (Material m in materials) {
                m.renderQueue = (int)value;
            }
        }
    }

    void SetShadowCasterPass() {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        if (shadows == null || shadows.hasMixedValue) {
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in materials) {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        BackedEmission();

        EditorGUILayout.Space();

        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
        }

    }

    // 透明物体的烘焙
    void CopyLightMappingProperties() {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);

        if (mainTex != null && baseMap != null) {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }

        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);

        if (color != null && baseColor != null) {
            color.colorValue = baseColor.colorValue;
        }

    }

    //设置烘焙自发光，将自发光烘焙到光照贴图和光照探针中
    void BackedEmission() {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck()) {
            foreach (Material m in editor.targets) {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }

    bool HasProperty(string name) => FindProperty(name, properties, false) != null;

    void SetProperty(string name, string keyworld, bool value) {
        if (SetProperty(name, value ? 1f : 0f)) {
            SetKeyworld(keyworld, value) ;
        }
    }

    bool SetProperty(string name, float value) {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null) {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    void SetKeyworld(string keyworld, bool enable) {
        if (enable)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyworld);
            }
        }
        else {
            foreach (Material m in materials) {
                m.DisableKeyword(keyworld);
            }
        }
    }

}
