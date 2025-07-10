using UnityEngine;
using System.Collections;

public class HostageDropOff : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (gameManager.instance.gameHostageSaved >= 1)
            {
                gameManager.instance.updateGameGoal(0 - gameManager.instance.gameHostageSaved);
                gameManager.instance.updateHostagesSaved(0 - gameManager.instance.gameHostageSaved);
            }
            else
            {
                Debug.Log("No Hostages, Go Go GO!");
            }
        }
    }
}
