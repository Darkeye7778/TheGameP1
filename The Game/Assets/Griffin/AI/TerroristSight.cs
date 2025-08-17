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
    
    public IDamagable Target;
    
    private float _memory;
    private float _forceMemory;

    public override bool CanSee() { return _memory == 0; }
    public override IDamagable GetTarget() { return Target; }
    
    public override bool TrySetTarget(IDamagable target)
    {
        bool canSee = CheckTargetRaycast(target);
        if (canSee)
        {
            Target = target;
            _forceMemory = 10;
        }
        return canSee;
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
                _memory = 0;
                Eye.rotation = Quaternion.LookRotation(Target.AimTargets()[0] - Eye.position, Vector3.up);
            }
            else
                _memory += Time.deltaTime;

            foreach (var target in Target.AimTargets())
                Debug.DrawRay(Eye.position, target - Eye.position, canSee ? Color.green : Color.red);
            
            if (_forceMemory <= 0 && (_memory > MemoryTime))
                Target = null;
        }

        if (Target != null) 
            return Target;
        
        Collider[] colliders = Physics.OverlapSphere(Eye.position, SightDistance, EnemyMask);

        float nearestDistance = float.MaxValue;
            
        foreach (Collider collider in colliders)
        {
            if (!collider.TryGetComponent(out IDamagable damagable) ||
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
        foreach (Vector3 aimTarget in target.AimTargets())
        {
            Vector3 direction = aimTarget - Eye.position; 
            distance = direction.magnitude;
            float angle = Vector3.Angle(transform.forward, direction / distance);

            if (distance > SightDistance || angle > SightAngle)
                continue;

            if (CheckTargetRaycast(target))
                return true;
        }
        return false;
    }

    private bool CheckTargetRaycast(IDamagable target)
    {
        Vector3 direction = target.AimTarget() - Eye.position;
        
        if (!Physics.Raycast(Eye.position, direction, out var hit, SightDistance, EnemyMask | EnvironmentMask))
            return false;
        return hit.collider.TryGetComponent(out IDamagable damagable) && damagable.Base() == target.Base();
    }
    
    private bool CheckTargetVisibility(IDamagable target)
    {
        return CheckTargetVisibility(target, out _);
    }
}
