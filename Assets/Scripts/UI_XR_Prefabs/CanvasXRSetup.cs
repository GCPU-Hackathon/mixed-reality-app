using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasXRSetup : MonoBehaviour
{
    void Awake()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
        {
            // essaie d’abord Camera.main
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                return;
            }

            // fallback : prend n’importe quelle caméra active
            var cams = FindObjectsOfType<Camera>();
            if (cams.Length > 0)
            {
                canvas.worldCamera = cams[0];
            }
        }
    }
}
