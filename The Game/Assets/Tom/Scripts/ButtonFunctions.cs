using UnityEditor;
using UnityEngine;
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
        gameManager.instance.stateUnpause();
        MapGenerator.Instance.GenerateSame();
        gameManager.instance.ResetLoseMenu();
    }

    public void NextLevel()
    {
        gameManager.instance.stateUnpause();
        gameManager.instance.NextLevel();
        gameManager.instance.Invoke("ShowLoadouts", 1.5f);
    }

public void Restart()
    {
        gameManager.instance.stateUnpause();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SetLoadout(LoadoutLoader loader)
    {
        Loadout loadout = loader.loadoutRef;
        gameManager.instance.stateUnpause();
        PlayerInventory inv = gameManager.instance.player.GetComponent<PlayerInventory>();
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
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
