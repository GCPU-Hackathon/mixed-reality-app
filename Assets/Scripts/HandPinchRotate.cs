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

    XRHandSubsystem handSubsystem;

    bool hasGrabbingHand = false;
    Handedness grabbingHandedness = Handedness.Left;
    float startObjY;
    float startHandAngleY;

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null) return;

        // si le scale est actif -> rotation coupée de force
        if (XRManipulationState.ScalingActive)
        {
            hasGrabbingHand = false;
            XRManipulationState.RotatingActive = false;
            return;
        }

        // si translation est active -> pas de rotation
        if (XRManipulationState.TranslatingActive)
        {
            hasGrabbingHand = false;
            XRManipulationState.RotatingActive = false;
            return;
        }

        HandleHand(handSubsystem.leftHand);
        HandleHand(handSubsystem.rightHand);
    }

    void HandleHand(XRHand hand)
    {
        if (!hand.isTracked)
        {
            if (hand.handedness == grabbingHandedness)
            {
                hasGrabbingHand = false;
                XRManipulationState.RotatingActive = false;
            }
            return;
        }

        // récup pinch
        if (!TryTipPose(hand, XRHandJointID.IndexTip, out Pose i) ||
            !TryTipPose(hand, XRHandJointID.ThumbTip, out Pose t))
            return;

        float pinchDist = Vector3.Distance(i.position, t.position);
        bool pinching = pinchDist < pinchDistanceThreshold;

        // DÉMARRAGE rotate ?
        if (!hasGrabbingHand)
        {
            // tu ne démarres PAS si Scale est actif
            if (XRManipulationState.ScalingActive) return;
            if (!pinching) return;

            // ok, une main pince seule, pas de scale -> rotate ON
            hasGrabbingHand = true;
            grabbingHandedness = hand.handedness;
            XRManipulationState.RotatingActive = true;

            startObjY = transform.eulerAngles.y;
            startHandAngleY = HandAngleAroundObjectXZ(hand);
            return;
        }

        // si c'est pas la main qui a le lock -> ignore
        if (hand.handedness != grabbingHandedness) return;

        // si on a relâché le pinch -> stop rotate
        if (!pinching && pinchDist > (pinchDistanceThreshold + releaseHysteresis))
        {
            hasGrabbingHand = false;
            XRManipulationState.RotatingActive = false;
            return;
        }

        // rotation active
        float currentHandAngle = HandAngleAroundObjectXZ(hand);
        float delta = Mathf.DeltaAngle(startHandAngleY, currentHandAngle);
        float targetY = startObjY + delta * sensitivity;

        transform.rotation = Quaternion.Euler(0f, targetY, 0f);
    }

    float HandAngleAroundObjectXZ(XRHand hand)
    {
        Pose jointPose;
        if (!TryTipPose(hand, XRHandJointID.IndexProximal, out jointPose))
            TryTipPose(hand, XRHandJointID.Wrist, out jointPose);

        Vector3 center = transform.position;
        Vector3 p = jointPose.position;
        Vector3 v = new Vector3(p.x - center.x, 0f, p.z - center.z);
        if (v.sqrMagnitude < 1e-6f) return 0f;
        return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }

    bool TryTipPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default;
        return false;
    }
}
