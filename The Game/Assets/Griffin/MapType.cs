using UnityEngine;

[CreateAssetMenu(fileName = "MapType", menuName = "Scriptable Objects/MapType")]
public class MapType : ScriptableObject
{
    public string Name;
    public RoomProperties[] Cells;
    public GameObject[] OpenDoors, ClosedDoors;
}
