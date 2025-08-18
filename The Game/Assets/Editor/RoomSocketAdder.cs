#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// Parametric sockets for rooms made of 10x10x5.3 m modules.
// Small = 1x1, Hallway = 2x1 or 1x2, Double = 2x2.
// Door frames are read from ConnectionProfile; sockets avoid door centers.
public static class RoomSocketAdder_BySizeAndDoors
{
    // --- Your base module (meters) ---
    const float BASE_W = 10f;   // local X
    const float BASE_D = 10f;   // local Z
    const float BASE_H = 5.3f;  // local Y

    // --- Placement tunables ---
    const float INSET_XZ = 0.30f; // keep off the shell
    const float WALL_Y_ABS = 1.35f; // wall socket height from floor
    const float CEIL_DOWN_ABS = 0.35f; // bring ceiling down from top
    const float DOOR_CLEAR_DIST = 1.20f; // planar skip distance from a door center along wall
    const float DOOR_HALF_WIDTH = 1.00f; // your door is 2.0 m wide

    // If a wall has a door at the module midpoint (e.g., Small 1x1),
    // place a pair of sockets flanking the door at this extra offset:
    const float FLANK_OFFSET = DOOR_HALF_WIDTH + 0.80f; // ~1.8 m from door center

    const bool REPLACE_EXISTING = true; // wipe old sockets first

    [MenuItem("Tools/Rooms/Add/Repair Sockets (by size & doors)")]
    public static void AddOrRepair()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var content = FindContentRoot(root.transform) ?? root.transform;

                if (REPLACE_EXISTING) RemoveAllPropSockets(content);

                var socketsParent = content.Find("Sockets");
                if (!socketsParent)
                {
                    socketsParent = new GameObject("Sockets").transform;
                    socketsParent.SetParent(content, false);
                }
                else if (REPLACE_EXISTING)
                {
                    var toDelete = new List<GameObject>();
                    foreach (Transform t in socketsParent) toDelete.Add(t.gameObject);
                    foreach (var go in toDelete) Object.DestroyImmediate(go);
                }

                // Bounds in content-local space
                var rends = content.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                {
                    Debug.LogWarning($"[Sockets] No renderers in {path} — skipped.");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    continue;
                }
                Bounds lb = LocalBoundsFromWorld(rends, content);

                // Derive module counts by rounding to nearest multiple
                float width = lb.size.x;
                float depth = lb.size.z;
                float height = lb.size.y;

                int nX = Mathf.Max(1, Mathf.RoundToInt(width / BASE_W));
                int nZ = Mathf.Max(1, Mathf.RoundToInt(depth / BASE_D));

                float modW = width / nX;
                float modD = depth / nZ;

                // Heights
                float floorY = lb.min.y;
                float ceilingY = lb.max.y - CEIL_DOWN_ABS; // pull down
                float wallY = floorY + WALL_Y_ABS;      // fixed eye height

                // Insets
                float minX = lb.min.x + INSET_XZ;
                float maxX = lb.max.x - INSET_XZ;
                float minZ = lb.min.z + INSET_XZ;
                float maxZ = lb.max.z - INSET_XZ;

                // Door centers (content-local)
                var doors = content.GetComponentsInChildren<ConnectionProfile>(true);
                var doorN = new List<float>(); // x along z=maxZ
                var doorS = new List<float>(); // x along z=minZ
                var doorE = new List<float>(); // z along x=maxX
                var doorW = new List<float>(); // z along x=minX

                // Classify each door to nearest wall plane
                foreach (var cp in doors)
                {
                    Vector3 p = content.InverseTransformPoint(cp.transform.position);
                    float dN = Mathf.Abs(p.z - maxZ);
                    float dS = Mathf.Abs(p.z - minZ);
                    float dE = Mathf.Abs(p.x - maxX);
                    float dW = Mathf.Abs(p.x - minX);
                    float m = Mathf.Min(Mathf.Min(dN, dS), Mathf.Min(dE, dW));

                    if (m == dN) doorN.Add(Mathf.Clamp(p.x, minX, maxX));
                    else if (m == dS) doorS.Add(Mathf.Clamp(p.x, minX, maxX));
                    else if (m == dE) doorE.Add(Mathf.Clamp(p.z, minZ, maxZ));
                    else doorW.Add(Mathf.Clamp(p.z, minZ, maxZ));
                }

                // Helpers
                void Sock(string name, Vector3 localPos, Vector3 forward, SocketType type, float clr = 0.35f)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(socketsParent, false);
                    go.transform.localPosition = localPos;
                    go.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
                    var ps = go.AddComponent<PropSocket>();
                    ps.Type = type;
                    ps.Clearance = clr;
                    ps.ForwardHint = Vector3.forward;
                }

                bool NearAny(List<float> doorList, float axisValue)
                {
                    if (doorList == null || doorList.Count == 0) return false;
                    foreach (var d in doorList)
                        if (Mathf.Abs(axisValue - d) <= DOOR_CLEAR_DIST) return true;
                    return false;
                }

                float ClampWall(float v, float min, float max) =>
                    Mathf.Clamp(v, min + INSET_XZ, max - INSET_XZ);

                // ---------- Place sockets ----------

                // Corners (floor)
                Vector3 c = lb.center;
                Sock("Socket_Corner_NE", new Vector3(maxX, floorY, maxZ), Vector3.Normalize(c - new Vector3(maxX, c.y, maxZ)), SocketType.Corner);
                Sock("Socket_Corner_NW", new Vector3(minX, floorY, maxZ), Vector3.Normalize(c - new Vector3(minX, c.y, maxZ)), SocketType.Corner);
                Sock("Socket_Corner_SE", new Vector3(maxX, floorY, minZ), Vector3.Normalize(c - new Vector3(maxX, c.y, minZ)), SocketType.Corner);
                Sock("Socket_Corner_SW", new Vector3(minX, floorY, minZ), Vector3.Normalize(c - new Vector3(minX, c.y, minZ)), SocketType.Corner);

                // CenterSmall per module (floor)
                for (int ix = 0; ix < nX; ix++)
                    for (int iz = 0; iz < nZ; iz++)
                    {
                        float cx = lb.min.x + (ix + 0.5f) * modW;
                        float cz = lb.min.z + (iz + 0.5f) * modD;
                        Sock($"Socket_Center_{ix}_{iz}", new Vector3(cx, floorY, cz), Vector3.forward, SocketType.CenterSmall);
                    }

                // CenterLarge at whole-room center for 2x2+ rooms
                if (nX >= 2 && nZ >= 2)
                    Sock("Socket_CenterLarge", new Vector3(lb.center.x, floorY, lb.center.z), Vector3.forward, SocketType.CenterLarge);

                // Wall sockets at each module midpoint along each wall (skip near doors; flank if needed)

                // NORTH (z=maxZ)
                {
                    var placed = 0;
                    for (int ix = 0; ix < nX; ix++)
                    {
                        float cx = lb.min.x + (ix + 0.5f) * modW;
                        if (!NearAny(doorN, cx))
                        {
                            Sock($"Socket_Wall_N_{ix}", new Vector3(cx, wallY, maxZ), Vector3.back, SocketType.Wall);
                            placed++;
                        }
                    }
                    // If Small 1x1 with a central door, place flanking pair
                    if (placed == 0 && doorN.Count > 0)
                    {
                        float d = doorN[0];
                        float a = ClampWall(d - FLANK_OFFSET, minX, maxX);
                        float b = ClampWall(d + FLANK_OFFSET, minX, maxX);
                        if (Mathf.Abs(a - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_N_L", new Vector3(a, wallY, maxZ), Vector3.back, SocketType.Wall);
                        if (Mathf.Abs(b - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_N_R", new Vector3(b, wallY, maxZ), Vector3.back, SocketType.Wall);
                    }
                }

                // SOUTH (z=minZ)
                {
                    var placed = 0;
                    for (int ix = 0; ix < nX; ix++)
                    {
                        float cx = lb.min.x + (ix + 0.5f) * modW;
                        if (!NearAny(doorS, cx))
                        {
                            Sock($"Socket_Wall_S_{ix}", new Vector3(cx, wallY, minZ), Vector3.forward, SocketType.Wall);
                            placed++;
                        }
                    }
                    if (placed == 0 && doorS.Count > 0)
                    {
                        float d = doorS[0];
                        float a = ClampWall(d - FLANK_OFFSET, minX, maxX);
                        float b = ClampWall(d + FLANK_OFFSET, minX, maxX);
                        if (Mathf.Abs(a - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_S_L", new Vector3(a, wallY, minZ), Vector3.forward, SocketType.Wall);
                        if (Mathf.Abs(b - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_S_R", new Vector3(b, wallY, minZ), Vector3.forward, SocketType.Wall);
                    }
                }

                // EAST (x=maxX)
                {
                    var placed = 0;
                    for (int iz = 0; iz < nZ; iz++)
                    {
                        float cz = lb.min.z + (iz + 0.5f) * modD;
                        if (!NearAny(doorE, cz))
                        {
                            Sock($"Socket_Wall_E_{iz}", new Vector3(maxX, wallY, cz), Vector3.left, SocketType.Wall);
                            placed++;
                        }
                    }
                    if (placed == 0 && doorE.Count > 0)
                    {
                        float d = doorE[0];
                        float a = ClampWall(d - FLANK_OFFSET, minZ, maxZ);
                        float b = ClampWall(d + FLANK_OFFSET, minZ, maxZ);
                        if (Mathf.Abs(a - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_E_L", new Vector3(maxX, wallY, a), Vector3.left, SocketType.Wall);
                        if (Mathf.Abs(b - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_E_R", new Vector3(maxX, wallY, b), Vector3.left, SocketType.Wall);
                    }
                }

                // WEST (x=minX)
                {
                    var placed = 0;
                    for (int iz = 0; iz < nZ; iz++)
                    {
                        float cz = lb.min.z + (iz + 0.5f) * modD;
                        if (!NearAny(doorW, cz))
                        {
                            Sock($"Socket_Wall_W_{iz}", new Vector3(minX, wallY, cz), Vector3.right, SocketType.Wall);
                            placed++;
                        }
                    }
                    if (placed == 0 && doorW.Count > 0)
                    {
                        float d = doorW[0];
                        float a = ClampWall(d - FLANK_OFFSET, minZ, maxZ);
                        float b = ClampWall(d + FLANK_OFFSET, minZ, maxZ);
                        if (Mathf.Abs(a - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_W_L", new Vector3(minX, wallY, a), Vector3.right, SocketType.Wall);
                        if (Mathf.Abs(b - d) > DOOR_HALF_WIDTH) Sock($"Socket_Wall_W_R", new Vector3(minX, wallY, b), Vector3.right, SocketType.Wall);
                    }
                }

                // Ceiling sockets — one per module center, pulled down
                for (int ix = 0; ix < nX; ix++)
                    for (int iz = 0; iz < nZ; iz++)
                    {
                        float cx = lb.min.x + (ix + 0.5f) * modW;
                        float cz = lb.min.z + (iz + 0.5f) * modD;
                        Sock($"Socket_Ceiling_{ix}_{iz}", new Vector3(cx, ceilingY, cz), Vector3.down, SocketType.Ceiling, 0.20f);
                    }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Sockets] {path}: modules {nX}x{nZ}, size=({width:F2},{depth:F2},{height:F2}) | doors N{doorN.Count}/S{doorS.Count}/E{doorE.Count}/W{doorW.Count}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    // ---------- helpers ----------
    static void RemoveAllPropSockets(Transform content)
    {
        var sockets = new List<PropSocket>(content.GetComponentsInChildren<PropSocket>(true));
        foreach (var s in sockets)
        {
            var go = s.gameObject;
            bool onlySocket = go.GetComponents<Component>().Length <= 2 && go.transform.childCount == 0;
            if (onlySocket) Object.DestroyImmediate(go);
            else Object.DestroyImmediate(s);
        }
        var container = content.Find("Sockets");
        if (container) foreach (Transform t in container) Object.DestroyImmediate(t.gameObject);
    }

    static Transform FindContentRoot(Transform root)
    {
        var m = root.Find("model");
        if (m) return m;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (string.Equals(c.name, "model", System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        if (root.GetComponentInChildren<Renderer>(true)) return root;
        if (root.childCount == 1) return root.GetChild(0);
        return root;
    }

    static Bounds LocalBoundsFromWorld(Renderer[] rends, Transform localSpace)
    {
        Bounds wb = rends[0].bounds;
        var lb = new Bounds(localSpace.InverseTransformPoint(wb.center), Vector3.zero);
        void Enc(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            Vector3[] pts = {
                new(min.x,min.y,min.z), new(max.x,min.y,min.z),
                new(min.x,max.y,min.z), new(max.x,max.y,min.z),
                new(min.x,min.y,max.z), new(max.x,min.y,max.z),
                new(min.x,max.y,max.z), new(max.x,max.y,max.z),
            };
            foreach (var p in pts) lb.Encapsulate(localSpace.InverseTransformPoint(p));
        }
        Enc(wb);
        for (int i = 1; i < rends.Length; i++) Enc(rends[i].bounds);
        return lb;
    }
}
#endif
