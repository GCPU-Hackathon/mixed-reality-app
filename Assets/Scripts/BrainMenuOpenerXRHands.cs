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
    [Tooltip("Référence PREFAB (asset Project OU instance scène).")]
    public GameObject menuPrefab;

    [Tooltip("Vitesse d'apparition du menu vers la pose cible (exponentiel).")]
    public float appearLerp = 14f;

    [Tooltip("Durée approximative de l'anim d'apparition.")]
    public float appearAnimDuration = 0.18f;

    [Header("Gesture")]
    public float doublePinchWindow = 0.30f;
    [Range(0.01f, 0.1f)] public float pinchThreshold = 0.03f;

    [Header("Spawn offsets (à côté de la main)")]
    [Tooltip("Décalage en avant de la paume (très faible juste pour ne pas être dedans).")]
    public float forwardOffsetNearHand = 0.03f;

    [Tooltip("Décalage vertical (au-dessus de la main).")]
    public float upOffsetNearHand = 0.02f;

    [Tooltip("Décalage latéral (côté pouce).")]
    public float sideOffsetNearHand = 0.10f;

    [Tooltip("Faire face à la caméra ? Sinon s'aligne avec la main.")]
    public bool faceCamera = true;

    [Header("Lock while menu is open")]
    [Tooltip("Scripts pinchs / grabs du monde à désactiver pendant que le menu est ouvert.")]
    public MonoBehaviour[] pinchBehavioursToDisable;

    [Header("Debug")]
    public bool debugLogs = true;

    public static bool MenuActive { get; private set; }

    XRHandSubsystem handSubsystem;
    GameObject menuInstance;      // instance runtime unique
    CanvasGroup menuCanvasGroup;  // pour cacher/montrer sans perdre l'état

    float lastPinchTimeLeft = -999f, lastPinchTimeRight = -999f;
    bool leftPrevPinch, rightPrevPinch;

    Coroutine appearRoutine;

    // On garde quelle main a ouvert le menu pour calculer le bon côté
    bool lastWasLeftHand = true;

    void Start()
    {
        // Récupère le subsystem mains XR Hands
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0)
            handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null)
            return;

        CheckHand(handSubsystem.leftHand,  true);
        CheckHand(handSubsystem.rightHand, false);
    }

    void CheckHand(XRHand hand, bool isLeft)
    {
        if (!hand.isTracked)
        {
            SetPrev(isLeft, false);
            return;
        }

        if (!TryPose(hand, XRHandJointID.IndexTip, out var indexTipPose) ||
            !TryPose(hand, XRHandJointID.ThumbTip, out var thumbTipPose))
        {
            SetPrev(isLeft, false);
            return;
        }

        bool pinching = Vector3.Distance(indexTipPose.position, thumbTipPose.position) < pinchThreshold;
        bool wasPinching = GetPrev(isLeft);

        if (!wasPinching && pinching)
        {
            float now = Time.time;
            float last = isLeft ? lastPinchTimeLeft : lastPinchTimeRight;

            // Détection du double pinch
            if (now - last <= doublePinchWindow)
            {
                lastWasLeftHand = isLeft;
                ToggleMenuAt(hand);
            }

            if (isLeft)
                lastPinchTimeLeft = now;
            else
                lastPinchTimeRight = now;
        }

        SetPrev(isLeft, pinching);
    }

    void EnsureMenuInstance()
    {
        if (menuInstance != null)
            return;

        if (menuPrefab == null)
        {
            Debug.LogError("[BrainMenuOpenerXRHands] menuPrefab n'est pas assigné.");
            return;
        }

        // On crée une vraie instance dans la scène (PAS le prefab asset)
        menuInstance = menuPrefab;

        // On force actif pour initialiser correctement les scripts UI / XR
        if (!menuInstance.activeSelf)
            menuInstance.SetActive(true);

        // Ajout/récupération du CanvasGroup pour hide/show sans perdre l'état runtime des toggles
        menuCanvasGroup = menuInstance.GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null)
            menuCanvasGroup = menuInstance.AddComponent<CanvasGroup>();

        // Démarrage caché
        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        // Lien VolumeDVR + sync initiale
        var linker = menuInstance.GetComponentInChildren<BrainMenuToggleToVolumeDVR>(true);
        if (linker != null)
        {
            VolumeDVR dvr = Object.FindFirstObjectByType<VolumeDVR>();
            if (dvr != null)
            {
                linker.volumeDVR = dvr;
                linker.SyncAll(); // sync une seule fois à la création
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
    }

    void ToggleMenuAt(XRHand hand)
    {
        if (MenuActive)
        {
            CloseMenu();
            return;
        }

        EnsureMenuInstance();
        if (menuInstance == null) return;

        // Récupère la paume (ou poignet en fallback)
        if (!TryPose(hand, XRHandJointID.Palm, out Pose palm))
            TryPose(hand, XRHandJointID.Wrist, out palm);

        // Calcule la pose du menu sur le côté de la main qui a ouvert
        Pose spawnPose = ComputeMenuPoseBesideHand(
            palm,
            forwardOffsetNearHand,
            upOffsetNearHand,
            sideOffsetNearHand,
            lastWasLeftHand,
            faceCamera
        );

        // Stop l'ancienne anim si spam
        if (appearRoutine != null)
        {
            StopCoroutine(appearRoutine);
            appearRoutine = null;
        }

        // Apparition smooth à côté de la main
        appearRoutine = StartCoroutine(AnimateAndShow(menuInstance.transform, spawnPose));

        MenuActive = true;
        LockWorldInteractions(true);

        if (debugLogs) Debug.Log("[BrainMenu] OPEN (side of hand)");
    }

    IEnumerator AnimateAndShow(Transform menuTr, Pose targetPose)
    {
        // Rendre visible / interactif
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }

        Vector3 endPos = targetPose.position;
        Quaternion endRot = targetPose.rotation;

        // petit offset arrière juste pour l'effet d'anim
        Vector3 startPos = endPos - (endRot * Vector3.forward * 0.03f);
        Quaternion startRot = endRot;

        // Pose de départ
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

        // Pose finale nette
        menuTr.SetPositionAndRotation(endPos, endRot);

        appearRoutine = null;
    }

    public void CloseMenu()
    {
        if (menuInstance && menuCanvasGroup)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        MenuActive = false;
        LockWorldInteractions(false);

        if (debugLogs) Debug.Log("[BrainMenu] CLOSE (hidden)");
    }

    void LockWorldInteractions(bool enableMenu)
    {
        if (pinchBehavioursToDisable != null)
        {
            foreach (var b in pinchBehavioursToDisable)
            {
                if (b)
                    b.enabled = !enableMenu;
            }
        }

        // Toujours : on ne touche pas aux interactionLayers des rayons,
        // car l'UI est gérée par TrackedDeviceGraphicRaycaster.
    }

    // --- helpers main/palm/pose ---
    bool TryPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose))
            return true;

        pose = default;
        return false;
    }

    // Pose du menu à côté de la main
static Pose ComputeMenuPoseBesideHand(
    Pose palm,
    float fwd,
    float up,
    float side,
    bool isLeftHand,
    bool faceCam
)
{
    // Axes locaux de la paume
    Vector3 forward = palm.rotation * Vector3.forward;
    Vector3 upVec   = palm.rotation * Vector3.up;
    Vector3 right   = palm.rotation * Vector3.right;

    // --- Ajustement de proximité ---
    // On inverse légèrement le forward (sinon menu trop "loin" dans la direction de la paume)
    // et on réduit le sideOffset pour le coller davantage.
    float actualSide = isLeftHand ? -side * 0.7f : side * 0.7f;
    float actualForward = -fwd * 0.4f; // vers la main plutôt qu’en avant
    float actualUp = up * 1.2f;        // un peu plus haut pour éviter les collisions

    Vector3 pos = palm.position
                + forward * actualForward
                + upVec   * actualUp
                + right   * actualSide;

    // --- Orientation ---
    Quaternion rot;
    if (faceCam && Camera.main)
    {
        // Le menu fait face à la caméra (joueur)
        Vector3 toCam = (Camera.main.transform.position - pos).normalized;
        rot = Quaternion.LookRotation(-toCam, Vector3.up);
    }
    else
    {
        // Aligné légèrement incliné vers la paume
        Quaternion tilt = Quaternion.AngleAxis(isLeftHand ? -20f : 20f, upVec);
        rot = Quaternion.LookRotation(tilt * -forward, upVec);
    }

    return new Pose(pos, rot);
}


    bool GetPrev(bool isLeft) => isLeft ? leftPrevPinch : rightPrevPinch;
    void SetPrev(bool isLeft, bool v)
    {
        if (isLeft) leftPrevPinch = v;
        else rightPrevPinch = v;
    }
}
