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

    private Connection[] _resolvedConnections;   // per-instance copy
    private int _entranceIdx = -1;

    private Connection[] GetResolved()
    {
        return _resolvedConnections ??= (Properties != null
            ? Properties.GetResolvedConnectionPoints()
            : Array.Empty<Connection>());
    }

    public void MarkEntrance(Connection used)
    {
        var arr = GetResolved();
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i].Transform.Position == used.Transform.Position &&
                arr[i].Transform.Rotation == used.Transform.Rotation)
            {
                arr[i].IsEntrance = true;   // mark the *used* CP as entrance
                _resolvedConnections[i] = arr[i];
                _entranceIdx = i;
                return;
            }
        }
    }

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
        var points = Properties != null ? Properties.GetResolvedConnectionPoints() : Array.Empty<Connection>();
        if (points.Length == 0) return;

        foreach (var parentCP in points)
        {
            if (!parentCP.Required)
            {
                if (MapGenerator.Instance.Parameters.RemainingRooms <= 0) continue;
                if (UnityEngine.Random.value > parentCP.Odds) continue;
            }

            for (uint t = 0; t < MapGenerator.Instance.MaxLeafRetry; t++)
            {
                var cell = MapGenerator.Instance.PickRandomCell();
                var prefab = cell != null ? cell.Prefab : null;
                if (!prefab) break;

                var go = Instantiate(prefab);
                var child = go.GetComponent<RoomProfile>();
                if (!child) { Destroy(go); continue; }

                var childCPs = child.Properties != null ? child.Properties.GetResolvedConnectionPoints() : Array.Empty<Connection>();
                Connection? match = null;
                var want = Direction.Opposite(parentCP.Transform.Rotation);
                foreach (var c in childCPs)
                    if (c.Transform.Rotation == want) { match = c; break; }
                if (match == null) { Destroy(go); continue; }

                if (!MapGenerator.TryAttach(this, parentCP, child, match.Value))
                {
                    Destroy(go);
                    continue;
                }

                child.Parent = this;
                IsLeaf = false;

                if (child.TryFit()) break;
            }
        }
    }




    public void GenerateConnections()
    {
        foreach (var connection in GetResolved())
            GenerateExit(connection);
    }

    private void GenerateExit(Connection connection)
    {
        GameObject exit = Instantiate(MapGenerator.Instance.ExitPrefab, transform);

        int exitLayer = MapGenerator.Instance.ExitLayer;
        foreach (var t in exit.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = exitLayer;

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

        if (didFit)
        {
            const float aabbMargin = 0.015f; // 1.5 cm
            if (!MapGenerator.Instance.RegisterPlacedRoom(this, aabbMargin))
                didFit = false;
        }

        if (!didFit)
        {
            Debug.LogWarning($"[FitFail] {name} under parent {Parent?.name}  phys=False aabb=True");
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            DestroyImmediate(gameObject);
        }
        else
        {
            Initialize();
        }
        return didFit;
    }





    public void Initialize()
    {
        MapGenerator.Instance.Parameters.IterationRooms.Add(this);
        MapGenerator.Instance.Parameters.Rooms.Add(this);

        if (IsEntry) return;

        if (Properties.Type != RoomType.Hallway)
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

        //right = -right;
        //forward = -forward;
            
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
