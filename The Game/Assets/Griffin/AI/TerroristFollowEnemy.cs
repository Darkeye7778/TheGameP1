using System;
using UnityEngine;

[Serializable]
public class InventoryThought : AIThought<EnemyAI>
{
    public Inventory.InputState Flags;
    
    public override void Think(EnemyAI controller)
    {
        if (Flags.HasFlag(Inventory.InputState.UsePrimary) || Flags.HasFlag(Inventory.InputState.UseSecondary))
            controller.InputFlags = 0;
        controller.InputFlags |= Flags;
    }

    public override bool Equals(object obj)
    {
        if (obj is not InventoryThought other)
            return false;

        return Flags == other.Flags;
    }
}

public class TerroristFollowEnemy : AIState
{
    [field: SerializeField] public AIState RetreatState { get; private set; }
    
    [field: SerializeField] public Transform Eye { get; private set; }
    
    public float FollowingDistance = 5;
    public float MinFollowingDistance = 2;
    public float FaceSpeed = 5;

    public float MinThinkTime, MaxThinkTime;

    private bool _shouldShoot;

    [Serializable]
    private class ShootThought : AIThought<EnemyAI>
    {
        public bool Shoot;

        public override void Think(EnemyAI t)
        {
            if (t.CurrentState is not TerroristFollowEnemy state)
                return;

            state._shouldShoot = Shoot;
        }

        public override bool Equals(object obj)
        {
            if (obj is not ShootThought other)
                return false;

            return Shoot == other.Shoot;
        }
    }

    public override bool OverriddenByEnemy()
    {
        return false;
    }

    public override void OnStart(EnemyAI controller)
    {
        base.OnStart(controller);

        Controller.Thoughts.Clear();
    }

    public override void OnUpdate()
    {
        Controller.Agent.stoppingDistance = Controller.Sight.CanSee() ? FollowingDistance : MinFollowingDistance;
        if(Controller.Target == null)
        {
            Controller.SetState(Controller.WanderState);
            return;
        }

        if (Controller.Sight.CanSee())
        {
            Vector3 offset = Controller.Target.AimTarget() - Eye.position;
            Eye.transform.rotation = Quaternion.LookRotation(offset, Vector3.up);

            Controller.Agent.SetDestination(Controller.Target.GameObject().transform.position);
            
            // Only rotate on z axis.
            offset.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(offset, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FaceSpeed);

            if(!Controller.CurrentWeapon.IsEmpty)
                Shoot();
        }
        else if(!Controller.IsUsingPrimary)
            Controller.SetState(RetreatState);
        
        if (Controller.IsUsingPrimary && Controller.Primary.IsEmpty)
        {
            InventoryThought thought = new InventoryThought
            {
                MinTime = MinThinkTime,
                MaxTime = MaxThinkTime,
                Flags = Inventory.InputState.UseSecondary
            };

            Controller.Thoughts.Push(thought);
        }

        if (!Controller.IsUsingPrimary && Controller.Secondary.IsEmpty)
        {
            InventoryThought thought = new InventoryThought
            {
                MinTime = MinThinkTime,
                MaxTime = MaxThinkTime,
                Flags = Inventory.InputState.Reload
            };

            Controller.Thoughts.Push(thought);
        }
        
        if(Controller.Primary.IsEmpty && Controller.Secondary.IsEmpty && !Controller.Secondary.HasReserve)
            Controller.SetState(RetreatState);
    }

    private void Shoot()
    {
        bool shoot = Controller.Sight.CheckRay(Controller.Eye.position, Controller.Eye.forward);
        Controller.Thoughts.Push(new ShootThought{MinTime = MinThinkTime, MaxTime = MaxThinkTime, Shoot = shoot});
        
        if (!_shouldShoot)
            return;
        
        Controller.InputFlags |= Inventory.InputState.Firing;

        if (Controller.CurrentWeapon.Mode == FireMode.Single)
        {
            InventoryThought thought = new InventoryThought
            {
                MinTime = MinThinkTime,
                MaxTime = MaxThinkTime,
                Flags = Inventory.InputState.FiringFirst
            };

            Controller.Thoughts.Push(thought);
        }
    }
}
