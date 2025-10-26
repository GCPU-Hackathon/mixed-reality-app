using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

public class BrainMenuAutoBuilder : MonoBehaviour
{
    [Header("Scene Parts")]
    [Tooltip("Parent of all brain subparts (e.g., brain_tumor_scene). If left empty, it will be auto-detected.")]
    public Transform brainRoot;

    [Tooltip("Optional path used if brainRoot is null, e.g. 'Brain_Pivot/brain_tumor_scene'.")]
    public string autoFindPath = "Brain_Pivot/brain_tumor_scene";

    [Tooltip("Scan children recursively? If false, only direct children of brainRoot are used.")]
    public bool recursive = false;

    [Tooltip("Only include parts that have a MeshRenderer somewhere under them.")]
    public bool requireMeshRenderer = true;

    [Header("UI")]
    [Tooltip("ScrollView -> Viewport -> Content (auto-detected if empty).")]
    public RectTransform contentRoot;

    [Tooltip("Prefab of one menu item (must have a Toggle).")]
    public GameObject toggleItemPrefab;

    [Tooltip("Child object name for the label inside the item prefab.")]
    public string labelChildName = "Label";

    [Header("State")]
    [Tooltip("ScriptableObject BrainMenuState to persist toggle states.")]
    public BrainMenuState state;
    public string keyPrefix = "Item/";

    [Header("Defaults (first run only)")]
    public bool forceAllOnAtFirstRun = true;
    public string firstRunKey = "BrainMenu_DefaultsApplied_v1";

    [Header("Name Mapping")]
    [Tooltip("Replace technical names (grp88287...) wSith readable ones in UI.")]
    public List<NameMap> nameMappings = new();

    [System.Serializable]
    public struct NameMap
    {
        public string objectName;  // ex: grp88287
        public string displayName; // ex: Hippocampe
    }


    // --- internals ---
    readonly List<(Toggle t, Transform part, string key)> _items = new();
    bool _building;

    void Reset() => AutoWireUI();
#if UNITY_EDITOR
    void OnValidate() { if (contentRoot == null) AutoWireUI(); }
#endif

    void AutoWireUI()
    {
        var sr = GetComponentInChildren<ScrollRect>(true);
        if (sr && sr.content) contentRoot = sr.content;
    }

    void Awake()
    {
        if (contentRoot == null) AutoWireUI();

        // 🔍 auto-find brainRoot if empty
        if (brainRoot == null)
        {
            if (!string.IsNullOrEmpty(autoFindPath))
            {
                var t = GameObject.Find(autoFindPath);
                if (t != null) brainRoot = t.transform;
            }

            if (brainRoot == null)
            {
                var pivot = GameObject.Find("Brain_Pivot") ?? GameObject.Find("brain_pivot");
                if (pivot != null)
                {
                    var child = pivot.transform.Find("brain_tumor_scene");
                    brainRoot = child ? child : pivot.transform;
                }
            }

            if (brainRoot == null)
            {
                var anyMr = FindObjectOfType<MeshRenderer>();
                if (anyMr) brainRoot = anyMr.transform.root;
            }
        }
    }

    void Start()
    {
        if (brainRoot == null || contentRoot == null || toggleItemPrefab == null)
        {
            Debug.LogWarning("[BrainMenuAutoBuilder] Missing refs (brainRoot/contentRoot/toggleItemPrefab). Auto-find may have failed.");
            return;
        }
        RebuildMenu();
    }

    string GetDisplayName(string originalName)
    {
        foreach (var map in nameMappings)
            if (map.objectName == originalName)
                return map.displayName;
        return originalName; // fallback: garde le nom du GameObject
    }

    [ContextMenu("Rebuild Menu")]
    public void RebuildMenu()
    {
        if (brainRoot == null || contentRoot == null || toggleItemPrefab == null)
        {
            Debug.LogWarning("[BrainMenuAutoBuilder] Missing refs (brainRoot/contentRoot/toggleItemPrefab).");
            return;
        }

        _building = true;

        // clear existing
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        _items.Clear();

        // collect brain parts
        var parts = new List<Transform>();
        CollectParts(brainRoot, parts, recursive, requireMeshRenderer);

        // build UI
        foreach (var part in parts)
        {
            var go = Instantiate(toggleItemPrefab, contentRoot);
            go.name = part.name;

            var toggle = go.GetComponentInChildren<Toggle>(true);
            if (!toggle)
            {
                Debug.LogError($"[BrainMenuAutoBuilder] Prefab '{toggleItemPrefab.name}' has no Toggle.");
                Destroy(go);
                continue;
            }

            if (toggle.group != null) toggle.group = null; // allow multi-select
            SetItemLabel(go.transform, labelChildName, GetDisplayName(part.name));

            string key = keyPrefix + part.name;

            bool on;
            if (forceAllOnAtFirstRun && PlayerPrefs.GetInt(firstRunKey, 0) == 0)
            {
                on = true;
                if (state) state.Set(key, true);
            }
            else
            {
                on = state ? state.Get(key, true) : true;
            }

            toggle.SetIsOnWithoutNotify(on);
            part.gameObject.SetActive(on);

            toggle.onValueChanged.AddListener(v =>
            {
                part.gameObject.SetActive(v);
                if (state) state.Set(key, v);
            });

            _items.Add((toggle, part, key));
        }

        // mark defaults applied
        if (forceAllOnAtFirstRun && PlayerPrefs.GetInt(firstRunKey, 0) == 0)
        {
            PlayerPrefs.SetInt(firstRunKey, 1);
            PlayerPrefs.Save();
        }

        _building = false;
    }

    // --- helpers ---
    static void CollectParts(Transform root, List<Transform> outList, bool recursive, bool requireRenderer)
    {
        if (!recursive)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!requireRenderer || HasMeshRendererDeep(c))
                    outList.Add(c);
            }
        }
        else
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (!requireRenderer || HasMeshRendererDeep(t))
                    outList.Add(t);
            }
        }
    }

    static bool HasMeshRendererDeep(Transform t)
    {
        return t.GetComponentInChildren<MeshRenderer>(true) != null ||
               t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    static void SetItemLabel(Transform item, string labelChildName, string text)
    {
        // 1) si labelChildName est fourni, on essaie d'abord ce chemin
        if (!string.IsNullOrEmpty(labelChildName))
        {
            var labelTf = item.Find(labelChildName);
            if (labelTf)
            {
    #if TMP_PRESENT || UNITY_TEXTMESHPRO
                var tmp = labelTf.GetComponent<TMPro.TMP_Text>();
                if (tmp) { tmp.text = text; return; }
    #endif
                var legacy = labelTf.GetComponent<UnityEngine.UI.Text>();
                if (legacy) { legacy.text = text; return; }
            }
        }

        // 2) fallback : premier composant texte trouvé n'importe où sous l'item
    #if TMP_PRESENT || UNITY_TEXTMESHPRO
        var anyTmp = item.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (anyTmp) { anyTmp.text = text; return; }
    #endif
        var anyLegacy = item.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (anyLegacy) { anyLegacy.text = text; return; }

        // 3) si vraiment rien trouvé, on log pour t'aider à cibler le bon child
        Debug.LogWarning($"[BrainMenuAutoBuilder] Aucun label trouvé sous '{item.name}'. " +
                        $"Spécifie 'Label Child Name' ou mets un TMP_Text/Text dans le prefab.");
    }
}
