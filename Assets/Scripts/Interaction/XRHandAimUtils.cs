using UnityEngine;
using UnityEngine.XR.Hands;

public static class XRHandAimUtils
{
    /// <summary>
    /// Retourne vrai si la main "vise" la cible :
    /// - Doigt index dirigé vers la cible (angle <= maxAngleDeg)
    /// - ET Raycast partant de l’index va bien toucher la cible (ou un enfant)
    /// </summary>
    public static bool IsHandAimingAt(
        XRHand hand,
        Transform target,
        float maxAngleDeg,
        float rayMaxDistance,
        LayerMask rayMask,
        out Vector3 rayOrigin,
        out Vector3 rayDir)
    {
        rayOrigin = default;
        rayDir    = default;

        if (hand == null || !hand.isTracked || target == null)
            return false;

        // On récupère une direction de "pointage" basée sur l’index
        if (!TryGet(hand, XRHandJointID.IndexProximal, out Pose idxProx) ||
            !TryGet(hand, XRHandJointID.IndexTip,      out Pose idxTip))
            return false;

        rayOrigin = idxTip.position;
        rayDir    = (idxTip.position - idxProx.position).normalized;   // direction du doigt
        if (rayDir.sqrMagnitude < 1e-6f) return false;

        // Direction vers le centre (ou bounds) de la cible
        Vector3 targetPoint = target.TryGetComponent<Collider>(out var col)
            ? col.bounds.ClosestPoint(rayOrigin)   // plus robuste si objet volumineux
            : target.position;

        Vector3 toTarget = (targetPoint - rayOrigin).normalized;
        float angle = Vector3.Angle(rayDir, toTarget);

        if (angle > maxAngleDeg) return false;

        // Raycast de confirmation
        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayMaxDistance, rayMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
                return true;
        }

        return false;
    }

    static bool TryGet(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default; return false;
    }
}
