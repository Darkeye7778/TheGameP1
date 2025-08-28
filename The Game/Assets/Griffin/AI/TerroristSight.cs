using System;
using UnityEngine;

public class TerroristSight : AISight
{
    public LayerMask EnvironmentMask; // kept for Inspector compatibility

    public Transform Eye;

    public float SightDistance;
    public float SightAngle;
    public float MemoryTime;

    [Tooltip("X = Distance, Y = Time to spot")]
    public AnimationCurve SpottingTime;

    [Tooltip("X = Angle, Y = Speed")]
    public AnimationCurve AimSpeed;

    public IDamagable Target;

    private float _spottingTime;
    private float _memory;
    private float _forceMemory;

    private EnemyAI _controller;

    public override bool CanSee() { return _memory == 0 && _spottingTime <= 0; }
    public override IDamagable GetTarget() { return Target; }

    private void Start()
    {
        _controller = GetComponent<EnemyAI>();
    }

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

    public override bool CheckTargetFOV(IDamagable target = null)
    {
        target ??= Target;
        if (target == null)
            return false;

        foreach (var aimTarget in target.AimTargets())
            if (CheckTargetFOV(aimTarget))
                return true;
        return false;
    }

    public override bool CheckTargetFOV(Vector3 position)
    {
        Vector3 direction = position - Eye.position;
        float distance = direction.magnitude;
        float angle = Vector3.Angle(transform.forward, direction / distance);

        return distance <= SightDistance && angle <= SightAngle;
    }

    public override bool CheckTargetRay(Ray ray, IDamagable target = null)
    {
        target ??= Target;
        if (target == null)
            return false;

        // Use what BLOCKS vision: enemies and walls (and players if desired),
        // NOT who is a valid target.
        if (!Physics.Raycast(ray, out var hit, SightDistance, _controller.VisionBlockMask))
            return false;

        return hit.collider.TryGetComponent(out IDamagable damagable) &&
               damagable.Base() == target.Base();
    }

    public override IDamagable FindTarget(EnemyAI controller)
    {
        if (Input.GetKeyDown(KeyCode.Plus))
        {
            Target = null;
            _forceMemory = 0;
        }

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

                Quaternion targetRotation = Quaternion.LookRotation(Target.AimTarget() - Eye.position, Vector3.up);
                float speed = AimSpeed.Evaluate(Quaternion.Angle(targetRotation, Eye.rotation));
                Eye.rotation = Quaternion.Slerp(Eye.rotation, targetRotation, Time.deltaTime * speed);
            }
            else
            {
                _memory += Time.deltaTime;
                _spottingTime = 1;
            }

            if (Target.IsDead() || _forceMemory <= 0 && _memory > MemoryTime)
                Target = null;
        }
        else
            _spottingTime = 1;

        if (Target != null)
            return Target;

        if (_controller == null)
            return null;

        // Use who is a VALID TARGET (e.g., Player), not allies
        float nearestDistance = float.MaxValue;
        Collider[] colliders = Physics.OverlapSphere(Eye.position, SightDistance, _controller.TargetMask);
        foreach (Collider collider in colliders)
        {
            bool isDamagable = collider.TryGetComponent(out IDamagable damagable);
            if (damagable != null)
            {
                damagable = damagable.Base();
                if (!isDamagable || damagable.IsDead() || !CheckTargetVisibility(damagable, out float dist) || !(dist < nearestDistance))
                    continue;

                Target = damagable;
                nearestDistance = dist;
            }
            else
            {
                break;
            }
        }

        return Target;
    }

    private bool CheckTargetVisibility(IDamagable target, out float distance)
    {
        distance = float.MaxValue;
        if (target.AimTargets() == null)
            return false;

        foreach (Vector3 aimTarget in target.AimTargets())
            if (CheckTargetFOV(aimTarget) && CheckTargetRaycast(target, aimTarget))
            {
                distance = Mathf.Min(distance, Vector3.Distance(aimTarget, Eye.position));
                return true;
            }

        return false;
    }

    private bool CheckTargetRaycast(IDamagable subCollider, Vector3 position)
    {
        Vector3 direction = position - Eye.position;

        if (!Physics.Raycast(Eye.position, direction, out var hit, SightDistance, _controller.VisionBlockMask))
            return false;

        return hit.collider.TryGetComponent(out IDamagable damagable) &&
               damagable.Base() == subCollider.Base();
    }

    private bool CheckTargetVisibility(IDamagable target)
    {
        return CheckTargetVisibility(target, out _);
    }
}
