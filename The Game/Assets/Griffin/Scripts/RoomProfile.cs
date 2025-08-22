//#define ROOM_KEEP_PARENT

using System;
using System.Collections.Generic;
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
                if (child == null)
                {
                    Debug.LogError("[MapGen] Prefab '" + prefab.name + "' is missing RoomProfile.");
                    DestroyImmediate(leaf);
                    continue;
                }

                ExitDirection want = Opposite(connection.Transform.Rotation);
                Connection[] childCps = child.Properties.GetResolvedConnectionPoints();
                List<int> candidates = new List<int>();
                for (int k = 0; k < childCps.Length; k++)
                {
                    if (childCps[k].Transform.Rotation == want && childCps[k].HasDoor && childCps[k].Odds > 0f)
                        candidates.Add(k);
                }

                if (candidates.Count == 0)
                {
                    DestroyImmediate(leaf);
                    continue;
                }

                int childCpIndex = candidates[Random.Range(0, candidates.Count)];
                Connection childCP = childCps[childCpIndex];

                bool attached = MapGenerator.TryAttach(this, connection, child, childCP, 0.30f, 0.02f);
                if (!attached)
                {
                    DestroyImmediate(leaf);
                    continue;
                }

                if (!MapGenerator.Instance.RegisterPlacedRoom(child))
                {
                    DestroyImmediate(leaf);
                    continue;
                }

                child.Parent = this;
                IsLeaf = false;
                if (child.TryFit())
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

    public Vector3 GetConnWorldPos(Connection c)
    {
        if (c.Transform.Position == null) return transform.position;

        float G = MapGenerator.GRID_SIZE;
        Vector2 p = c.Transform.Position; // grid (x,z)

        Vector3 origin = (entranceAnchor != null) ? entranceAnchor.position : transform.position;

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        Vector3 world = origin + right * (p.x * G) + forward * (p.y * G);

        float y = (entranceAnchor != null) ? entranceAnchor.position.y : origin.y;
        world.y = y;

        return world;
    }

    Quaternion GetConnWorldRot(Connection c)
    {
        return transform.rotation * Direction.ToQuaternion(c.Transform.Rotation);
    }

    static ExitDirection Opposite(ExitDirection d)
    {
        switch (d)
        {
            case ExitDirection.ZPositive: return ExitDirection.ZNegative;
            case ExitDirection.ZNegative: return ExitDirection.ZPositive;
            case ExitDirection.XPositive: return ExitDirection.XNegative;
            case ExitDirection.XNegative: return ExitDirection.XPositive;
        }
        return d;
    }

}
