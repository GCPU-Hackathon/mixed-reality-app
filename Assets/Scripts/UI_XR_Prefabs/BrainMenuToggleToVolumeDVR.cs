using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

/// <summary>
/// Génère une liste de toggles dans un ScrollView à partir de volumeDVR.labelInfos.
/// Chaque toggle pilote la visibilité du label correspondant dans VolumeDVR.
/// Après génération initiale, on ne reconstruit plus.
/// </summary>
public class BrainMenuToggleToVolumeDVR : MonoBehaviour
{
    [Header("Volume DVR Target")]
    public VolumeDVR volumeDVR;

    [Header("UI Wiring")]
    [Tooltip("Le Content du ScrollRect (Scroll View/Viewport/Content). " +
             "Si vide, je vais essayer de le trouver automatiquement.")]
    public RectTransform contentRoot;

    [Tooltip("Prefab / template d'un item (Toggle + Texte). " +
             "Si null, je prends un enfant 'Item 1' (ou le premier enfant).")]
    public GameObject itemPrefab;

    [Tooltip("Ignorer les labels qui ne sont pas visibles par défaut ?")]
    public bool hideFullyHiddenAtStart = true;

    // runtime
    private GameObject _template;          // le template réel qu'on clone
    private bool _wiredUI   = false;       // est-ce qu'on a trouvé contentRoot / template ?
    private bool _builtMenu = false;       // est-ce que la liste de toggles a déjà été générée ?

    // mapping runtime
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

    /// <summary>
    /// Appelle ça manuellement si tu veux *forcer* une reconstruction complète (rare).
    /// </summary>
    public void RebuildFromScratch()
    {
        if (contentRoot == null)
        {
            Debug.LogWarning("[BrainMenu] Rebuild demandé mais contentRoot introuvable.");
            return;
        }


        _toggles.Clear();
        _toggleToLabel.Clear();

        WireUIIfNeeded();

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            var ch = contentRoot.GetChild(i).gameObject;
            if (ch == _template) continue;
            Destroy(ch);
        }

        _builtMenu = false;
        TryBuildOnce();
        SyncAll();
    }

    /// <summary>
    /// Essaie d'identifier le contentRoot et le template une seule fois.
    /// </summary>
    private void WireUIIfNeeded()
    {
        if (_wiredUI) return;

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

        if (itemPrefab != null)
        {
            _template = itemPrefab;
        }
        else
        {
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Transform ch = contentRoot.GetChild(i);
                if (ch.name == "Item 1")
                {
                    _template = ch.gameObject;
                    break;
                }
            }

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

        _template.SetActive(false);

        _wiredUI = true;
    }

    /// <summary>
    /// Construit la liste de toggles UNE SEULE FOIS.
    /// Après ça, on ne touche plus à la hiérarchie (donc l'utilisateur garde ses états).
    /// </summary>
    private void TryBuildOnce()
    {
        if (_builtMenu) return;

        WireUIIfNeeded();
        if (!_wiredUI)
        {
            Debug.LogWarning("[BrainMenu] TryBuildOnce() abandonné : UI pas câblée.");
            return;
        }

        if (volumeDVR == null)
            volumeDVR = FindObjectOfType<VolumeDVR>();

        if (volumeDVR == null)
        {
            Debug.LogWarning("[BrainMenu] Aucun VolumeDVR trouvé.");
            return;
        }

        if (contentRoot == null || _template == null)
        {
            Debug.LogWarning("[BrainMenu] contentRoot ou template manquant.");
            return;
        }


        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            var ch = contentRoot.GetChild(i).gameObject;
            if (ch == _template) continue;
            Destroy(ch);
        }

        _toggles.Clear();
        _toggleToLabel.Clear();

        var infos = volumeDVR.labelInfos;
        if (infos == null || infos.Count == 0)
        {
            Debug.LogWarning("[BrainMenu] No label found in VolumeDVR.");
        }
        else
        {
            BuildUIFromLabelInfos(infos);
        }

        _builtMenu = true;
    }

    /// <summary>
    /// Instancie un toggle par label visible et met en place les callbacks.
    /// </summary>
    private void BuildUIFromLabelInfos(List<VolumeLabelInfoRuntime> infos)
    {
        int createdCount = 0;

        foreach (var info in infos)
        {
            if (hideFullyHiddenAtStart && !info.defaultVisible)
                continue;

            GameObject clone = Instantiate(_template, contentRoot);
            clone.name = !string.IsNullOrEmpty(info.displayName)
                ? info.displayName
                : $"Label {info.labelIndex}";
            clone.SetActive(true);

            Toggle toggle = clone.GetComponentInChildren<Toggle>(true);

            Text legacyText = clone.GetComponentInChildren<Text>(true);

#if TMP_PRESENT || UNITY_TEXTMESHPRO
            TMP_Text tmpText = clone.GetComponentInChildren<TMP_Text>(true);
#else
            TMP_Text tmpText = null;
#endif

            string labelText = info.displayName;
            if (legacyText != null) legacyText.text = labelText;
            if (tmpText != null)    tmpText.text    = labelText;

            if (toggle != null)
            {
                toggle.isOn = info.defaultVisible;

                _toggles.Add(toggle);
                _toggleToLabel[toggle] = info.labelIndex;

                int capturedIndex = info.labelIndex;
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (volumeDVR != null)
                        volumeDVR.SetLabelVisible(capturedIndex, isOn);

                    Transform activeMarkTf = FindChildByName(clone.transform, "Active");
                    if (activeMarkTf != null)
                        activeMarkTf.gameObject.SetActive(isOn);
                });
            }

            createdCount++;
        }

        Debug.Log($"[BrainMenu] Créé {createdCount} toggles depuis {_template.name}");
    }

    /// <summary>
    /// Met en cohérence l'état du VolumeDVR avec l'état actuel des toggles déjà construits.
    /// (Ne rebuild PAS l'UI, ne rajoute PAS de listeners.)
    /// Appelée à Start() et peut être rappelée par le menu opener à l'ouverture si tu veux resynchroniser.
    /// </summary>
    public void SyncAll()
    {
        if (!_builtMenu) return;
        if (volumeDVR == null) return;

        for (int i = 0; i < _toggles.Count; i++)
        {
            var uiToggle = _toggles[i];
            if (uiToggle == null) continue;

            if (!_toggleToLabel.TryGetValue(uiToggle, out int labelIdx))
                continue;

            bool isOn = uiToggle.isOn;

            volumeDVR.SetLabelVisible(labelIdx, isOn);

            Transform activeMarkTf = FindChildByName(uiToggle.transform, "Active");
            if (activeMarkTf != null)
                activeMarkTf.gameObject.SetActive(isOn);
        }
    }

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
