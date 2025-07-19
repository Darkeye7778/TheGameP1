using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class GenerationParams
{
    // Number of rooms created.
    public int RemainingRooms;
    
    public List<RoomProfile> Rooms, RoomsBackbuffer;
}

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance;
    
    [field: SerializeField] public MapType Type { get; private set; }
    
    public uint TargetRooms = 10;
    public uint MaxIterations = 20;
    public uint MaxLeafRetry = 3;
    public int Seed = 0;
    
    public const int GRID_SIZE = 5;

    public GenerationParams Parameters { get; private set; }
    
    void Awake()
    {
        Instance = this;

        Parameters = new GenerationParams
        {
            RemainingRooms = (int) TargetRooms,
            Rooms = new List<RoomProfile>(),
            RoomsBackbuffer = new List<RoomProfile>()
        };
        
        if (Seed == 0)
            Seed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(Seed);

        GameObject newCell = Instantiate(PickRandomCell().Prefab);
        Parameters.Rooms.Add(newCell.GetComponent<RoomProfile>());
        
        for(uint i = 0; Parameters.RemainingRooms > 0 && i < MaxIterations; i++)
            Iterate();
        
        Debug.Log($"Remaining rooms: {Parameters.RemainingRooms}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            Iterate();
    }

    public RoomProperties PickRandomCell()
    {
        return Type.Cells[Random.Range(0, Type.Cells.Length)];
    }
    
    public void Iterate()
    {
        Parameters.RoomsBackbuffer.Clear();
        Utils.Swap(ref Parameters.Rooms, ref Parameters.RoomsBackbuffer);
        
        foreach (RoomProfile room in Parameters.RoomsBackbuffer) 
            room.GenerateLeafs();
        
        Parameters.RoomsBackbuffer.Clear();
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
}