using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;

public class OpenDoorThought : AIThought<DoorOpener>
{
    public Doors Door;
    
    public override void Think(DoorOpener t)
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
    public Transform Eye;
    
    public LayerMask DoorMask;

    private AIThoughtQueue<DoorOpener> _thoughts = new AIThoughtQueue<DoorOpener>();
    
    void Update()
    {
        _thoughts.OnUpdate(this);
        
        if (!Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, OpeningRadius, DoorMask))
            return;
        
        if (!hit.transform.TryGetComponent(out Doors door))
            return;
        
        if (door.IsOpen)
            return;

        _thoughts.Push(new OpenDoorThought
        {
            MinTime = 0.4f,
            MaxTime = 0.5f,
            Door = door
        });
    }
}
