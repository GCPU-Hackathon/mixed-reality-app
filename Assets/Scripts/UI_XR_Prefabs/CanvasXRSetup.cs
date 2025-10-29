using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasXRSetup : MonoBehaviour
{
    void Awake()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
        {
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                return;
            }

            var cams = FindObjectsOfType<Camera>();
            if (cams.Length > 0)
            {
                canvas.worldCamera = cams[0];
            }
        }
    }
}
