using System;
using UnityEngine;

public enum RoomType
{
    Hallway,
    Room,
}

public enum ExitDirection
{
    ZPositive,
    XPositive,
    ZNegative,
    XNegative,
    
    North = ZPositive,
    East = XPositive,
    South = ZNegative,
    West = XNegative
}

public class Direction
{
    static ExitDirection AddDirection(ExitDirection a, ExitDirection b)
    {
        return (ExitDirection) ((uint)a + (uint)b % 3);
    }

    static float GetDirection(ExitDirection direction)
    {
        return (float)direction * 90.0f;
    }
}

[Serializable]
public struct Connection
{
    public ExitDirection Direction;
    public Vector3Int Position;
}

[CreateAssetMenu(fileName = "RoomProperties", menuName = "Scriptable Objects/RoomProperties")]
public class RoomProperties : ScriptableObject
{
    public GameObject Prefab;
    public Vector3Int Size;
    public RoomType Type;

    public Connection[] ConnectionPoints;
}
