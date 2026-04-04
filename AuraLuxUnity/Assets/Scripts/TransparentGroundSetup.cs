using UnityEngine;

/// <summary>
/// Sets up a URP Lit material for proper transparency at runtime.
/// Attach to the Ground GameObject. Safe to remove after baking.
/// </summary>
[ExecuteAlways]
public class TransparentGroundSetup : MonoBehaviour
{
    [Range(0f, 1f)]
    public float alpha = 0.2f;

    [ColorUsage(false)]
    public Color baseColor = new Color(0.4f, 0.1f, 0.6f);

    [Range(0f, 1f)]
    public float smoothness = 0.9f;

    private Material _mat;

    void OnEnable() => Apply();

    void OnValidate() => Apply();

    void Apply()
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        // Work on the instance material so we don't dirty the asset
        _mat = mr.sharedMaterial;
        if (_mat == null) return;

        Color c = baseColor;
        c.a = alpha;
        _mat.color = c;
        _mat.SetFloat("_Surface", 1f);          // 1 = Transparent
        _mat.SetFloat("_Blend", 0f);            // 0 = Alpha blend
        _mat.SetFloat("_AlphaClip", 0f);
        _mat.SetFloat("_ZWrite", 0f);
        _mat.SetFloat("_Smoothness", smoothness);
        _mat.SetFloat("_Metallic", 0f);
        _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _mat.DisableKeyword("_ALPHATEST_ON");
        _mat.DisableKeyword("_ALPHABLEND_ON");

        _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
