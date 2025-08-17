using UnityEngine;

public class TerroristFollowEnemy : AIState
{
    [field: SerializeField] public AIState RetreatState { get; private set; }
    
    public float FollowingDistance = 5;
    public float MinFollowingDistance = 2;
    public float FaceSpeed = 5;

    public float MinThinkTime, MaxThinkTime;

    private AIThoughtQueue<TerroristFollowEnemy> _thoughts = new();

    private Inventory.InputState _queueState;
    
    public class InventoryThought : AIThought<TerroristFollowEnemy>
    {
        public Inventory.InputState Flags;
    
        public override void Think(TerroristFollowEnemy t)
        {
            t.Controller.InputFlags |= Flags;
            t._queueState &= ~Flags;
        }
    }

    public override bool OverriddenByEnemy()
    {
        return false;
    }

    public override void OnStart(EnemyAI controller)
    {
        base.OnStart(controller);
        
        _thoughts.Clear();
        _queueState = 0;
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
            Vector3 offset = Controller.Target.AimTargets()[0] - transform.position;
            offset.y = 0;

            Controller.Agent.SetDestination(Controller.Target.GameObject().transform.position);
            
            Quaternion targetRotation = Quaternion.LookRotation(offset, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FaceSpeed);

            if(!Controller.CurrentWeapon.IsEmpty)
                Controller.InputFlags |= Inventory.InputState.Firing | Inventory.InputState.FiringFirst;
        }
        
        if (Controller.IsUsingPrimary && Controller.Primary.IsEmpty && !_queueState.HasFlag(Inventory.InputState.UseSecondary))
        {
            InventoryThought thought = new InventoryThought
            {
                MinTime = MinThinkTime,
                MaxTime = MaxThinkTime,
                Flags = Inventory.InputState.UseSecondary
            };

            _queueState |= Inventory.InputState.UseSecondary;

            _thoughts.Push(thought);
        }

        if (!Controller.IsUsingPrimary && Controller.Secondary.IsEmpty && !_queueState.HasFlag(Inventory.InputState.Reload))
        {
            InventoryThought thought = new InventoryThought
            {
                MinTime = MinThinkTime,
                MaxTime = MaxThinkTime,
                Flags = Inventory.InputState.Reload
            };
            
            _queueState |= Inventory.InputState.Reload;

            _thoughts.Push(thought);
        }
        
        if(Controller.Primary.IsEmpty && Controller.Secondary.IsEmpty && !Controller.Secondary.HasReserve)
            Controller.SetState(RetreatState);

        _thoughts.OnUpdate(this);
    }
}
