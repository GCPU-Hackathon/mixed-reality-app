using UnityEngine;

public class VolumeClipController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Composant VolumeDVR du volume à clipper.")]
    public VolumeDVR volumeDVR;

    [Tooltip("Transform représentant la paume / la 'lame' de la main.")]
    public Transform palmTransform;

    [Header("Clipping")]
    [Tooltip("Activer le clipping du volume.")]
    public bool clipEnabled = false;

    [Tooltip("Décalage du plan dans le sens de la normale (m). Utile pour 'pousser' le plan un peu derrière la paume.")]
    public float planeOffsetAlongNormal = 0.0f;

    [Tooltip("Utiliser quel axe comme normale de la paume ? (forward / up / -up etc.)")]
    public PalmNormalAxis palmNormalAxis = PalmNormalAxis.Forward;

    public enum PalmNormalAxis
    {
        Forward,
        Back,
        Up,
        Down,
        Right,
        Left
    }

    Material _mat;

    void Start()
    {
        if (volumeDVR == null || volumeDVR.volumeMaterial == null)
        {
            Debug.LogError("[VolumeClipController] Missing volumeDVR or its material.");
            enabled = false;
            return;
        }

        _mat = volumeDVR.volumeMaterial;
    }

    void Update()
    {
        if (_mat == null || palmTransform == null) return;

        Vector3 palmPosW = palmTransform.position;
        Vector3 palmNormalW = GetPalmNormalWorld();

        palmPosW += palmNormalW * planeOffsetAlongNormal;

        Matrix4x4 worldToObj = volumeDVR.transform.worldToLocalMatrix;

        Vector3 palmPosObj    = worldToObj.MultiplyPoint3x4(palmPosW);
        Vector3 palmNormalObj = worldToObj.MultiplyVector(palmNormalW).normalized;

        _mat.SetInt("_ClipEnabled", clipEnabled ? 1 : 0);
        _mat.SetVector("_ClipPlaneNormal", new Vector4(palmNormalObj.x, palmNormalObj.y, palmNormalObj.z, 0f));
        _mat.SetVector("_ClipPlanePoint",  new Vector4(palmPosObj.x, palmPosObj.y, palmPosObj.z, 1f));
    }

    Vector3 GetPalmNormalWorld()
    {
        switch (palmNormalAxis)
        {
            case PalmNormalAxis.Forward: return  palmTransform.forward;
            case PalmNormalAxis.Back:    return -palmTransform.forward;
            case PalmNormalAxis.Up:      return  palmTransform.up;
            case PalmNormalAxis.Down:    return -palmTransform.up;
            case PalmNormalAxis.Right:   return  palmTransform.right;
            case PalmNormalAxis.Left:    return -palmTransform.right;
        }
        return palmTransform.forward;
    }

    public void EnableClipping()  { clipEnabled = true;  }
    public void DisableClipping() { clipEnabled = false; }
}
