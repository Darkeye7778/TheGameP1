using UnityEngine;
using System.Collections;

public class Claymore : MonoBehaviour
{
    public float detectionDistance = 5f;
    public float explosionDelay = 3f; // Delay before the claymore activates
    public bool shouldDebug = false;
    public AudioSource activationSound;
    public AudioSource explosionSound;

    bool isActivated = false;
    bool isPlayerInTrigger = false;
    void Update()
    {
        if (isActivated)
            return; 

        if (CheckForPlayer()) StartCoroutine(Activate());

        if (shouldDebug)
        {
            Debug.DrawRay(transform.position, transform.forward * detectionDistance, Color.red);
        }
    }

    IEnumerator Activate()
    {
        isActivated = true;
        if (activationSound != null) activationSound.Play();
        Debug.Log("Claymore activated! Waiting for explosion...");
        yield return new WaitForSeconds(explosionDelay);
        if (explosionSound != null)  explosionSound.Play();
        Debug.Log("Claymore exploded!");
        if (isPlayerInTrigger)
        {
            // Here you can add logic to deal damage to the player
            Debug.Log("Player hit by Claymore explosion!");
        }

    }

    bool CheckForPlayer()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, detectionDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                Debug.Log("Player detected by Claymore!");
                return true;
            }
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
        }
    }
}
