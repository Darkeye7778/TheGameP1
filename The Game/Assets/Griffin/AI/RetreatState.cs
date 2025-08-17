using Unity.Mathematics;
using UnityEngine;

public class RetreatState : AIState
{
    public override void OnStart(EnemyAI controller)
    {
        base.OnStart(controller);
        
    }

    public override bool OverriddenByEnemy()
    {
        return false;
    }

    public override void OnUpdate()
    {
        Controller.Agent.stoppingDistance = 0;
        
        Vector3 target = Controller.Agent.transform.position - Controller.Target.GameObject().transform.position;
        target.z = 0;
        target.Normalize();
        
        Controller.Agent.SetDestination(Controller.Agent.transform.position + target);
        transform.rotation = quaternion.LookRotation(-target, Vector3.up);

        if (!Controller.Primary.HasReserve && !Controller.Secondary.HasReserve)
            return;
        
        if(Controller.Target == null)
        {
            Controller.SetState(Controller.WanderState);
            return;
        }
        
        if (!Controller.IsUsingPrimary && !Controller.Secondary.IsFull)
            Controller.InputFlags |= Inventory.InputState.Reload;
        else if (!Controller.IsUsingPrimary)
            Controller.InputFlags |= Inventory.InputState.UsePrimary;
        else if (Controller.IsUsingPrimary && !Controller.Primary.IsFull)
            Controller.InputFlags |= Inventory.InputState.Reload;
        else if(!Controller.Primary.CanReload && !Controller.Secondary.CanReload)
            Controller.SetState(Controller.EnemySpottedState);
    }
}
