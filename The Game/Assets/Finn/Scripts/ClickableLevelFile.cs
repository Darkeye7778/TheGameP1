using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ClickableLevelFile : MonoBehaviour
{
    public LevelDefinition level;

    [Header("Interaction")]
    public float maxDistance = 6f;
    public bool requireLineOfSight = true;
    public LayerMask lineOfSightMask = ~0;   // set this to your Interactable/Default layers

    [Header("UI")]
    public GameObject pressFUI;              // assign your existing "F to interact" UI object

    Camera cam;
    Collider[] myColliders;
    bool canInteract;

    void Awake()
    {
        cam = Camera.main;
        myColliders = GetComponentsInChildren<Collider>(true);
        ShowPrompt(false);
    }

    void OnDisable() => ShowPrompt(false);

    void Update()
    {
        if (!cam) cam = Camera.main;
        canInteract = false;

        if (cam)
        {
            // Cast from center of screen
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (!requireLineOfSight)
            {
                // Distance-only check to this object
                float dist = Vector3.Distance(cam.transform.position, transform.position);
                canInteract = dist <= maxDistance;
            }
            else if (Physics.Raycast(ray, out var hit, maxDistance, lineOfSightMask, QueryTriggerInteraction.Collide))
            {
                // Interactable only if the thing we're looking at is THIS file (or a child)
                if (hit.collider && IsMine(hit.collider))
                    canInteract = true;
            }
        }

        ShowPrompt(canInteract);

        if (canInteract && Input.GetKeyDown(KeyCode.F))
        {
            var lm = LevelManager.Instance;
            if (lm != null) lm.LoadLevel(level);
            else Debug.LogError("LevelManager.Instance is null");
        }
    }

    bool IsMine(Collider c)
    {
        if (c.transform == transform || c.transform.IsChildOf(transform)) return true;
        if (myColliders != null)
            for (int i = 0; i < myColliders.Length; i++)
                if (c == myColliders[i]) return true;
        return false;
    }

    void ShowPrompt(bool show)
    {
        if (pressFUI && pressFUI.activeSelf != show)
            pressFUI.SetActive(show);
    }
}
