using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

/// <summary>
/// Construit dynamiquement le menu de visibilité des labels à partir
/// du VolumeDVR chargé (volumeDVR.labelInfos).
/// Chaque entrée = un toggle qui appelle SetLabelVisible(labelIndex, isOn).
/// </summary>
public class BrainMenuToggleToVolumeDVR : MonoBehaviour
{
    [Header("Volume DVR Target")]
    [Tooltip("Référence vers le composant VolumeDVR déjà présent dans la scène (et déjà initialisé).")]
    public VolumeDVR volumeDVR;

    [Header("UI Setup")]
    [Tooltip("Le Content du ScrollRect où on va instancier les toggles.")]
    public RectTransform contentRoot;

    [Tooltip("Prefab d'un item (doit contenir un Toggle + un texte). " +
             "Optionnellement une Image pour afficher la couleur du label.")]
    public GameObject toggleItemPrefab;

    [Header("Filtrage")]
    [Tooltip("Masquer les labels qui ont alpha initiale ~0 (donc invisibles par défaut) ?")]
    public bool hideFullyHiddenAtStart = true;

    // runtime
    private readonly List<Toggle> _toggles = new();
    private readonly Dictionary<Toggle, int> _toggleToLabel = new();

    private bool _built = false; // pour éviter double build si OnEnable/Start rejoue

    void Awake()
    {
        // fallback auto si pas assigné dans l'inspecteur
        if (contentRoot == null)
        {
            var sr = GetComponentInChildren<ScrollRect>(true);
            if (sr && sr.content)
                contentRoot = sr.content;
        }

        if (!contentRoot)
            Debug.LogError("[BrainMenuToggleToVolumeDVR] contentRoot is not assigned.");

        if (!toggleItemPrefab)
            Debug.LogError("[BrainMenuToggleToVolumeDVR] toggleItemPrefab is not assigned.");

        if (!volumeDVR)
            Debug.LogWarning("[BrainMenuToggleToVolumeDVR] volumeDVR is not assigned yet.");
    }

    void OnEnable()
    {
        // Quand le menu apparait on essaie de construire (si pas déjà fait)
        TryBuild();
        // Puis on synchronise l'état visuel → volume
        SyncAll();
    }

    void Start()
    {
        // Cas où OnEnable a été appelé avant que volumeDVR ait fini Start():
        // On retente ici aussi.
        TryBuild();
        SyncAll();
    }

    /// <summary>
    /// Construit l'UI une seule fois, à partir de volumeDVR.labelInfos.
    /// </summary>
    private void TryBuild()
    {
        if (_built) return;

        if (volumeDVR == null)
        {
            Debug.LogWarning("[BrainMenuToggleToVolumeDVR] No VolumeDVR yet, cannot build menu.");
            return;
        }

        // On attend que VolumeDVR ait peuplé labelInfos
        // labelInfos doit contenir les labels extraits de la TF
        List<VolumeLabelInfoRuntime> infos = volumeDVR.labelInfos;
        if (infos == null || infos.Count == 0)
        {
            Debug.LogWarning("[BrainMenuToggleToVolumeDVR] volumeDVR.labelInfos is empty, menu not built yet.");
            return;
        }

        if (!contentRoot || !toggleItemPrefab)
        {
            Debug.LogError("[BrainMenuToggleToVolumeDVR] Missing contentRoot or toggleItemPrefab.");
            return;
        }

        BuildUIFromLabelInfos(infos);
        _built = true;
    }

    /// <summary>
    /// Fabrique un toggle par label connu du VolumeDVR.
    /// On utilise displayName, labelIndex, defaultVisible, et la couleur TF.
    /// </summary>
    private void BuildUIFromLabelInfos(List<VolumeLabelInfoRuntime> infos)
    {
        // Nettoyer le Content d'abord
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        _toggles.Clear();
        _toggleToLabel.Clear();

        foreach (var info in infos)
        {
            int idx = info.labelIndex;
            if (idx < 0 || idx > 255) continue;

            // Optionnel: on ne veut pas spammer l'UI avec 200 labels invisibles
            if (hideFullyHiddenAtStart && !info.defaultVisible)
            {
                // Si par défaut c'est complètement masqué (alpha quasi zéro)
                // tu peux choisir de ne pas créer d'entrée.
                continue;
            }

            GameObject go = Instantiate(toggleItemPrefab, contentRoot);

            // Récupération du Toggle
            Toggle t = go.GetComponentInChildren<Toggle>(true);
            if (t == null)
            {
                Debug.LogError("[BrainMenuToggleToVolumeDVR] toggleItemPrefab has no Toggle component.");
                Destroy(go);
                continue;
            }

            // on ne veut pas de ToggleGroup lock step
            if (t.group != null) t.group = null;

            // Nom affiché
            string niceName = string.IsNullOrEmpty(info.displayName)
                ? $"Label {idx}"
                : info.displayName;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
            TMP_Text tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                tmp.text = niceName;
#endif
            Text legacy = go.GetComponentInChildren<Text>(true);
            if (legacy != null)
                legacy.text = niceName;

            // État du toggle initial = visibilité par défaut dans le volume
            // (defaultVisible ~ alpha>0 à l'import)
            t.isOn = info.defaultVisible;

            // Si on veut, on peut colorer un patch UI avec la couleur du label
            // Cherche une Image enfant nommée "ColorSwatch" par ex
            Image swatch = FindChildImageByName(go.transform, "ColorSwatch");
            if (swatch != null)
            {
                // On prend la couleur du TF (RGB). On ne va pas multiplier par alpha,
                // sinon les trucs translucides deviennent gris.
                swatch.color = new Color(info.color.r, info.color.g, info.color.b, 1f);
            }

            // On mappe ce toggle à ce labelIndex
            _toggles.Add(t);
            _toggleToLabel[t] = idx;

            // Listener runtime : cocher/ décocher => SetLabelVisible(label, bool)
            int captured = idx;
            t.onValueChanged.AddListener(v =>
            {
                if (volumeDVR != null)
                    volumeDVR.SetLabelVisible(captured, v);
            });

            // (debug convenience)
            var debugIndex = go.GetComponent<BrainMenuItemLabelIndex>();
            if (debugIndex == null)
                debugIndex = go.AddComponent<BrainMenuItemLabelIndex>();
            debugIndex.labelIndex = idx;
        }
    }

    /// <summary>
    /// Appelé quand le menu apparait ou à la demande.
    /// Pousse l'état actuel de chaque toggle vers VolumeDVR
    /// pour que le rendu colle à ce que l'UI montre.
    /// </summary>
    public void SyncAll()
    {
        if (volumeDVR == null) return;

        foreach (var t in _toggles)
        {
            if (!_toggleToLabel.TryGetValue(t, out int labelIdx))
                continue;

            volumeDVR.SetLabelVisible(labelIdx, t.isOn);
        }
    }

    /// Cherche récursivement une Image enfant avec ce nom (ex: "ColorSwatch").
    private Image FindChildImageByName(Transform root, string name)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name == name) return img;
        }
        return null;
    }
}


// Petit helper debug purement informatif dans l'Inspector.
public class BrainMenuItemLabelIndex : MonoBehaviour
{
    [Range(0,255)]
    public int labelIndex = 0;
}
