using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GenerationParams
{
    // Number of rooms created.
    public int RemainingRooms;
    
    public List<RoomProfile> IterationRooms, IterationBackbuffer;
    public List<RoomProfile> Rooms;
    public List<ConnectionProfile> Connections;
    public List<HostageSpawnPoint> HostageSpawnPoints;
    public List<PlayerSpawnPoint> PlayerSpawnPoints;
    public List<EnemySpawnPoint> EnemySpawnPoints;
    public List<TrapSpawnPoint> TrapSpawnPoints;
    public List<Bounds> PlacedBounds;
}

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance;
    
    [field: SerializeField] public MapType Type { get; private set; }
    [field: SerializeField] public GameObject ExitPrefab { get; private set; }

    public int GridLayer { get; private set; }
    public int ExitLayer { get; private set; }

    public LayerMask GridMask => 1 << GridLayer;
    public LayerMask ExitMask => 1 << ExitLayer;

    [Tooltip("Leave 0 to randomly generate a new seed.")]
    public int CustomSeed = 0;
    public int CustomSpawnSeed = 0;

    [Header("Parameters")]
    public uint TargetRooms = 10;
    public int EnemySpawnAmount = 4;
    public int TrapSpawnAmount = 4;
    public int HostageSpawnAmount = 2;
    
    [Header("Generation Settings")]
    public uint MaxIterations = 20;
    public uint MaxLeafRetry = 3;
    
    public int Seed { get; private set; }
    public int SpawnSeed { get; private set; }

    public const int GRID_SIZE = 5;

    public GenerationParams Parameters { get; private set; }

    private NavMeshSurface _navMeshSurface;

    private List<GameObject> _spawnedEntities = new List<GameObject>();

    [field: SerializeField] public LevelDefinition CurrentLevelDefinition { get; private set; }

    [SerializeField] int maxSeedRetries = 8;
    int _seedTries = 0;

    [Header("Debug")]
    [SerializeField] bool DebugTrace = true;
    [SerializeField] bool ForceFirstAttachIfEmpty = true;

    [SerializeField] LayerMask navIncludeLayers = ~0;
    [SerializeField] bool navUseColliders = true;
    [SerializeField] float navPadding = 2f;

    //[SerializeField] string navProxyLayerName = "NavProxy";
    //[SerializeField] float navProxyThickness = 0.04f;
    //[SerializeField] float navProxyPad = 0.02f;
    //private float _baseFloorY;
    void Awake()
    {
        Instance = this;
        
        GridLayer = LayerMask.NameToLayer("Map Generator Grid");
        ExitLayer = LayerMask.NameToLayer("Map Generator Connection");

        _navMeshSurface = GetComponent<NavMeshSurface>();

        Generate();
    }

    public void ApplyLevel(LevelDefinition def)
    {
        CurrentLevelDefinition = def;
        // MapType + counts
        if (def.MapType != null) Type = def.MapType;
        TargetRooms = def.TargetRooms;
        EnemySpawnAmount = def.EnemySpawnAmount;
        TrapSpawnAmount = def.TrapSpawnAmount;
        HostageSpawnAmount = def.HostageSpawnAmount;

        // Seeds
        CustomSeed = def.UseFixedSeed ? def.FixedSeed : 0;
        CustomSpawnSeed = def.UseFixedSpawnSeed ? def.FixedSpawnSeed : 0;
    }

    public void Generate(LevelDefinition def)
    {
        ApplyLevel(def);
        Generate();
    }

    public void Generate()
    {
        Cleanup();

        Seed = (CustomSeed == 0) ? Random.Range(int.MinValue, int.MaxValue) : CustomSeed;
        SpawnSeed = (CustomSpawnSeed == 0) ? System.Guid.NewGuid().GetHashCode() : CustomSpawnSeed;
        Random.InitState(Seed);

        const bool debugTrace = true;
        const bool forceFirstAttachIfEmpty = true;

        void TraceTileset()
        {
            if (!debugTrace) return;
            Debug.Log($"[GenDiag] MapType={(Type ? Type.name : "NULL")}  TargetRooms={TargetRooms}  MaxIterations={MaxIterations}  MaxLeafRetry={MaxLeafRetry}");
            Debug.Log($"[GenDiag] Pools: Starting={(Type?.StartingRooms?.Length ?? 0)} Rooms={(Type?.Rooms?.Length ?? 0)} Halls={(Type?.Hallways?.Length ?? 0)}");
        }

        void TraceEntry(RoomProfile entry)
        {
            if (!debugTrace || !entry || !entry.Properties) return;
            var cps = entry.Properties.GetResolvedConnectionPoints();
            Debug.Log($"[GenDiag] Entry={entry.name} CPs={cps.Length}");
            for (int i = 0; i < cps.Length; i++)
                Debug.Log($"[GenDiag]  CP[{i}] pos={cps[i].Transform.Position} dir={cps[i].Transform.Rotation} required={cps[i].Required} hasDoor={cps[i].HasDoor}");
        }

        bool TryForceFirstAttach(RoomProfile entry)
        {
            if (!entry || !entry.Properties) return false;

            var points = entry.Properties.GetResolvedConnectionPoints();
            if (points == null || points.Length == 0)
            {
                Debug.LogWarning("[GenDiag] Entry has 0 connection points.");
                return false;
            }

            var ordered = new List<Connection>(points);
            ordered.Sort((a, b) => b.Required.CompareTo(a.Required));
            
            int maxTries = Mathf.Max(6, (int)MaxLeafRetry * 2);

            foreach (var parentCP in ordered)
            {
                for (int t = 0; t < maxTries; t++)
                {
                    var cell = PickRandomCell();
                    var prefab = cell != null ? cell.Prefab : null;
                    if (!prefab) break;

                    var go = Instantiate(prefab);
                    var child = go.GetComponent<RoomProfile>();
                    if (!child) { Destroy(go); continue; }

                    var childCPs = child.Properties != null ? child.Properties.GetResolvedConnectionPoints() : Array.Empty<Connection>();
                    Connection? match = null;
                    var want = Direction.Opposite(parentCP.Transform.Rotation);
                    foreach (var c in childCPs) if (c.Transform.Rotation == want) { match = c; break; }
                    if (match == null) { Destroy(go); continue; }

                    if (!TryAttach(entry, parentCP, child, match.Value))
                    {
                        Destroy(go);
                        continue;
                    }

                    child.Parent = entry;

                    var m = typeof(RoomProfile).GetMethod("TryFit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    bool ok = m != null && (bool)m.Invoke(child, null);

                    if (ok)
                    {
                        if (debugTrace) Debug.Log($"[GenDiag] Forced first attach: entry={entry.name} -> child={child.name} via {parentCP.Transform.Rotation}");
                        return true;
                    }

                    Destroy(go);
                }
            }

            if (debugTrace) Debug.LogWarning("[GenDiag] Failed to force first attach on entry.");
            return false;
        }
        
        TraceTileset();
        if (!Type)
        {
            Debug.LogError("[GenDiag] MapType is NULL. Cannot generate.");
            return;
        }
        if ((Type.Rooms == null || Type.Rooms.Length == 0) && (Type.Hallways == null || Type.Hallways.Length == 0))
        {
            Debug.LogError("[GenDiag] MapType has no Rooms and no Hallways.");
            return;
        }
        
        GameObject entryGO = Instantiate(Utils.PickRandom(Type.StartingRooms).Prefab);
        var entry = entryGO.GetComponent<RoomProfile>();

        entry.IsEntry = true;
        foreach (var r in entryGO.GetComponentsInChildren<RoomProfile>())
            r.IsInEntryZone = true;

        entry.Initialize();

        RegisterPlacedRoom(entry);

        TraceEntry(entry);

        if (MaxIterations == 0 && forceFirstAttachIfEmpty)
            TryForceFirstAttach(entry);

        for (uint i = 0; Parameters.RemainingRooms > 0 && i < MaxIterations; i++)
            Iterate();

        Debug.Log($"Remaining rooms: {Parameters.RemainingRooms}");

        for (int i = 0; i < Parameters.Rooms.Count; i++)
            Parameters.Rooms[i].HasRoomLeaf = false;

        for (int i = 0; i < Parameters.Rooms.Count; i++)
        {
            var r = Parameters.Rooms[i];
            if (r && r.Properties && r.Properties.Type != RoomType.Hallway)
                r.HasRoomLeaf = true;
        }
        
        for (int i = Parameters.Rooms.Count; i > 0; i--)
        {
            var r = Parameters.Rooms[i - 1];
            if (r && r.Parent && r.HasRoomLeaf)
                r.Parent.HasRoomLeaf = true;
        }

        int before = Parameters.Rooms.Count;
        int removed = Parameters.Rooms.RemoveAll(RemoveRoomlessLeafs);
        if (removed > 0)
            Debug.Log($"[MapGen] Pruned {removed} roomless leaves, kept {Parameters.Rooms.Count}/{before}");

        if (Parameters.RemainingRooms > 0)
        {
            _seedTries++;
            if (_seedTries >= maxSeedRetries)
            {
                Debug.LogError($"[MapGenerator] Failed after {maxSeedRetries} attempts. Remaining = {Parameters.RemainingRooms}. " +
                               "Check Starting room connection points and room pools.");
                _seedTries = 0;
                return;
            }
            Debug.LogWarning($"[MapGenerator] Degenerate Layout (remaining = {Parameters.RemainingRooms}). Retrying seed {_seedTries}/{maxSeedRetries}...");
            CustomSeed = 0;
            StartCoroutine(RestartNextFrame());
            return;
        }
        _seedTries = 0;

        foreach (RoomProfile room in Parameters.Rooms)
            room.GenerateConnections();

        Physics.SyncTransforms();

        foreach (ConnectionProfile connection in Parameters.Connections)
            connection.Generate();

        Physics.SyncTransforms();

        RebuildNavMeshForRooms();

        if (CurrentLevelDefinition != null && CurrentLevelDefinition.CategoryTable != null)
        {
            var catRng = new System.Random(Seed);
            CategoryAssigner.AssignCategories(Parameters.Rooms, CurrentLevelDefinition.CategoryTable, catRng);
        }

        if (CurrentLevelDefinition != null && CurrentLevelDefinition.Theme != null)
        {
            var rng = new System.Random(SpawnSeed);
            PropDresser.DressRooms(Parameters.Rooms, CurrentLevelDefinition.Theme, rng);
        }

        StartCoroutine(SpawnAllDeferred());
    }



    public void SpawnAll()
    {
        if (true) Debug.Log("[GenDiag] SpawnAll() start");
        System.Random spawnRng = new System.Random(SpawnSeed);

        Transform spawnPoint;
        bool usedFallback = false;

        if (Parameters.PlayerSpawnPoints != null && Parameters.PlayerSpawnPoints.Count > 0)
        {
            spawnPoint = Utils.PickRandom(Parameters.PlayerSpawnPoints).transform;
            Debug.Log($"[GenDiag] Using PlayerSpawnPoint: {spawnPoint.name} at {spawnPoint.position}");
        }
        else
        {
            usedFallback = true;
            
            var entryRoom = Parameters.Rooms.Find(r => r.IsEntry);
            Vector3 pos = entryRoom ? entryRoom.transform.position + Vector3.up : Vector3.up;

            if (entryRoom && entryRoom.floor)
            {
                var floorCol = entryRoom.floor.GetComponent<Collider>();
                if (floorCol)
                    pos = new Vector3(floorCol.bounds.center.x, floorCol.bounds.max.y + 0.05f, floorCol.bounds.center.z);
            }

            var tmp = new GameObject("FallbackPlayerSpawn");
            tmp.transform.position = pos;
            gameManager.instance.RegisterEntity(tmp);

            Debug.LogWarning("No PlayerSpawnPoint found; using fallback near entry.");
            spawnPoint = tmp.transform;
            Debug.Log($"[GenDiag] Fallback spawn at {spawnPoint.position}");
        }

        Vector3 p = spawnPoint.position;
        
        if (usedFallback)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                p = hit.point + Vector3.up * 0.05f;
            
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var nHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                p = nHit.position + Vector3.up * 0.05f;
        }

        Debug.Log($"[GenDiag] Final player spawn pos: {p}  (usedFallback={usedFallback})");

        GameObject player = gameManager.instance.player;
        if (player == null)
        {
            player = Instantiate(Type.Player, p, Quaternion.identity);
            gameManager.instance.SetPlayer(player);

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.ResetState();
                pc.GrantTemporaryInvulnerability(1.0f);
            }
        }
        else
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                controller.transform.position = p;
                controller.enabled = true;
            }
            else player.transform.position = p;

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.ResetState();
                pc.GrantTemporaryInvulnerability(1.0f);
            }

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null) inventory.ResetWeapons();
        }

        SpawnOnNavMesh(Type.Enemies, EnemySpawnAmount, spawnRng);
        SpawnOnNavMesh(Type.Hostages, HostageSpawnAmount, spawnRng);
        Spawn(ref Parameters.TrapSpawnPoints, ref Type.Traps, TrapSpawnAmount);
    }


    private void CollectSpawnPointsFromRooms()
    {
        // Clear to avoid duplicates on regen
        Parameters.PlayerSpawnPoints.Clear();
        Parameters.EnemySpawnPoints.Clear();
        Parameters.TrapSpawnPoints.Clear();
        Parameters.HostageSpawnPoints.Clear();

        // Sweep every generated room (entry + all leaves)
        foreach (var room in Parameters.Rooms)
        {
            if (!room) continue;
            Parameters.PlayerSpawnPoints.AddRange(room.GetComponentsInChildren<PlayerSpawnPoint>(true));
            Parameters.EnemySpawnPoints.AddRange(room.GetComponentsInChildren<EnemySpawnPoint>(true));
            Parameters.TrapSpawnPoints.AddRange(room.GetComponentsInChildren<TrapSpawnPoint>(true));
            Parameters.HostageSpawnPoints.AddRange(room.GetComponentsInChildren<HostageSpawnPoint>(true));
        }

        Debug.Log($"Spawns � Player:{Parameters.PlayerSpawnPoints.Count} " +
                  $"Enemy:{Parameters.EnemySpawnPoints.Count} Hostage:{Parameters.HostageSpawnPoints.Count} Trap:{Parameters.TrapSpawnPoints.Count}");
    }
    
    private void SpawnOnNavMesh(GameObject[] prefabPool, int count, System.Random spawnRng)
    {
        // Nothing to spawn? Bail out safely (Hub case).
        if (count <= 0 || prefabPool == null || prefabPool.Length == 0)
            return;

        RoomProfile entryRoom = Parameters.Rooms.Find(room => room.IsEntry);
        if (entryRoom == null)
        {
            Debug.LogWarning("SpawnOnNavMesh: No entry room found.");
            return;
        }

        // Non-entry rooms only
        List<RoomProfile> spawnableRooms = new List<RoomProfile>();
        foreach (RoomProfile room in Parameters.Rooms)
            if (!room.IsInEntryZone)
                spawnableRooms.Add(room);

        if (spawnableRooms.Count == 0)
        {
            Debug.LogWarning("No available rooms to spawn in (excluding entry room).");
            return;
        }

        int totalSpawns = count;
        int rooms = spawnableRooms.Count;
        int basePerRoom = totalSpawns / rooms;
        int remainder = totalSpawns % rooms;

        foreach (RoomProfile room in spawnableRooms)
        {
            int spawnsThisRoom = basePerRoom + (remainder > 0 ? 1 : 0);
            if (remainder > 0) remainder--;

            for (int i = 0; i < spawnsThisRoom; i++)
            {
                Vector3 spawnPosition = GetRandomPointInRoom(room, spawnRng);
                GameObject prefab = Utils.PickRandom(prefabPool, spawnRng);
                GameObject spawn = Instantiate(prefab, spawnPosition, Quaternion.identity);
                gameManager.instance.RegisterEntity(spawn);
            }
        }
    }


    private void Spawn<T>(ref List<T> positions, ref GameObject[] spawns, int targetCount) where T : MonoBehaviour
    {
        if (targetCount <= 0 || positions == null || positions.Count == 0 || spawns == null || spawns.Length == 0)
            return;

        int remaining = targetCount;
        for (int i = 0; i < positions.Count; i++)
        {
            if (UnityEngine.Random.Range(0.0f, 1.0f) <= (float)remaining / (positions.Count - i))
            {
                Instantiate(Utils.PickRandom(spawns), positions[i].transform);
                remaining--;
                if (remaining <= 0) break;
            }
        }
    }


    public void Cleanup()
    {
        if (_navMeshSurface != null)
            _navMeshSurface.RemoveData();

        if (Parameters != null)
        {
            // Snapshot everything we might destroy to avoid "collection modified" and MissingReference during enumeration
            var toDestroy = new HashSet<GameObject>();

            void AddAll<T>(IEnumerable<T> comps) where T : Component
            {
                if (comps == null) return;
                foreach (var c in comps)
                {
                    if (!c) continue;                // destroyed/null-safe
                    var go = c.gameObject;
                    if (go) toDestroy.Add(go);
                }
            }

            AddAll(Parameters.Rooms);
            AddAll(Parameters.Connections);
            AddAll(Parameters.PlayerSpawnPoints);
            AddAll(Parameters.EnemySpawnPoints);
            AddAll(Parameters.TrapSpawnPoints);
            AddAll(Parameters.HostageSpawnPoints);

            // Destroy unique gameobjects (safe in Edit/Play)
            foreach (var go in toDestroy)
                SafeDestroy(go);

            // Make sure lists are empty and won’t hold destroyed refs
            Parameters.Rooms.Clear();
            Parameters.Connections.Clear();
            Parameters.PlayerSpawnPoints.Clear();
            Parameters.EnemySpawnPoints.Clear();
            Parameters.TrapSpawnPoints.Clear();
            Parameters.HostageSpawnPoints.Clear();
        }

        // Fresh parameter object for the next run
        Parameters = new GenerationParams
        {
            RemainingRooms = (int)TargetRooms,
            Rooms = new List<RoomProfile>(),
            IterationRooms = new List<RoomProfile>(),
            IterationBackbuffer = new List<RoomProfile>(),
            Connections = new List<ConnectionProfile>(),
            PlayerSpawnPoints = new List<PlayerSpawnPoint>(),
            EnemySpawnPoints = new List<EnemySpawnPoint>(),
            HostageSpawnPoints = new List<HostageSpawnPoint>(),
            TrapSpawnPoints = new List<TrapSpawnPoint>(),
            PlacedBounds = new List<Bounds>()
        };
    }

    private static bool RemoveRoomlessLeafs(RoomProfile room)
    {
        // NEVER remove the entry room
        if (room.IsEntry)
            return false;

        bool remove = !room.HasRoomLeaf;

        if (remove)
            DestroyImmediate(room.gameObject);

        return remove;
    }


    public RoomProperties PickRandomCell()
    {
        var rooms = Type.Rooms;
        var halls = Type.Hallways;

        bool hasRooms = rooms != null && rooms.Length > 0;
        bool hasHalls = halls != null && halls.Length > 0;

        if (!hasRooms && !hasHalls)
            throw new InvalidOperationException($"[MapGen] MapType '{Type?.name}' has no Rooms or Hallways.");

        float r = UnityEngine.Random.Range(0f, 1f);
        if (r < Type.RoomOdds)
            return hasRooms ? Utils.PickRandom(rooms) : Utils.PickRandom(halls);
        else
            return hasHalls ? Utils.PickRandom(halls) : Utils.PickRandom(rooms);
    }

    public void Iterate()
    {
        Parameters.IterationBackbuffer.Clear();
        Utils.Swap(ref Parameters.IterationRooms, ref Parameters.IterationBackbuffer);
        
        foreach (RoomProfile room in Parameters.IterationBackbuffer) 
            room.GenerateLeafs();
        
        Parameters.IterationBackbuffer.Clear();
    }

    public void GenerateSame()
    {
        CustomSeed = Instance.Seed;
        Generate();
    }

    Vector3 GetRandomPosition(Vector3 center, float range)
    {

        for (int i = 0; i < 30; i++) // Try 30 random points
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * range;
            randomPoint.y = center.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        Debug.LogWarning("Failed to find valid NavMesh position.");
        return center;
    }

    Vector3 GetRandomPointInRoom(RoomProfile room, System.Random rng)
    {
        Collider floorCollider = room.floor.GetComponent<Collider>();
        if (!floorCollider)
        {
            Debug.LogWarning($"Room {room.name} has no collider.");
            return room.transform.position;
        }

        Bounds bounds = floorCollider.bounds;

        for (int i = 0; i < 30; i++)
        {
            float x = (float)(rng.NextDouble() * (bounds.max.x - bounds.min.x) + bounds.min.x);
            float z = (float)(rng.NextDouble() * (bounds.max.z - bounds.min.z) + bounds.min.z);
            float y = bounds.center.y;

            Vector3 candidate = new Vector3(x, y, z);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
                return hit.position;
        }

        Debug.LogWarning($"Failed to find valid NavMesh point in room {room.name}");
        return room.transform.position;
    }

    private IEnumerator SpawnAllDeferred()
    {
        // let all newly-instantiated components run Start()
        yield return null;
        CollectSpawnPointsFromRooms(); // see below
        SpawnAll();
    }

    public void ApplyDefinition(LevelDefinition def)
    {
        if (def == null) return;

        // tileset
        Type = def.MapType;

        // generation targets
        TargetRooms = def.TargetRooms;
        EnemySpawnAmount = def.EnemySpawnAmount;
        TrapSpawnAmount = def.TrapSpawnAmount;
        HostageSpawnAmount = def.HostageSpawnAmount;

        // seeds (0 means �randomize this run�)
        CustomSeed = def.UseFixedSeed ? def.FixedSeed : 0;
        CustomSpawnSeed = def.UseFixedSpawnSeed ? def.FixedSpawnSeed : 0;
    }

    private IEnumerator RestartNextFrame()
    {
        yield return null;
        Generate();
    }

    static Vector3 Facing(ExitDirection d, Vector3 right, Vector3 forward)
    {
        switch (d)
        {
            case ExitDirection.East: return right;
            case ExitDirection.West: return -right;
            case ExitDirection.North: return forward;
            case ExitDirection.South: return -forward;
        }
        return forward;
    }

    static float YawFromTo(Vector3 fromDir, Vector3 toDir)
    {
        fromDir.y = 0f; toDir.y = 0f;
        if (fromDir.sqrMagnitude < 1e-6f || toDir.sqrMagnitude < 1e-6f) return 0f;
        fromDir.Normalize(); toDir.Normalize();
        float angle = Vector3.SignedAngle(fromDir, toDir, Vector3.up);
        return angle;
    }

    static void RotateYaw(Transform t, float deltaYawDeg)
    {
        var e = t.eulerAngles;
        e.y += deltaYawDeg;
        t.eulerAngles = e;
    }

    public Bounds WorldAABB(RoomProfile room)
    {
        Transform t = room.transform;
        Vector3 size = room.Properties.CollisionBox;
        Vector3 center = room.Properties.CollisionOffset;

        Vector3 half = size * 0.5f;

        Vector3[] corners = new Vector3[8];
        int k = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 local = center + new Vector3(half.x * sx, half.y * sy, half.z * sz);
                    corners[k++] = t.TransformPoint(local);
                }

        Bounds b = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 8; i++) b.Encapsulate(corners[i]);
        return b;
    }

    static Bounds Encapsulate(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    static bool OverlapsAny(Bounds b, List<Bounds> existing, float margin)
    {
        b.Expand(margin);
        for (int i = 0; i < existing.Count; i++)
            if (b.Intersects(existing[i])) return true;
        return false;
    }

    public static bool TryAttach(RoomProfile parent, Connection parentCP, RoomProfile child, Connection childCP, float epsilon = 0.005f)
    {
        Vector3 P = parent.GetConnWorldPos(parentCP);
        Vector3 C = child.GetConnWorldPos(childCP);

        Vector3 pr = Vector3.ProjectOnPlane(parent.transform.right, Vector3.up).normalized;
        Vector3 pf = Vector3.ProjectOnPlane(parent.transform.forward, Vector3.up).normalized;
        Vector3 cr = Vector3.ProjectOnPlane(child.transform.right, Vector3.up).normalized;
        Vector3 cf = Vector3.ProjectOnPlane(child.transform.forward, Vector3.up).normalized;

        Vector3 faceP = Facing(parentCP.Transform.Rotation, pr, pf);
        Vector3 faceC = Facing(childCP.Transform.Rotation, cr, cf);

        float yaw = YawFromTo(faceC, -faceP);
        RotateYaw(child.transform, yaw);

        C = child.GetConnWorldPos(childCP);
        
        Vector3 cCenter = child.transform.TransformPoint(child.Properties.CollisionOffset);

        static int DominantAxisIndex(Vector3 n, Transform t)
        {
            n = n.normalized;
            float dx = Mathf.Abs(Vector3.Dot(n, t.right));
            float dy = Mathf.Abs(Vector3.Dot(n, t.up));
            float dz = Mathf.Abs(Vector3.Dot(n, t.forward));
            if (dx >= dy && dx >= dz) return 0;
            if (dy >= dz) return 1;
            return 2;
        }
        static float HalfSizeAlong(int axisIndex, Vector3 size)
            => (axisIndex == 0 ? size.x : axisIndex == 1 ? size.y : size.z) * 0.5f;

        static float DistancePointToFace(Vector3 n, Transform t, Vector3 centerWorld, Vector3 size, Vector3 pointWorld)
        {
            n = n.normalized;
            int i = DominantAxisIndex(n, t);
            Vector3 axis = (i == 0 ? t.right : i == 1 ? t.up : t.forward);
            float half = HalfSizeAlong(i, size);
            float sign = Mathf.Sign(Vector3.Dot(n, axis));
            Vector3 facePoint = centerWorld + axis * (sign * half);
            return Vector3.Dot(n, (facePoint - pointWorld));
        }

        Vector3 nChildOut = -faceP;
        float dChild = DistancePointToFace(nChildOut, child.transform, cCenter, child.Properties.CollisionBox, C);

        if (Mathf.Abs(dChild) > 1e-5f)
        {
            child.transform.position += -nChildOut * dChild;
            C = child.GetConnWorldPos(childCP);
        }

        AlignChildFloorYToParent(parent, child);
        C = child.GetConnWorldPos(childCP);

        Vector3 delta = P - C;
        child.transform.position += delta;

        if (epsilon > 0f)
            child.transform.position += faceP * epsilon;

        return true;
    }


    public bool RegisterPlacedRoom(RoomProfile room, float margin = 0.015f)
    {
        Bounds trueB = WorldAABB(room);

        Bounds testB = trueB;
        testB.Expand(-2f * margin);

        foreach (var existing in Parameters.PlacedBounds)
        {
            Bounds ex = existing;
            ex.Expand(-2f * margin);

            if (testB.Intersects(ex))
            {
                Debug.Log($"[AABB] Reject {room.name} against existing bounds.");
                return false;
            }
        }

        Parameters.PlacedBounds.Add(trueB);
        return true;
    }
    static void SafeDestroy(GameObject go)
    {
        if (!go) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    Bounds ComputeRoomsWorldBounds()
    {
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool inited = false;
        foreach (var r in Parameters.Rooms)
        {
            if (!r) continue;
            var rb = WorldAABB(r);
            if (!inited) { b = rb; inited = true; }
            else b.Encapsulate(rb);
        }
        if (!inited) b = new Bounds(transform.position, Vector3.one * 4f);
        b.Expand(new Vector3(navPadding * 2f, 4f, navPadding * 2f));
        return b;
    }

    void RebuildNavMeshForRooms()
    {
        _navMeshSurface.layerMask = navIncludeLayers;
        _navMeshSurface.useGeometry = navUseColliders ? NavMeshCollectGeometry.PhysicsColliders : NavMeshCollectGeometry.RenderMeshes;

        if (_navMeshSurface.collectObjects == CollectObjects.Volume)
        {
            var wb = ComputeRoomsWorldBounds();
            _navMeshSurface.center = _navMeshSurface.transform.InverseTransformPoint(wb.center);
            _navMeshSurface.size = Vector3.Scale(wb.size, new Vector3(1f / _navMeshSurface.transform.lossyScale.x, 1f / _navMeshSurface.transform.lossyScale.y, 1f / _navMeshSurface.transform.lossyScale.z));
        }
        else if (_navMeshSurface.collectObjects == CollectObjects.Children)
        {
            foreach (var r in Parameters.Rooms)
                if (r && r.transform.parent != _navMeshSurface.transform)
                    r.transform.SetParent(_navMeshSurface.transform, true);
        }

        _navMeshSurface.BuildNavMesh();
    }

    static void AlignChildFloorYToParent(RoomProfile parent, RoomProfile child)
    {
        if (!parent || !child) return;
        if (!parent.floor || !child.floor) return;

        var pCol = parent.floor.GetComponent<Collider>();
        var cCol = child.floor.GetComponent<Collider>();
        if (!pCol || !cCol) return;

        float parentTop = pCol.bounds.max.y;
        float childTop = cCol.bounds.max.y;

        float dy = parentTop - childTop;
        if (Mathf.Abs(dy) > 0.0005f)
            child.transform.position += new Vector3(0f, dy, 0f);
    }
}

public static class Utils
{
    public static void Swap<T>(ref T lhs, ref T rhs)
    {
        (lhs, rhs) = (rhs, lhs);
    }

    public static int Mod(int lhs, int rhs)
    {
        return (lhs % rhs + rhs) % rhs;
    }

    public static T PickRandom<T>(T[] arr)
    {
        return arr[Random.Range(0, arr.Length)];
    }
    public static T PickRandom<T>(List<T> arr)
    {
        return arr[Random.Range(0, arr.Count)];
    }
    public static T PickRandom<T>(List<T> arr, System.Random rng)
    {
        return arr[rng.Next(arr.Count)];
    }
    public static T PickRandom<T>(T[] arr, System.Random rng)
    {
        return arr[rng.Next(arr.Length)];
    }
}