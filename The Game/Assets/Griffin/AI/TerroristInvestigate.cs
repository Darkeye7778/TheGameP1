using UnityEngine;

public class TerroristInvestigate : AIInvestigateState
{
    public float StoppingDistance;
    public Vector3 Target { get; private set; }
    [field: SerializeField] public Sound InvestigateSound { get; private set; }

    public override void SetTarget(Vector3 position)
    {
        Target = position;
    }

    public override bool OverriddenByInvestigate()
    {
        return false;
    }

    public override void OnStart(EnemyAI controller, AIState previousState)
    {
        base.OnStart(controller, previousState);

        Controller.Agent.SetDestination(Target);
        Controller.Agent.stoppingDistance = StoppingDistance;
        Controller.IK.LookAt = Target;
        
        if (previousState == controller.WanderState)
            Controller.AudioSource.PlayOneShot(InvestigateSound.PickSound());
    }

    public override void OnUpdate()
    {
        Controller.Agent.destination = Target;
        Controller.IK.LookAtWeight = Mathf.Lerp(Controller.IK.LookAtWeight, Controller.Sight.CheckTargetFOV(Target) ? 1f : 0f, Time.deltaTime * 10);
        
        if(!Controller.Agent.pathPending && Controller.Agent.remainingDistance < StoppingDistance) 
            Controller.SetState(Controller.WanderState);
    }
}
