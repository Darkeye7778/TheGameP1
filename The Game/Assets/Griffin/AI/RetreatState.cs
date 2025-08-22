using Unity.Mathematics;
using UnityEngine;

public class RetreatState : AIState
{
    public override void OnStart(EnemyAI controller, AIState previousState)
    {
        base.OnStart(controller, previousState);
    }

    public override bool OverriddenByEnemy()
    {
        return false;
    }

    public override void OnUpdate()
    {
        Controller.Agent.stoppingDistance = 0;
        
        if(Controller.Target == null)
        {
            Controller.SetState(Controller.WanderState);
            return;
        }
        
        Vector3 target = Controller.Agent.transform.position - Controller.Target.GameObject().transform.position;
        target.y = 0;
        target.Normalize();
        
        Controller.Agent.SetDestination(Controller.Agent.transform.position + target);
        transform.rotation = quaternion.LookRotation(-target, Vector3.up);

        if (!Controller.Primary.HasAnyAmmo && !Controller.Secondary.HasAnyAmmo)
            return;
        
        if (!Controller.IsUsingPrimary && !Controller.Secondary.IsLoaded && Controller.Secondary.CanReload)
            Controller.InputFlags |= Inventory.InputState.Reload;
        else if (!Controller.IsUsingPrimary)
            Controller.InputFlags |= Inventory.InputState.UsePrimary;
        else if (Controller.IsUsingPrimary && !Controller.Primary.IsLoaded)
            Controller.InputFlags |= Inventory.InputState.Reload;
        
        if(Controller.Primary.IsLoaded)
            Controller.SetState(Controller.EnemySpottedState);
    }
}