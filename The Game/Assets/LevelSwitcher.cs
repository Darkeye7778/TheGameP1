using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public class LevelSwitcher : MonoBehaviour, Interactable
{
    public string Level;

    public void OnInteract(GameObject interactor)
    {
        if (Level != null) SceneManager.LoadScene(Level);
        else Debug.Log("No level selected");
    }
}
