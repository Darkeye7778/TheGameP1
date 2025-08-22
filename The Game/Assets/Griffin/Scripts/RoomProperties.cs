using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public enum RoomType
{
    Hallway,
    Room
}
public enum RoomArchetype { 
    Hallway, 
    SmallRoom, 
    DoubleRoom }
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
        return (ExitDirection) ((uint)a + (uint)b % 4);
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
    
    public Vector2 Position;
    public ExitDirection Rotation;

    public Vector3 WorldPosition => new Vector3(MapGenerator.GRID_SIZE * Position.x, 0, MapGenerator.GRID_SIZE * Position.y);

    public static GridTransform operator*(GridTransform a, GridTransform b)
    {
        Vector3 rotated = Direction.ToQuaternion(a.Rotation) * new Vector3(b.Position.x, 0, b.Position.y);
        
        return new GridTransform(
            a.Position + new Vector2(rotated.x, rotated.z),
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
    public Vector2Int Size;
    public Vector2 Offset;
    public RoomType Type;

    [FormerlySerializedAs("EntranceDoor")] public bool HasEntranceDoor = false;
    public Connection[] ConnectionPoints;

    public Vector3 CollisionBox => new Vector3(Size.x, 1f / MapGenerator.GRID_SIZE, Size.y) * MapGenerator.GRID_SIZE * 2f;
    public Vector3 CollisionOffset => new Vector3(0f, 0f, CollisionBox.z / 2f) + new Vector3(Offset.x, 0f, Offset.y) * MapGenerator.GRID_SIZE;

    public RoomArchetype Archetype = RoomArchetype.SmallRoom;

    [Header("Connection Points • Prefab Markers (opt-in)")]
    [Tooltip("When ON, connection points are pulled from marker transforms on the Prefab instead of the serialized array.")]
    public bool UsePrefabConnectionMarkers = false;

    [Tooltip("If ExplicitMarkerRefs is empty, any child whose name starts with this is used (on the prefab).")]
    public string MarkerPrefix = "ConnectionPoint_";

    [Tooltip("Optional explicit marker references from the PREFAB asset (drag from Prefab Mode).")]
    public Transform[] ExplicitMarkerRefs;

    [Tooltip("Child name under the prefab used as the positional origin (e.g., DoorAnchor). If not found, falls back to prefab root.")]
    public string AnchorName = "DoorAnchor";

    public Connection[] GetResolvedConnectionPoints()
    {
        if (!UsePrefabConnectionMarkers || Prefab == null)
        {
            Debug.LogError($"The points of connections are {ConnectionPoints} on {Prefab.name}");
            return ConnectionPoints;
        }
        //Debug.Log($"The points of connection are {BuildConnectionsFromPrefab(Prefab)}");
        return ConnectionPoints;
    }

    Connection[] BuildConnectionsFromPrefab(GameObject prefab)
    {
        GameObject temp = null;
        try
        {
            temp = Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            var root = temp.transform;

            Transform anchor = FindChildByName(root, AnchorName);
            if (!anchor) anchor = root;

            var markers = new List<Transform>();
            if (ExplicitMarkerRefs != null && ExplicitMarkerRefs.Length > 0)
            {
                foreach (var assetT in ExplicitMarkerRefs)
                {
                    if (!assetT) continue;
                    string path = GetPathFromPrefabRoot(assetT);
                    var inst = FindByPath(root, path);
                    if (!inst) inst = FindChildByName(root, assetT.name);
                    if (inst) markers.Add(inst);
                }
            }
            else
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root) continue;
                    if (!string.IsNullOrEmpty(MarkerPrefix) && t.name.StartsWith(MarkerPrefix))
                        markers.Add(t);
                }
            }

            if (markers.Count == 0) return Array.Empty<Connection>();

            var result = new List<Connection>(markers.Count);

            Vector3 anchorLocalPos = root.InverseTransformPoint(anchor.position);

            float G = MapGenerator.GRID_SIZE;

            foreach (var m in markers)
            {
                Vector3 local = root.InverseTransformPoint(m.position);
                local -= anchorLocalPos;
                local.y = 0f;

                Vector2 posGrid = new Vector2(local.x / G, local.z / G);

                Vector3 fLocal = root.InverseTransformDirection(m.forward);
                fLocal.y = 0f;
                ExitDirection dir = QuantizeToCardinal(fLocal);

                var gt = new GridTransform(posGrid, dir);

                result.Add(new Connection
                {
                    Transform = gt,
                    Required = true,
                    HasDoor = true,
                    IsEntrance = false,
                    Odds = 1f
                });
            }

            return result.ToArray();
        }
        finally
        {
            if (temp != null)
            {
                if (Application.isPlaying) Destroy(temp);
                else DestroyImmediate(temp);
            }
        }
    }

    ExitDirection QuantizeToCardinal(Vector3 fLocal)
    {
        if (fLocal.sqrMagnitude < 1e-8f) return ExitDirection.North;
        fLocal.y = 0f;
        if (Mathf.Abs(fLocal.x) >= Mathf.Abs(fLocal.z))
            return (fLocal.x >= 0f) ? ExitDirection.East : ExitDirection.West;
        else
            return (fLocal.z >= 0f) ? ExitDirection.North : ExitDirection.South;
    }

    Transform FindChildByName(Transform root, string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    string GetPathFromPrefabRoot(Transform t)
    {
        if (!t) return null;
        var names = new List<string>();
        var c = t;
        while (c != null && c.parent != null)
        {
            names.Add(c.name);
            c = c.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    Transform FindByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        return root.Find(path);
    }
}
