using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class HandPinchRotate : MonoBehaviour
{
    [Header("Pinch")]
    [Range(0.01f, 0.1f)] public float pinchDistanceThreshold = 0.03f;
    public float releaseHysteresis = 0.005f;

    [Header("Rotation")]
    public float sensitivity = 1.0f;
    public bool clampToYOnly = true;

    [Header("Debug")]
    public bool debugLogs = true;

    XRHandSubsystem handSubsystem;

    bool hasGrabbingHand = false;
    Handedness grabbingHandedness = Handedness.Left;
    float startObjY;
    float startHandAngleY;
    string lastLog = "";

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        handSubsystem = subs.Count > 0 ? subs[0] : null;
        if (handSubsystem != null) Log($"XRHandSubsystem found: {handSubsystem}");
        else Warn("No XRHandSubsystem found.");
    }

    void Update()
    {
        if (handSubsystem == null) { Warn("handSubsystem is NULL"); return; }

        if (HandPinchScaleXRHands.ScalingActive || HandFistTranslateXRHands.TranslatingActive)
        {
            if (hasGrabbingHand) { Log("[Rotate] Busy (scale/translate) → release"); hasGrabbingHand = false; }
            return;
        }

        // >>> BLOQUE LA ROTATION TANT QUE LE SCALE EST ACTIF
        if (HandPinchScaleXRHands.ScalingActive)
        {
            if (hasGrabbingHand)
            {
                Log("[Rotate] Scaling active → release rotation");
                hasGrabbingHand = false;
            }
            return; // ne pas traiter la rotation
        }

        HandleHand(handSubsystem.leftHand);
        HandleHand(handSubsystem.rightHand);

        if (hasGrabbingHand)
        {
            XRHand active = (grabbingHandedness == Handedness.Left) ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!active.isTracked)
            {
                Log($"[{grabbingHandedness}] lost tracking → release");
                hasGrabbingHand = false;
            }
        }
    }

    void HandleHand(XRHand hand)
    {
        if (!hand.isTracked) { LogOnce($"{hand.handedness}: not tracked"); return; }

        if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose indexPose)) { LogOnce($"{hand.handedness}: IndexTip pose unavailable"); return; }
        if (!TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose thumbPose))  { LogOnce($"{hand.handedness}: ThumbTip pose unavailable");  return; }

        float pinchDist = Vector3.Distance(indexPose.position, thumbPose.position);
        bool pinching = pinchDist < pinchDistanceThreshold;

        if (!hasGrabbingHand)
        {
            if (pinching)
            {
                hasGrabbingHand = true;
                grabbingHandedness = hand.handedness;

                startObjY = transform.eulerAngles.y;
                startHandAngleY = HandAngleAroundObjectXZ(hand);
                Log($"[{hand.handedness}] START ROTATE | objY={startObjY:F1} handAngleY={startHandAngleY:F1}");
            }
            return;
        }

        if (hand.handedness != grabbingHandedness) { LogOnce($"{hand.handedness}: ignoring (other hand grabbing)"); return; }

        if (!pinching && pinchDist > (pinchDistanceThreshold + releaseHysteresis))
        {
            Log($"[{hand.handedness}] RELEASE ROTATE (dist={pinchDist:F3})");
            hasGrabbingHand = false;
            return;
        }

        float currentHandAngle = HandAngleAroundObjectXZ(hand);
        float delta = Mathf.DeltaAngle(startHandAngleY, currentHandAngle);
        float targetY = startObjY + delta * sensitivity;

        transform.rotation = Quaternion.Euler(0f, targetY, 0f);
        LogOnce($"[{hand.handedness}] ROTATE Δ={delta:F1} → Y={targetY:F1}");
    }

    float HandAngleAroundObjectXZ(XRHand hand)
    {
        Pose jointPose;
        if (!TryGetJointPose(hand, XRHandJointID.IndexProximal, out jointPose))
        {
            TryGetJointPose(hand, XRHandJointID.Wrist, out jointPose);
            LogOnce($"{hand.handedness}: using Wrist fallback");
        }
        Vector3 center = transform.position;
        Vector3 p = jointPose.position;
        Vector3 v = new Vector3(p.x - center.x, 0f, p.z - center.z);
        if (v.sqrMagnitude < 1e-6f) return 0f;
        return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }

    bool TryGetJointPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var joint = hand.GetJoint(id);
        if (joint.TryGetPose(out pose)) return true;
        pose = default; return false;
    }

    void Log(string m){ if (debugLogs) { Debug.Log($"[HandPinchRotate] {m}"); lastLog = ""; } }
    void Warn(string m){ if (debugLogs) { Debug.LogWarning($"[HandPinchRotate] {m}"); lastLog = ""; } }
    void LogOnce(string m){ if (!debugLogs) return; if (lastLog == m) return; Debug.Log($"[HandPinchRotate] {m}"); lastLog = m; }
}
