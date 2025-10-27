using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PokePressReceiver : MonoBehaviour
{
    [Tooltip("Comportement à appeler pour activer/désactiver le label")]
    public XRMenuItemInteractable targetItem;

    [Tooltip("Tag du fingertip (doit avoir Rigidbody kinematic + Collider non trigger)")]
    public string fingerTipTag = "FingerTip";

    [Tooltip("Distance max autorisée (en mètres monde) pour considérer que le doigt n'a pas 'glissé' mais 'tapé'.")]
    public float maxTapMove = 0.02f; // 2 cm

    [Tooltip("Durée max (en secondes) entre entrée et sortie pour qu'on considère que c'est un tap intentionnel.")]
    public float maxTapTime = 0.4f;

    // interne
    private bool _fingerInside = false;
    private Vector3 _enterPosWorld;
    private float _enterTime;
    private Collider _currentFinger;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[TapReceiver] No collider on " + gameObject.name);
        }
        else
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(fingerTipTag))
            return;

        // si déjà un doigt pris, ignore (évite multi doigts chelous)
        if (_fingerInside) return;

        _fingerInside = true;
        _currentFinger = other;
        _enterPosWorld = other.transform.position;
        _enterTime = Time.time;

        // Debug
        // Debug.Log($"[TapReceiver] ENTER {gameObject.name} by {other.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_fingerInside) return;
        if (other != _currentFinger) return;

        // le doigt sort du bouton => on évalue si c'était un 'tap'
        float dt = Time.time - _enterTime;
        float dist = Vector3.Distance(_enterPosWorld, other.transform.position);

        // Debug.Log($"[TapReceiver] EXIT {gameObject.name} dt={dt:F3} dist={dist:F3}");

        _fingerInside = false;
        _currentFinger = null;

        // Critères de TAP :
        // - geste court dans le temps
        // - geste pas trop 'glissé' spatialement
        if (dt <= maxTapTime && dist <= maxTapMove)
        {
            // TAP VALIDÉ => toggle l'item
            if (targetItem != null)
            {
                // Debug.Log($"[TapReceiver] TAP on {gameObject.name} => Press()");
                targetItem.Press();
            }
            else
            {
                Debug.LogWarning("[TapReceiver] targetItem is null on " + gameObject.name);
            }
        }
        else
        {
            // C'était un swipe/scroll => on ne fait rien
            // Debug.Log("[TapReceiver] gesture treated as scroll, not click");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // facultatif : on pourrait annuler le 'tap' si le doigt glisse trop pendant le stay,
        // mais pour l'instant on check juste à la sortie.
    }
}
