//#define ROOM_KEEP_PARENT

using System;
using UnityEngine;
using UnityEngine.LightTransport;
using static UnityEngine.UI.Image;
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
    [SerializeField] public GameObject floor;

    [SerializeField] private Transform entranceAnchor;
    [SerializeField] private bool useAnchorAsOrigin = true;

    float G => MapGenerator.GRID_SIZE;
    Vector3 GridToLocal(Vector2 grid) => new Vector3(grid.x * G, 0f, grid.y * G);

    private void Reset()
    {
        if (entranceAnchor == null) entranceAnchor = transform.Find("DoorAnchor");
    }

    private void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        gameObject.layer = MapGenerator.Instance.GridLayer;
        
        collider.size = Properties.CollisionBox;
        collider.center = Properties.CollisionOffset;

        if (entranceAnchor == null) entranceAnchor = transform.Find("DoorAnchor");
    }
    
    public void GenerateLeafs()
    {
        foreach (Connection connection in Properties.GetResolvedConnectionPoints())
        {
            if (!connection.Required &&
                Random.Range(0f, 1f) > connection.Odds &&
                MapGenerator.Instance.Parameters.RemainingRooms <= 0)
                continue;

            Vector3 connPos = GetConnWorldPos(connection);
            Quaternion connRot = GetConnWorldRot(connection);

            for (uint i = 0; i < MapGenerator.Instance.MaxLeafRetry; i++)
            {
                var cell = MapGenerator.Instance.PickRandomCell();
                var prefab = cell.Prefab;

                GameObject leaf = Instantiate(prefab);
                RoomProfile child = leaf.GetComponent<RoomProfile>();

                Transform childAnchor =
                    (child && child.entranceAnchor) ? child.entranceAnchor : leaf.transform;

                leaf.transform.rotation = connRot * /*Quaternion.Euler(0, 180, 0) */ Quaternion.Inverse(childAnchor.localRotation);

                leaf.transform.position = connPos - leaf.transform.rotation * childAnchor.localPosition;

                if (child == null)
                {
                    Debug.LogError($"[MapGen] Prefab '{prefab.name}' is missing RoomProfile.");
                    DestroyImmediate(leaf);
                    continue;
                }

                child.Parent = this;
                IsLeaf = false;

                if (child.TryFit()) // If it fits, let it be. Otherwise, try again.
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
        
        foreach (Connection connection in Properties.GetResolvedConnectionPoints())
            GenerateExit(connection);
    }

    private void GenerateExit(Connection connection)
    {
        GameObject exit = Instantiate(MapGenerator.Instance.ExitPrefab, transform);
        exit.transform.SetPositionAndRotation(
            GetConnWorldPos(connection),
            GetConnWorldRot(connection)
        );

        exit.GetComponent<ConnectionProfile>().Connection = connection;
    }

    private bool CheckCollision()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        collider.enabled = false;
        bool hit = Physics.CheckBox(
        transform.position + transform.rotation * Properties.CollisionOffset,
        Properties.CollisionBox * 0.5f,
        transform.rotation,
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


    Vector3 AnchorLocalToRootLocal(Vector3 anchorLocal)
    {
        if (!useAnchorAsOrigin || !entranceAnchor) return anchorLocal;
        return entranceAnchor.localPosition + entranceAnchor.localRotation * anchorLocal;
    }

    Vector3 GetConnWorldPos(Connection c)
    {
        float G = MapGenerator.GRID_SIZE;               // 5
        Vector2 p = c.Transform.Position;                 // Vector2 (gridX, gridZ), origin at entry
        Vector3 origin = (entranceAnchor != null) ? entranceAnchor.position : transform.position;      // world position of entry
        var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        right = -right;
        forward = -forward;

        Vector3 world = origin
              + right * (p.x * G)             // left/right along room X
              + forward * (p.y * G);             // “into the room” along room Z
        float y = (entranceAnchor != null) ? entranceAnchor.position.y : 0;
        world.y = y;          // clamp height to door height (or 0)
        return world;
    }

    Quaternion GetConnWorldRot(Connection c)
    {
        return transform.rotation * Direction.ToQuaternion(c.Transform.Rotation);
    }
}
