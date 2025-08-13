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
    }

    public void NextLevel()
    {
        gameManager.instance.stateUnpause();
        gameManager.instance.NextLevel();
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
        player.WalkingSpeed *= loadout.SpeedMult;
        player.RunningSpeed *= loadout.SpeedMult;
        inv.ResetInventory();
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
