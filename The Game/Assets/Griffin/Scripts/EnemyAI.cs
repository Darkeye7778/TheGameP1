using System;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : Inventory, IDamagable
{
    public Animator Animator;
    
    private Vector3 _previousPosition;
    private NavMeshAgent _agent;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    new void Start()
    {
        base.Start();
        _agent = GetComponent<NavMeshAgent>();


    }

    // Update is called once per frame
    new void Update()
    {
        InputFlags = 0;
        if(Input.GetKeyDown(KeyCode.P))
            Animator.SetTrigger("Death");

        Vector3 velocity = (transform.position - _previousPosition) / Time.deltaTime;
        _previousPosition = transform.position;

        //velocity = Quaternion.Inverse(transform.rotation) * velocity;
        
        Animator.SetFloat("Velocity X", velocity.x);
        Animator.SetFloat("Velocity Y", velocity.z);

        _agent.SetDestination(Vector3.zero);
        
        base.Update();
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        
    }

    public GameObject GameObject()
    {
        return gameObject;
    }
}
