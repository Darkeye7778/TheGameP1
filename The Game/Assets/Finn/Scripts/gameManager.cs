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

    public bool isPaused;
    public GameObject player;
    public PlayerController playerScript;
    public PlayerInventory inventoryScript;

    float timeScaleOrig;

    public int gameTerroristCount;
    public int gameHostageCount;
    public int gameHostageSaved;

    public int EnemySpawnAmount = 4;
    public int TrapSpawnAmount = 4;
    public int HostageSpawnAmount = 2;

    public GameObject PlayerDamagedFlash;
    public GameObject HostagePrefab;
    public GameObject[] EnemyPrefabs;
    public GameObject TrapPrefab;

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

    [SerializeField] private Sprite helicopterSprite;

    private bool hasTriggeredHelicopterMessage;

    void Awake()
    {
        menuActive = null;
        stateUnpause();

        instance = this;

        player = GameObject.FindWithTag("Player");
        playerScript = player.GetComponent<PlayerController>();
        inventoryScript = player.GetComponent<PlayerInventory>();
        timeScaleOrig = Time.timeScale;
        int hostageSpawned = HostageSpawnAmount;
        GameObject hostageLocations = GameObject.FindWithTag("HostageLocation");
        for (int i = 0; i < hostageLocations.transform.childCount; i++)
            if (Random.Range(0.0f, 1.0f) <= (float) hostageSpawned / (hostageLocations.transform.childCount - i))
            {
                Instantiate(HostagePrefab, hostageLocations.transform.GetChild(i));
                hostageSpawned--;
            }

        int enemySpawned = EnemySpawnAmount;
        GameObject enemyLocations = GameObject.FindWithTag("EnemyLocation");
        for (int i = 0; i < enemyLocations.transform.childCount; i++)
            if (Random.Range(0.0f, 1.0f) <= (float)enemySpawned / (enemyLocations.transform.childCount - i))
            {
                Instantiate(EnemyPrefabs[Random.Range(0, EnemyPrefabs.Length)], enemyLocations.transform.GetChild(i));
                enemySpawned--;
            }

        int trapsSpawned = TrapSpawnAmount;
        GameObject trapLocations = GameObject.FindWithTag("Trap");
        for (int i = 0; i < trapLocations.transform.childCount; i++)
            if (Random.Range(0.0f, 1.0f) <= (float) trapsSpawned / (trapLocations.transform.childCount - i))
            {
                Instantiate(TrapPrefab, trapLocations.transform.GetChild(i));
                trapsSpawned--;
            }
        _timer = StartingTime;
        if (TimerTxt != null)
        {
            timerColorOrig = TimerTxt.color;
        }
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
                menuActive.SetActive(false);
            }
        }
        
        SetGunModeText(inventoryScript.CurrentWeapon.Mode);
        SetAmmoTxt(inventoryScript.CurrentWeapon.LoadedAmmo, inventoryScript.CurrentWeapon.ReserveAmmo);
        
        GunAmmoBar.fillAmount = (float) inventoryScript.CurrentWeapon.LoadedAmmo / inventoryScript.CurrentWeapon.Weapon.Capacity;
        GunName.text = inventoryScript.CurrentWeapon.Weapon.name;
        PlayerSprintBar.fillAmount = playerScript.StaminaRelative;
        PlayerHealthBar.fillAmount = playerScript.HealthRelative;

        PrimaryGun.sprite = inventoryScript.CurrentWeapon.Weapon.Image;
        SecondaryGun.sprite = inventoryScript.HolsteredWeapon.Weapon.Image;

        _timer -= Time.deltaTime;
        instance.TimerTxt.text = $"{(int)_timer / 60:00}:{(int)_timer % 60:00}";

        if (playerScript.IsDead)
            youLose();

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
            
            Time.timeScale = 0;
            isPaused = true;

            int secondsRemaining = Mathf.RoundToInt(timerFlashThreshold);
            string unit = secondsRemaining == 1 ? "second" : "seconds";
            DialogManager.Instance.ShowDialog(helicopterSprite, "Ground Control", $"We're running out of time! We have leave in {secondsRemaining} {unit}! Get those hostages and run!");
        }
        if (_timer <= 0.0f)
            youLose();

        /*if (playerScript.TookDamage)
            StartCoroutine(PlayerHurtFlash());*/
    }

    public void statePause()
    {
        isPaused = true;
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