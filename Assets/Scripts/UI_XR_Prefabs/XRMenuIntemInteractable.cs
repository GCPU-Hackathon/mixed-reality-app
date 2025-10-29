using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Représente un item du menu (un label anatomique / structure).
/// Press() inverse son état visible dans VolumeDVR + met à jour l'UI.
/// </summary>
public class XRMenuItemInteractable : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("Index du label dans VolumeDVR (0..255).")]
    public int labelIndex;

    [Tooltip("Référence vers VolumeDVR dans la scène.")]
    public VolumeDVR volumeDVR;

    [Header("UI (optionnel)")]
    [Tooltip("Le Toggle visuel qui reflète on/off si présent dans le prefab.")]
    public Toggle uiToggle;

    [Tooltip("Une icône ou un GameObject nommé 'Active' qui montre l'état actuel.")]
    public GameObject activeMark;

    [Header("Events (optionnel)")]
    public UnityEvent onPressed; // sons, haptique, debug, etc.

    /// <summary>
    /// Inverse l'état du label :
    /// - Met à jour le toggle,
    /// - Allume ou éteint l'icône Active,
    /// - Appelle VolumeDVR.SetLabelVisible().
    /// </summary>
    public void Press()
    {
        bool newState = true;

        if (uiToggle != null)
        {
            newState = !uiToggle.isOn;
            uiToggle.isOn = newState;
        }

        if (activeMark != null)
        {
            activeMark.SetActive(newState);
        }

        if (volumeDVR != null)
        {
            volumeDVR.SetLabelVisible(labelIndex, newState);
        }

        onPressed?.Invoke();

        Debug.Log($"[XRMenuItemInteractable] Label {labelIndex} => {(newState ? "ON" : "OFF")}");
    }
}
