using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AI;
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
        if (!_queue.Exists(thought.Equals))
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
    public virtual void OnStart(EnemyAI controller, AIState previousState)
    {
        Controller = controller;
    }
    public abstract void OnUpdate();
    public virtual bool OverriddenByEnemy() { return true; }
    public virtual bool OverriddenByInvestigate() { return OverriddenByEnemy(); }
    public virtual void OnExit(AIState nextState) { }
    public virtual void OnDeath() { }
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

    public abstract bool CheckTargetFOV(IDamagable target = null);
    public abstract bool CheckTargetFOV(Vector3 position);
    public abstract bool CheckTargetRay(Ray ray, IDamagable target = null);

    public bool CheckTargetRay(Vector3 position, Vector3 direction, IDamagable target = null)
    {
        return CheckTargetRay(new Ray(position, direction), target);
    }
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

    [Header("Audio")]
    [SerializeField] private AudioSource _footstepAudioSource;
    [field: SerializeField] public Sound DeathSound { get; private set; }

    [Header("Hitbox")]
    public Transform[] HitPoints;
    private Vector3[] _hitPoints;

    [Header("Misc")]
    public float FootstepOffset = 1.25f;
    public LayerMask GroundMask;

    [Header("Masks")]
    [Tooltip("Who this AI considers valid targets (e.g., Player).")]
    [field: SerializeField] public LayerMask TargetMask { get; private set; }

    [Tooltip("What physically stops bullets (e.g., Environment | Enemy | Player).")]
    [field: SerializeField] public LayerMask BulletStopMask { get; private set; }

    [Tooltip("What blocks vision/line-of-sight (e.g., Environment | Enemy | Player).")]
    [field: SerializeField] public LayerMask VisionBlockMask { get; private set; }

    private SoundListener _listener;

    private GroundState _ground;
    private bool _moving => _ground.NearGround && _velocity.sqrMagnitude > 0.01;
    private float _standingTimer, _footstepOffset;
    private Vector3 _previousPosition, _velocity;

    public AIState CurrentState { get; private set; }
    [CanBeNull] private AIState _newState;

    public InputState InputFlags
    {
        get => base.InputFlags;
        set => base.InputFlags = value;
    }

    public void SetState(AIState state)
    {
        _newState = state;
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

    public NavMeshAgent Agent { get; private set; }

    new void Start()
    {
        base.Start();
        Agent = GetComponent<NavMeshAgent>();
        Thoughts = new AIThoughtQueue<EnemyAI>();
        _listener = GetComponent<SoundListener>();

        SetState(WanderState);
    }

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
        GetFootsteps();

        if (_newState != null)
        {
            AIState oldState = CurrentState;

            CurrentState = _newState;
            if (oldState != null)
                oldState.OnExit(CurrentState);
            CurrentState.OnStart(this, oldState);
            _newState = null;
        }
        CurrentState.OnUpdate();

        IDamagable target = Sight.FindTarget(this);
        if (Target != null && Sight.CanSee() && (!CurrentState || CurrentState.OverriddenByEnemy()))
            SetState(EnemySpottedState);

        if (_listener && _listener.SoundChanged && CurrentState.OverriddenByInvestigate())
        {
            SetState(InvestigateState);
            InvestigateState.SetTarget(_listener.CurrentSoundInstance.Position);
        }

#if UNITY_EDITOR
        _targetGameObject = target?.GameObject();
#endif

        if (Pivot)
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
        IK.LookAtWeight = Mathf.Lerp(IK.LookAtWeight, target != null ? 1f : 0f, Time.deltaTime * 10);

        Thoughts.OnUpdate(this);

        base.Update();
    }

    private void CalculateVelocity()
    {
        _velocity = (transform.position - _previousPosition) / Time.deltaTime;
        _velocity.x /= transform.lossyScale.x;
        _velocity.y /= transform.lossyScale.y;
        _velocity.z /= transform.lossyScale.z;
        _previousPosition = transform.position;

        _velocity = transform.InverseTransformDirection(_velocity);

        Animator.SetFloat("Speed", Mathf.Max(_velocity.magnitude, 1));
        Animator.SetFloat("Velocity X", _velocity.x);
        Animator.SetFloat("Velocity Y", _velocity.z);
    }

    private void GetFootsteps()
    {
        _ground = GroundState.GetGround(transform.position, 0.1f, GroundMask);

        _standingTimer += Time.deltaTime;
        if (_moving)
            _standingTimer = 0.0f;

        if (_standingTimer > 0.1)
            _footstepOffset = 0.0f;

        _footstepOffset += _velocity.magnitude * Time.deltaTime;

        if (_moving && _footstepOffset >= FootstepOffset)
        {
            _footstepAudioSource.clip = _ground.SoundSettings.Footstep.PickSound();
            _footstepAudioSource.volume = _ground.SoundSettings.Footstep.Volume;
            _footstepAudioSource.Play();
            _footstepOffset %= FootstepOffset;
        }
    }

    public void OnTakeDamage(DamageSource source, float damage)
    {
        bool wasDead = IsDead;
        Debug.Log($"{source.Name}, {damage}");
        Health -= damage;

        if (Target == null && source.Object.TryGetComponent(out IDamagable damagable))
        {
            if (Sight.TrySetTarget(damagable) && CurrentState.OverriddenByInvestigate())
            {
                SetState(InvestigateState);
                InvestigateState.SetTarget(damagable.AimTarget());
            }
        }

        if (!IsDead || wasDead)
            return;

        CurrentState.OnDeath();

        Agent.enabled = false;
        Animator.SetTrigger("Death");

        AudioSource.PlayOneShot(DeathSound.PickSound());

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
