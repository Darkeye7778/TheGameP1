using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public class RoomInstance
{
    public RoomInstance(GameObject obj, RoomProfile profile)
    {
        Object = obj;
        Profile = profile;
    }
    
    // Will already be instantiated
    public GameObject Object;
    public RoomProfile Profile;
}

public class MapGenerator : MonoBehaviour
{
    [field: SerializeField] public MapType Type { get; private set; }
    [field: SerializeField] public Vector3Int MapSize { get; private set; }

    public RoomInstance[,,] Grid { get; private set; }

    private const int GRID_SIZE = 5; 
    private const int ROOM_HEIGHT = 10; 
    
    void Awake()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
        Grid = new RoomInstance[MapSize.x, MapSize.y, MapSize.z];

        Vector3Int entrance, exit;
        ExitDirection exitDirection = (ExitDirection)Utils.Mod(Random.Range((int)ExitDirection.West - 4, (int)ExitDirection.East + 1), 4);
        Debug.Log(exitDirection.ToString());
        
        // South
        entrance = new Vector3Int(Random.Range(Type.DoorOffset, MapSize.x - Type.DoorOffset), 0, 0);
        exit = exitDirection switch
        {
            ExitDirection.North => new Vector3Int(Random.Range(Type.DoorOffset, MapSize.x - Type.DoorOffset + 1), 0, MapSize.z),
            ExitDirection.West => new Vector3Int(0, 0, Random.Range(Type.DoorOffset, MapSize.z - Type.DoorOffset + 1)),
            ExitDirection.East => new Vector3Int(MapSize.x, 0, Random.Range(Type.DoorOffset, MapSize.z - Type.DoorOffset + 1)),
            _ => new Vector3Int()
        };
        
        GenerateHallways(exitDirection, entrance, exit);
    }

    void GenerateHallways(ExitDirection direction, Vector3Int entrance, Vector3Int exit)
    {
        if (direction == ExitDirection.North)
            GenerateHallwayNorth(entrance, exit);
        else 
            GenerateHallwayEastWest(entrance, exit);
    }

    void GenerateHallwayNorth(Vector3Int entrance, Vector3Int exit)
    {
        if (entrance.x == exit.x)
            GenerateHallwayStraight(entrance, exit);
        else
        {
            int middle = (exit.z - entrance.z) / 2;
            
            //   d
            // b-c
            // a

            Vector3Int a, b, c, d;
            a = entrance;
            b = new Vector3Int(entrance.x, entrance.y, entrance.z + middle);
            c = new Vector3Int(exit.x, entrance.y, entrance.z + middle);
            d = exit;
            
            GenerateHallwayStraight(a, b);
            GenerateHallwayStraight(b, c);
            GenerateHallwayStraight(c, d);
        }
    }

    void GenerateHallwayEastWest(Vector3Int entrance, Vector3Int exit)
    {
        // c-b
        //   a

        Vector3Int a, b, c;
        a = entrance;
        b = new Vector3Int(entrance.x, entrance.y, exit.z);
        c = exit;
        
        GenerateHallwayStraight(a, b);
        GenerateHallwayStraight(b, c);
    }

    RoomProperties PickRandom(RoomProperties[] arr)
    {
        return arr[Random.Range(0, arr.Length)];
    }

    void GenerateHallwayStraight(Vector3Int entrance, Vector3Int exit)
    {
        if(entrance.x == exit.x)
        {
            if (entrance.z > exit.z)
                Utils.Swap(ref entrance, ref exit);
            
            for (int z = 0; z < exit.z - entrance.z; z++)
            {
                Vector3Int gridPosition = new Vector3Int(entrance.x, entrance.y, entrance.z + z);

                GameObject instance = Instantiate(PickRandom(Type.HallwayStraight).Prefab, GridToWorld(gridPosition), Quaternion.identity);

                Grid[gridPosition.x, gridPosition.y, gridPosition.z] = new RoomInstance(instance, instance.GetComponent<RoomProfile>());
            }
        }
        else if (entrance.z == exit.z)
        {
            if (entrance.x > exit.x)
                Utils.Swap(ref entrance, ref exit);
            
            for (int x = 0; x < exit.x - entrance.x; x++)
            {
                Vector3Int gridPosition = new Vector3Int(entrance.x + x, entrance.y, entrance.z);

                GameObject instance = Instantiate(PickRandom(Type.HallwayStraight).Prefab, GridToWorld(gridPosition), quaternion.Euler(0, 90.0f * Mathf.Deg2Rad, 0));

                Grid[gridPosition.x, gridPosition.y, gridPosition.z] = new RoomInstance(instance, instance.GetComponent<RoomProfile>());
            }
        }
    }

    static Vector3 GridToWorld(Vector3Int grid)
    {
        return new Vector3(grid.x * GRID_SIZE, grid.y * ROOM_HEIGHT, grid.z * GRID_SIZE);
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