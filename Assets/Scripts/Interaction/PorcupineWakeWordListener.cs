using System;
using System.Collections.Generic;
using UnityEngine;
using Pv.Unity;

public class PorcupineWakeWordListener : MonoBehaviour
{
    [Header("Picovoice / Porcupine")]
    [SerializeField] private string accessKey = "YOUR_ACCESS_KEY_HERE";

    [Header("Wake Word Personnalisé (.ppn)")]
    [SerializeField] private string customKeywordPath = "";
    [SerializeField] private string customModelPath = "";
    [Range(0f, 1f)]
    [SerializeField] private float customSensitivity = 0.6f;

    [Header("Wake Word Intégré (fallback si pas de .ppn)")]
    [SerializeField] private Porcupine.BuiltInKeyword builtInKeyword = Porcupine.BuiltInKeyword.PORCUPINE;

    [Header("Lifecycle")]
    [SerializeField] private bool autoStart = true;

    public event Action OnWakeWordDetected;

    private PorcupineManager porcupineManager;
    private bool isRunning = false; // moteur audio tournant
    private bool isPaused  = false; // doit-on ignorer / empêcher callbacks ?

    private void Start()
    {
        // normalize paths if relative
        if (!string.IsNullOrEmpty(customKeywordPath) && !System.IO.Path.IsPathRooted(customKeywordPath))
            customKeywordPath = System.IO.Path.Combine(Application.streamingAssetsPath, customKeywordPath);

        if (!string.IsNullOrEmpty(customModelPath) && !System.IO.Path.IsPathRooted(customModelPath))
            customModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, customModelPath);

        try
        {
            if (!string.IsNullOrEmpty(customKeywordPath))
            {
                var keywordList = new List<string> { customKeywordPath };
                var sens = new List<float> { customSensitivity };

                porcupineManager = PorcupineManager.FromKeywordPaths(
                    accessKey,
                    keywordList,
                    HandleWakeWordDetected,
                    string.IsNullOrEmpty(customModelPath) ? null : customModelPath,
                    sens,
                    OnPorcupineError
                );
            }
            else
            {
                porcupineManager = PorcupineManager.FromBuiltInKeywords(
                    accessKey,
                    new List<Porcupine.BuiltInKeyword> { builtInKeyword },
                    HandleWakeWordDetected,
                    modelPath: null,
                    sensitivities: null,
                    processErrorCallback: OnPorcupineError
                );
            }

            if (autoStart)
            {
                StartListening();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[WakeWordListener] ❌ Init Porcupine: " + ex.Message);
        }
    }

    private void OnDestroy()
    {
        if (porcupineManager != null)
        {
            if (isRunning)
            {
                porcupineManager.Stop();
            }
            porcupineManager.Delete();
            porcupineManager = null;
        }
    }

    // callback Porcupine
    private void HandleWakeWordDetected(int keywordIndex)
    {
        // si en pause logique → ne rien déclencher
        if (isPaused)
        {
            Debug.Log("[WakeWordListener] Wake word détecté mais ignoré (pause)");
            return;
        }

        Debug.Log("[WakeWordListener] ✅ wake word détecté, index " + keywordIndex);
        OnWakeWordDetected?.Invoke();
    }

    private void OnPorcupineError(PorcupineException e)
    {
        Debug.LogError("[WakeWordListener] 🚨 Porcupine runtime error: " + e.Message);
    }

    // -------- API publique --------

    // Cassandra doit écouter normalement
    public void StartListening()
    {
        if (porcupineManager == null) return;

        if (!isRunning)
        {
            porcupineManager.Start();
            isRunning = true;
        }

        isPaused = false;
        Debug.Log("[WakeWordListener] ▶ StartListening -> running, not paused");
    }

    // Cassandra se tait totalement (désactive le hotword complètement)
    public void StopListening()
    {
        if (porcupineManager == null) return;
        if (isRunning)
        {
            porcupineManager.Stop();
            isRunning = false;
        }
        isPaused = true; // en pratique si elle est stoppée on la considère pas prête
        Debug.Log("[WakeWordListener] ⏹ StopListening -> stopped, paused");
    }

    // Cassandra coupe juste l'écoute PENDANT que l'humain parle au micro
    public void PauseListening()
    {
        if (porcupineManager == null) return;

        if (isRunning)
        {
            porcupineManager.Stop();
            isRunning = false;
        }

        isPaused = true;
        Debug.Log("[WakeWordListener] ⏸ PauseListening -> stopped, paused");
    }

    // Cassandra doit réécouter le wake word IMMÉDIATEMENT (pour barge-in ou nouvelle requête)
    public void ResumeListening()
    {
        if (porcupineManager == null) return;

        if (!isRunning)
        {
            porcupineManager.Start();
            isRunning = true;
        }

        isPaused = false;
        Debug.Log("[WakeWordListener] ▶ ResumeListening -> running, not paused");
    }
}
