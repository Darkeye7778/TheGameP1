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

    void Awake()
    {
        Instance = this;
        
        GridLayer = LayerMask.NameToLayer("Map Generator Grid");
        ExitLayer = LayerMask.NameToLayer("Map Generator Connection");

        _navMeshSurface = GetComponent<NavMeshSurface>();
    }
    
    public void Generate()
    {
        Cleanup();

        if (CustomSeed == 0)
            Seed = Random.Range(int.MinValue, int.MaxValue);
        else
            Seed = CustomSeed;
        
        Random.InitState(Seed);

        if (CustomSpawnSeed == 0)
            SpawnSeed = System.Guid.NewGuid().GetHashCode();
        else
            SpawnSeed = CustomSpawnSeed;

        GameObject entry = Instantiate(Utils.PickRandom(Type.StartingRooms).Prefab);
        RoomProfile entryProfile = entry.GetComponent<RoomProfile>();
        
        entryProfile.IsEntry = true;
        foreach (RoomProfile room in entry.GetComponentsInChildren<RoomProfile>())
        {
            room.IsInEntryZone = true;
        }
        entryProfile.Initialize();
        
        for(uint i = 0; Parameters.RemainingRooms > 0 && i < MaxIterations; i++)
            Iterate();
        
        Debug.Log($"Remaining rooms: {Parameters.RemainingRooms}");

        for (int i = Parameters.Rooms.Count; i > 0; i--)
        {
            RoomProfile room = Parameters.Rooms[i - 1];
            
            if(room.Parent == null)
                continue;
            
            if (room.Properties.Type != RoomType.Hallway || room.HasRoomLeaf) 
                room.Parent.HasRoomLeaf = room.HasRoomLeaf = true;
        }

        Parameters.Rooms.RemoveAll(RemoveRoomlessLeafs);
        
        foreach (RoomProfile room in Parameters.Rooms)
            room.GenerateConnections();
        
        Physics.SyncTransforms();
        
        foreach (ConnectionProfile connection in Parameters.Connections) 
            connection.Generate();
        
        Physics.SyncTransforms();

        _navMeshSurface.BuildNavMesh();
        
        SpawnAll();

        // Degenerate seed
        if (Parameters.RemainingRooms <= 0)
            return;
        
        Debug.Log("Regenerating degenerate seed.");
        
        CustomSeed = 0;
        Generate();
    }

    public void SpawnAll()
    {
        System.Random spawnRng = new System.Random(SpawnSeed);

        Transform spawnPoint = Utils.PickRandom(Parameters.PlayerSpawnPoints).transform;

        GameObject player = gameManager.instance.player;
        if (player == null)
        {
            player = Instantiate(Type.Player, spawnPoint.position, Quaternion.identity);
            gameManager.instance.SetPlayer(player);
        }
        else
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false; // Prevent movement interference
                controller.transform.position = spawnPoint.position;
                controller.enabled = true;
            }
            else
            {
                player.transform.position = spawnPoint.position;
            }

            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.ResetState();

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
                inventory.ResetWeapons();
        }


        SpawnOnNavMesh(Type.Enemies, EnemySpawnAmount, spawnRng);
        SpawnOnNavMesh(Type.Hostages, HostageSpawnAmount, spawnRng);
        SpawnOnNavMesh(Type.Traps, TrapSpawnAmount, spawnRng);
    }

    private void SpawnOnNavMesh(GameObject[] prefabPool, int count, System.Random spawnRng)
    {
        RoomProfile entryRoom = Parameters.Rooms.Find(room => room.IsEntry);
        Debug.Log($"Entry room: {entryRoom.name} at {entryRoom.transform.position}");

        List<RoomProfile> spawnableRooms = new List<RoomProfile>();
        foreach (RoomProfile room in Parameters.Rooms)
        {
            if (!room.IsInEntryZone)
                spawnableRooms.Add(room);
        }

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
                Debug.Log($"Spawning {prefab.name} in room: {room.name} | IsInEntryZone: {room.IsInEntryZone}");
                GameObject spawn = Instantiate(prefab, spawnPosition, Quaternion.identity);
                gameManager.instance.RegisterEntity(spawn);
            }
        }
    }

    private void Spawn<T>(ref List<T> positions, ref GameObject[] spawns, int targetCount) where T : MonoBehaviour
    {
        int remaining = targetCount;
        for (int i = 0; i < positions.Count; i++)
            if (Random.Range(0.0f, 1.0f) <= (float)remaining / (positions.Count - i))
            {
                Instantiate(Utils.PickRandom(spawns), positions[i].transform);
                remaining--;
            }
    }

    public void Cleanup()
    {
        foreach (GameObject go in gameManager.instance.SpawnedEntities)
        {
            if (go != null)
                DestroyImmediate(go);
        }
        gameManager.instance.SpawnedEntities.Clear();

        if (_navMeshSurface != null)
        {
            _navMeshSurface.RemoveData();
        }

        if (Parameters != null)
        {
            foreach (RoomProfile room in Parameters.Rooms)
                DestroyImmediate(room.gameObject);

            foreach (EnemySpawnPoint spawn in Parameters.EnemySpawnPoints)
                DestroyImmediate(spawn);
            
            foreach (TrapSpawnPoint spawn in Parameters.TrapSpawnPoints)
                DestroyImmediate(spawn);
            
            foreach (HostageSpawnPoint spawn in Parameters.HostageSpawnPoints)
                DestroyImmediate(spawn);
            
            foreach (PlayerSpawnPoint spawn in Parameters.PlayerSpawnPoints)
                DestroyImmediate(spawn);
        }

        Parameters = new GenerationParams
        {
            RemainingRooms = (int) TargetRooms,
            Rooms = new List<RoomProfile>(),
            IterationRooms = new List<RoomProfile>(),
            IterationBackbuffer = new List<RoomProfile>(),
            Connections = new List<ConnectionProfile>(),
            PlayerSpawnPoints = new List<PlayerSpawnPoint>(),
            EnemySpawnPoints = new List<EnemySpawnPoint>(),
            HostageSpawnPoints = new List<HostageSpawnPoint>(),
            TrapSpawnPoints = new List<TrapSpawnPoint>()
        };
    }

    private static bool RemoveRoomlessLeafs(RoomProfile room)
    {
        bool remove = !room.HasRoomLeaf;

        if(remove)
            DestroyImmediate(room.gameObject);
        
        return remove;
    }

    public RoomProperties PickRandomCell()
    {
        if(Random.Range(0f, 1f) < Type.RoomOdds)
            return Utils.PickRandom(Type.Rooms);
        return Utils.PickRandom(Type.Hallways);
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
        Collider floorCollider = room.GetComponent<Collider>();
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