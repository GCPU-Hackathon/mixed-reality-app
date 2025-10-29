using UnityEngine;
using UnityEngine.UI;  // pour Dropdown

public class DropdownVolumeLoader : MonoBehaviour
{
    [Tooltip("Le Dropdown UI qui contient les 4 options")]
    public Dropdown dropdown;

    [Tooltip("Référence vers le VolumeDVR dans ta scène")]
    public VolumeDVR volumeDVR;

    void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<Dropdown>();

        // Abonne l'événement
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDestroy()
    {
        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    private void OnDropdownChanged(int idx)
    {
        // Récupérer le texte de l'option choisie ("t1c", "t1n", "t2f", "t2w")
        string code = dropdown.options[idx].text;

        if (volumeDVR != null)
        {
            volumeDVR.LoadVolumeByCode(code);
        }
        else
        {
            Debug.LogError("[DropdownVolumeLoader] volumeDVR ref manquante !");
        }
    }
}
