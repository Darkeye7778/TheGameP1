//#define ROOM_KEEP_PARENT

using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoomProfile : MonoBehaviour
{ 
    [field: SerializeField]
    public RoomProperties Properties { get; private set; }
    [Header("Design")]
    public RoomCategory AllowedCategories = RoomCategory.None;

    [HideInInspector] public RoomCategory Category = RoomCategory.None;

    [NonSerialized] public RoomProfile Parent;
    [NonSerialized] public bool HasRoomLeaf = false;
    [NonSerialized] public bool IsLeaf = true;
    [NonSerialized] public bool IsEntry = false;
    [NonSerialized] public bool IsInEntryZone = false;

    private void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        gameObject.layer = MapGenerator.Instance.GridLayer;
        
        collider.size = Properties.CollisionBox;
        collider.center = Properties.CollisionOffset;
    }
    
    public void GenerateLeafs()
    {
        foreach (Connection connection in Properties.ConnectionPoints)
        {
            if (!connection.Required && Random.Range(0f, 1f) > connection.Odds && MapGenerator.Instance.Parameters.RemainingRooms <= 0) 
                continue;
            
            Vector3 position = transform.position + transform.rotation * connection.Transform.WorldPosition;
            Quaternion rotation = transform.rotation * Direction.ToQuaternion(connection.Transform.Rotation);

            for (uint i = 0; i < MapGenerator.Instance.MaxLeafRetry; i++)
            {
                GameObject leaf = Instantiate(MapGenerator.Instance.PickRandomCell().Prefab, position, rotation);
                RoomProfile roomProfile = leaf.GetComponent<RoomProfile>();
                    
                roomProfile.Parent = this;
                IsLeaf = false;
                
                if (roomProfile.TryFit()) // If it fits, let it be. Otherwise, try again.
                    break;
            }
        }
    }

    public void GenerateConnections()
    {
        if(!IsEntry)
            GenerateExit(new Connection
            {
                Transform = new GridTransform(Vector2Int.zero, ExitDirection.South),
                Required = true,
                IsEntrance = true,
                HasDoor = Properties.HasEntranceDoor
            });
        
        foreach (Connection connection in Properties.ConnectionPoints)
            GenerateExit(connection);
    }

    private void GenerateExit(Connection connection)
    {
        GameObject exit = Instantiate(MapGenerator.Instance.ExitPrefab, transform);
        exit.transform.localRotation = Direction.ToQuaternion(connection.Transform.Rotation);
        exit.transform.localPosition = connection.Transform.WorldPosition;
 
        exit.GetComponent<ConnectionProfile>().Connection = connection;
    }

    private bool CheckCollision()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        collider.enabled = false;
        bool hit = Physics.CheckBox(
                transform.position + transform.rotation * Properties.CollisionOffset, 
                Properties.CollisionBox * 0.45f, 
                Quaternion.identity,
                MapGenerator.Instance.GridMask
            );
        collider.enabled = true;
        
        return hit;
    }

    private bool TryFit()
    {
        bool didFit = !CheckCollision();
        
        if(!didFit)
        {
            GetComponent<Collider>().enabled = false;
            DestroyImmediate(gameObject);
        }
        else
            Initialize();

        return didFit;
    }

    public void Initialize()
    {
        MapGenerator.Instance.Parameters.IterationRooms.Add(this);
        MapGenerator.Instance.Parameters.Rooms.Add(this);
            
        if(Properties.Type != RoomType.Hallway)
            MapGenerator.Instance.Parameters.RemainingRooms--;
    }
}
