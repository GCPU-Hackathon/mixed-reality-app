using UnityEngine;

public class VolumeClipController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Le volume à clipper (celui qui a VolumeDVR + le MeshRenderer/Materiel du raymarch).")]
    public VolumeDVR volumeDVR;

    [Tooltip("Transform représentant la paume de la main (ou un proxy stable de la main ouverte).")]
    public Transform palmTransform;

    [Header("Settings")]
    [Tooltip("Activer/désactiver le clipping.")]
    public bool clipEnabled = true;

    [Tooltip("Décalage en m pour éviter de couper tout le volume instantanément. " +
             "Ex: pousser le plan un peu derrière la paume.")]
    public float planeOffsetAlongNormal = 0.0f;

    // cache
    private Material _mat; // on va récupérer le material du volumeDVR

    void Start()
    {
        if (volumeDVR == null)
        {
            Debug.LogError("[VolumeClipController] volumeDVR is null.");
            enabled = false;
            return;
        }

        if (volumeDVR.volumeMaterial == null)
        {
            Debug.LogError("[VolumeClipController] volumeDVR.volumeMaterial is null.");
            enabled = false;
            return;
        }

        _mat = volumeDVR.volumeMaterial;
    }

    void Update()
    {
        if (_mat == null || palmTransform == null)
            return;

        // 1. lire position / normale de la main EN WORLD SPACE
        Vector3 palmPosW = palmTransform.position;
        Vector3 palmNormalW = palmTransform.forward; 
        // NOTE: selon ton rig, forward n'est peut-être pas la normale de la paume.
        // Tu peux essayer palmTransform.up ou -palmTransform.up.
        // On ajustera après test visuel.

        // offset pour pousser le plan un peu "derrière" la paume
        palmPosW += palmNormalW * planeOffsetAlongNormal;

        // 2. convertir en OBJ SPACE du volume
        //    rappel: dans le shader, on travaille en objet VolumeDVR local space
        //    On peut obtenir la matrice monde->objet via volumeDVR.transform.worldToLocalMatrix
        Matrix4x4 worldToObj = volumeDVR.transform.worldToLocalMatrix;

        Vector3 palmPosObj = worldToObj.MultiplyPoint3x4(palmPosW);
        Vector3 palmNormalObj = worldToObj.MultiplyVector(palmNormalW).normalized;

        // 3. envoyer au matériau
        _mat.SetInt("_ClipEnabled", clipEnabled ? 1 : 0);
        _mat.SetVector("_ClipPlaneNormal", new Vector4(palmNormalObj.x, palmNormalObj.y, palmNormalObj.z, 0f));
        _mat.SetVector("_ClipPlanePoint", new Vector4(palmPosObj.x, palmPosObj.y, palmPosObj.z, 1f));
    }
}
