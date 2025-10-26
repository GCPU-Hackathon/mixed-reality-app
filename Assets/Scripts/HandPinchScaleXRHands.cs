using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[RequireComponent(typeof(Collider))]
public class HandPinchScaleXRHands : MonoBehaviour
{
    // --- ÉTAT GLOBAL : vrai pendant le scale à 2 mains ---
    public static bool ScalingActive = false;

    [Header("Pinch")]
    [Range(0.01f, 0.1f)] public float pinchDistanceThreshold = 0.03f;
    public float releaseHysteresis = 0.005f;

    [Header("Scaling")]
    public float minScale = 0.1f;
    public float maxScale = 3.0f;
    [Tooltip("1 = linéaire, <1 = plus doux, >1 = plus fort")]
    public float scaleResponse = 1.0f;
    public float rayMaxDistance = 2.0f;
    public LayerMask rayMask = ~0;
    public Transform targetRoot; // si null, utilise ce transform

    [Header("Rotation Lock")]
    public bool lockRotationWhileScaling = true;
    Quaternion lockedRotation;

    [Header("Debug")]
    public bool debugLogs = true;
    public bool drawDebug = true;

    XRHandSubsystem handSubsystem;

    // état
    bool leftPinching, rightPinching;
    float startDist = 0f;
    Vector3 startScale;

    void Start()
    {
        targetRoot = targetRoot ? targetRoot : transform;

        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) { handSubsystem = subs[0]; Log($"XRHandSubsystem: {handSubsystem}"); }
        else { Warn("XRHandSubsystem introuvable (OpenXR Hands)"); }
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand left = handSubsystem.leftHand;
        XRHand right = handSubsystem.rightHand;

        if (!left.isTracked || !right.isTracked)
        {
            // stop scale si une main perd le tracking
            startDist = 0f;
            ScalingActive = false;
            return;
        }

        // détection pinch + milieux pincement
        Vector3 lPinchPos, rPinchPos;
        leftPinching  = GetPinch(left,  out lPinchPos);
        rightPinching = GetPinch(right, out rPinchPos);

        // si les deux ne pincent pas -> fin du scale (avec hystérésis)
        if (!leftPinching || !rightPinching)
        {
            if (ShouldRelease(left, leftPinching) || ShouldRelease(right, rightPinching))
            {
                startDist = 0f;
                ScalingActive = false;
            }
            return;
        }

        // à partir d'ici : 2 mains pincent => mode scale
        // rayons vers le centre du cerveau
        Vector3 center = targetRoot.position;
        bool hL = TryRayToTarget(lPinchPos, (center - lPinchPos).normalized, out Vector3 lHit);
        bool hR = TryRayToTarget(rPinchPos, (center - rPinchPos).normalized, out Vector3 rHit);

        float distNow;
        if (hL && hR)
        {
            if (drawDebug)
            {
                Debug.DrawLine(lPinchPos, lHit, Color.cyan);
                Debug.DrawLine(rPinchPos, rHit, Color.cyan);
                Debug.DrawLine(lHit, rHit, Color.cyan);
            }
            distNow = Vector3.Distance(lHit, rHit);
        }
        else
        {
            // fallback : distance espace entre pinces
            distNow = Vector3.Distance(lPinchPos, rPinchPos);
            if (drawDebug) Debug.DrawLine(lPinchPos, rPinchPos, Color.red);
        }

        DoScale(distNow);
    }

    void LateUpdate()
    {
        // verrou rotation APRES toutes les autres écritures de la frame
        if (lockRotationWhileScaling && ScalingActive)
            transform.rotation = lockedRotation;
    }

    void DoScale(float distNow)
    {
        if (distNow <= 1e-6f) return;

        if (startDist <= 0f)
        {
            startDist  = Mathf.Max(distNow, 1e-4f);
            startScale = transform.localScale;
            if (lockRotationWhileScaling) lockedRotation = transform.rotation;
            ScalingActive = true; // <- annonce : on est en scale
            Log($"[Scale] START d0={startDist:F3} scale0={startScale.x:F2}");
            return;
        }

        float ratio = Mathf.Clamp(distNow / startDist, 0.01f, 100f);
        float response = Mathf.Pow(ratio, Mathf.Max(0.01f, scaleResponse));
        float s = Mathf.Clamp(startScale.x * response, minScale, maxScale);
        transform.localScale = new Vector3(s, s, s);
        LogOnce($"[Scale] d={distNow:F3} r={ratio:F2} → {s:F2}");
    }

    // --- helpers mains ---
    bool GetPinch(XRHand hand, out Vector3 pinchPos)
    {
        pinchPos = default;
        if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose i) ||
            !TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose t)) return false;

        float d = Vector3.Distance(i.position, t.position);
        pinchPos = 0.5f * (i.position + t.position);
        return d < pinchDistanceThreshold;
    }

    bool ShouldRelease(XRHand hand, bool currentlyPinching)
    {
        if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose i) ||
            !TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose t)) return true;
        float d = Vector3.Distance(i.position, t.position);
        return (!currentlyPinching && d > (pinchDistanceThreshold + releaseHysteresis));
    }

    bool TryRayToTarget(Vector3 origin, Vector3 dir, out Vector3 hitPoint)
    {
        hitPoint = default;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayMaxDistance, rayMask, QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.transform;
            if (t == targetRoot || t.IsChildOf(targetRoot))
            {
                hitPoint = hit.point;
                return true;
            }
        }
        return false;
    }

    bool TryGetJointPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var joint = hand.GetJoint(id);
        if (joint.TryGetPose(out pose)) return true;
        pose = default; return false;
    }

    // debug
    string lastLog = "";
    void Log(string m){ if (debugLogs){ Debug.Log($"[HandPinchScaleXRHands] {m}"); lastLog = ""; } }
    void Warn(string m){ if (debugLogs){ Debug.LogWarning($"[HandPinchScaleXRHands] {m}"); lastLog = ""; } }
    void LogOnce(string m){ if (!debugLogs) return; if (lastLog == m) return; Debug.Log($"[HandPinchScaleXRHands] {m}"); lastLog = m; }
}
