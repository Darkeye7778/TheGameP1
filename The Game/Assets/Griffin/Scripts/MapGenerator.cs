using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
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
    
    [Header("Parameters")]
    public uint TargetRooms = 10;
    public int EnemySpawnAmount = 4;
    public int TrapSpawnAmount = 4;
    public int HostageSpawnAmount = 2;
    
    [Header("Generation Settings")]
    public uint MaxIterations = 20;
    public uint MaxLeafRetry = 3;
    
    public int Seed { get; private set; }
    
    public const int GRID_SIZE = 5;

    public GenerationParams Parameters { get; private set; }

    private NavMeshSurface _navMeshSurface;
    
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

        GameObject newCell = Instantiate(Utils.PickRandom(Type.StartingRooms).Prefab);
        newCell.GetComponent<RoomProfile>().Initialize();
        
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
        
        _navMeshSurface.BuildNavMesh();
        
        // Makes enemies always random.
        Random.InitState(Random.Range(int.MinValue, int.MaxValue));
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
        GameObject player = Instantiate(Type.Player, Utils.PickRandom(Parameters.PlayerSpawnPoints).transform);
        gameManager.instance.SetPlayer(player);

        Spawn(ref Parameters.EnemySpawnPoints, ref Type.Enemies, EnemySpawnAmount);
        Spawn(ref Parameters.HostageSpawnPoints, ref Type.Hostages, HostageSpawnAmount);
        Spawn(ref Parameters.TrapSpawnPoints, ref Type.Traps, TrapSpawnAmount);
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
}