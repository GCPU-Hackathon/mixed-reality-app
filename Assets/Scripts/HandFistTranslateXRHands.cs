using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[RequireComponent(typeof(Collider))]
public class HandFistTranslateXRHands : MonoBehaviour
{
    [Header("Détection du poing")]
    public float fistCloseThreshold = 0.06f;
    public float fistReleaseBuffer = 0.015f;

    [Header("Référence main")]
    public XRHandJointID referenceJoint = XRHandJointID.Palm;

    [Header("Mouvement")]
    public float followSmoothing = 12f;

    XRHandSubsystem handSubsystem;

    bool translating = false;
    Handedness activeHand = Handedness.Left;
    Vector3 startObjOffset;

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null) return;

        // si scale ou rotate → pas de translation
        if (XRManipulationState.ScalingActive || XRManipulationState.RotatingActive)
        {
            StopTranslate();
            return;
        }

        XRHand L = handSubsystem.leftHand;
        XRHand R = handSubsystem.rightHand;

        Vector3 refL, refR;
        bool leftFist  = IsFist(L, out refL);
        bool rightFist = IsFist(R, out refR);

        // démarrage
        if (!translating)
        {
            // une main en poing, pas les deux
            if (leftFist ^ rightFist)
            {
                translating = true;
                XRManipulationState.TranslatingActive = true;

                activeHand = leftFist ? Handedness.Left : Handedness.Right;
                startObjOffset = transform.position - (leftFist ? refL : refR);
            }
            else
            {
                return;
            }
        }

        // suivi
        XRHand h = (activeHand == Handedness.Left) ? L : R;

        Vector3 refPos;
        bool fistNow = IsFist(h, out refPos);
        if (!fistNow || !h.isTracked)
        {
            StopTranslate();
            return;
        }

        Vector3 targetPos = refPos + startObjOffset;
        if (followSmoothing <= 0f)
            transform.position = targetPos;
        else
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Exp(-followSmoothing * Time.deltaTime)
            );
    }

    void StopTranslate()
    {
        if (translating)
        {
            translating = false;
            XRManipulationState.TranslatingActive = false;
        }
    }

    bool IsFist(XRHand hand, out Vector3 palmPos)
    {
        palmPos = default;
        if (!hand.isTracked) return false;

        Pose palm;
        if (!TryGet(hand, referenceJoint, out palm))
        {
            if (!TryGet(hand, XRHandJointID.Wrist, out palm))
                return false;
        }
        palmPos = palm.position;

        Pose tipI, tipM, tipR, tipL, tipT;
        if (!TryGet(hand, XRHandJointID.IndexTip, out tipI) ||
            !TryGet(hand, XRHandJointID.MiddleTip, out tipM) ||
            !TryGet(hand, XRHandJointID.RingTip, out tipR) ||
            !TryGet(hand, XRHandJointID.LittleTip, out tipL) ||
            !TryGet(hand, XRHandJointID.ThumbTip, out tipT))
            return false;

        float dI = Vector3.Distance(tipI.position, palm.position);
        float dM = Vector3.Distance(tipM.position, palm.position);
        float dR = Vector3.Distance(tipR.position, palm.position);
        float dL = Vector3.Distance(tipL.position, palm.position);
        float dT = Vector3.Distance(tipT.position, palm.position);

        float close = fistCloseThreshold;
        float open  = fistCloseThreshold + fistReleaseBuffer;

        bool closed =
            dI < close && dM < close && dR < close && dL < close && dT < (close + 0.02f);

        bool opened =
            dI > open || dM > open || dR > open || dL > open || dT > (open + 0.02f);

        return closed && !opened;
    }

    bool TryGet(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default;
        return false;
    }
}
