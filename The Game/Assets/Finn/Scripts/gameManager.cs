using System;
using System.Collections;
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

    public bool isPaused;
    public GameObject player;
    public PlayerController playerScript;
    public PlayerInventory inventoryScript;

    float timeScaleOrig;

    public int gameTerroristCount;
    public int gameHostageCount;
    public int gameHostageSaved;
    [SerializeField] TextMeshProUGUI GunName;
    [SerializeField] TextMeshProUGUI TimerTxt;
    [SerializeField] TextMeshProUGUI HostageTxt;
    [SerializeField] TextMeshProUGUI CurrentAmmoTxt;
    [SerializeField] TextMeshProUGUI GunModeTxt;

    public Image PlayerHealthBar;
    public Image PlayerSprintBar;
    public Image PrimaryGun;
    public Image SecondaryGun;
    public Image GunAmmoBar;
    public GameObject PlayerHurt;

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
        MapGenerator.Instance.Generate();
        gameHostageCount = MapGenerator.Instance.HostageSpawnAmount;
        gameHostageSaved = 0;
        updateGameGoal(0);
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
            }
            else if (menuActive == menuPause)
            {
                stateUnpause();
            }
        }

        SetGunModeText(inventoryScript.CurrentWeapon.Mode);
        SetAmmoTxt(inventoryScript.CurrentWeapon.LoadedAmmo, inventoryScript.CurrentWeapon.ReserveAmmo);

        GunAmmoBar.fillAmount = (float)inventoryScript.CurrentWeapon.LoadedAmmo / inventoryScript.CurrentWeapon.Weapon.Capacity;
        GunName.text = inventoryScript.CurrentWeapon.Weapon.name;
        PlayerSprintBar.fillAmount = playerScript.StaminaRelative;
        PlayerHealthBar.fillAmount = playerScript.HealthRelative;

        PrimaryGun.sprite = inventoryScript.CurrentWeapon.Weapon.Image;
        SecondaryGun.sprite = inventoryScript.HolsteredWeapon.Weapon.Image;

        _timer -= Time.deltaTime;
        instance.TimerTxt.text = $"{(int)_timer / 60:00}:{(int)_timer % 60:00}";



        if (_timer <= timerFlashThreshold)
        {
            float t = Mathf.PingPong(Time.unscaledTime * flashSpeed, 1f);
            TimerTxt.color = Color.Lerp(timerColorOrig, Color.red, t);
        }
        else
            TimerTxt.color = Color.white;

        if (_timer <= timerFlashThreshold && !hasTriggeredHelicopterMessage)
        {
            hasTriggeredHelicopterMessage = true;

            int secondsRemaining = Mathf.RoundToInt(timerFlashThreshold);
            string unit = secondsRemaining == 1 ? "second" : "seconds";
            DialogManager.Instance.ShowDialog(helicopterSprite, "Ground Control", $"We're running out of time! We have to leave in {secondsRemaining} {unit}! Get those hostages and run!");
        }
        if (_timer <= 0.0f)
            youLose();

        if (playerScript.CurrentInteractable != null)
            InteractionPopup.SetActive(true);
        else
            InteractionPopup.SetActive(false);

        if (playerScript.TookDamage)
            StartCoroutine(PlayerHurtFlash());
    }
    private void LateUpdate()
    {
        if (playerScript.IsDead && !loseMenuUp)
        {
            loseMenuUp = true;
            youLose();
        }
    }

    public void statePause()
    {
        isPaused = true;
        InteractionPopup.SetActive(false);
        Time.timeScale = 0;
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
            case FireMode.ThreeRoundBurst:
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
        CurrentAmmoTxt.text = $"{currAmount} | {reserveAmount}";
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
        statePause();
        menuActive = menuLose;
        menuActive.SetActive(true);
    }

    IEnumerator PlayerHurtFlash()
    {
        PlayerHurt.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        PlayerHurt.SetActive(false);
    }
}