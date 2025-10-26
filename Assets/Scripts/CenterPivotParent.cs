using UnityEngine;

public class CenterPivotParent : MonoBehaviour
{
    [ContextMenu("Center Pivot (Auto)")]
    void CenterPivot()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (!rend)
        {
            Debug.LogWarning("❌ No Renderer found — cannot center pivot automatically.");
            return;
        }

        // World-space bounds center of the mesh
        Vector3 center = rend.bounds.center;

        // Create a new parent at that position
        GameObject pivot = new GameObject(name + "_Pivot");
        pivot.transform.position = center;
        pivot.transform.rotation = transform.rotation;

        // Match scale and parent
        pivot.transform.localScale = transform.localScale;
        pivot.transform.SetParent(transform.parent, true);

        // Reparent the current object under it
        transform.SetParent(pivot.transform, true);

        Debug.Log($"✅ Pivot created at {center} for {name}");
    }
}
