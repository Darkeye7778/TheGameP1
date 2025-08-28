using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;


public class LoadoutMenuStarter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject loadoutRoot;     // Canvas/Panel containing the loadout UI
    [SerializeField] private GameObject firstSelected;   // Optional: first selectable UI element

    [Header("Behavior")]
    [SerializeField] private bool pauseWorld = true;     // Pause entire world while menu open
    [SerializeField] private string playerTag = "Player";

    private float _prevTimeScale = 1f;
    private bool _open;
    private PlayerFreezeToggle _freeze;   // found later when player spawns

    private void Start()
    {
        ShowLoadout();
    }

    private void Update()
    {
        // If player spawns AFTER world start, find and freeze them while menu is open.
        if (_open && _freeze == null)
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                _freeze = player.GetComponentInChildren<PlayerFreezeToggle>(true);
                if (_freeze == null) _freeze = player.AddComponent<PlayerFreezeToggle>();
                _freeze.SetFrozen(true);
            }
        }
    }

    public void ShowLoadout()
    {
        if (_open) return;
        _open = true;

        // 1) (Optional) Pause world FIRST so nothing moves while UI comes in.
        if (pauseWorld)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // 2) Enable menu
        if (loadoutRoot) loadoutRoot.SetActive(true);

        // 3) Kick animations on the NEXT frame (after layout & OnEnable/Start run)
        StartCoroutine(KickUIAnimationsNextFrame());

        // 4) Cursor/focus
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (firstSelected && EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    private IEnumerator KickUIAnimationsNextFrame()
    {
        // Let Unity finish OnEnable/Start + layout pass
        yield return null;
        Canvas.ForceUpdateCanvases(); // just in case there’s a pending layout calc

        // Now restart all UIAnimation components so they animate from startPos -> endPos
        if (loadoutRoot)
        {
            var anims = loadoutRoot.GetComponentsInChildren<UIAnimation>(true);
            foreach (var a in anims) a.Restart(); // uses unscaled time while paused
        }
    }

    public void OnLoadoutChosen(string loadoutId)
    {
        CloseLoadout();
    }

    public void CloseLoadout()
    {
        if (!_open) return;
        _open = false;

        if (pauseWorld)
            Time.timeScale = _prevTimeScale;

        if (loadoutRoot) loadoutRoot.SetActive(false);

        if (_freeze != null) _freeze.SetFrozen(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
