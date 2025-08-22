using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public enum RoomType
{
    Hallway,
    Room,
}

public enum ExitDirection
{
    ZPositive, // 0
    XPositive, // 1
    ZNegative, // 2
    XNegative, // 3
    
    North = ZPositive,
    East = XPositive,
    South = ZNegative,
    West = XNegative
}

public static class Direction
{
    public static ExitDirection AddDirection(ExitDirection a, ExitDirection b)
    {
        return (ExitDirection) ((uint)a + (uint)b % 3);
    }

    public static float GetDirection(ExitDirection direction)
    {
        return (float)direction * 90.0f;
    }

    public static Quaternion ToQuaternion(ExitDirection direction)
    {
        return Quaternion.Euler(0, GetDirection(direction), 0.0f);
    }

    public static bool DoorsAlign(ExitDirection a, ExitDirection b)
    {
        return Opposite(a) == b;
    }

    public static ExitDirection Opposite(ExitDirection a)
    {
        return AddDirection(a, (ExitDirection)2);
    }

    public static ExitDirection RotateToSouth(ExitDirection a)
    {
        return a switch
        {
            ExitDirection.North => ExitDirection.South,
            ExitDirection.East => ExitDirection.East,
            ExitDirection.South => ExitDirection.North,
            ExitDirection.West => ExitDirection.West,
            _ => 0
        };
    }
}

[Serializable]
public struct GridTransform
{
    public GridTransform(Vector2 position, ExitDirection direction)
    {
        Position = position;
        Rotation = direction;
    }
    
    public Vector3 Position;
    public ExitDirection Rotation;

    public Vector3 WorldPosition => new Vector3(MapGenerator.GRID_SIZE * Position.x, MapGenerator.ROOM_HEIGHT * Position.y, MapGenerator.GRID_SIZE * Position.z);

    public static GridTransform operator*(GridTransform a, GridTransform b)
    {
        Vector3 rotated = Direction.ToQuaternion(a.Rotation) * b.Position;
        
        return new GridTransform(
            a.Position + rotated,
            Direction.AddDirection(a.Rotation, b.Rotation)
        );
    }

    public GridTransform Inverse()
    {
        return new GridTransform(-Position, Direction.RotateToSouth(Rotation));
    }
}

[Serializable]
public struct Connection
{
    public GridTransform Transform;
    public bool Required, HasDoor;
    [NonSerialized] public bool IsEntrance;
    public float Odds;
}

[CreateAssetMenu(fileName = "RoomProperties", menuName = "Scriptable Objects/RoomProperties")]
public class RoomProperties : ScriptableObject
{
    public GameObject Prefab;
    public Vector3Int Size;
    public Vector2 Offset;
    public RoomType Type;

    [FormerlySerializedAs("EntranceDoor")] public bool HasEntranceDoor = false;
    public Connection[] ConnectionPoints;
    
    public Vector3 CollisionBox => new Vector3(Size.x * MapGenerator.GRID_SIZE * 2, Size.y * MapGenerator.ROOM_HEIGHT, Size.z * MapGenerator.GRID_SIZE * 2);
    public Vector3 CollisionOffset => new Vector3(0f, CollisionBox.y / 2f, CollisionBox.z / 2f) + new Vector3(Offset.x, 0f, Offset.y) * MapGenerator.GRID_SIZE;
}
