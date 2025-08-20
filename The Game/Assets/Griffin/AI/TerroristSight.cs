using System;
using UnityEngine;

public class TerroristSight : AISight
{
    public LayerMask EnemyMask;
    public LayerMask EnvironmentMask;

    public Transform Eye;
    
    public float SightDistance;
    public float SightAngle;
    public float MemoryTime;
    
    [Tooltip("X = Distance, Y = Time to spot")]
    public AnimationCurve SpottingTime;
    
    public IDamagable Target;

    private float _spottingTime;
    private float _memory;
    private float _forceMemory;

    public override bool CanSee() { return _memory == 0 && _spottingTime <= 0; }
    public override IDamagable GetTarget() { return Target; }
    
    public override bool TrySetTarget(IDamagable target)
    {
        bool canSee = false;
        foreach (Vector3 aimTarget in target.AimTargets())
            canSee |= CheckTargetRaycast(target, aimTarget);

        if (!canSee)
            return false;
        
        _forceMemory = 10;
        
        return true;
    }

    public override bool CheckRay(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, SightDistance, EnemyMask | EnvironmentMask))
            return false;
        return hit.collider.TryGetComponent(out IDamagable damagable) && damagable.Base() == Target.Base();
    }

    public override IDamagable FindTarget(EnemyAI controller)
    {
        if (_forceMemory > 0)
            _forceMemory -= Time.deltaTime;

        if (Target != null)
        {
            var canSee = CheckTargetVisibility(Target);
            if (canSee)
            {
                float distance = Vector3.Distance(Eye.position, Target.AimTarget());
                _spottingTime -= Time.deltaTime / SpottingTime.Evaluate(distance);
                _memory = 0;
                Eye.rotation = Quaternion.LookRotation(Target.AimTargets()[0] - Eye.position, Vector3.up);
            }
            else
            {
                _memory += Time.deltaTime;
                _spottingTime = 1;
            }

            foreach (var target in Target.AimTargets())
                Debug.DrawRay(Eye.position, target - Eye.position, canSee ? Color.green : Color.red);

            if (Target.IsDead() || _forceMemory <= 0 && _memory > MemoryTime)
                Target = null;
        }
        else
            _spottingTime = 1;

        if (Target != null) 
            return Target;
        
        Collider[] colliders = Physics.OverlapSphere(Eye.position, SightDistance, EnemyMask);

        float nearestDistance = float.MaxValue;
            
        foreach (Collider collider in colliders)
        {
            bool isDamagable = collider.TryGetComponent(out IDamagable damagable);
            damagable = damagable.Base();
            if (!isDamagable || damagable.IsDead() ||
                !CheckTargetVisibility(damagable, out float dist) ||
                !(dist < nearestDistance))
                    continue;
            
            Target = damagable;
            nearestDistance = dist;
        }

        return Target;
    }

    private bool CheckTargetVisibility(IDamagable target, out float distance)
    {
        distance = float.MaxValue;
        if (target.AimTargets() == null)
            return false;
        foreach (Vector3 aimTarget in target.AimTargets())
        {
            Vector3 direction = aimTarget - Eye.position; 
            distance = direction.magnitude;
            float angle = Vector3.Angle(transform.forward, direction / distance);

            if (distance > SightDistance || angle > SightAngle)
                continue;

            if (CheckTargetRaycast(target, aimTarget))
                return true;
        }
        return false;
    }

    private bool CheckTargetRaycast(IDamagable subCollider, Vector3 position)
    {
        Vector3 direction = position - Eye.position;
        
        if (!Physics.Raycast(Eye.position, direction, out var hit, SightDistance, EnemyMask | EnvironmentMask))
            return false;
        return hit.collider.TryGetComponent(out IDamagable damagable) && damagable.Base() == subCollider.Base();
    }
    
    private bool CheckTargetVisibility(IDamagable target)
    {
        return CheckTargetVisibility(target, out _);
    }
}
