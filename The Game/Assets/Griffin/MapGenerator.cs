using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GenerationParams
{
    // Number of rooms created.
    public uint RemainingRooms;
    // Chance to stop generating rooms.
    public float StopOdds;
    public List<RoomProfile> Rooms, RoomsBackbuffer;

    public void Iterate()
    {
        RoomsBackbuffer.Clear();
        Utils.Swap(ref Rooms, ref RoomsBackbuffer);
        
        foreach (RoomProfile room in RoomsBackbuffer)
        {
            room.GenerateLeafs(this);
        }
        
        RoomsBackbuffer.Clear();
    }
}

public class MapGenerator : MonoBehaviour
{
    [field: SerializeField] public MapType Type { get; private set; }
    [field: SerializeField] public Vector3Int MapSize { get; private set; }

    public static MapGenerator Instance;
    public uint RoomCount = 10;
    public float StopOdds = 0.05f;
    public const int GRID_SIZE = 5;

    public GenerationParams _params;
    
    void Awake()
    {
        Instance = this;

        _params = new GenerationParams
        {
            RemainingRooms = RoomCount,
            StopOdds = StopOdds,
            Rooms = new List<RoomProfile>(),
            RoomsBackbuffer = new List<RoomProfile>()
        };

        GameObject newCell = Instantiate(PickRandomCell().Prefab);
        _params.Rooms.Add(newCell.GetComponent<RoomProfile>());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _params.Iterate();
    }

    public RoomProperties PickRandomCell()
    {
        return Type.Cells[Random.Range(0, Type.Cells.Length)];
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