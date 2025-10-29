using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class VolumeDVROverlay : MonoBehaviour
{
    [Header("Input (.vrdfw in StreamingAssets)")]
    [Tooltip("Fichier overlay continu coloré exporté pour un canal (ex: scene_ch0.vrdfw)")]
    public string vrdfOverlayFileName = "scene_ch0.vrdfw";

    [Header("Raymarch material (must use Custom/VolumeOverlay_URP)")]
    public Material overlayMaterial;

    [Header("Debug / Quality")]
    public bool verboseDebug = false;

    private VRDFVolumeData _data;
    private Texture3D _volumeTex;
    private Texture2D _tfTex;

    private Material _runtimeMat;

    private static Texture3D _blackTex3D;
    private static Texture3D BlackTex3D
    {
        get
        {
            if (_blackTex3D == null)
            {
                Color black = new Color(0, 0, 0, 0);
                _blackTex3D = new Texture3D(1, 1, 1, TextureFormat.RFloat, false);
                _blackTex3D.SetPixel(0, 0, 0, black);
                _blackTex3D.Apply(false, false);
            }
            return _blackTex3D;
        }
    }

    void Start()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, vrdfOverlayFileName);
        _data = VRDFLoader.LoadFromFile(fullPath);
        VRDFLoader.BuildUnityTextures(_data);

        _volumeTex = _data.volumeTexture;

        _tfTex = _data.tfLUTTextureSoft;

        _runtimeMat = new Material(overlayMaterial);

        ApplyToMaterial();

        FitVolumeScaleFromSpacing(_data);

        if (verboseDebug)
        {
            Debug.Log($"[VolumeDVROverlay] Loaded {vrdfOverlayFileName} " +
                      $"dim={DimToString(_data.meta.dim)} " +
                      $"mode={_data.meta.mode} tf.type={_data.tf.type} origin={_data.tf.origin}");
        }
    }

    private void ApplyToMaterial()
    {
        if (_runtimeMat == null)
        {
            Debug.LogError("[VolumeDVROverlay] runtime material not ready.");
            return;
        }

        _runtimeMat.SetTexture("_VolumeTex", _volumeTex ? _volumeTex : BlackTex3D);

        _runtimeMat.SetTexture("_TFTex", _tfTex ? _tfTex : Texture2D.blackTexture);

        float p1 = 0f;
        float p99 = 1f;
        if (_data.meta.intensity_range != null && _data.meta.intensity_range.Length == 2)
        {
            p1  = _data.meta.intensity_range[0];
            p99 = _data.meta.intensity_range[1];
        }
        _runtimeMat.SetFloat("_P1",  p1);
        _runtimeMat.SetFloat("_P99", p99);

        _runtimeMat.SetInt("_IsLabelMap", 0);
        _runtimeMat.SetInt("_HasWeights", 0);

        var mr = GetComponent<MeshRenderer>();
        if (mr && mr.sharedMaterial != _runtimeMat)
            mr.sharedMaterial = _runtimeMat;
    }

    private string DimToString(int[] d)
    {
        if (d == null || d.Length < 3) return "???";
        return d[0] + "x" + d[1] + "x" + d[2];
    }

    private void FitVolumeScaleFromSpacing(VRDFVolumeData data)
    {
        var meta = data.meta;
        int dimX = meta.dim[0];
        int dimY = meta.dim[1];
        int dimZ = meta.dim[2];

        float sx = (meta.spacing_mm != null && meta.spacing_mm.Length > 0) ? meta.spacing_mm[0] : 1f;
        float sy = (meta.spacing_mm != null && meta.spacing_mm.Length > 1) ? meta.spacing_mm[1] : 1f;
        float sz = (meta.spacing_mm != null && meta.spacing_mm.Length > 2) ? meta.spacing_mm[2] : 1f;

        Vector3 sizeMeters = new Vector3(
            dimX * sx * 0.001f,
            dimY * sy * 0.001f,
            dimZ * sz * 0.001f
        );

        transform.localScale    = sizeMeters;
        transform.localRotation = Quaternion.Euler(-90, 0, 0);
    }
}
