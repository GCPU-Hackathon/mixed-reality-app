// Assets/BrainUI/BrainMenuState.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BrainUI/Brain Menu State")]
public class BrainMenuState : ScriptableObject
{
    [Serializable]
    public class BoolKV { public string key; public bool value; }

    [SerializeField] List<BoolKV> entries = new();
    Dictionary<string, bool> map;

    void OnEnable()
    {
        if (map == null)
        {
            map = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var e in entries) map[e.key] = e.value;
        }
    }

    public bool Get(string key, bool defaultValue = false)
        => map != null && map.TryGetValue(key, out var v) ? v : defaultValue;

    public void Set(string key, bool value)
    {
        if (map == null) OnEnable();
        map[key] = value;

        // maintien de la liste sérialisée (utile en mode Éditeur et pour persister entre Play)
        var idx = entries.FindIndex(e => e.key == key);
        if (idx >= 0) entries[idx].value = value;
        else entries.Add(new BoolKV { key = key, value = value });
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
