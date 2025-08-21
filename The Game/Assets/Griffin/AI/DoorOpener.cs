using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;


public class OpenDoorThought : AIThought<EnemyAI>
{
    public Doors Door;
    
    public override void Think(EnemyAI t)
    {
        Door.OnInteract(t.gameObject);
    }

    public override bool Equals(object obj)
    {
        if (obj is not OpenDoorThought other)
            return false;

        return Door == other.Door;
    }
}
public class DoorOpener : MonoBehaviour
{
    public float OpeningRadius = 1;
    public LayerMask DoorMask;
    

    private EnemyAI _controller;
    
    void Start()
    {
        _controller = GetComponent<EnemyAI>(); 
    }
    
    void Update()
    {
        // Center of enemy collider
        Vector3 center = transform.position + _controller.Agent.height * 0.5f * Vector3.up;
        if (!Physics.Raycast(center, transform.forward, out RaycastHit hit, OpeningRadius, DoorMask))
            return;

        if (!hit.collider.TryGetComponent(out Doors door) || door.IsOpen)
            return;

        _controller.Thoughts.Push(new OpenDoorThought
        {
            MinTime = 0.4f,
            MaxTime = 0.5f,
            Door = door
        });
    }
}
