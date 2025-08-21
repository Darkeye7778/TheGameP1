using System.Collections;
using UnityEngine;

public class IceTrap : MonoBehaviour
{
    [SerializeField] GameObject iceTrapEffect; // The effect to spawn when the trap is triggered
    [SerializeField] float freezeDuration;
    [SerializeField] float slowWalkSpeed;
    [SerializeField] float slowRunSpeed;
    [SerializeField] float damage;
    PlayerController playermovement;
    private float initWalkspeed, initRunspeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playermovement = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        initWalkspeed = playermovement.WalkingSpeed;
        initRunspeed = playermovement.RunningSpeed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            StartCoroutine(Freeze());
    }

    // slow the player when they interact with the trap
    private void SetMovementSpeed(float Walk, float Run)
    {
        playermovement.WalkingSpeed = Walk;
        playermovement.RunningSpeed = Run;
    }



    IEnumerator Freeze()
    {
        SetMovementSpeed(0.34f, 2f);
        DamageSource dmg = new DamageSource("Ice Trap", gameObject);
        playermovement.OnTakeDamage(dmg, damage);                               // Apply damage to player
        Instantiate(iceTrapEffect, transform.position, Quaternion.identity);    // Spawn the ice trap effect
        yield return new WaitForSeconds(freezeDuration);
        SetMovementSpeed(initWalkspeed, initRunspeed);                          // Reset to original speed
    }
}