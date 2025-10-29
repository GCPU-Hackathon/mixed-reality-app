using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BubbleAppear : MonoBehaviour
{
    [SerializeField] private float animDuration = 0.2f; // vitesse d'apparition
    private CanvasGroup cg;
    private RectTransform rt;
    private float timer;
    private bool playing;

    void Awake()
    {
        // On récupère/ajoute un CanvasGroup pour gérer l'alpha facilement
        cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }

        rt = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // état initial avant anim
        cg.alpha = 0f;
        if (rt != null) rt.localScale = Vector3.one * 0.8f;
        timer = 0f;
        playing = true;
    }

    void Update()
    {
        if (!playing) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / animDuration);

        // easing doux (pop un peu élastique)
        float smooth = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic

        // alpha
        cg.alpha = Mathf.Lerp(0f, 1f, smooth);

        // scale
        if (rt != null)
        {
            float scale = Mathf.Lerp(0.8f, 1f, smooth);
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        if (t >= 1f)
        {
            playing = false;
            cg.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;
        }
    }
}
