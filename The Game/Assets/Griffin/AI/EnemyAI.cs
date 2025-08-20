using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[Serializable]
public class AIThought<T>
{
    [field: SerializeField] public float MinTime { get; set; }
    [field: SerializeField] public float MaxTime { get; set; }

    public virtual void Think(T t) { }
}

[Serializable]
public class AIThoughtQueue<T>
{
    private List<AIThought<T>> _queue = new();

    private float _timer;

    public void Push(AIThought<T> thought)
    {
        if(!_queue.Exists(thought.Equals)) 
            PushDuplicate(thought);
    }
    
    public void PushDuplicate(AIThought<T> thought)
    {
        if (_queue.Count == 0)
            _timer = Random.Range(thought.MinTime, thought.MaxTime);
        _queue.Add(thought);
    }
    
    public void Clear()
    {
        _timer = 0;
        _queue.Clear();
    }

    public bool OnUpdate(T t)
    {
        if (_queue.Count == 0)
            return false;
        
        _timer -= Time.deltaTime;

        if (_timer > 0) 
            return false;
        
        _queue[0].Think(t);
        _queue.RemoveAt(0);

        if (_queue.Count != 0) 
            _timer = Random.Range(_queue[0].MinTime, _queue[0].MaxTime);

        return true;
    }
}

public abstract class AIState : MonoBehaviour
{
    public virtual void OnStart(EnemyAI controller)
    {
        Controller = controller;
    }
    
    public abstract void OnUpdate();
    public virtual bool OverriddenByEnemy() { return true; }
    public EnemyAI Controller { get; private set; }
}

public abstract class AIInvestigateState : AIState
{
    public abstract void SetTarget(Vector3 position);
}

public abstract class AISight : MonoBehaviour
{
    public abstract IDamagable FindTarget(EnemyAI controller);

    // Can actually see target; rather than remembering location.
    public abstract bool CanSee();
    public abstract IDamagable GetTarget();
    public abstract bool TrySetTarget(IDamagable target);
    public abstract bool CheckRay(Ray ray);
    public bool CheckRay(Vector3 origin, Vector3 direction) { return CheckRay(new Ray(origin, direction)); }
}

public class EnemyAI : Inventory, IDamagable
{
    [Header("EnemyAI")]
    public IDamagable Target => Sight.GetTarget();
    public AIThoughtQueue<EnemyAI> Thoughts { get; private set; }
    
    [field: Header("Display")]
    [field: SerializeField] public WeaponRotationPivot Pivot { get; private set; }
    [field: SerializeField] public Vector3 AimOffset { get; private set; }
    [field: SerializeField] public Transform WeaponOffsetOrigin { get; private set; }
    
    [Header("Hitbox")] 
    public Transform[] HitPoints;
    private Vector3[] _hitPoints;
    
    public AIState CurrentState { get; private set; }
    private bool _queueState;

    public InputState InputFlags
    {
        get => base.InputFlags;
        set => base.InputFlags = value;
    }

    public void SetState(AIState state)
    {
        CurrentState = state;
        _queueState = true;
    }
    
#if UNITY_EDITOR
    private GameObject _targetGameObject;
#endif

    [field: SerializeField] public AISight Sight { get; private set; }
    [field: SerializeField] public AIState WanderState { get; private set; }
    [field: SerializeField] public AIInvestigateState InvestigateState { get; private set; }
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
        Thoughts = new AIThoughtQueue<EnemyAI>();
        
        SetState(WanderState);
    }

    // Update is called once per frame
    new void Update()
    {
        if (IsDead)
            return;

        InputFlags = 0;
        
        if (_hitPoints == null || _hitPoints.Length != HitPoints.Length)
            _hitPoints = new Vector3[HitPoints.Length];
        for (int i = 0; i < HitPoints.Length; i++)
            _hitPoints[i] = HitPoints[i].position;
         
        CalculateVelocity();

        if (_queueState)
        {
            CurrentState.OnStart(this);
            _queueState = false;
        }
        CurrentState.OnUpdate();

        IDamagable target = Sight.FindTarget(this);
        if(Target != null && (!CurrentState || CurrentState.OverriddenByEnemy()))
            SetState(EnemySpottedState);

    #if UNITY_EDITOR
        _targetGameObject = target?.GameObject();
    #endif
        
        IK.LookAtWeight = Mathf.Lerp(IK.LookAtWeight, target != null ? 1f : 0f, Time.deltaTime * 10);

        if(Pivot)
        {
            if (Target != null)
            {
                Vector3 difference = Target.LookTarget() + AimOffset - WeaponOffsetOrigin.position;
                float angle = Mathf.Asin(difference.y / difference.magnitude) * Mathf.Rad2Deg;
                Pivot.Rotation = angle;
            }
            else
                Pivot.Rotation = 0;
        }
        
        if (target != null)
            IK.LookAt = target.LookTarget();

        Thoughts.OnUpdate(this);
        
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
        {
            if (Sight.TrySetTarget(damagable))
            {
                SetState(InvestigateState);
                InvestigateState.SetTarget(damagable.AimTarget());
            }
        }

        if (!IsDead)
            return;

        if (wasDead)
            return;

        Agent.enabled = false;
        Animator.SetTrigger("Death");
        
        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        IK.LookAtWeight = IK.LeftGripWeight = IK.RightGripWeight = 0;

        DestroyAllModels();
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
