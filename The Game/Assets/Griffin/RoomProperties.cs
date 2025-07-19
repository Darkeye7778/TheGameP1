using System;
using Unity.Mathematics;
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
    public GridTransform(Vector2Int position, ExitDirection direction)
    {
        Position = position;
        Rotation = direction;
    }
    
    public Vector2Int Position;
    public ExitDirection Rotation;

    public Vector3 WorldPosition => new Vector3(MapGenerator.GRID_SIZE * Position.x, 0, MapGenerator.GRID_SIZE * Position.y);

    public static GridTransform operator*(GridTransform a, GridTransform b)
    {
        Vector3 rotated = Direction.ToQuaternion(a.Rotation) * new Vector3(b.Position.x, 0, b.Position.y);
        
        return new GridTransform(
            a.Position + new Vector2Int((int) rotated.x, (int) rotated.z),
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
    public bool Required;
    public float Odds;
}

[CreateAssetMenu(fileName = "RoomProperties", menuName = "Scriptable Objects/RoomProperties")]
public class RoomProperties : ScriptableObject
{
    public GameObject Prefab;
    public Vector2Int Size;
    public RoomType Type;

    public Connection[] ConnectionPoints;
    
    public Vector3 CollisionBox => new Vector3(Size.x, 1f / MapGenerator.GRID_SIZE, Size.y) * MapGenerator.GRID_SIZE * 2f;
    public Vector3 CollisionOffset => new Vector3(0f, 0f, CollisionBox.z / 2f);
}
