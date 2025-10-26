using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class RoundedRectMaterialSync : MonoBehaviour
{
    public string rectSizePropName = "_RectSize";
    private RectTransform _rt;
    private Material _mat;

    void OnEnable()
    {
        _rt = GetComponent<RectTransform>();
        var img = GetComponent<Image>();
        // Important: utiliser materialForRendering en runtime si ce GO est masqué/masquable,
        // mais ici on veut ajuster l'instance du material assigné.
        _mat = img.material;
        UpdateMaterial();
    }

    void OnRectTransformDimensionsChange() => UpdateMaterial();

    void UpdateMaterial()
    {
        if (_rt == null || _mat == null) return;
        var size = _rt.rect.size; // en pixels (Canvas Scaler gère l’échelle)
        _mat.SetVector(rectSizePropName, new Vector4(size.x, size.y, 0, 0));
    }
}
