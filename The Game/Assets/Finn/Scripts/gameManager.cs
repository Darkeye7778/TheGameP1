using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class gameManager : MonoBehaviour
{
    public static gameManager instance;

    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject InteractionPopup;
    [SerializeField] GameObject playerUI;
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
    private float _timer;

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

        _timer = StartingTime;
        if (TimerTxt != null)
        {
            timerColorOrig = TimerTxt.color;
        }
    }

    private void Start()
    {
        if (LevelManager.Instance == null)
        {
            MapGenerator.Instance.Generate();
            gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
            gameHostageSaved = 0;
            updateGameGoal(0);
        }
    }

    public void SetPlayer(GameObject _player)
    {
        player = _player;
        playerScript = player.GetComponent<PlayerController>();
        inventoryScript = player.GetComponent<PlayerInventory>();
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
                playerUI.SetActive(false);
            }
            else if (menuActive == menuPause)
            {
                stateUnpause();
                playerUI.SetActive(true);
            }
        }

        
        _timer -= Time.deltaTime;
        if (TimerTxt) TimerTxt.text = $"{(int)_timer / 60:00}:{(int)_timer % 60:00}";
        if (_timer <= timerFlashThreshold)
        {
            float t = Mathf.PingPong(Time.unscaledTime * flashSpeed, 1f);
            if (TimerTxt) TimerTxt.color = Color.Lerp(timerColorOrig, Color.red, t);
        }
        else if (TimerTxt) TimerTxt.color = Color.white;

        if (_timer <= 0.0f) youLose();

        if (!PlayerReady)
        {
            if (InteractionPopup) InteractionPopup.SetActive(false);
            return;
        }

        SetGunModeText(inventoryScript.CurrentWeapon.Mode);
        SetAmmoTxt(inventoryScript.CurrentWeapon.LoadedAmmo, inventoryScript.CurrentWeapon.ReserveAmmo);

        GunAmmoBar.fillAmount = (float)inventoryScript.CurrentWeapon.LoadedAmmo / inventoryScript.CurrentWeapon.Weapon.Capacity;
        GunName.text = inventoryScript.CurrentWeapon.Weapon.name;
        PlayerSprintBar.fillAmount = playerScript.StaminaRelative;
        PlayerHealthBar.fillAmount = playerScript.HealthRelative;

        PrimaryGun.sprite = inventoryScript.CurrentWeapon.Weapon.Image;
        SecondaryGun.sprite = inventoryScript.HolsteredWeapon.Weapon.Image;

        if (playerScript.CurrentInteractable != null)
            InteractionPopup.SetActive(true);
        else
            InteractionPopup.SetActive(false);

        if (playerScript.TookDamage) StartCoroutine(PlayerHurtFlash());
        if (playerScript.GainedHealth) StartCoroutine(PlayerHealthFlash());
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
        _timer = StartingTime;
        updateGameGoal(0);
    }

    public void Retry()
    {
        stateUnpause();
        loseMenuUp = false;
        MapGenerator.Instance.GenerateSame();
        gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
        gameHostageSaved = 0;
        _timer = StartingTime;
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