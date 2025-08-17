using UnityEngine;
using UnityEngine.AI;

public class TerroristWander : AIState
{
    [Range(0, 1)] public float RoamChance;
    [Range(0, 1)] public float FavoriteWaypointChance;
    public float MinRoamDistance, MaxRoamDistance;
    public float StoppingDistance = 2f;

    public float MinWaitTime, MaxWaitTime;
    
    public Vector3[] Waypoints;
    public uint FavoriteWaypoint;

    private bool _startTimer;
    private float _resetTimer;
    
    public override void OnStart(EnemyAI controller)
    {
        _resetTimer = 0;
        _startTimer = false;
        
        base.OnStart(controller);
        
        Controller.Agent.stoppingDistance = StoppingDistance;
        
        Vector3 target;
        if (Random.Range(0f, 1f) < RoamChance)
        {
            float randomDistance = Random.Range(MinRoamDistance, MaxRoamDistance);
            float randomAngle = Random.Range(0, 2 * Mathf.PI);
            target = transform.position + new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle)) * randomDistance;
        }
        else
            target = Random.Range(0f, 1f) < FavoriteWaypointChance ? Waypoints[FavoriteWaypoint] : Utils.PickRandom(Waypoints);
        
        NavMeshHit hit;
        NavMesh.SamplePosition(target, out hit, MaxRoamDistance, 1);
        controller.Agent.SetDestination(hit.position);
    }

    public override void OnUpdate()
    {
        if (_startTimer)
        {
            _resetTimer -= Time.deltaTime;
            
            if(_resetTimer <= 0)
                Controller.SetState(this);

            return;
        }

        _startTimer = Controller.Agent.isStopped || Controller.Agent.remainingDistance < StoppingDistance;
        if (_startTimer)
            _resetTimer = Random.Range(MinWaitTime, MaxWaitTime);

        if (!Controller.IsUsingPrimary && !Controller.Secondary.IsFull)
            Controller.InputFlags |= Inventory.InputState.Reload;
        else if (!Controller.IsUsingPrimary)
            Controller.InputFlags |= Inventory.InputState.UsePrimary;
        else if (Controller.IsUsingPrimary && !Controller.Primary.IsFull)
            Controller.InputFlags |= Inventory.InputState.Reload;
    }
}
