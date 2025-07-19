//#define ROOM_KEEP_PARENT

using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoomProfile : MonoBehaviour
{ 
    [field: SerializeField]
    public RoomProperties Properties { get; private set; }

    public bool AlreadyGenerated { get; private set; } = false;

    private void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        
        collider.size = Properties.CollisionBox;
        collider.center = Properties.CollisionOffset;
        
        if(CheckCollision())
            Destroy(gameObject);
        else
            MapGenerator.Instance._params.Rooms.Add(this);
    }

    public bool CheckCollision()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        collider.enabled = false;
        bool hit = Physics.CheckBox(transform.position + transform.rotation * Properties.CollisionOffset, Properties.CollisionBox * 0.45f);
        collider.enabled = true;
        
        return hit;
    }

    public bool GenerateLeafs(GenerationParams parameters)
    {
        AlreadyGenerated = true;
        
        if(Properties.Type != RoomType.Hallway)
            parameters.RemainingRooms--;

        foreach (Connection connection in Properties.ConnectionPoints)
        {
            if (parameters.RemainingRooms <= 0)
                break;

            if (!connection.Required && Random.Range(0f, 1f) > connection.Odds) 
                continue;
            
            Vector3 position = transform.position + transform.rotation * connection.Transform.WorldPosition;
            Quaternion rotation = transform.rotation * Direction.ToQuaternion(connection.Transform.Rotation);

        #if ROOM_KEEP_PARENT
            GameObject newCell = Instantiate(MapGenerator.Instance.PickRandomCell().Prefab, transform);
            newCell.transform.position = position;
            newCell.transform.rotation = rotation;
        #else
            GameObject newCell = Instantiate(MapGenerator.Instance.PickRandomCell().Prefab, position, rotation);
        #endif
        }
        
        return true;
    }

    private void Update()
    {
        bool collision = CheckCollision();
        Debug.DrawRay(transform.position, transform.forward * 1f, collision ? Color.red :  Color.green);
    }
}
