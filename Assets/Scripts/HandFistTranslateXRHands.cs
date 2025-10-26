using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[RequireComponent(typeof(Collider))]
public class HandFistTranslateXRHands : MonoBehaviour
{
    // Exposé pour coordination globale avec les autres scripts
    public static bool TranslatingActive = false;

    [Header("Référence main")]
    [Tooltip("Joint utilisé comme point de référence du mouvement (Palm conseillé, Wrist en secours).")]
    public XRHandJointID referenceJoint = XRHandJointID.Palm;

    [Header("Détection du poing")]
    [Tooltip("Distance max (m) entre les tips et la paume pour considérer 'poing fermé'.")]
    public float fistCloseThreshold = 0.06f;
    [Tooltip("Hystérésis de relâchement (m) au-dessus du seuil pour arrêter le poing.")]
    public float fistReleaseBuffer = 0.015f;

    [Header("Mouvement")]
    [Tooltip("Lissage (0 = sans lissage, 10-20 = fluide).")]
    public float followSmoothing = 12f; // lerp speed (par seconde)

    [Header("Debug")]
    public bool debugLogs = true;
    public bool drawDebug = false;

    XRHandSubsystem handSubsystem;

    bool leftFist, rightFist;
    bool translating = false;
    Handedness activeHand = Handedness.Left;
    Vector3 startObjOffset;   // objet - main
    Vector3 lastRefPos;

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) { handSubsystem = subs[0]; Log($"XRHandSubsystem: {handSubsystem}"); }
        else { Warn("XRHandSubsystem introuvable (OpenXR Hands)"); }
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand L = handSubsystem.leftHand;
        XRHand R = handSubsystem.rightHand;

        // Si le scale est actif, on ne traduit pas
        if (HandPinchScaleXRHands.ScalingActive)
        {
            StopTranslate();
            return;
        }

        // Détection poing sur chaque main
        Vector3 lRef, rRef;
        leftFist  = IsFist(L, out lRef);
        rightFist = IsFist(R, out rRef);

        // Démarrage translation : une seule main en poing, l’autre pas
        if (!translating)
        {
            if (leftFist ^ rightFist) // XOR
            {
                translating = true;
                TranslatingActive = true;
                activeHand = leftFist ? Handedness.Left : Handedness.Right;
                lastRefPos = leftFist ? lRef : rRef;
                startObjOffset = transform.position - lastRefPos;
                Log($"[Translate] START via {activeHand} | offset={startObjOffset}");
            }
            else
            {
                return; // rien à faire
            }
        }

        // En cours de translation : suivre la main active
        XRHand h = (activeHand == Handedness.Left) ? L : R;
        Vector3 refPos;
        bool fistNow = IsFist(h, out refPos);

        // Si la main active perd le poing → stop
        if (!fistNow || !h.isTracked)
        {
            StopTranslate();
            return;
        }

        // Cible de déplacement = ref main + offset initial
        Vector3 targetPos = refPos + startObjOffset;

        if (followSmoothing <= 0f)
            transform.position = targetPos;
        else
            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

        lastRefPos = refPos;

        if (drawDebug)
        {
            Debug.DrawLine(refPos, targetPos, Color.green);
            Debug.DrawRay(refPos, Vector3.up * 0.03f, Color.green);
        }
    }

    void StopTranslate()
    {
        if (translating)
        {
            Log("[Translate] STOP");
            translating = false;
            TranslatingActive = false;
        }
    }

    // ------ Détection poing ------
    bool IsFist(XRHand hand, out Vector3 refPos)
    {
        refPos = default;
        if (!hand.isTracked) return false;

        // Pose de référence (palm préféré, sinon wrist)
        Pose palmPose;
        if (!TryGet(hand, referenceJoint, out palmPose))
        {
            if (!TryGet(hand, XRHandJointID.Wrist, out palmPose))
                return false;
        }
        refPos = palmPose.position;

        // Tips → Paume
        Pose tipI, tipM, tipR, tipL, tipT;
        if (!TryGet(hand, XRHandJointID.IndexTip,  out tipI)) return false;
        if (!TryGet(hand, XRHandJointID.MiddleTip, out tipM)) return false;
        if (!TryGet(hand, XRHandJointID.RingTip,   out tipR)) return false;
        if (!TryGet(hand, XRHandJointID.LittleTip, out tipL)) return false;
        if (!TryGet(hand, XRHandJointID.ThumbTip,  out tipT)) return false;

        float dI = Vector3.Distance(tipI.position, palmPose.position);
        float dM = Vector3.Distance(tipM.position, palmPose.position);
        float dR = Vector3.Distance(tipR.position, palmPose.position);
        float dL = Vector3.Distance(tipL.position, palmPose.position);
        float dT = Vector3.Distance(tipT.position, palmPose.position);

        // Seuils avec hystérésis
        float close = fistCloseThreshold;
        float open  = fistCloseThreshold + fistReleaseBuffer;

        // Heuristique : 4 doigts proches + pouce proche → poing.
        // (on utilise 'open' pour relâchement automatique plus stable)
        bool closed =
            dI < close && dM < close && dR < close && dL < close && dT < (close + 0.02f);

        bool opened =
            dI > open || dM > open || dR > open || dL > open || dT > (open + 0.02f);

        // On retourne simplement l’état instantané « fermé »
        return closed && !opened;
    }

    bool TryGet(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default; return false;
    }

    // ---- Debug helpers ----
    string lastLog = "";
    void Log(string m){ if (debugLogs){ Debug.Log($"[HandFistTranslate] {m}"); lastLog = ""; } }
    void Warn(string m){ if (debugLogs){ Debug.LogWarning($"[HandFistTranslate] {m}"); lastLog = ""; } }
}
