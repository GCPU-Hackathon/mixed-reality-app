using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Nouveau Input System

public class MicButtonPulse : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image outerRing;
    [SerializeField] private Image innerRing;
    [SerializeField] private Image micIcon;

    [Header("Wake Word Listener")]
    [SerializeField] private PorcupineWakeWordListener wakeWordListener;
    // Drag & drop le GameObject qui a PorcupineWakeWordListener dessus

    [Header("Colors")]
    [SerializeField] private Color idleRingColor = new Color(0.2f, 0.4f, 0.8f, 1f);
    [SerializeField] private Color listeningRingColor = new Color(0.1f, 0.8f, 1f);
    [SerializeField] private Color idleInnerColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color listeningInnerColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color idleMicColor = Color.white;
    [SerializeField] private Color listeningMicColor = Color.white;

    [Header("Pulse")]
    [SerializeField] private float pulseScaleMin = 1.0f;
    [SerializeField] private float pulseScaleMax = 1.15f;
    [SerializeField] private float pulseSpeed = 4f;

    [Header("Keyboard debug key (optional)")]
    [SerializeField] private bool enableKeyboardDebug = true;
    [SerializeField] private Key keyboardToggleKey = Key.V;

    [Header("Voice Interface")]
    [SerializeField] private GeminiVoiceInterface geminiVoiceInterface;

    private InputAction toggleAction;
    private bool isListening = false;
    private RectTransform outerRingRect;


    // ----------------------------------------------------------------
    // LIFECYCLE
    // ----------------------------------------------------------------
    private void Awake()
    {
        if (outerRing != null)
            outerRingRect = outerRing.GetComponent<RectTransform>();

        if (enableKeyboardDebug)
        {
            // Création d'une action d'input dynamique compatible Input System
            toggleAction = new InputAction(
                name: "ToggleMic",
                type: InputActionType.Button,
                binding: "<Keyboard>/" + KeyToControlPath(keyboardToggleKey)
            );

            toggleAction.performed += ctx =>
            {
                ToggleListening();
            };
        }
    }

    private void OnEnable()
    {
        if (enableKeyboardDebug && toggleAction != null)
            toggleAction.Enable();

        if (geminiVoiceInterface != null)
        {
            geminiVoiceInterface.OnStartListening += HandleStartListening;
            geminiVoiceInterface.OnStopListening += HandleStopListening;
        }

        // S'abonner à Porcupine
        if (wakeWordListener != null)
        {
            wakeWordListener.OnWakeWordDetected += HandleWakeWord;
        }
        else
        {
            Debug.LogWarning("[MicButtonPulse] wakeWordListener n'est pas assigné dans l'inspecteur !");
        }
    }

    private void OnDisable()
    {
        if (enableKeyboardDebug && toggleAction != null)
            toggleAction.Disable();

        if (geminiVoiceInterface != null)
        {
            geminiVoiceInterface.OnStartListening -= HandleStartListening;
            geminiVoiceInterface.OnStopListening -= HandleStopListening;
        }

        // Se désabonner proprement
        if (wakeWordListener != null)
        {
            wakeWordListener.OnWakeWordDetected -= HandleWakeWord;
        }
    }

    private void Start()
    {
        SetVisualIdle();
    }

    private void Update()
    {
        // Animation "pulse" quand on est en écoute
        if (isListening && outerRingRect != null)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0..1
            float scale = Mathf.Lerp(pulseScaleMin, pulseScaleMax, t);
            outerRingRect.localScale = new Vector3(scale, scale, 1f);

            // boost visuel
            Color baseC = listeningRingColor;
            float glow = Mathf.Lerp(0.0f, 0.25f, t);
            var c = baseC * (1f + glow);
            outerRing.color = new Color(c.r, c.g, c.b, 1f);
        }
        else if (!isListening && outerRingRect != null)
        {
            // état repos
            outerRingRect.localScale = Vector3.one;
        }
    }


    // ----------------------------------------------------------------
    // EVENT CALLBACKS
    // ----------------------------------------------------------------
    private void HandleWakeWord()
    {
        // Ici tu décides ce que signifie "Cassandra".
        // Option 1 : si pas déjà en écoute -> passe en écoute.
        // Option 2 : toggle à chaque fois.
        // Ici je fais "si pas déjà en écoute, active":
        // if (!isListening)
        // {
        //     ToggleListening();
        // }
        // else
        // {
        //     // si tu veux que dire "cassandra" à nouveau coupe l'écoute,
        //     // décommente la ligne suivante :
        //     // ToggleListening();
        // }
    }


    // ----------------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------------
    public void ToggleListening()
    {
        isListening = !isListening;

        if (isListening)
            SetVisualListening();
        else
            SetVisualIdle();
    }


    // ----------------------------------------------------------------
    // VISUELS
    // ----------------------------------------------------------------
    private void SetVisualIdle()
    {
        if (outerRing != null) outerRing.color = idleRingColor;
        if (innerRing != null) innerRing.color = idleInnerColor;
        if (micIcon != null) micIcon.color = idleMicColor;

        if (outerRingRect != null)
            outerRingRect.localScale = Vector3.one;
    }

    private void SetVisualListening()
    {
        if (outerRing != null) outerRing.color = listeningRingColor;
        if (innerRing != null) innerRing.color = listeningInnerColor;
        if (micIcon != null) micIcon.color = listeningMicColor;
    }


    // ----------------------------------------------------------------
    // HELPERS
    // ----------------------------------------------------------------
    private string KeyToControlPath(Key k)
    {
        return k.ToString().ToLower();
    }

    private void HandleStartListening()
    {
        isListening = true;
        SetVisualListening();
    }

    private void HandleStopListening()
    {
        isListening = false;
        SetVisualIdle();
}

}
