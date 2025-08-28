using UnityEngine;
using System;
public class AmmoBox : MonoBehaviour, Interactable
{
    public uint ammoCount = 10;
    PlayerInventory inventory;
    private void Start()
    {
        inventory = GameObject.FindWithTag("Player").GetComponent<PlayerInventory>();
    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            inventory.CurrentWeapon.ReserveAmmo = Math.Min(
                inventory.CurrentWeapon.ReserveAmmo + ammoCount,
                inventory.CurrentWeapon.Weapon.ReserveCapacity
            );
            Destroy(gameObject);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        inventory.CurrentWeapon.ReserveAmmo = Math.Min(
            inventory.CurrentWeapon.ReserveAmmo + ammoCount,
            inventory.CurrentWeapon.Weapon.ReserveCapacity
        );
        Destroy(gameObject);
    }

}
