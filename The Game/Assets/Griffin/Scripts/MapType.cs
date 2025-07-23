using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MapType", menuName = "Scriptable Objects/MapType")]
public class MapType : ScriptableObject
{
    public string Name;
    
    [Range(0f, 1f)] public float RoomOdds;
    [Range(0f, 1f)] public float ConnectRoomsOdds;
    
    [Header("Rooms")]
    public RoomProperties[] StartingRooms;
    public RoomProperties[] Hallways;
    public RoomProperties[] Rooms;
    public GameObject[] OpenDoors, ClosedDoors;

    [Header("Spawns")] 
    public GameObject[] Enemies;
    public GameObject[] Hostages;
    public GameObject[] Traps;

    public GameObject Player;
}
