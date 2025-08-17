using UnityEngine;

public class TerroristFollowEnemy : AIState
{
    public float FollowingDistance = 5;
    public float FaceSpeed = 5;
    
    public override void OnStart(EnemyAI controller)
    {
        base.OnStart(controller);
    }

    public override void OnUpdate()
    {
        Controller.Agent.stoppingDistance = Controller.Sight.CanSee() ? FollowingDistance : 0;
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

            Controller.InputFlags |= Inventory.InputState.Firing | Inventory.InputState.FiringFirst;
        }
        
        if (Controller.IsUsingPrimary && Controller.Primary.IsEmpty)
            Controller.InputFlags |= Inventory.InputState.UseSecondary;
        
        if (!Controller.IsUsingPrimary && Controller.Secondary.IsEmpty)
            Controller.InputFlags |= Inventory.InputState.Reload;
    }
}
