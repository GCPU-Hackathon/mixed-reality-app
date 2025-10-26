using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrainMenuBinder : MonoBehaviour
{
    [Header("State")]
    public BrainMenuState state;

    [Header("Scan")]
    public RectTransform contentRoot;      // auto-wired if null
    public bool keyFromObjectName = true;
    public string keyPrefix = "Item/";

    [Header("Defaults (first run only)")]
    public bool forceAllOnAtFirstRun = true;
    [Tooltip("Bump version to re-apply defaults after an update (e.g., v2, v3...).")]
    public string firstRunKey = "BrainMenu_DefaultsApplied_v1";

    readonly List<(Toggle t, string key)> _items = new();
    bool _applying;

    void Reset()      { AutoWire(); }
#if UNITY_EDITOR
    void OnValidate() { if (contentRoot == null) AutoWire(); }
#endif

    void AutoWire()
    {
        var sr = GetComponentInChildren<ScrollRect>(true);
        if (sr && sr.content) contentRoot = sr.content;
        if (contentRoot == null)
            contentRoot = GetComponentInChildren<RectTransform>(true);
    }

    void Awake()
    {
        if (contentRoot == null) AutoWire();
        _items.Clear();

        foreach (var t in contentRoot.GetComponentsInChildren<Toggle>(true))
        {
            // IMPORTANT: allow multi-select by detaching from ToggleGroup
            if (t.group != null) t.group = null;

            var key = keyFromObjectName
                ? keyPrefix + t.gameObject.name
                : t.gameObject.GetInstanceID().ToString();

            _items.Add((t, key));
            t.onValueChanged.AddListener(v => OnToggleChanged(t, key, v));
        }
    }

    void OnEnable()
    {
        _applying = true;

        bool firstRunNeedsDefaults = forceAllOnAtFirstRun &&
                                     PlayerPrefs.GetInt(firstRunKey, 0) == 0;

        if (firstRunNeedsDefaults)
        {
            // Turn everything ON once, persist to state
            foreach (var (t, key) in _items)
            {
                t.SetIsOnWithoutNotify(true);
                if (state) state.Set(key, true);
            }

            PlayerPrefs.SetInt(firstRunKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            // Normal restore from state
            foreach (var (t, key) in _items)
                t.isOn = state ? state.Get(key, t.isOn) : t.isOn;
        }

        _applying = false;
    }

    void OnToggleChanged(Toggle t, string key, bool value)
    {
        if (_applying) return;
        if (state) state.Set(key, value);
    }
}
