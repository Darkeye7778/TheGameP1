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
        Controller.IK.LookAtWeight = 1;
    }

    public override void OnUpdate()
    {
        
    }
}
