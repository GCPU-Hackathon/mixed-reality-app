using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class BrainMenuOpenerXRHands : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuPrefab;
    public float appearLerp = 14f;

    [Header("Gesture")]
    public float doublePinchWindow = 0.30f;
    [Range(0.01f, 0.1f)] public float pinchThreshold = 0.03f;

    [Header("Spawn in front of palm (no follow)")]
    public float forwardOffset = 0.18f;   // devant la paume
    public float upOffset      = 0.04f;   // légère hauteur
    public float sideOffset    = 0.00f;   // décalage latéral (vers le pouce si >0)
    public bool faceCamera = true;    // sinon : aligné sur la paume

    [Header("Lock while menu is open")]
    public MonoBehaviour[] pinchBehavioursToDisable;
    public XRRayInteractor[] rayInteractors;
    public InteractionLayerMask uiOnlyMask;

    [Header("Debug")]
    public bool debugLogs = true;

    public static bool MenuActive { get; private set; }

    XRHandSubsystem handSubsystem;
    GameObject menuInstance;

    float lastPinchTimeLeft = -999f, lastPinchTimeRight = -999f;
    bool leftPrevPinch, rightPrevPinch;
    Vector3 targetPos;

    // garde la main qui a ouvert le menu
    bool followLeftHand = true;

    InteractionLayerMask[] _savedInteractorMasks;

    void Start()
    {
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];

        if (rayInteractors != null && rayInteractors.Length > 0)
        {
            _savedInteractorMasks = new InteractionLayerMask[rayInteractors.Length];
            for (int i = 0; i < rayInteractors.Length; i++)
                if (rayInteractors[i] != null)
                    _savedInteractorMasks[i] = rayInteractors[i].interactionLayers;
        }
    }

    void Update()
    {
        if (handSubsystem == null) return;

        if (MenuActive && menuInstance)
            menuInstance.transform.position = Vector3.Lerp(
                menuInstance.transform.position, targetPos,
                1f - Mathf.Exp(-appearLerp * Time.deltaTime)
            );

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
        if (MenuActive) { CloseMenu(); return; }
        if (menuPrefab == null) return;

        // Récupère la paume (ou poignet en fallback)
        if (!TryPose(hand, XRHandJointID.Palm, out Pose palm))
            TryPose(hand, XRHandJointID.Wrist, out palm);

        // Axes paume
        Vector3 forward = palm.rotation * Vector3.forward;
        Vector3 up      = palm.rotation * Vector3.up;
        Vector3 right   = palm.rotation * Vector3.right;

        // Position devant la paume (une seule fois)
        Vector3 pos = palm.position + forward * forwardOffset + up * upOffset + right * sideOffset;

        // Orientation : vers la caméra (lisible) ou comme la paume
        Quaternion rot;
        if (faceCamera && Camera.main)
        {
            Vector3 toCam = (Camera.main.transform.position - pos).normalized;
            rot = Quaternion.LookRotation(-toCam, Vector3.up);
        }
        else
        {
            rot = Quaternion.LookRotation(forward, up);
        }

        // Instanciation / réactivation
        if (menuInstance == null)
            menuInstance = Instantiate(menuPrefab);

        var linker = menuInstance.GetComponentInChildren<BrainMenuToggleToVolumeDVR>(true);
        if (linker != null)
        {
            // trouve le VolumeDVR actif dans la scène
            VolumeDVR dvr = Object.FindFirstObjectByType<VolumeDVR>();
            if (dvr != null)
            {
                linker.volumeDVR = dvr;

                // force sync initiale des toggles -> DVR
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

        menuInstance.transform.SetPositionAndRotation(pos, rot);
        menuInstance.SetActive(true);

        // Arrivée douce vers une cible FIXE (pas de suivi)
        targetPos = pos;
        MenuActive = true;

        // Verrouillage des autres interactions (déjà dans ton script)
        LockWorldInteractions(true);

        if (debugLogs) Debug.Log("[BrainMenu] OPEN (front of palm, no follow)");
    }

    public void CloseMenu()
    {
        if (menuInstance) menuInstance.SetActive(false); // <-- conserve l’état
        MenuActive = false;
        LockWorldInteractions(false);
        if (debugLogs) Debug.Log("[BrainMenu] CLOSE");
    }

    void LockWorldInteractions(bool enableMenu)
    {
        // désactive tes scripts pinch
        if (pinchBehavioursToDisable != null)
            foreach (var b in pinchBehavioursToDisable)
                if (b) b.enabled = !enableMenu;

        // rayons en UI-only
        if (rayInteractors != null && rayInteractors.Length > 0)
        {
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                var ri = rayInteractors[i];
                if (!ri) continue;
                ri.interactionLayers = enableMenu ? uiOnlyMask : _savedInteractorMasks[i];
            }
        }
    }

    // --- helpers main/palm/pose ---
    bool TryGetPalmPose(XRHand hand, out Pose pose) =>
        TryPose(hand, XRHandJointID.Palm, out pose) || TryPose(hand, XRHandJointID.Wrist, out pose);

    bool TryPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose)) return true;
        pose = default; return false;
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
            rot = Quaternion.LookRotation(-toCam, Vector3.up); // lisible côté joueur
        }
        else
        {
            rot = Quaternion.LookRotation(forward, upVec);      // collé à l’orientation de la paume
        }

        return new Pose(pos, rot);
    }

    bool GetPrev(bool isLeft) => isLeft ? leftPrevPinch : rightPrevPinch;
    void SetPrev(bool isLeft, bool v) { if (isLeft) leftPrevPinch = v; else rightPrevPinch = v; }
}
