#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public static class SocketValidation
{
    const float INSET = 0.30f;
    const float WALL_Y = 1.35f;
    const float WALL_Y_TOL = 0.2f;
    const float CEIL_DOWN = 0.35f;
    const float EDGE_TOL = 0.25f;
    const float DOOR_CLEAR = 1.2f;

    [MenuItem("Tools/Rooms/Validate Sockets (selected prefabs)")]
    public static void Validate()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (!path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var content = root.transform;
                var rends = content.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) { Debug.LogWarning($"[{path}] no renderers"); continue; }

                Bounds lb = LocalBounds(rends, content);
                float floor = lb.min.y, ceil = lb.max.y;
                float minX = lb.min.x + INSET, maxX = lb.max.x - INSET;
                float minZ = lb.min.z + INSET, maxZ = lb.max.z - INSET;
                float expectCeilY = ceil - CEIL_DOWN;

                var doors = content.GetComponentsInChildren<ConnectionProfile>(true)
                                   .Select(cp => content.InverseTransformPoint(cp.transform.position))
                                   .ToList();

                int warn = 0;
                foreach (var s in content.GetComponentsInChildren<PropSocket>(true))
                {
                    var t = s.transform;
                    var p = t.localPosition;
                    var fwd = (t.rotation * Vector3.forward);

                    switch (s.Type)
                    {
                        case SocketType.Wall:
                            bool onN = Mathf.Abs(p.z - maxZ) <= EDGE_TOL;
                            bool onS = Mathf.Abs(p.z - minZ) <= EDGE_TOL;
                            bool onE = Mathf.Abs(p.x - maxX) <= EDGE_TOL;
                            bool onW = Mathf.Abs(p.x - minX) <= EDGE_TOL;
                            if (!(onN || onS || onE || onW))
                                Log($"Wall socket '{t.name}' not on any wall edge", ref warn, path);
                            if (Mathf.Abs(p.y - (floor + WALL_Y)) > WALL_Y_TOL)
                                Log($"Wall socket '{t.name}' y={p.y:F2} (expected ~{(floor + WALL_Y):F2})", ref warn, path);
                            // must face inward
                            Vector3 inward = onN ? Vector3.back : onS ? Vector3.forward : onE ? Vector3.left : Vector3.right;
                            if (Vector3.Dot(fwd, inward) < 0.8f)
                                Log($"Wall socket '{t.name}' not facing inward", ref warn, path);
                            break;

                        case SocketType.Ceiling:
                            if (Mathf.Abs(p.y - expectCeilY) > 0.15f)
                                Log($"Ceiling socket '{t.name}' y={p.y:F2} (expected ~{expectCeilY:F2})", ref warn, path);
                            if (Vector3.Dot(fwd, Vector3.down) < 0.8f)
                                Log($"Ceiling socket '{t.name}' not facing down", ref warn, path);
                            break;

                        case SocketType.Corner:
                            // near one of the four floor corners
                            bool nearCorner =
                                (Mathf.Abs(p.x - minX) <= EDGE_TOL || Mathf.Abs(p.x - maxX) <= EDGE_TOL) &&
                                (Mathf.Abs(p.z - minZ) <= EDGE_TOL || Mathf.Abs(p.z - maxZ) <= EDGE_TOL) &&
                                Mathf.Abs(p.y - floor) <= 0.1f;
                            if (!nearCorner)
                                Log($"Corner socket '{t.name}' not at a floor corner", ref warn, path);
                            break;
                    }

                    // door apron check (planar)
                    foreach (var d in doors)
                    {
                        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(d.x, d.z)) < DOOR_CLEAR)
                        {
                            Log($"Socket '{t.name}' too close to a door (~{DOOR_CLEAR}m)", ref warn, path);
                            break;
                        }
                    }
                }

                Debug.Log($"[{path}] socket validation done {(warn == 0 ? "good" : $"with {warn} warnings")}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Log(string msg, ref int warn, string path) { warn++; Debug.LogWarning($"[{path}] {msg}"); }
        static Bounds LocalBounds(Renderer[] rends, Transform space)
        {
            var lb = new Bounds(space.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
            foreach (var r in rends)
            {
                var b = r.bounds;
                Vector3[] pts = {
                    b.min, new Vector3(b.max.x,b.min.y,b.min.z),
                    new Vector3(b.min.x,b.max.y,b.min.z), new Vector3(b.max.x,b.max.y,b.min.z),
                    new Vector3(b.min.x,b.min.y,b.max.z), new Vector3(b.max.x,b.min.y,b.max.z),
                    new Vector3(b.min.x,b.max.y,b.max.z), b.max
                };
                foreach (var p in pts) lb.Encapsulate(space.InverseTransformPoint(p));
            }
            return lb;
        }
    }
}
#endif
