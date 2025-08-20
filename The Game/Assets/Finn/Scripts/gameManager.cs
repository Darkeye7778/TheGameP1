using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class gameManager : MonoBehaviour
{
    public static gameManager instance;

    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject loadoutsScreen;
    [SerializeField] GameObject InteractionPopup;
    [SerializeField] GameObject playerUI;
    public Loadout LastLoadout;
    public bool isPaused;
    public GameObject player;
    public PlayerController playerScript;
    public PlayerInventory inventoryScript;

    float timeScaleOrig;

    public int gameTerroristCount;
    public int gameHostageCount;
    public int gameHostageSaved;
    [SerializeField] public TextMeshProUGUI TimerTxt;
    [SerializeField] public TextMeshProUGUI HostageTxt;
    [SerializeField] TextMeshProUGUI GunName;
    [SerializeField] TextMeshProUGUI CurrentAmmoTxt;
    [SerializeField] TextMeshProUGUI GunModeTxt;
    [SerializeField] Loadout[] loadouts;
    [SerializeField] LoadoutLoader[]  loadoutLoaders;
    public Image PlayerHealthBar;
    public Image PlayerSprintBar;
    public Image PrimaryGun;
    public Image SecondaryGun;
    public Image GunAmmoBar;
    public GameObject PlayerHurt;
    public GameObject HealthFlash;

    public List<GameObject> SpawnedEntities = new List<GameObject>();

    [SerializeField] private float timerFlashThreshold;
    [SerializeField] private float flashSpeed;

    public float StartingTime = 120;
    public float Timer { get; set; }

    private Color timerColorOrig;
    private bool isFlashing;
    private Coroutine flashRoutine;
    private bool loseMenuUp;
    [SerializeField] private Sprite helicopterSprite;

    private bool hasTriggeredHelicopterMessage;

    private bool PlayerReady =>
    playerScript != null && inventoryScript != null;

    void Awake()
    {
        menuActive = null;
        stateUnpause();
        loseMenuUp = false;
        menuLose.SetActive(false);
        instance = this;
        timeScaleOrig = Time.timeScale;

        Timer = StartingTime;
        if (TimerTxt != null)
        {
            timerColorOrig = TimerTxt.color;
        }
    }

    private void Start()
    {
        Invoke(nameof(ShowLoadouts), 1.5f);
        
    }

    public void SetPlayer(GameObject _player)
    {
        player = _player;
        playerScript = player.GetComponent<PlayerController>();
        inventoryScript = player.GetComponent<PlayerInventory>();
    }
    
    public void SetPlayer()
    {
        SetPlayer(GameObject.FindWithTag("Player"));
    }

    public void ResetLoseMenu()
    {
        loseMenuUp = false;
        playerScript.ResetState();
    }
    void Update()
    {
        if (Input.GetButtonDown("Cancel"))
        {
            if (menuActive == null)
            {
                statePause(); 
                menuActive = menuPause; 
                menuActive.SetActive(true);
                
            }
            else if (menuActive == menuPause)
            {
                stateUnpause();
                
            }
        }

        
        Timer -= Time.deltaTime;
        if (TimerTxt) TimerTxt.text = $"{(int)Timer / 60:00}:{(int)Timer % 60:00}";
        if (Timer <= timerFlashThreshold)
        {
            float t = Mathf.PingPong(Time.unscaledTime * flashSpeed, 1f);
            if (TimerTxt) TimerTxt.color = Color.Lerp(timerColorOrig, Color.red, t);
        }
        else if (TimerTxt) TimerTxt.color = Color.white;

        if (Timer <= 0.0f) youLose();

        if (!PlayerReady)
        {
            if (InteractionPopup) InteractionPopup.SetActive(false);
            return;
        }

        SetGunModeText(inventoryScript.CurrentWeapon.Mode);
        SetAmmoTxt(inventoryScript.CurrentWeapon.LoadedAmmo, inventoryScript.CurrentWeapon.ReserveAmmo);

        if (inventoryScript != null && playerUI != null)
        {
            GunAmmoBar.fillAmount = (float)inventoryScript.CurrentWeapon.LoadedAmmo /
                                    inventoryScript.CurrentWeapon.Weapon.Capacity;
            GunName.text = inventoryScript.CurrentWeapon.Weapon.name;
            
            PrimaryGun.sprite = inventoryScript.CurrentWeapon.Weapon.Image;
            SecondaryGun.sprite = inventoryScript.HolsteredWeapon.Weapon.Image;
        }
        if (playerScript != null && playerUI != null)
        {
            PlayerSprintBar.fillAmount = playerScript.StaminaRelative;
            PlayerHealthBar.fillAmount = playerScript.HealthRelative;

            
        }

        if (playerScript != null)
        {
            if (playerScript.CurrentInteractable != null)
                InteractionPopup.SetActive(true);
            else
                InteractionPopup.SetActive(false);

            if (playerScript.TookDamage) StartCoroutine(PlayerHurtFlash());
            if (playerScript.GainedHealth) StartCoroutine(PlayerHealthFlash());
        }
    }
    
    
    private void LateUpdate()
    {
        if (!PlayerReady) return;                   
        if (playerScript.IsDead && !loseMenuUp)
        {
            Debug.LogError($"[Lose] Player died. Health={playerScript.Health}, Max={playerScript.MaximumHealth}");
            loseMenuUp = true;
            youLose();
        }
    }

    public void statePause()
    {
        isPaused = true;
        InteractionPopup.SetActive(false);
        Time.timeScale = 0.0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (playerUI != null) playerUI.SetActive(false);
    }

    public void ShowLoadouts()
    {
        menuActive = loadoutsScreen;
        menuActive.SetActive(true);
        LoadLoadouts();
        statePause();
    }
    void LoadLoadouts()
    {
        foreach (LoadoutLoader loader in loadoutLoaders)
        {
            loader.gameObject.SetActive(true);
            loader.Load(loadouts[Random.Range(0, loadouts.Length)]);
        }
        statePause();
    }
    public void stateUnpause()
    {
        isPaused = false;
        Time.timeScale = 1.0f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if(menuActive != null)
            menuActive.SetActive(false);
        menuActive = null;
        if (playerUI != null) playerUI.SetActive(true);
    }

    public void updateGameGoal(int amount)
    {
        gameHostageCount += amount;
        HostageTxt.text = gameHostageCount.ToString("F0");

        if (gameHostageCount <= 0)
        {
            statePause();
            menuActive = menuWin;
            menuActive.SetActive(true);
        }
    }

    public void updateTerroristCount(int amount)
    {
        gameTerroristCount += amount;
    }

    public void updateHostagesSaved(int amount)
    {
        gameHostageSaved += amount;
    }

    public void SetGunModeText(FireMode fireMode)
    {
        if (GunModeTxt == null) return;
        switch(fireMode)
        {
            case FireMode.Single:
                GunModeTxt.text = "Single";
                break;
            case FireMode.Burst:
                GunModeTxt.text = "Burst";
                break;
            case FireMode.Auto:
                GunModeTxt.text = "Auto";
                break;
            default:
                GunModeTxt.text = "Error";
                break;
        }
    }
    public void SetAmmoTxt(uint currAmount, uint reserveAmount)
    {
        if (CurrentAmmoTxt == null) return;
        CurrentAmmoTxt.text = $"<size=150%>{currAmount} | </size>{reserveAmount}";
    }

    public void youWin()
    {
        statePause();
        menuActive = menuWin;
        menuActive.SetActive(true);
    }

    public void NextLevel()
    {
        MapGenerator.Instance.TargetRooms += 2;
        MapGenerator.Instance.EnemySpawnAmount++;
        MapGenerator.Instance.HostageSpawnAmount++;
        MapGenerator.Instance.TrapSpawnAmount++;
        loseMenuUp = false;
        stateUnpause();
        MapGenerator.Instance.Generate();
        gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
        gameHostageSaved = 0;
        StartingTime += 30;
        Timer = StartingTime;
        updateGameGoal(0);
    }

    public void Retry()
    {
        stateUnpause();
        loseMenuUp = false;
        MapGenerator.Instance.GenerateSame();
        gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
        gameHostageSaved = 0;
        Timer = StartingTime;
        updateGameGoal(0);
    }
    public void youLose()
    {
        Debug.LogError($"[Lose] Player died. Health={playerScript.Health}, Max={playerScript.MaximumHealth}");
        statePause();
        menuActive = menuLose;
        menuActive.SetActive(true);
    }

    public void RegisterEntity(GameObject obj)
    {
        SpawnedEntities.Add(obj);
    }


    IEnumerator PlayerHurtFlash()
    {
        PlayerHurt.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        PlayerHurt.SetActive(false);
    }

    IEnumerator PlayerHealthFlash()
    {
        HealthFlash.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        HealthFlash.SetActive(false);
    }
}