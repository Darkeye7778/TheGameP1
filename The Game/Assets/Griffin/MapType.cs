using UnityEngine;

[CreateAssetMenu(fileName = "MapType", menuName = "Scriptable Objects/MapType")]
public class MapType : ScriptableObject
{
    public string Name;
    
    public RoomProperties[] RoomTypes;
    
    public RoomProperties[] HallwayStraight;
    public RoomProperties[] HallwayBend;
    public RoomProperties[] HallwayTIntersection;
    public RoomProperties[] HallwayIntersection;
    
    public int DoorOffset;
}
