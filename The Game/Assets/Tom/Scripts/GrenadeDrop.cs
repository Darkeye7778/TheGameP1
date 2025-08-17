using UnityEngine;

public class GrenadeDrop : MonoBehaviour , Interactable
{

    PlayerInventory inventory;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inventory = GameObject.FindWithTag("Player").GetComponent<PlayerInventory>();

    }
    public void OnInteract(GameObject interactor)
    {
        //Add to Grenade Inventory
    }
}
