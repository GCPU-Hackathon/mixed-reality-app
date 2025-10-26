using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// If you use TextMeshPro for labels:
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

public class BrainMenuToggleToParts : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Parent of all brain subparts (e.g., brain_tumor_scene).")]
    public Transform brainRoot;

    [Header("UI")]
    [Tooltip("ScrollRect.content (auto-detected if left empty).")]
    public RectTransform contentRoot;

    [Tooltip("Child object name holding the label under each toggle item (optional).")]
    public string labelChildName = "Label";

    [Tooltip("If true, also try reading a TMP_Text/Text label to derive the part name.")]
    public bool readLabelText = true;

    // Cache
    readonly List<Toggle> _toggles = new();
    readonly Dictionary<Toggle, Transform> _toggleToPart = new();

    void Reset()      { AutoWire(); }
#if UNITY_EDITOR
    void OnValidate() { if (contentRoot == null) AutoWire(); }
#endif

    void AutoWire()
    {
        var sr = GetComponentInChildren<ScrollRect>(true);
        if (sr && sr.content) contentRoot = sr.content;
    }

    void Awake()
    {
        if (contentRoot == null) AutoWire();
        if (brainRoot == null)
        {
            Debug.LogWarning("[BrainMenuToggleToParts] brainRoot not set.");
            return;
        }

        _toggles.Clear();
        _toggleToPart.Clear();

        foreach (var t in contentRoot.GetComponentsInChildren<Toggle>(true))
        {
            // allow multi-selection
            if (t.group != null) t.group = null;

            var partName = ResolvePartName(t);
            var partTf   = FindDeepChildByName(brainRoot, partName);

            if (partTf == null)
            {
                Debug.LogWarning($"[BrainMenuToggleToParts] Part '{partName}' not found under '{brainRoot.name}' (toggle: {t.gameObject.name}).");
            }

            _toggles.Add(t);
            _toggleToPart[t] = partTf;

            // subscribe
            t.onValueChanged.AddListener(v =>
            {
                var target = _toggleToPart[t];
                if (target != null) target.gameObject.SetActive(v);
            });
        }
    }

    void OnEnable()
    {
        // One-time sync so scene matches current toggle states (Binder may set without notify)
        SyncAll();
    }

    public void SyncAll()
    {
        foreach (var t in _toggles)
        {
            if (_toggleToPart.TryGetValue(t, out var tf) && tf != null)
                tf.gameObject.SetActive(t.isOn);
        }
    }

    string ResolvePartName(Toggle t)
    {
        // 1) Optional: read label text (TMP or legacy Text) if present
        if (readLabelText && !string.IsNullOrEmpty(labelChildName))
        {
            var labelTf = t.transform.Find(labelChildName);
            if (labelTf != null)
            {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
                var tmp = labelTf.GetComponent<TMP_Text>();
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text.Trim();
#endif
                var legacy = labelTf.GetComponent<UnityEngine.UI.Text>();
                if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
                    return legacy.text.Trim();
            }
        }

        // 2) Fallback: toggle GameObject name
        return t.gameObject.name.Trim();
    }

    // Recursive search by name under a root
    static Transform FindDeepChildByName(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            var r = FindDeepChildByName(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
