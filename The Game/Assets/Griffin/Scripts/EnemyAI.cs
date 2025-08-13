using System;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : Inventory, IDamagable
{
    [Header("EnemyAI")]
    public Animator Animator;
    public IKSolver IK;
        
    [field: SerializeField] public float Health { get; private set; } = 100;
    public bool IsDead => Health <= 0;
    
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
        if (IsDead)
            return;
        
        InputFlags = 0;
        
        CalculateVelocity();

        _agent.SetDestination(Vector3.zero);

        TransformData target = TransformData.FromGlobal(Viewmodel.transform) * CurrentWeapon.Weapon.Grip;
        IK.TargetPosition = target.Position;
        IK.TargetRotation = target.Rotation;
        
        base.Update();
    }

    private void CalculateVelocity()
    {
        Vector3 velocity = (transform.position - _previousPosition) / Time.deltaTime;
        velocity.x /= transform.lossyScale.x;
        velocity.y /= transform.lossyScale.y;
        velocity.z /= transform.lossyScale.z;
        _previousPosition = transform.position;

        velocity = Quaternion.Inverse(transform.rotation) * velocity;
        
        Animator.SetFloat("Speed", Mathf.Max(velocity.magnitude, 1));
        Animator.SetFloat("Velocity X", velocity.x);
        Animator.SetFloat("Velocity Y", velocity.z);
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        bool wasDead = IsDead;
        
        Health -= damage;

        if (!IsDead)
            return;
        
        Animator.SetTrigger("Death");
        
        if(!wasDead)
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

        IK.Weight = 0;
    }

    public GameObject GameObject()
    {
        return gameObject;
    }
}
