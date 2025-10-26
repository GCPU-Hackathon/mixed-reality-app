using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.SubsystemsImplementation;
using System;
using System.Collections.Generic;

public class PinchToOpenBrainMenu : MonoBehaviour
{
    [Header("Pinch Detect")]
    [Range(0.01f, 0.1f)] public float pinchDistanceThreshold = 0.03f;
    public float openCooldown = 0.5f;

    [Header("Focus")]
    public bool requireGazeFocus = true;
    public float focusMaxDistance = 5f;

    BrainMenuController menu;
    XRHandSubsystem handSubsystem;
    float lastOpenTime = -10f;

    void Awake()
    {
        menu = GetComponent<BrainMenuController>();
        AcquireHandSubsystem();
    }

    void OnEnable()  { AcquireHandSubsystem(); }
    void OnDisable() { handSubsystem = null; }

    void Update()
    {
        // Provider may start/stop at runtime – reacquire if needed
        if (!IsSubsystemReady())
        {
            AcquireHandSubsystem();
            return;
        }

        if (Time.time < lastOpenTime + openCooldown) return;

        bool leftPinch  = IsPinchingSafe(handSubsystem.leftHand);
        bool rightPinch = IsPinchingSafe(handSubsystem.rightHand);
        if (!(leftPinch || rightPinch)) return;

        if (requireGazeFocus && !IsGazeOnThis()) return;

        menu?.ShowMenu();
        lastOpenTime = Time.time;
    }

    // --------- Helpers ---------

    void AcquireHandSubsystem()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        handSubsystem = (list.Count > 0) ? list[0] : null;
    }

    bool IsSubsystemReady()
    {
        // 'running' exists on SubsystemWithProvider; some older versions only have 'running' via 'XRHandSubsystem.running'
        return handSubsystem != null && handSubsystem.running;
    }

    bool IsPinchingSafe(XRHand hand)
    {
        try
        {
            if (!hand.isTracked) return false;

            if (!TryGetJointSafe(hand, XRHandJointID.IndexTip, out Pose indexPose)) return false;
            if (!TryGetJointSafe(hand, XRHandJointID.ThumbTip, out Pose thumbPose)) return false;

            float dist = Vector3.Distance(indexPose.position, thumbPose.position);
            return dist < pinchDistanceThreshold;
        }
        catch (ObjectDisposedException)
        {
            // Provider restarted between frames; ignore this frame and reacquire next Update
            handSubsystem = null;
            return false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PinchToOpenBrainMenu] Unexpected joint read error: {e.Message}");
            return false;
        }
    }

    bool TryGetJointSafe(XRHand hand, XRHandJointID id, out Pose pose)
    {
        // Accessing hand.GetJoint can throw if the provider disposed its buffers mid-frame
        try
        {
            var j = hand.GetJoint(id);
            if (j.TryGetPose(out pose)) return true;
        }
        catch (ObjectDisposedException)
        {
            pose = default;
            throw; // handled by caller
        }

        pose = default;
        return false;
    }

    bool IsGazeOnThis()
    {
        var cam = Camera.main;
        if (!cam) return false;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, focusMaxDistance))
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        return false;
    }
}
