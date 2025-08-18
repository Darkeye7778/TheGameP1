// Editor/RoomsColliderBakerWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RoomsColliderBakerWindow : EditorWindow
{
    // Target parenting
    string shellName = "Shell";                 // child under which colliders should live
    bool includeInactive = true;

    // Layers
    string environmentLayerName = "Environment"; // must collide with Player in Physics matrix

    // Grid / dimensions
    bool autoGridFromMapGenerator = true;
    float gridSize = 1f;
    float wallHeight = 3f;
    float wallThickness = 0.2f;

    // Doors (uniform; extend per-connection if needed)
    float doorWidth = 1.0f;
    float doorHeight = 2.1f;
    float doorSillY = 0f;

    // Actions
    bool mode_AddCollidersToExistingShellMeshes = true;  // add MeshColliders to MeshFilters under Shell
    bool mode_BakeColliderOnlyWallsAndFloor = false; // generate collider-only meshes (no renderers) under Shell
    bool cleanOldBakedFirst = true;                      // remove children starting with "BakedCol_"

    [MenuItem("Tools/Rooms/Collider Baker (Editor-Only)")]
    static void Open() => GetWindow<RoomsColliderBakerWindow>("Rooms Collider Baker");

    // Floors
    bool floorAsBoxCollider = true;      // use BoxCollider instead of a thin MeshCollider
    float floorBoxThickness = 0.1f;      // thickness in meters

    void OnGUI()
    {
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
        shellName = EditorGUILayout.TextField(new GUIContent("Shell Child Name", "Colliders will be created under this child transform"), shellName);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
        environmentLayerName = EditorGUILayout.TextField(new GUIContent("Environment Layer", "Must collide with Player in Project Settings -> Physics"), environmentLayerName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid / Dimensions", EditorStyles.boldLabel);
        autoGridFromMapGenerator = EditorGUILayout.Toggle(new GUIContent("Auto grid from MapGenerator.GRID_SIZE"), autoGridFromMapGenerator);
        gridSize = EditorGUILayout.FloatField("Grid Size (fallback)", gridSize);
        wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
        wallThickness = EditorGUILayout.FloatField("Wall Thickness", wallThickness);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Door Openings", EditorStyles.boldLabel);
        doorWidth = EditorGUILayout.FloatField("Door Width", doorWidth);
        doorHeight = EditorGUILayout.FloatField("Door Height", doorHeight);
        doorSillY = EditorGUILayout.FloatField("Door Sill Y", doorSillY);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        mode_AddCollidersToExistingShellMeshes = EditorGUILayout.ToggleLeft("Add MeshColliders to EXISTING Shell meshes", mode_AddCollidersToExistingShellMeshes);
        mode_BakeColliderOnlyWallsAndFloor = EditorGUILayout.ToggleLeft("BAKE collider-only Floor & Walls (with door gaps) under Shell", mode_BakeColliderOnlyWallsAndFloor);
        cleanOldBakedFirst = EditorGUILayout.ToggleLeft("Clean previous baked (children named 'BakedCol_*')", cleanOldBakedFirst);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Floor Collider", EditorStyles.boldLabel);
        floorAsBoxCollider = EditorGUILayout.ToggleLeft("Bake floor as BoxCollider", floorAsBoxCollider);
        floorBoxThickness = EditorGUILayout.FloatField("Floor Box Thickness", floorBoxThickness);

        EditorGUILayout.Space();
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS"))
            ProcessSelection(prefabAssets: true);

        if (GUILayout.Button("Process SELECTED SCENE OBJECTS"))
            ProcessSelection(prefabAssets: false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This runs in the Editor only. It modifies prefab contents directly (via PrefabUtility), " +
            "adds MeshColliders to Shell or generates collider-only geometry under Shell. No runtime scripts are added.",
            MessageType.Info);
    }

    // Core

    void ProcessSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("Nothing selected."); return; }

        int changed = 0, total = 0;

        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (!root) continue;

                if (ProcessRoot(root)) { PrefabUtility.SaveAsPrefabAsset(root, path); changed++; }
                PrefabUtility.UnloadPrefabContents(root);
                total++;
            }
            else if (obj is GameObject go)
            {
                if (ProcessRoot(go)) changed++;
                total++;
            }
        }

        Debug.Log($"[RoomsColliderBaker] Processed {total} object(s); modified {changed}.");
    }

    bool ProcessRoot(GameObject root)
    {
        var rp = root.GetComponent<RoomProfile>();
        if (!rp || !rp.Properties)
        {
            Debug.LogWarning($"[RoomsColliderBaker] {root.name}: missing RoomProfile/Properties.");
            return false;
        }

        if (autoGridFromMapGenerator)
            TryReadGridSizeFromMapGenerator(ref gridSize);

        var shell = FindChildByName(root.transform, shellName, includeInactive);
        if (!shell)
        {
            Debug.LogWarning($"[RoomsColliderBaker] {root.name}: Shell '{shellName}' not found.");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Rooms Collider Baker");

        if (cleanOldBakedFirst)
            DeleteChildrenByPrefix(shell, "BakedCol_");

        int env = LayerMask.NameToLayer(environmentLayerName);
        if (env < 0) env = 0; // Default

        bool modified = false;

        if (mode_AddCollidersToExistingShellMeshes)
        {
            foreach (var mf in shell.GetComponentsInChildren<MeshFilter>(includeInactive))
            {
                var go = mf.gameObject;
                var mc = go.GetComponent<MeshCollider>();
                if (!mc) { mc = go.AddComponent<MeshCollider>(); modified = true; }
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                go.layer = env;
                EditorUtility.SetDirty(go);
            }
        }

        if (mode_BakeColliderOnlyWallsAndFloor)
        {
            var size = rp.Properties.Size; // (x,y) cells
            float halfW = size.x * gridSize;
            float depth = 2f * size.y * gridSize;

            // floor (single quad)
            bool floorMod;
            if (floorAsBoxCollider)
            {
                floorMod = BuildFloorAsBox(shell, "BakedCol_FloorBox", halfW, depth, env);
            }
            else
            {
                floorMod = BuildQuad(shell, "BakedCol_Floor",
                    new Vector3(-halfW, 0f, 0f),
                    new Vector3(+halfW, 0f, 0f),
                    new Vector3(+halfW, 0f, depth),
                    new Vector3(-halfW, 0f, depth),
                    Vector3.up, env, true);
            }
            modified |= floorMod;

            // walls (split by openings)
            var south = OpeningsAlongX(rp, ExitDirection.South, halfW);
            var north = OpeningsAlongX(rp, ExitDirection.North, halfW);
            var east = OpeningsAlongZ(rp, ExitDirection.East, depth);
            var west = OpeningsAlongZ(rp, ExitDirection.West, depth);

            // South (z=0, normal -Z)
            modified |= BuildWallSegmentsX(shell, -halfW, +halfW, 0f, Vector3.back, south, env, "BakedCol_Wall_S");
            // North (z=depth, normal +Z)
            modified |= BuildWallSegmentsX(shell, -halfW, +halfW, depth, Vector3.forward, north, env, "BakedCol_Wall_N");
            // East (x=+halfW, normal +X)
            modified |= BuildWallSegmentsZ(shell, 0f, depth, +halfW, Vector3.right, east, env, "BakedCol_Wall_E");
            // West (x=-halfW, normal -X)
            modified |= BuildWallSegmentsZ(shell, 0f, depth, -halfW, Vector3.left, west, env, "BakedCol_Wall_W");
        }

        if (modified)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        return modified;
    }

    // Helpers

    void TryReadGridSizeFromMapGenerator(ref float g)
    {
        var mg = System.AppDomain.CurrentDomain.GetAssemblies()
                 .SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "MapGenerator");
        if (mg != null)
        {
            var f = mg.GetField("GRID_SIZE", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (f != null && f.FieldType == typeof(float))
            {
                float v = (float)f.GetValue(null);
                if (v > 0.0001f) g = v;
            }
        }
    }

    Transform FindChildByName(Transform root, string name, bool includeInactiveArg)
    {
        var direct = root.Find(name);
        if (direct) return direct;
        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (t.name == name) return t;
        return null;
    }

    void DeleteChildrenByPrefix(Transform parent, string prefix)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var ch = parent.GetChild(i);
            if (ch.name.StartsWith(prefix))
            {
                Undo.DestroyObjectImmediate(ch.gameObject);
            }
        }
    }

    struct Interval { public float a, b; }

    List<Interval> OpeningsAlongX(RoomProfile rp, ExitDirection side, float halfW)
    {
        var list = new List<Interval>();
        var cps = rp.Properties.ConnectionPoints;
        if (cps != null)
        {
            foreach (var c in cps)
            {
                if (!c.HasDoor) continue;
                if (c.Transform.Rotation != side) continue;
                float cx = c.Transform.Position.x * gridSize;
                float hw = doorWidth * 0.5f;
                list.Add(new Interval { a = Mathf.Clamp(cx - hw, -halfW, +halfW), b = Mathf.Clamp(cx + hw, -halfW, +halfW) });
            }
        }
        return MergeIntervals(list, 0.005f);
    }

    List<Interval> OpeningsAlongZ(RoomProfile rp, ExitDirection side, float depth)
    {
        var list = new List<Interval>();
        var cps = rp.Properties.ConnectionPoints;
        if (cps != null)
        {
            foreach (var c in cps)
            {
                if (!c.HasDoor) continue;
                if (c.Transform.Rotation != side) continue;
                float cz = c.Transform.Position.y * gridSize;
                float hz = doorWidth * 0.5f;
                list.Add(new Interval { a = Mathf.Clamp(cz - hz, 0f, depth), b = Mathf.Clamp(cz + hz, 0f, depth) });
            }
        }
        return MergeIntervals(list, 0.005f);
    }

    List<Interval> MergeIntervals(List<Interval> list, float eps)
    {
        if (list.Count <= 1) return list;
        list.Sort((u, v) => u.a.CompareTo(v.a));
        var outList = new List<Interval>();
        var cur = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            var n = list[i];
            if (n.a <= cur.b + eps) cur.b = Mathf.Max(cur.b, n.b);
            else { outList.Add(cur); cur = n; }
        }
        outList.Add(cur);
        return outList;
    }

    // Build a single quad and attach MeshCollider (no renderer)
    bool BuildQuad(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int envLayer, bool collidersOnly)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var mf = go.AddComponent<MeshFilter>();
        var mesh = new Mesh { name = name };
        var verts = new List<Vector3> { a, b, c, d };
        var norms = new List<Vector3> { normal, normal, normal, normal };
        var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        var tris = new List<int> { 0, 1, 2, 0, 2, 3 };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false;
        go.layer = envLayer;

        EditorUtility.SetDirty(go);
        return true;
    }

    // Walls along X (z = plane), splitting by openings
    bool BuildWallSegmentsX(Transform parent, float xMin, float xMax, float zPlane, Vector3 normal, List<Interval> gaps, int envLayer, string prefix)
    {
        bool mod = false;
        float y0 = doorSillY, y1 = doorHeight, y2 = wallHeight;
        float cursor = xMin;

        foreach (var iv in gaps)
        {
            if (iv.a > cursor)
                mod |= BuildWallStripX(parent, cursor, iv.a, y0, y2, zPlane, normal, envLayer, prefix + "_Seg");

            // header over the door (doorHeight..wallHeight)
            mod |= BuildWallStripX(parent, iv.a, iv.b, y1, y2, zPlane, normal, envLayer, prefix + "_Hdr");
            cursor = Mathf.Max(cursor, iv.b);
        }
        if (cursor < xMax)
            mod |= BuildWallStripX(parent, cursor, xMax, y0, y2, zPlane, normal, envLayer, prefix + "_Tail");
        return mod;
    }

    // Walls along Z (x = plane)
    bool BuildWallSegmentsZ(Transform parent, float zMin, float zMax, float xPlane, Vector3 normal, List<Interval> gaps, int envLayer, string prefix)
    {
        bool mod = false;
        float y0 = doorSillY, y1 = doorHeight, y2 = wallHeight;
        float cursor = zMin;

        foreach (var iv in gaps)
        {
            if (iv.a > cursor)
                mod |= BuildWallStripZ(parent, cursor, iv.a, y0, y2, xPlane, normal, envLayer, prefix + "_Seg");

            mod |= BuildWallStripZ(parent, iv.a, iv.b, y1, y2, xPlane, normal, envLayer, prefix + "_Hdr");
            cursor = Mathf.Max(cursor, iv.b);
        }
        if (cursor < zMax)
            mod |= BuildWallStripZ(parent, cursor, zMax, y0, y2, xPlane, normal, envLayer, prefix + "_Tail");
        return mod;
    }

    // Extruded strip (X-span)
    bool BuildWallStripX(Transform parent, float x0, float x1, float y0, float y1, float z, Vector3 normal, int envLayer, string baseName)
    {
        if (x1 - x0 <= 0.001f || y1 - y0 <= 0.001f) return false;
        var inward = -normal * wallThickness;
        bool m = false;

        m |= BuildQuad(parent, baseName + "_Face",
            new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
            normal, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Back",
            new Vector3(x0, y0, z) + inward, new Vector3(x1, y0, z) + inward, new Vector3(x1, y1, z) + inward, new Vector3(x0, y1, z) + inward,
            -normal, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Top",
            new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y1, z) + inward, new Vector3(x0, y1, z) + inward,
            Vector3.up, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Left",
            new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x0, y1, z) + inward, new Vector3(x0, y0, z) + inward,
            Vector3.left, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Right",
            new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x1, y1, z) + inward, new Vector3(x1, y0, z) + inward,
            Vector3.right, envLayer, true);

        return m;
    }

    // Extruded strip (Z-span)
    bool BuildWallStripZ(Transform parent, float z0, float z1, float y0, float y1, float x, Vector3 normal, int envLayer, string baseName)
    {
        if (z1 - z0 <= 0.001f || y1 - y0 <= 0.001f) return false;
        var inward = -normal * wallThickness;
        bool m = false;

        m |= BuildQuad(parent, baseName + "_Face",
            new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
            normal, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Back",
            new Vector3(x, y0, z0) + inward, new Vector3(x, y0, z1) + inward, new Vector3(x, y1, z1) + inward, new Vector3(x, y1, z0) + inward,
            -normal, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Top",
            new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(x, y1, z1) + inward, new Vector3(x, y1, z0) + inward,
            Vector3.up, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Left",
            new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z0) + inward, new Vector3(x, y0, z0) + inward,
            Vector3.back, envLayer, true);

        m |= BuildQuad(parent, baseName + "_Right",
            new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z1) + inward, new Vector3(x, y0, z1) + inward,
            Vector3.forward, envLayer, true);

        return m;
    }

    bool BuildFloorAsBox(Transform parent, string name, float halfW, float depth, int envLayer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(2f * halfW, floorBoxThickness, depth);
        bc.center = new Vector3(0f, floorBoxThickness * 0.5f, depth * 0.5f);
        go.layer = envLayer;
        EditorUtility.SetDirty(go);
        return true;
    }
}
#endif
