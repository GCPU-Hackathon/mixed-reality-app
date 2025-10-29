using UnityEngine;
using TMPro;
using System.Collections;

public class TypingBubbleAnimator : MonoBehaviour
{
    [Header("Static text before the dots (can be empty)")]
    [TextArea]
    public string baseText = "";

    [Header("Animation timing")]
    [Tooltip("Seconds between each dot step")]
    public float stepDuration = 0.4f;

    [Tooltip("Max number of dots in the cycle (e.g. 3 -> '.', '..', '...', then back to '.')")]
    [Range(1, 4)]
    public int maxDots = 3;

    private TextMeshProUGUI targetTMP;
    private Coroutine animRoutine;
    private bool running = false;

    private void Awake()
    {
        targetTMP = GetComponentInChildren<TextMeshProUGUI>();
        if (targetTMP == null)
        {
            Debug.LogError("[TypingBubbleAnimator] Aucun TextMeshProUGUI trouvé !");
        }
    }

    private void OnEnable()
    {
        StartAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    public void StartAnimation()
    {
        if (running) return;
        if (targetTMP == null) return;

        running = true;
        animRoutine = StartCoroutine(AnimateDots());
    }

    public void StopAnimation()
    {
        running = false;
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
    }

    private IEnumerator AnimateDots()
    {
        int dotCount = 0;

        while (running)
        {
            dotCount++;
            if (dotCount > maxDots)
            {
                dotCount = 1;
            }

            string dots = new string('.', dotCount);

            if (targetTMP != null)
            {
                if (!string.IsNullOrEmpty(baseText))
                {
                    // "Listening" + " .."
                    targetTMP.text = baseText + " " + dots;
                }
                else
                {
                    // just "..." style
                    targetTMP.text = dots;
                }
            }

            yield return new WaitForSeconds(stepDuration);
        }
    }
}
