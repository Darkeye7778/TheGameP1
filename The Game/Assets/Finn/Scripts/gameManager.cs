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
    
    public GameObject HostagePrefab;
    public GameObject[] EnemyPrefabs;
    public GameObject TrapPrefab;
    
    [SerializeField] TextMeshProUGUI GunName;
    [SerializeField] TextMeshProUGUI TerroristCountTxt;
    [SerializeField] TextMeshProUGUI TimerTxt;
    [SerializeField] TextMeshProUGUI HostageTxt;
    [SerializeField] TextMeshProUGUI AmmoReserveTxt;
    [SerializeField] TextMeshProUGUI GunModeTxt;
    
    public Image PlayerHealthBar;
    public Image PlayerSprintBar;
    public Image GunAmmoBar;

    public float StartingTime = 120;
    private float _timer;

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

        if (playerScript.IsDead)
            youLose();
        
        GunAmmoBar.fillAmount = (float) inventoryScript.CurrentWeapon.LoadedAmmo / inventoryScript.CurrentWeapon.Weapon.Capacity;
        GunName.text = inventoryScript.CurrentWeapon.Weapon.name;
        AmmoReserveTxt.text = $"{inventoryScript.CurrentWeapon.LoadedAmmo}/{inventoryScript.CurrentWeapon.ReserveAmmo}";
        GunModeTxt.text = inventoryScript.CurrentWeapon.Mode.ToString();

        PlayerSprintBar.fillAmount = playerScript.StaminaRelative;
        PlayerHealthBar.fillAmount = playerScript.HealthRelative;

        _timer -= Time.deltaTime;
        TimerTxt.text = $"{(int)_timer / 60}:{Mathf.Max(_timer % 60, 0.0f):F0}";
        
        if(_timer <= 0.0f)
            youLose();
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
        TerroristCountTxt.text = gameTerroristCount.ToString("F0");
    }
    
    public void updateHostagesSaved(int amount)
    {
        gameHostageSaved += amount;
    }

    public void youLose()
    {
        statePause();
        menuActive = menuLose;
        menuActive.SetActive(true);
    }
}