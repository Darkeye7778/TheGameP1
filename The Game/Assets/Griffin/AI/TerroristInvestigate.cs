using UnityEngine;

public class TerroristInvestigate : AIInvestigateState
{
    public float StoppingDistance;
    public Vector3 Target { get; private set; }

    public override void SetTarget(Vector3 position)
    {
        Target = position;
    }

    public override void OnStart(EnemyAI controller)
    {
        base.OnStart(controller);

        controller.Agent.SetDestination(Target);

        Controller.Agent.stoppingDistance = StoppingDistance;

        Controller.IK.LookAt = Target;
    }

    public override void OnUpdate()
    {
        Controller.IK.LookAtWeight = Mathf.Lerp(Controller.IK.LookAtWeight, Controller.Sight.CheckTargetFOV(Target) ? 1f : 0f, Time.deltaTime * 10);
        
        if(Controller.Agent.isStopped || Controller.Agent.remainingDistance < StoppingDistance)
            Controller.SetState(Controller.WanderState);
    }
}
