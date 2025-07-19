using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public class GenerationParams
{
    // Number of rooms created.
    public uint RemainingRooms;
}

public class MapGenerator : MonoBehaviour
{
    [field: SerializeField] public MapType Type { get; private set; }
    [field: SerializeField] public Vector3Int MapSize { get; private set; }

    public static MapGenerator Instance;
    public uint RoomCount;
    public const int GRID_SIZE = 5; 
    
    void Awake()
    {
        Instance = this;

        GenerationParams generationParams = new GenerationParams
        {
            RemainingRooms = RoomCount
        };

        GameObject newCell = Instantiate(PickRandomCell().Prefab);
        newCell.GetComponent<RoomProfile>().GenerateLeafs(0, new GridTransform(), ref generationParams);
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