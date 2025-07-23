using UnityEngine;
using UnityEngine.SceneManagement;
public class ButtonFunctions : MonoBehaviour
{
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
        Restart();
        gameManager.instance.NextLevel();

    }
    public void Restart()
    {
        gameManager.instance.stateUnpause();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
