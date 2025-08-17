using System;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public abstract class AIState : MonoBehaviour
{
    public virtual void OnStart(EnemyAI controller)
    {
        Controller = controller;
    }
    
    public abstract void OnUpdate();
    public bool OverriddenByEnemy { get; private set; } = true;
    protected EnemyAI Controller { get; private set; }
}

public abstract class AISight : MonoBehaviour
{
    public abstract IDamagable FindTarget(EnemyAI controller);

    // Can actually see target; rather than remembering location.
    public abstract bool CanSee();
    public abstract IDamagable GetTarget();
    public abstract bool TrySetTarget(IDamagable target);
}

public class EnemyAI : Inventory, IDamagable
{
    [Header("EnemyAI")]
    public IDamagable Target => Sight.GetTarget();
    
    [Header("Hitbox")] 
    public Transform[] HitPoints;
    private Vector3[] _hitPoints;
    
    private AIState _currentState;
    private bool _queueState;

    public InputState InputFlags
    {
        get => base.InputFlags;
        set => base.InputFlags = value;
    }

    public void SetState(AIState state)
    {
        _currentState = state;
        _queueState = true;
    }
    
#if UNITY_EDITOR
    private GameObject _targetGameObject;
#endif

    [field: SerializeField] public AISight Sight { get; private set; }
    [field: SerializeField] public AIState WanderState { get; private set; }
    [field: SerializeField] public AIState InvestigateState { get; private set; }
    [field: SerializeField] public AIState EnemySpottedState { get; private set; }
        
    [field: SerializeField] public float Health { get; private set; } = 100;
    public bool IsDead => Health <= 0;
    
    private Vector3 _previousPosition;
    public NavMeshAgent Agent { get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    new void Start()
    {
        base.Start();
        Agent = GetComponent<NavMeshAgent>();
        
        SetState(WanderState);
    }

    // Update is called once per frame
    new void Update()
    {
        if (IsDead)
            return;
        
        if (_hitPoints == null || _hitPoints.Length != HitPoints.Length)
            _hitPoints = new Vector3[HitPoints.Length];
        for (int i = 0; i < HitPoints.Length; i++)
            _hitPoints[i] = HitPoints[i].position;
        
        InputFlags = 0;

        if (Input.GetKey(KeyCode.P))
            InputFlags |= InputState.Firing;
        if (Input.GetKeyDown(KeyCode.P))
            InputFlags |= InputState.FiringFirst;
        
        if (Input.GetKeyDown(KeyCode.K))
            InputFlags |= InputState.CycleFireMode;
        if (Input.GetKeyDown(KeyCode.L))
            InputFlags |= InputState.Reload;

        if (Input.GetKeyDown(KeyCode.Semicolon))
            InputFlags |= CurrentWeapon == Primary ? InputState.UseSecondary : InputState.UsePrimary;
         
        CalculateVelocity();

        if (_queueState)
        {
            _currentState.OnStart(this);
            _queueState = false;
        }
        _currentState.OnUpdate();

        IDamagable target = Sight.FindTarget(this);
        if(Target != null && (!_currentState || _currentState.OverriddenByEnemy))
            SetState(EnemySpottedState);

    #if UNITY_EDITOR
        _targetGameObject = target?.GameObject();
    #endif
        
        IK.LookAtWeight = Mathf.Lerp(IK.LookAtWeight, target != null ? 1f : 0f, Time.deltaTime * 10);
        if (target != null)
            IK.LookAt = target.LookTarget();
        
        base.Update();
    }

    private void CalculateVelocity()
    {
        Vector3 velocity = (transform.position - _previousPosition) / Time.deltaTime;
        velocity.x /= transform.lossyScale.x;
        velocity.y /= transform.lossyScale.y;
        velocity.z /= transform.lossyScale.z;
        _previousPosition = transform.position;

        velocity = transform.InverseTransformDirection(velocity);
        
        Animator.SetFloat("Speed", Mathf.Max(velocity.magnitude, 1));
        Animator.SetFloat("Velocity X", velocity.x);
        Animator.SetFloat("Velocity Y", velocity.z);
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        bool wasDead = IsDead;
        
        Health -= damage;

        if (Target == null && source.Object.TryGetComponent(out IDamagable damagable))
            if(Sight.TrySetTarget(damagable))
                SetState(EnemySpottedState);

        if (!IsDead)
            return;

        Agent.enabled = false;
        Animator.SetTrigger("Death");
        
        if(!wasDead)
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

        IK.LookAtWeight = IK.GripWeight = 0;
    }

    public GameObject GameObject()
    {
        return gameObject;
    }

    public Vector3[] AimTargets()
    {
        return _hitPoints;
    }

    public Vector3 LookTarget()
    {
        return Eye.position;
    }

    bool IDamagable.IsDead()
    {
        return IsDead;
    }
}
