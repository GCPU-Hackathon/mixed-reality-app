using UnityEngine;

public enum BrainInteractionMode { None, Rotate }

public class BrainMenuController : MonoBehaviour
{
    [Header("Menu Prefab")]
    public GameObject menuPrefab;
    public float menuDistance = 0.25f;
    public Vector3 menuOffset = new Vector3(0, 0.12f, 0);

    [Header("State (read-only)")]
    public BrainInteractionMode mode = BrainInteractionMode.None;

    GameObject menuInstance;

    public void ShowMenu()
    {
        if (!menuPrefab) { Debug.LogWarning("No menuPrefab set."); return; }

        if (!menuInstance)
            menuInstance = Instantiate(menuPrefab);

        PositionMenu();
        menuInstance.SetActive(true);
    }

    public void HideMenu()
    {
        if (menuInstance) menuInstance.SetActive(false);
    }

    void PositionMenu()
    {
        var cam = Camera.main;
        if (!cam || !menuInstance) return;

        Vector3 forward = (transform.position - cam.transform.position).normalized;
        // put the menu in front of the object, facing the user
        Vector3 pos = transform.position + forward * menuDistance + menuOffset;

        menuInstance.transform.position = pos;
        menuInstance.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        menuInstance.transform.localScale = Vector3.one * 0.002f; // safe default
    }

    void Update()
    {
        if (menuInstance && menuInstance.activeSelf) PositionMenu(); // keep facing player
    }

    // ----- Button hooks -----
    public void OnRotatePressed()
    {
        mode = BrainInteractionMode.Rotate;
        HideMenu();
    }

    public void OnStopPressed()
    {
        mode = BrainInteractionMode.None;
        HideMenu();
    }

    public void OnResetPressed()
    {
        transform.rotation = Quaternion.identity;
        HideMenu();
    }
}
