using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public class LevelSwitcher : MonoBehaviour, Interactable
{
    public SceneAsset Level;

    public void OnInteract(GameObject interactor)
    {
        if (Level != null) SceneManager.LoadScene(Level.name);
        else Debug.Log("No level selected");
    }
}
