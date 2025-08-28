using UnityEngine;
using System;
using UnityEngine.LowLevel;
public class HealthPickup : MonoBehaviour, Interactable
{
    public uint healthAmount;
    PlayerController player;
    private void Start()
    {
        player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
    }
    public void OnInteract(GameObject interactor)
    {
        DamageSource dmg = new DamageSource("Health Pickup", gameObject);
        player.OnTakeDamage(dmg, -healthAmount);
        Destroy(gameObject);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            DamageSource dmg = new DamageSource("Health Pickup", gameObject);
            player.OnTakeDamage(dmg, -healthAmount);
            Destroy(gameObject);
        }
    }

}
