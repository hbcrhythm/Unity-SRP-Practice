using UnityEngine;


/// <summary>
/// SRP BATCHER, 在GPU上缓存了材质属性，这样如果数据没有发生改变，那么就不需要进行Set Pass Call， SRP Batcher是否即使物体使用了不同的材质，但是shader变种相同，就不会被打断，传统的合批要求同种材质
/// 测试GPU INSTANCING , 用来处理相同网格，不同材质的物件，降低DrawCall
/// </summary>
public class PerObjectMaterialProperties: MonoBehaviour{

    static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        cutoffId = Shader.PropertyToID("_Cutoff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness"),
        emissionColorId = Shader.PropertyToID("_EmissionColor");



    [SerializeField,ColorUsage(false, true)]
    Color emissionColor = Color.black;


	static MaterialPropertyBlock block;

	[SerializeField]
	Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (block == null) {
            block = new MaterialPropertyBlock();
        }

        block.SetColor(emissionColorId, emissionColor);
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, alphaCutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);

        GetComponent<Renderer>().SetPropertyBlock(block);
    }


}