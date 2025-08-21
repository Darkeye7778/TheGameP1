using UnityEngine;

public class EnergyDrop : MonoBehaviour, Interactable
{
    float EnergyAmount;
    PlayerController playerMovement;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayerController player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
    }

    // Update is called once per frame
    public void OnInteract(GameObject interactor)
    {
        playerMovement.MaximumStamina += EnergyAmount;
        Destroy(gameObject);
    }
}
