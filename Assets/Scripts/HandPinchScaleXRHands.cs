using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[RequireComponent(typeof(Collider))]
public class HandPinchScaleXRHands : MonoBehaviour
{
    [Header("Pinch")]
    [Range(0.01f, 0.1f)] public float pinchDistanceThreshold = 0.03f;
    public float releaseHysteresis = 0.005f;

    [Header("Scaling")]
    public float minScale = 0.1f;
    public float maxScale = 3.0f;
    [Tooltip("1 = linéaire, <1 = plus doux, >1 = plus fort")]
    public float scaleResponse = 1.0f;
    public Transform targetRoot; // objet à scaler

    [Header("Rotation Lock")]
    public bool lockRotationWhileScaling = true;
    Quaternion lockedRotation;

    XRHandSubsystem handSubsystem;

    bool leftPinching, rightPinching;
    float startDist = 0f;
    Vector3 startScale;

    void Start()
    {
        targetRoot = targetRoot ? targetRoot : transform;

        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand L = handSubsystem.leftHand;
        XRHand R = handSubsystem.rightHand;

        // si une main pas trackée -> on coupe le scale
        if (!L.isTracked || !R.isTracked)
        {
            StopScaling();
            return;
        }

        // détection pinch pour chaque main
        Vector3 lPinchPos, rPinchPos;
        leftPinching  = GetPinch(L,  out lPinchPos);
        rightPinching = GetPinch(R, out rPinchPos);

        // CAS 1: les deux mains pincent -> SCALE PRIORITAIRE ABSOLUE
        if (leftPinching && rightPinching)
        {
            // on écrase immédiatement la rotation et la translation
            XRManipulationState.RotatingActive = false;
            XRManipulationState.TranslatingActive = false;

            // on active le mode scale
            XRManipulationState.ScalingActive = true;

            // si c'est la première frame de scale, on initialise
            if (startDist <= 0f)
            {
                float distNow0 = Vector3.Distance(lPinchPos, rPinchPos);
                startDist  = Mathf.Max(distNow0, 1e-4f);
                startScale = targetRoot.localScale;
                if (lockRotationWhileScaling) lockedRotation = transform.rotation;
            }

            // maintenant on applique l'échelle
            float distNow = Vector3.Distance(lPinchPos, rPinchPos);
            ApplyScale(distNow);
            return;
        }

        // CAS 2: pas deux mains en pinch -> on arrête le scale
        StopScaling();
    }

    void LateUpdate()
    {
        // garder l'orientation stable pendant le scale
        if (lockRotationWhileScaling && XRManipulationState.ScalingActive)
        {
            transform.rotation = lockedRotation;
        }
    }

    void ApplyScale(float distNow)
    {
        if (distNow <= 1e-6f) return;
        if (startDist <= 0f) return;

        float ratio = Mathf.Clamp(distNow / startDist, 0.01f, 100f);
        float response = Mathf.Pow(ratio, Mathf.Max(0.01f, scaleResponse));

        float s = startScale.x * response;
        s = Mathf.Clamp(s, minScale, maxScale);

        if (!float.IsNaN(s) && !float.IsInfinity(s))
        {
            targetRoot.localScale = new Vector3(s, s, s);
        }
    }

    void StopScaling()
    {
        if (XRManipulationState.ScalingActive)
        {
            // on libère le mode scale
            XRManipulationState.ScalingActive = false;
        }
        startDist = 0f;
    }

    // détecter si la main pince
    bool GetPinch(XRHand hand, out Vector3 pinchPos)
    {
        pinchPos = default;
        if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose i) ||
            !TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose t))
            return false;

        float d = Vector3.Distance(i.position, t.position);
        pinchPos = 0.5f * (i.position + t.position);
        return d < pinchDistanceThreshold;
    }

    bool TryGetJointPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var joint = hand.GetJoint(id);
        if (joint.TryGetPose(out pose)) return true;
        pose = default; return false;
    }
}
