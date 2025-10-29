using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class GeminiVoiceInterface : MonoBehaviour
{
    [Header("Google Cloud / Backend URLs")]
    public string speechToText_URL;
    public string gemini_URL;
    public string textToSpeech_URL;

    [Header("Input / Wake")]
    [Tooltip("Bouton manette / clavier pour démarrer manuellement l'écoute")]
    public InputActionReference voiceAction;
    [Tooltip("Composant wake word (Porcupine) qui déclenche la prise de parole")]
    public PorcupineWakeWordListener wakeWordListener;

    [Header("UI")]
    [Tooltip("Texte d'état sous le micro, genre 'Recording...'")]
    public TextMeshProUGUI statusText;
    [Tooltip("Lecture audio TTS sort ici")]
    public AudioSource audioSource;

    [Header("Chat Binding")]
    [Tooltip("Référence vers le ChatManager dans la scène")]
    public ChatManager chatManager;

    // --- VAD / timing auto ---
    [Header("Silence Detection (VAD)")]
    [Tooltip("Volume moyen en-dessous duquel on considère que l'utilisateur est silencieux")]
    public float silenceThreshold = 0.01f;
    [Tooltip("Durée de silence avant d'arrêter l'enregistrement")]
    public float silenceDuration = 5.0f;
    private float silenceTimer = 0f;
    private int sampleWindow = 512; // chunk analysé

    // --- Audio capture ---
    private bool isRecording = false;
    private AudioClip recording;
    private const int RECORD_DURATION = 15; // secondes max
    private const int SAMPLE_RATE = 16000;

    private bool isSpeaking = false;

    public event System.Action OnStartListening;
    public event System.Action OnStopListening;

    private void Awake()
    {
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (statusText != null)
            statusText.text = "Dis le mot-clé ou appuie pour parler";

        if (wakeWordListener == null)
        {
            Debug.LogWarning("[GeminiVoiceInterface] WakeWordListener pas assigné : le hotword ne pourra pas déclencher automatiquement.");
        }
        else
        {
            // on s'abonne au wake word
            wakeWordListener.OnWakeWordDetected += OnWakeWordHeard;
        }
    }

    private void OnEnable()
    {
        if (voiceAction != null)
            voiceAction.action.performed += OnVoiceButtonPressed;
    }

    private void OnDisable()
    {
        if (voiceAction != null)
            voiceAction.action.performed -= OnVoiceButtonPressed;

        if (wakeWordListener != null)
            wakeWordListener.OnWakeWordDetected -= OnWakeWordHeard;
    }

    // -----
    // INPUTS
    // -----

    private void OnVoiceButtonPressed(InputAction.CallbackContext ctx)
    {
        // bouton manuel (touche A manette, etc.)
        ToggleRecordingOrStop();
    }

private void OnWakeWordHeard()
{
    // Si Cassandra est en train de parler -> le wake word sert à STOP la voix
    if (isSpeaking)
    {
        Debug.Log("[GeminiVoiceInterface] Wake word reçu pendant TTS -> on coupe");
        ForceStopTTS();
        return;
    }

    // Si elle n'est pas déjà en train d'enregistrer ta voix -> on lance l'écoute utilisateur
    if (!isRecording)
    {
        ToggleRecordingOrStop(); // va appeler StartRecording()
    }
    // Si elle est déjà en train d'écouter ta voix, on ne fait rien.
}


    private void ToggleRecordingOrStop()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopAndProcessRecording();
        }
    }

    // -----
    // LOOP
    // -----
    private void Update()
    {
        // Debug clavier pour test rapide
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            ToggleRecordingOrStop();
        }

        // VAD logique pendant l'enregistrement
        if (!isRecording)
            return;

        float[] samples = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(null);

        if (micPosition <= 0)
            return; // le micro vient juste de démarrer

        int startPos = micPosition - sampleWindow;
        if (startPos < 0) startPos = 0;

        recording.GetData(samples, startPos);

        // volume moyen absolu
        float sum = 0;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        float avgVolume = sum / sampleWindow;

        // reset timer si ça parle
        if (avgVolume > silenceThreshold)
        {
            silenceTimer = 0f;
        }
        else
        {
            silenceTimer += Time.deltaTime;
        }

        if (silenceTimer >= silenceDuration)
        {
            Debug.Log($"[GeminiVoiceInterface] Silence {silenceDuration}s -> stop auto.");
            StopAndProcessRecording();
        }
    }

    // -----
    // RECORD
    // -----

private void StartRecording()
{
    if (Microphone.devices.Length == 0)
    {
        Debug.LogError("[GeminiVoiceInterface] Aucun micro détecté !");
        return;
    }

    // on ne veut PAS que Cassandra se réveille pendant que tu parles
    if (wakeWordListener != null)
        wakeWordListener.PauseListening();

    silenceTimer = 0f;
    isRecording = true;
    OnStartListening?.Invoke();

    if (chatManager != null)
        chatManager.CreateListeningBubble(); // "🎤 Listening ..."

    if (statusText != null)
        statusText.text = "J'écoute... (je coupe après silence)";

    Debug.Log("[GeminiVoiceInterface] 🎤 StartRecording()");
    recording = Microphone.Start(null, false, RECORD_DURATION, SAMPLE_RATE);

}




private void StopAndProcessRecording()
{
    if (!isRecording) return;

    isRecording = false;
    OnStopListening?.Invoke();
    Microphone.End(null);
    silenceTimer = 0f;

    if (statusText != null)
        statusText.text = "Transcription en cours...";

    Debug.Log("[GeminiVoiceInterface] ⏹ StopRecording() -> STT");

    // On enlève la bulle "Listening..."
    if (chatManager != null)
    {
        chatManager.RemoveListeningBubble();

        // On montre la bulle STT "..." bleue
        chatManager.CreateUserTypingBubble();
    }

    // ⬅⬅⬅ CRUCIAL : Cassandra doit déjà réentendre le wake word maintenant
    if (wakeWordListener != null)
        wakeWordListener.ResumeListening();

    byte[] audioData = WavUtility.FromAudioClip(recording);
    StartCoroutine(SendAudioToSTT(audioData));
}





    // -----
    // STEP 1: STT
    // -----
    private IEnumerator SendAudioToSTT(byte[] audioData)
    {
        UnityWebRequest request = UnityWebRequest.PostWwwForm(speechToText_URL, "POST");
        request.uploadHandler = new UploadHandlerRaw(audioData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "audio/wav");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string transcription = request.downloadHandler.text;
            Debug.Log("[GeminiVoiceInterface] 🗣 STT -> " + transcription);

            // ✨ NOUVEAU : on remplace la bulle bleue "…" par le texte final
            if (chatManager != null)
            {
                chatManager.FinalizeUserTypingBubble(transcription);
            }

            // Maintenant on va demander à Cassandra
            StartCoroutine(SendPromptToGemini(transcription));
        }
        else
        {
            Debug.LogError("[GeminiVoiceInterface] STT Error: " + request.error);
            if (statusText != null)
                statusText.text = "Erreur STT. Réessaie.";

            // si STT échoue, la bulle bleue "..." resterait moche.
            // on peut choisir de la finaliser avec "[inaudible]" :
            if (chatManager != null)
            {
                chatManager.FinalizeUserTypingBubble("[Transcription échouée]");
            }

            if (wakeWordListener != null)
                wakeWordListener.PauseListening();
        }
    }

    // -----
    // STEP 2: Gemini
    // -----
    private IEnumerator SendPromptToGemini(string userText)
    {
        if (statusText != null)
            statusText.text = "Cassandra réfléchit...";

        // ✨ NOUVEAU : on crée la bulle blanche "Cassandra est en train d'écrire…"
        if (chatManager != null)
        {
            chatManager.CreateBotTypingBubble();
        }

        GeminiRequest data = new GeminiRequest { prompt = userText };
        string jsonData = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(gemini_URL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string geminiResponse = request.downloadHandler.text;
            Debug.Log("[GeminiVoiceInterface] 🤖 Gemini -> " + geminiResponse);

            // ✨ NOUVEAU : on remplace le placeholder blanc par la vraie réponse
            if (chatManager != null)
            {
                chatManager.FinalizeBotTypingBubble(geminiResponse);
            }

            // lire à voix haute
            StartCoroutine(SendToTTSAndPlay(geminiResponse));
        }
        else
        {
            Debug.LogError("[GeminiVoiceInterface] Gemini Error: " + request.error);
            if (statusText != null)
                statusText.text = "Erreur Cassandra.";

            if (chatManager != null)
            {
                chatManager.FinalizeBotTypingBubble("[Cassandra Error]");
            }

            if (wakeWordListener != null)
                wakeWordListener.ResumeListening();
        }
    }


    // -----
    // STEP 3: TTS
    // -----

private IEnumerator SendToTTSAndPlay(string text)
{
    if (statusText != null)
        statusText.text = "Lecture de la réponse...";

    TTSRequest data = new TTSRequest { text = text };
    string jsonData = JsonUtility.ToJson(data);

    UnityWebRequest request = new UnityWebRequest(textToSpeech_URL, "POST");
    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
    request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
    request.downloadHandler = new DownloadHandlerAudioClip(textToSpeech_URL, AudioType.MPEG);
    request.SetRequestHeader("Content-Type", "application/json");

    yield return request.SendWebRequest();

    if (request.result == UnityWebRequest.Result.Success)
    {
        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
        {
            audioSource.clip = clip;
            audioSource.Play();
            isSpeaking = true;

            // Cassandra parle → à ce moment-là, wakeWordListener est déjà ResumeListening(),
            // donc elle peut entendre son nom pour interruption

            yield return new WaitUntil(() => !audioSource.isPlaying);

            isSpeaking = false;
        }
        else
        {
            Debug.LogError("[GeminiVoiceInterface] TTS: AudioClip invalide.");
        }

        if (statusText != null)
            statusText.text = "Dis le mot-clé ou appuie pour parler";
    }
    else
    {
        Debug.LogError("[GeminiVoiceInterface] TTS Error: " + request.error);

        if (statusText != null)
            statusText.text = "Erreur TTS.";
    }
}


public void ForceStopTTS()
{
    if (isSpeaking && audioSource != null && audioSource.isPlaying)
    {
        Debug.Log("[GeminiVoiceInterface] ⛔ Interruption vocale demandée");
        audioSource.Stop();
        isSpeaking = false;

        if (statusText != null)
            statusText.text = "Dis le mot-clé ou appuie pour parler";
    }
    else
    {
        Debug.Log("[GeminiVoiceInterface] ForceStopTTS() appelé mais rien à stopper.");
    }
}



}

// Data payloads
[System.Serializable]
public class GeminiRequest { public string prompt; }

[System.Serializable]
public class TTSRequest { public string text; }
