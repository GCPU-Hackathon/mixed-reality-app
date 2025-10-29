using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Threading.Tasks;

public class ChatManager : MonoBehaviour
{
    [Header("Prefabs & Layout")]
    public GameObject userBubblePrefab; // bulle BLEUE
    public GameObject botBubblePrefab;  // bulle BLANCHE
    public Transform chatContainer;
    public ScrollRect chatScrollRect;

    [Header("Typing Bubble Texts")]
    [Tooltip("Texte de base pour l'utilisateur pendant STT (ex: vide => juste '...')")]
    public string userTypingBaseText = ""; // après enregistrement, STT en cours
    [Tooltip("Texte de base pour Cassandra pendant génération")]
    public string botTypingBaseText = "Cassandra est en train d'écrire";
    [Tooltip("Texte de base pendant l'écoute micro EN DIRECT")]
    public string listeningBaseText = "🎤 Listening";

    [Tooltip("Vitesse d'animation des points (sec)")]
    public float typingStepDuration = 0.4f;
    [Tooltip("Nombre max de points dans l'animation")]
    [Range(1,4)]
    public int typingMaxDots = 3;

    [Header("Debug keys (optional)")]
    public Key debugUserKey = Key.Space;
    public Key debugBotKey  = Key.Enter;

    private InputAction sendUserTestAction;
    private InputAction sendBotTestAction;

    // historique final propre
    public class ChatEntry
    {
        public string role; // "user" | "assistant"
        public string text;
    }
    private readonly List<ChatEntry> history = new List<ChatEntry>();

    // bulles temporaires
    private GameObject listeningBubble;      // "🎤 Listening ..."
    private GameObject pendingUserBubble;    // user STT "..."
    private GameObject pendingBotBubble;     // bot typing "Cassandra ..."

    private void Awake()
    {
        // debug pour tester vite
        sendUserTestAction = new InputAction(
            name: "SendUserMsg",
            type: InputActionType.Button,
            binding: "<Keyboard>/" + KeyToControlPath(debugUserKey)
        );
        sendUserTestAction.performed += _ =>
        {
            // simulate pipeline:
            CreateListeningBubble();
            StartCoroutine(FakeAfterDelay(1.0f, () =>
            {
                RemoveListeningBubble();
                CreateUserTypingBubble();
                StartCoroutine(FakeAfterDelay(1.0f, () =>
                {
                    FinalizeUserTypingBubble("Je veux commander un café.");
                    CreateBotTypingBubble();
                    StartCoroutine(FakeAfterDelay(1.0f, () =>
                    {
                        FinalizeBotTypingBubble("Très bien. Quelle boisson exactement ?");
                    }));
                }));
            }));
        };

        sendBotTestAction = new InputAction(
            name: "SendBotMsg",
            type: InputActionType.Button,
            binding: "<Keyboard>/" + KeyToControlPath(debugBotKey)
        );
        sendBotTestAction.performed += _ =>
        {
            CreateBotTypingBubble();
            StartCoroutine(FakeAfterDelay(1.2f, () =>
            {
                FinalizeBotTypingBubble("Réponse Cassandra simulée.");
            }));
        };
    }

    private void OnEnable()
    {
        sendUserTestAction.Enable();
        sendBotTestAction.Enable();
    }

    private void OnDisable()
    {
        sendUserTestAction.Disable();
        sendBotTestAction.Disable();
    }

    private void Start()
    {
        AddBotMessage("Bonjour, je suis Cassandra. En quoi puis-je t’aider ?");
    }

    // -------------------------
    // PUBLIC: bulles "Listening"
    // -------------------------

    // Appelée quand l'enregistrement micro commence
    public void CreateListeningBubble()
    {
        // On détruit l’ancienne si elle traîne
        if (listeningBubble != null)
        {
            Destroy(listeningBubble);
            listeningBubble = null;
        }

        listeningBubble = SpawnTypingBubble(
            isUser: true,
            baseText: listeningBaseText // "🎤 Listening"
        );
    }

    // Appelée quand on arrête l'enregistrement micro (avant STT)
    public void RemoveListeningBubble()
    {
        if (listeningBubble != null)
        {
            Destroy(listeningBubble);
            listeningBubble = null;
        }
        ScrollToBottomNextFrame();
    }

    // -------------------------
    // PUBLIC: placeholder user (STT)
    // -------------------------

    public void CreateUserTypingBubble()
    {
        // On enlève une éventuelle bulle STT précédente
        if (pendingUserBubble != null)
        {
            Destroy(pendingUserBubble);
            pendingUserBubble = null;
        }

        pendingUserBubble = SpawnTypingBubble(
            isUser: true,
            baseText: userTypingBaseText // généralement "" pour afficher juste "..."
        );
    }

    public void FinalizeUserTypingBubble(string finalText)
    {
        if (pendingUserBubble != null)
        {
            StopTypingAnimationAndSetText(pendingUserBubble, finalText);

            history.Add(new ChatEntry
            {
                role = "user",
                text = finalText
            });

            pendingUserBubble = null;
        }
        else
        {
            // fallback si pas de bulle STT (ex: STT direct)
            AddUserMessage(finalText);
        }

        ScrollToBottomNextFrame();
    }

    // -------------------------
    // PUBLIC: placeholder bot (Gemini en train d'écrire)
    // -------------------------

    public void CreateBotTypingBubble()
    {
        if (pendingBotBubble != null)
        {
            Destroy(pendingBotBubble);
            pendingBotBubble = null;
        }

        pendingBotBubble = SpawnTypingBubble(
            isUser: false,
            baseText: botTypingBaseText // "Cassandra est en train d'écrire"
        );
    }

    public void FinalizeBotTypingBubble(string finalText)
    {
        if (pendingBotBubble != null)
        {
            StopTypingAnimationAndSetText(pendingBotBubble, finalText);

            history.Add(new ChatEntry
            {
                role = "assistant",
                text = finalText
            });

            pendingBotBubble = null;
        }
        else
        {
            AddBotMessage(finalText);
        }

        ScrollToBottomNextFrame();
    }

    // -------------------------
    // PUBLIC: messages finaux directs
    // -------------------------

    public void AddUserMessage(string text)
    {
        // crée une bulle BLEUE finale sans anim
        SpawnFinalBubble(text, isUser: true);

        history.Add(new ChatEntry
        {
            role = "user",
            text = text
        });
    }

    public void AddBotMessage(string text)
    {
        // crée une bulle BLANCHE finale sans anim
        SpawnFinalBubble(text, isUser: false);

        history.Add(new ChatEntry
        {
            role = "assistant",
            text = text
        });
    }

    public IReadOnlyList<ChatEntry> GetHistory() => history.AsReadOnly();

    // -------------------------
    // INTERNAL UTILS
    // -------------------------

    private GameObject SpawnTypingBubble(bool isUser, string baseText)
    {
        GameObject prefab = isUser ? userBubblePrefab : botBubblePrefab;
        GameObject bubble = Instantiate(prefab, chatContainer);

        var tmp = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            // set text de base (sans points dynamiques)
            tmp.text = baseText;
        }

        // brancher l'animateur "..."
        var animator = bubble.AddComponent<TypingBubbleAnimator>();
        animator.baseText = baseText;
        animator.stepDuration = typingStepDuration;
        animator.maxDots = typingMaxDots;
        animator.StartAnimation();

        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContainer as RectTransform);
        ScrollToBottomNextFrame();
        return bubble;
    }

    private void StopTypingAnimationAndSetText(GameObject bubble, string finalText)
    {
        var animator = bubble.GetComponent<TypingBubbleAnimator>();
        if (animator != null)
        {
            animator.StopAnimation();
            Destroy(animator);
        }

        var tmp = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = finalText;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContainer as RectTransform);
        ScrollToBottomNextFrame();
    }

    private void SpawnFinalBubble(string messageText, bool isUser)
    {
        GameObject prefab = isUser ? userBubblePrefab : botBubblePrefab;
        GameObject bubble = Instantiate(prefab, chatContainer);

        var tmp = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = messageText;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContainer as RectTransform);
        ScrollToBottomNextFrame();
    }

    private async void ScrollToBottomNextFrame()
    {
        await Task.Yield();
        if (chatScrollRect != null)
        {
            chatScrollRect.verticalNormalizedPosition = 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContainer as RectTransform);
        }
    }

    private string KeyToControlPath(Key k)
    {
        return k.ToString().ToLower();
    }

    private IEnumerator FakeAfterDelay(float seconds, System.Action cb)
    {
        yield return new WaitForSeconds(seconds);
        cb?.Invoke();
    }
}
