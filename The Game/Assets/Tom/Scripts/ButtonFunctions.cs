using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
public class ButtonFunctions : MonoBehaviour
{

    [SerializeField] string Level1;
    public void NewGame()
    {
        SceneManager.LoadScene(Level1);
    }

    public void Resume()
    {
        gameManager.instance.stateUnpause();
    }

    public void Respawn()
    {
        gameManager.instance.updateHostagesSaved(-(gameManager.instance.gameHostageSaved));
        gameManager.instance.stateUnpause();
        MapGenerator.Instance.GenerateSame();
        gameManager.instance.ResetLoseMenu();
        gameManager.instance.SetPlayer();
        gameManager.instance.ResetTimer();
        SetLoadout(gameManager.instance.LastLoadout);
    }

    public void NextLevel()
    {
        gameManager.instance.stateUnpause();
        gameManager.instance.NextLevel();
        gameManager.instance.Invoke("ShowLoadouts", 1.5f);
        gameManager.instance.SetPlayer();
        SetLoadout(gameManager.instance.LastLoadout);
    }

public void Restart()
    {
        gameManager.instance.stateUnpause();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        gameManager.instance.SetPlayer();
        SetLoadout(gameManager.instance.LastLoadout);
    }

    public void SetLoadout(LoadoutLoader loader)
    {
        SetLoadout(loader.loadoutRef);
    }
    
    public void SetLoadout(Loadout loadout)
    {
        if (loadout == null)
            return;
        
        gameManager.instance.LastLoadout = loadout;
        gameManager.instance.stateUnpause();
        PlayerInventory inv = gameManager.instance.inventoryScript;
        PlayerController player = gameManager.instance.playerScript;
        inv.Primary.Weapon = loadout.Primary;
        inv.Secondary.Weapon = loadout.Secondary;
        player.MaximumHealth = (int)loadout.Health;
        player.MaximumStamina = (int)loadout.Stamina;
        player.WalkingSpeed = 2 * loadout.SpeedMult; // this should change
        player.RunningSpeed = 4 * loadout.SpeedMult; // this should change
        inv.ResetInventory();
        inv.SetCurrentWeapon(inv.Primary);
        player.ResetState();
    }

    public void LoadHub()
    {
        SceneManager.LoadScene("Hub");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("Start Menu");
    }

    public void SetAudio(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("VolumeSaver", volume);
    }
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
