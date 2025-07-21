using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class GenerationParams
{
    // Number of rooms created.
    public int RemainingRooms;
    
    public List<RoomProfile> IterationRooms, IterationBackbuffer;
    public List<RoomProfile> Rooms;
    public List<ConnectionProfile> Connections;
}

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance;
    
    [field: SerializeField] public MapType Type { get; private set; }
    [field: SerializeField] public GameObject ExitPrefab { get; private set; }

    public int GridLayer { get; private set; }
    public int ExitLayer { get; private set; }

    public LayerMask ExitMask => 1 << ExitLayer;

    public uint TargetRooms = 10;
    public uint MaxIterations = 20;
    public uint MaxLeafRetry = 3;
    public int Seed = 0;
    
    public const int GRID_SIZE = 5;

    public GenerationParams Parameters { get; private set; }

    private bool _generatedDoors = false;
    
    void Awake()
    {
        Instance = this;
        
        GridLayer = LayerMask.NameToLayer("Map Generator Grid");
        ExitLayer = LayerMask.NameToLayer("Map Generator Connection");

        Parameters = new GenerationParams
        {
            RemainingRooms = (int) TargetRooms,
            Rooms = new List<RoomProfile>(),
            IterationRooms = new List<RoomProfile>(),
            IterationBackbuffer = new List<RoomProfile>(),
            Connections = new List<ConnectionProfile>()
        };
        
        if (Seed == 0)
            Seed = Random.Range(int.MinValue, int.MaxValue);
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
        return Utils.PickRandom(Type.Cells);
    }
    
    public void Iterate()
    {
        Parameters.IterationBackbuffer.Clear();
        Utils.Swap(ref Parameters.IterationRooms, ref Parameters.IterationBackbuffer);
        
        foreach (RoomProfile room in Parameters.IterationBackbuffer) 
            room.GenerateLeafs();
        
        Parameters.IterationBackbuffer.Clear();
    }

    public void Update()
    {
        if(_generatedDoors)
            return;

        _generatedDoors = true;
        
        foreach (ConnectionProfile connection in Parameters.Connections) 
            connection.Generate();
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
}