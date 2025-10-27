using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class BrainMenuOpenerXRHands : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuPrefab;
    [Tooltip("Vitesse d'apparition du menu en ouverture (position/scale lerp).")]
    public float appearLerp = 14f;
    [Tooltip("Durée min visée pour l'anim d'apparition (secondes).")]
    public float appearAnimDuration = 0.18f;

    [Header("Gesture")]
    public float doublePinchWindow = 0.30f;
    [Range(0.01f, 0.1f)] public float pinchThreshold = 0.03f;

    [Header("Spawn in front of palm (no follow)")]
    public float forwardOffset = 0.18f;
    public float upOffset      = 0.04f;
    public float sideOffset    = 0.00f;
    public bool faceCamera = true;

    [Header("Lock while menu is open")]
    public MonoBehaviour[] pinchBehavioursToDisable;

    [Header("Debug")]
    public bool debugLogs = true;

    public static bool MenuActive { get; private set; }

    XRHandSubsystem handSubsystem;
    GameObject menuInstance;

    float lastPinchTimeLeft = -999f, lastPinchTimeRight = -999f;
    bool leftPrevPinch, rightPrevPinch;

    // coroutine ref pour éviter double anim
    Coroutine appearRoutine;

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null) return;

        CheckHand(handSubsystem.leftHand,  true);
        CheckHand(handSubsystem.rightHand, false);
    }

    void CheckHand(XRHand hand, bool isLeft)
    {
        if (!hand.isTracked) { SetPrev(isLeft,false); return; }
        if (!TryPose(hand, XRHandJointID.IndexTip, out var i) || !TryPose(hand, XRHandJointID.ThumbTip, out var t))
        { SetPrev(isLeft,false); return; }

        bool pinching = Vector3.Distance(i.position, t.position) < pinchThreshold;
        bool wasPinching = GetPrev(isLeft);

        if (!wasPinching && pinching)
        {
            float now = Time.time;
            float last = isLeft ? lastPinchTimeLeft : lastPinchTimeRight;
            if (now - last <= doublePinchWindow)
                ToggleMenuAt(hand);
            if (isLeft) lastPinchTimeLeft = now; else lastPinchTimeRight = now;
        }

        SetPrev(isLeft, pinching);
    }

    void ToggleMenuAt(XRHand hand)
    {
        if (MenuActive)
        {
            CloseMenu();
            return;
        }
        if (menuPrefab == null) return;

        // Récupère la paume / poignet
        if (!TryPose(hand, XRHandJointID.Palm, out Pose palm))
            TryPose(hand, XRHandJointID.Wrist, out palm);

        // Calcule pose cible
        Pose spawnPose = ComputeMenuPoseFromPalm(
            palm,
            forwardOffset,
            upOffset,
            sideOffset,
            faceCamera
        );

        // Utiliser l'instance fournie dans la scène
        if (menuInstance == null)
            menuInstance = menuPrefab;

        // Lier VolumeDVR <-> menu
        var linker = menuInstance.GetComponentInChildren<BrainMenuToggleToVolumeDVR>(true);
        if (linker != null)
        {
            VolumeDVR dvr = Object.FindFirstObjectByType<VolumeDVR>();
            if (dvr != null)
            {
                linker.volumeDVR = dvr;
                linker.SyncAll();
            }
            else
            {
                Debug.LogWarning("[BrainMenuOpenerXRHands] Aucun VolumeDVR trouvé dans la scène pour lier le menu.");
            }
        }
        else
        {
            Debug.LogWarning("[BrainMenuOpenerXRHands] Pas de BrainMenuToggleToVolumeDVR sur le menuInstance.");
        }

        // On active avant l'anim pour que les canvases / colliders soient interactifs
        menuInstance.SetActive(true);

        // Stop ancienne anim si spam double pinch
        if (appearRoutine != null) StopCoroutine(appearRoutine);
        appearRoutine = StartCoroutine(AnimateAppear(menuInstance.transform, spawnPose));

        MenuActive = true;

        // Verrouille les autres interactions
        LockWorldInteractions(true);

        if (debugLogs) Debug.Log("[BrainMenu] OPEN");
    }

    IEnumerator AnimateAppear(Transform menuTr, Pose targetPose)
{
    // On ne touche plus à la taille du menu (garde le scale d'origine)
    Vector3 endPos = targetPose.position;
    Quaternion endRot = targetPose.rotation;

    // Position de départ légèrement en retrait (effet smooth d'apparition)
    Vector3 startPos = endPos - (endRot * Vector3.forward * 0.05f);
    Quaternion startRot = endRot;

    // Pose initiale
    menuTr.SetPositionAndRotation(startPos, startRot);

    float elapsed = 0f;
    while (elapsed < appearAnimDuration)
    {
        elapsed += Time.deltaTime;
        float t = 1f - Mathf.Exp(-appearLerp * elapsed);

        menuTr.position = Vector3.Lerp(startPos, endPos, t);
        menuTr.rotation = Quaternion.Slerp(startRot, endRot, t);

        yield return null;
    }

    // Snap final propre
    menuTr.SetPositionAndRotation(endPos, endRot);

    appearRoutine = null;
}

    public void CloseMenu()
    {
        if (menuInstance) menuInstance.SetActive(false);
        MenuActive = false;
        LockWorldInteractions(false);
        if (debugLogs) Debug.Log("[BrainMenu] CLOSE");
    }

    void LockWorldInteractions(bool enableMenu)
    {
        if (pinchBehavioursToDisable != null)
        {
            foreach (var b in pinchBehavioursToDisable)
            {
                if (b) b.enabled = !enableMenu;
            }
        }
    }


    // --- helpers main/palm/pose ---
    bool TryPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default;
        return false;
    }

    static Pose ComputeMenuPoseFromPalm(Pose palm, float fwd, float up, float side, bool faceCam)
    {
        Vector3 forward = palm.rotation * Vector3.forward;
        Vector3 upVec   = palm.rotation * Vector3.up;
        Vector3 right   = palm.rotation * Vector3.right;

        Vector3 pos = palm.position + forward * fwd + upVec * up + right * side;

        Quaternion rot;
        if (faceCam && Camera.main)
        {
            Vector3 toCam = (Camera.main.transform.position - pos).normalized;
            rot = Quaternion.LookRotation(-toCam, Vector3.up);
        }
        else
        {
            rot = Quaternion.LookRotation(forward, upVec);
        }

        return new Pose(pos, rot);
    }

    bool GetPrev(bool isLeft) => isLeft ? leftPrevPinch : rightPrevPinch;
    void SetPrev(bool isLeft, bool v) { if (isLeft) leftPrevPinch = v; else rightPrevPinch = v; }
}
