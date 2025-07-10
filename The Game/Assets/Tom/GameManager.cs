using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class gamemanager : MonoBehaviour
{
    public static gamemanager instance;
    [SerializeField] GameObject ActiveMenu;
    [SerializeField] GameObject PauseMenu;
    [SerializeField] GameObject LoseMenu;
    [SerializeField] GameObject WinMenu;
    [SerializeField] TMP_Text GunName;
    [SerializeField] TMP_Text TerroristCountTxt;
    [SerializeField] TMP_Text TimerTxt;
    [SerializeField] TMP_Text HostageTxt;
    [SerializeField] TMP_Text AmmoCountTxt;
    [SerializeField] TMP_Text AmmoReserveTxt;
    [SerializeField] TMP_Text GunModeTxt;
    public bool isPaused;
    public bool PrimaryWeaponEquipped;

    public GameObject Player;
    public GameObject PlayerDamagePanel;
    public Image PlayerHealthBar;
    public Image PlayerSprintBar;
    public Image GunAmmoBar;
    public PlayerController playerScript;
    float timeScaleOriginal;

    int TerroristCount;
    int Timer;
    int HostageCount;
    int AmmoCount;
    int Health;
    int Stamina;

    void Awake()
    {
        instance = this;

        Player = GameObject.FindWithTag("Player");
        playerScript = Player.GetComponent<PlayerController>();
        timeScaleOriginal = Time.timeScale;
    }

    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            if(ActiveMenu == null)
            {
                StatePause();
                ActiveMenu = PauseMenu;
                ActiveMenu.SetActive(true);
            }
            else if (ActiveMenu == PauseMenu)
            {
                StateResume();
            }
        }
    }


    public void UpdateHostageCount(int amount)
    {
       HostageCount += amount;
       HostageTxt.text = HostageCount.ToString("F0");

       if (HostageCount <= 0)
       {
            StatePause();
            ActiveMenu = WinMenu;
            ActiveMenu.SetActive(true);
       }
    }

    public void UpdateTerroristCount(int amount)
    {
        TerroristCount += amount;
        TerroristCountTxt.text = TerroristCount.ToString("F0");

    }
    public void UpdateTimer(int amount)
    {
        Timer += amount;
        TimerTxt.text = (Timer/60).ToString() + " : " + Timer.ToString().Substring(Timer.ToString().Length-2);

    }
    public void UpdateAmmoCount(int amount)
    {
        AmmoCount += amount;
        // Assuming you have a UI Text element to display ammo count
        // AmmoCountTxt.text = AmmoCount.ToString("F0");
    }

    public void StatePause()
    {
        isPaused = !isPaused;
        Time.timeScale = 0;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

    }
    public void StateResume()
    {
        isPaused = !isPaused;
        Time.timeScale = timeScaleOriginal;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        ActiveMenu.SetActive(false);
        ActiveMenu = null;
    }

    public void YouLose()
    {
        StatePause();
        ActiveMenu = LoseMenu;
        ActiveMenu.SetActive(true);
    }
}