using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

/// <summary>
/// Génère une liste de toggles dans un ScrollView à partir de volumeDVR.labelInfos.
/// Chaque toggle pilote la visibilité du label correspondant dans VolumeDVR.
/// </summary>
public class BrainMenuToggleToVolumeDVR : MonoBehaviour
{
    [Header("Volume DVR Target")]
    public VolumeDVR volumeDVR;

    [Header("UI Wiring")]
    [Tooltip("Le Content du ScrollRect (Scroll View/Viewport/Content). " +
             "Si vide, je vais essayer de le trouver automatiquement.")]
    public RectTransform contentRoot;

    [Tooltip("Prefab de l'item (doit contenir Toggle+Texte...). " +
             "Si null, je prends l'enfant 'Item 1' dans Content comme template.")]
    public GameObject itemPrefab;

    [Tooltip("Ignorer les labels qui ne sont pas visibles par défaut ?")]
    public bool hideFullyHiddenAtStart = true;

    // runtime
    private GameObject _template; // le prefab effectif utilisé (itemPrefab ou Item 1)
    private bool _wiredUI   = false;
    private bool _builtMenu = false;

    // mapping toggles -> index de label DVR
    private readonly List<Toggle> _toggles = new();
    private readonly Dictionary<Toggle, int> _toggleToLabel = new();

    void Awake()
    {
        WireUIIfNeeded();
    }

    void Start()
    {
        TryBuildOnce();
        SyncAll();
    }

    void OnEnable()
    {
        TryBuildOnce();
        SyncAll();
    }

    /// <summary>
    /// Appelle ça si tu veux régénérer la liste à la volée.
    /// </summary>
    public void RebuildFromScratch()
    {
        _builtMenu = false;
        TryBuildOnce();
        SyncAll();
    }

    /// <summary>
    /// Essaie de résoudre contentRoot + template une seule fois.
    /// </summary>
    private void WireUIIfNeeded()
    {
        if (_wiredUI) return;

        // 1. contentRoot auto
        if (contentRoot == null)
        {
            ScrollRect sr = GetComponentInChildren<ScrollRect>(true);
            if (sr != null && sr.content != null)
            {
                contentRoot = sr.content;
            }
            else
            {
                foreach (var rt in GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "Content")
                    {
                        contentRoot = rt;
                        break;
                    }
                }
            }
        }

        if (contentRoot == null)
        {
            Debug.LogWarning("[BrainMenu] No contentRoot found. Assign it in inspector.");
            return;
        }

        // 2. choisir le template (_template)
        if (itemPrefab != null)
        {
            _template = itemPrefab;
        }
        else
        {
            // prend l'enfant "Item 1" si présent
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform ch = contentRoot.GetChild(i);
                if (ch.name == "Item 1")
                {
                    _template = ch.gameObject;
                    break;
                }
            }

            // fallback = premier enfant
            if (_template == null && contentRoot.childCount > 0)
            {
                _template = contentRoot.GetChild(0).gameObject;
            }
        }

        if (_template == null)
        {
            Debug.LogWarning("[BrainMenu] No itemPrefab or fallback template found.");
            return;
        }

        // IMPORTANT : on veut que le template de base soit caché dans la scène.
        _template.SetActive(false);

        _wiredUI = true;
    }

private void TryBuildOnce()
{
    if (_builtMenu) return;

    // Trouve VolumeDVR
    if (volumeDVR == null)
        volumeDVR = FindObjectOfType<VolumeDVR>();

    if (volumeDVR == null)
    {
        Debug.LogWarning("[BrainMenu] Aucun VolumeDVR trouvé.");
        return;
    }

    // Trouve Content root
    if (contentRoot == null)
    {
        ScrollRect sr = GetComponentInChildren<ScrollRect>(true);
        if (sr != null && sr.content != null)
            contentRoot = sr.content;
    }

    if (contentRoot == null)
    {
        Debug.LogWarning("[BrainMenu] Aucun contentRoot assigné.");
        return;
    }

    // Si prefab pas défini, prend le premier enfant existant
    if (itemPrefab == null)
    {
        if (contentRoot.childCount > 0)
            itemPrefab = contentRoot.GetChild(0).gameObject;
        else
        {
            Debug.LogWarning("[BrainMenu] Aucun itemPrefab trouvé.");
            return;
        }
    }

    // Cache le template
    itemPrefab.SetActive(false);

    // Nettoie tout sauf le template
    for (int i = contentRoot.childCount - 1; i >= 0; i--)
    {
        var ch = contentRoot.GetChild(i);
        if (ch.gameObject != itemPrefab)
            Destroy(ch.gameObject);
    }

        if (volumeDVR.labelInfos.Count <= 0)
            Debug.LogWarning("[BrainMenu] No label found.");
        BuildUIFromLabelInfos(volumeDVR.labelInfos);
    _builtMenu = true;
}


private void BuildUIFromLabelInfos(List<VolumeLabelInfoRuntime> infos)
{
    if (infos == null || infos.Count == 0) return;

    foreach (var info in infos)
    {
        // Ignore si on veut cacher les labels invisibles
        if (hideFullyHiddenAtStart && !info.defaultVisible)
            continue;

        // Clone identique du prefab
        GameObject clone = Instantiate(itemPrefab, contentRoot);
        clone.name = info.displayName != "" ? info.displayName : $"Label {info.labelIndex}";
        clone.SetActive(true);

        // Trouve le Toggle et le texte
        Toggle toggle = clone.GetComponentInChildren<Toggle>(true);
        Text text = clone.GetComponentInChildren<Text>(true);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        TMP_Text tmpText = clone.GetComponentInChildren<TMP_Text>(true);
#endif

        if (text != null) text.text = info.displayName;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText != null) tmpText.text = info.displayName;
#endif

        // Mets le toggle à l’état par défaut
        toggle.isOn = info.defaultVisible;

        // Ajoute juste la fonction VolumeDVR sans rien supprimer
        toggle.onValueChanged.AddListener(isOn =>
        {
            volumeDVR.SetLabelVisible(info.labelIndex, isOn);
        });
    }

    Debug.Log($"[BrainMenu] Créé {infos.Count} toggles depuis {itemPrefab.name}");
}

    /// <summary>
    /// Force la scène VolumeDVR à matcher l'état des toggles,
    /// et raffraîchit aussi les visuels 'Active'.
    /// </summary>
    public void SyncAll()
    {
        if (!_builtMenu) return;
        if (volumeDVR == null) return;

        foreach (var uiToggle in _toggles)
        {
            if (uiToggle == null) continue;
            if (!_toggleToLabel.TryGetValue(uiToggle, out int labelIdx)) continue;

            bool isOn = uiToggle.isOn;

            // pousser état dans VolumeDVR
            volumeDVR.SetLabelVisible(labelIdx, isOn);

            // afficher l'icône Active si présente
            Transform activeMarkTf = FindChildByName(uiToggle.transform.root, "Active");
            if (activeMarkTf != null)
                activeMarkTf.gameObject.SetActive(isOn);
        }
    }

    // Helpers utilitaires
    private Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
        {
            if (c.name == name)
                return c;
        }
        return null;
    }

    private Image FindImageChildByName(Transform root, string name)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.name == name)
                return img;
        }
        return null;
    }
}

/// <summary>
/// Juste pour afficher le labelIndex dans l'Inspector sur chaque item clone.
/// </summary>
public class BrainMenuItemLabelIndex : MonoBehaviour
{
    [Range(0, 255)]
    public int labelIndex = 0;
}
