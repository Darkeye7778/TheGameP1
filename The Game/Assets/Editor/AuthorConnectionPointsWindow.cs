// Editor/AuthorConnectionPointsWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AuthorConnectionPointsWindow : EditorWindow
{
    // ----- Marker creation settings -----
    string doorAnchorFilter = "DoorAnchor"; // any child whose name contains this will be used as a door reference
    string floorChildName = "Floor";        // where we compute "top"
    string markerPrefix = "ConnectionPoint_";
    bool includeInactive = true;
    bool preferDirectChildren = false;
    bool matchCase = false;

    // offsets when dropping markers
    float upOffset = 0f;      // along Floor local +Y
    float forwardOffset = 0f; // along anchor local +Z
    float sideOffset = 0f;    // along anchor local +X

    // ----- Baking settings -----
    bool setRequired = true;
    bool setHasDoor = true;
    float defaultOdds = 1f;

    [MenuItem("Tools/Rooms/Author Connection Points")]
    static void Open() => GetWindow<AuthorConnectionPointsWindow>("Author Connection Points");

    void OnGUI()
    {
        EditorGUILayout.LabelField("1) Create/Refresh Markers on Prefabs", EditorStyles.boldLabel);
        doorAnchorFilter = EditorGUILayout.TextField("Door Anchor Name Filter", doorAnchorFilter);
        floorChildName = EditorGUILayout.TextField("Floor Child Name", floorChildName);
        markerPrefix = EditorGUILayout.TextField("Marker Name Prefix", markerPrefix);

        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        preferDirectChildren = EditorGUILayout.Toggle("Prefer Direct Children", preferDirectChildren);
        matchCase = EditorGUILayout.Toggle("Match Case", matchCase);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Marker Offsets (local)", EditorStyles.boldLabel);
        upOffset = EditorGUILayout.FloatField("Up (+Y of Floor)", upOffset);
        forwardOffset = EditorGUILayout.FloatField("Forward (+Z of Anchor)", forwardOffset);
        sideOffset = EditorGUILayout.FloatField("Side (+X of Anchor)", sideOffset);

        if (GUILayout.Button("Create/Refresh markers from DoorAnchors (SELECTED PREFAB ASSETS)"))
        {
            ProcessPrefabsSelection(CreateOrRefreshMarkersOnPrefab);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2) Bake Markers into RoomProperties", EditorStyles.boldLabel);
        setRequired = EditorGUILayout.Toggle("Set Required=true", setRequired);
        setHasDoor = EditorGUILayout.Toggle("Set HasDoor=true", setHasDoor);
        defaultOdds = EditorGUILayout.Slider("Default Odds", defaultOdds, 0f, 1f);

        if (GUILayout.Button("Bake markers into RoomProperties (SELECTED RoomProperties assets)"))
        {
            BakeMarkersIntoRoomProperties();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Workflow:\n" +
            " - Select prefab assets and click 'Create/Refresh markers...' to drop ConnectionPoint_* under the prefab root.\n" +
            " - Select RoomProperties assets and click 'Bake...' to read those markers from the prefab in RoomProperties.Prefab\n" +
            "   and overwrite ConnectionPoints with the computed grid offsets and directions.",
            MessageType.Info);
    }

    // ====== step 1: create markers on prefab from DoorAnchors ======

    void ProcessPrefabsSelection(System.Func<GameObject, bool> perPrefab)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[AuthorCP] Nothing selected.");
            return;
        }

        int total = 0, modified = 0;

        foreach (var obj in objs)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            Undo.RegisterFullObjectHierarchyUndo(root, "Author Connection Points");
            bool did = perPrefab(root);
            if (did)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                modified++;
            }
            PrefabUtility.UnloadPrefabContents(root);
            total++;
        }

        Debug.Log($"[AuthorCP] Processed {total} prefab(s); modified {modified}.");
    }

    bool CreateOrRefreshMarkersOnPrefab(GameObject root)
    {
        var floor = FindChild(root.transform, floorChildName, includeInactive, preferDirectChildren);
        if (!floor)
        {
            Debug.LogWarning($"[AuthorCP] {root.name}: Floor '{floorChildName}' not found.");
            return false;
        }

        // Find all door anchors
        var anchors = FindChildrenByFilter(root.transform, doorAnchorFilter, includeInactive, preferDirectChildren, matchCase);
        if (anchors.Count == 0)
        {
            Debug.LogWarning($"[AuthorCP] {root.name}: No door anchors matching '{doorAnchorFilter}'.");
            return false;
        }

        // Compute floor "top" Y in world
        float floorTopY;
        if (!TryGetFloorTopWorldY(floor, out floorTopY))
        {
            // fallback: pivot Y
            floorTopY = floor.position.y;
        }

        int made = 0;
        foreach (var a in anchors)
        {
            // place marker at anchor XZ, floor top Y, with optional offsets
            Vector3 worldPos = a.position;
            worldPos.y = floorTopY;

            // offsets: in ANCHOR local space
            worldPos += a.right * sideOffset;
            worldPos += a.forward * forwardOffset;

            // up offset: along FLOOR local up
            worldPos += floor.up * upOffset;

            // name for marker
            string markerName = NextUniqueChildName(root.transform, markerPrefix + a.name);

            // find existing marker under root that matches prefix+anchor
            var existing = FindChild(root.transform, markerName, true, false);
            Transform m;
            if (existing) m = existing;
            else
            {
                var go = new GameObject(markerName);
                go.transform.SetParent(root.transform, false);
                m = go.transform;
            }

            m.position = worldPos;

            // orient marker to point OUT through the doorway: use anchor forward projected to XZ of room
            Vector3 fwdWorld = a.forward;
            fwdWorld.y = 0f;
            if (fwdWorld.sqrMagnitude < 1e-6f) fwdWorld = root.transform.forward;
            m.rotation = Quaternion.LookRotation(fwdWorld.normalized, Vector3.up);

            EditorUtility.SetDirty(m.gameObject);
            made++;
        }

        EditorUtility.SetDirty(root);
        return made > 0;
    }

    bool TryGetFloorTopWorldY(Transform floor, out float yTop)
    {
        var bc = floor.GetComponent<BoxCollider>();
        if (bc)
        {
            var localTop = bc.center + new Vector3(0f, bc.size.y * 0.5f, 0f);
            yTop = floor.TransformPoint(localTop).y;
            return true;
        }

        var mf = floor.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            var b = mf.sharedMesh.bounds;
            var localTop = b.center + Vector3.up * b.extents.y;
            yTop = floor.TransformPoint(localTop).y;
            return true;
        }

        var rends = floor.GetComponentsInChildren<Renderer>(includeInactive);
        if (rends.Length > 0)
        {
            Bounds w = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) w.Encapsulate(rends[i].bounds);
            yTop = w.center.y + w.extents.y;
            return true;
        }

        yTop = floor.position.y;
        return false;
    }

    // ====== step 2: bake markers into RoomProperties ======

    void BakeMarkersIntoRoomProperties()
    {
        var assets = Selection.objects;
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("[AuthorCP] Select one or more RoomProperties assets.");
            return;
        }

        int total = 0, modified = 0;

        foreach (var obj in assets)
        {
            var roomProps = obj as RoomProperties;
            if (!roomProps) continue;

            if (!roomProps.Prefab)
            {
                Debug.LogWarning($"[AuthorCP] {roomProps.name}: RoomProperties.Prefab is not assigned.");
                continue;
            }

            var path = AssetDatabase.GetAssetPath(roomProps.Prefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[AuthorCP] {roomProps.name}: could not resolve prefab path.");
                continue;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            // collect markers
            var markers = root.transform.GetComponentsInChildren<Transform>(includeInactive)
                           .Where(t => t != root.transform && t.name.StartsWith(markerPrefix))
                           .ToList();

            if (markers.Count == 0)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning($"[AuthorCP] {roomProps.name}: no markers named '{markerPrefix}*' found in prefab.");
                continue;
            }

            // build Connection[] from markers
            var conns = new List<Connection>(markers.Count);
            foreach (var m in markers)
            {
                // local pos relative to room root
                Vector3 local = root.transform.InverseTransformPoint(m.position);
                local.y = 0f;

                // convert to grid coordinates used by GridTransform.WorldPosition (XZ * GRID_SIZE)
                float G = MapGenerator.GRID_SIZE; // constant grid size used throughout generation
                Vector2 posGrid = new Vector2(local.x / G, local.z / G); // GridTransform.Position (Vector2) -> X,Z in cells  :contentReference[oaicite:3]{index=3}

                // get local forward to quantize into ExitDirection (N/E/S/W)
                Vector3 fLocal = root.transform.InverseTransformDirection(m.forward);
                fLocal.y = 0f;
                ExitDirection dir = QuantizeToCardinal(fLocal);

                var gt = new GridTransform(posGrid, dir);
                Connection c = new Connection
                {
                    Transform = gt,
                    Required = setRequired,
                    HasDoor = setHasDoor,
                    IsEntrance = false,
                    Odds = Mathf.Clamp01(defaultOdds)
                };
                conns.Add(c);
            }

            // write back
            Undo.RecordObject(roomProps, "Bake ConnectionPoints");
            roomProps.ConnectionPoints = conns.ToArray();
            EditorUtility.SetDirty(roomProps);
            AssetDatabase.SaveAssets();

            PrefabUtility.UnloadPrefabContents(root);

            modified++;
            total++;
        }

        Debug.Log($"[AuthorCP] Baked {modified}/{total} RoomProperties asset(s).");
    }

    // Quantize local forward to the nearest axis (+Z,+X,-Z,-X) -> North/East/South/West
    ExitDirection QuantizeToCardinal(Vector3 fLocal)
    {
        // North = +Z, East = +X, South = -Z, West = -X  (see your ExitDirection enum)  :contentReference[oaicite:4]{index=4}
        if (Mathf.Abs(fLocal.x) >= Mathf.Abs(fLocal.z))
            return fLocal.x >= 0f ? ExitDirection.East : ExitDirection.West;
        else
            return fLocal.z >= 0f ? ExitDirection.North : ExitDirection.South;
    }

    // ----- helpers -----
    Transform FindChild(Transform root, string name, bool includeInactiveArg, bool preferDirect)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (preferDirect)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                if (ch.name == name) return ch;
            }
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (t.name == name) return t;

        return null;
    }

    List<Transform> FindChildrenByFilter(Transform root, string filter, bool includeInactiveArg, bool preferDirectArg, bool matchCaseArg)
    {
        var list = new List<Transform>();
        if (string.IsNullOrEmpty(filter)) return list;

        var comps = root.GetComponentsInChildren<Transform>(includeInactiveArg);
        for (int i = 0; i < comps.Length; i++)
        {
            var t = comps[i];
            if (t == root) continue;
            string hay = matchCaseArg ? t.name : t.name.ToLowerInvariant();
            string needle = matchCaseArg ? filter : filter.ToLowerInvariant();

            if (preferDirectArg && t.parent == root && hay.Contains(needle))
                list.Add(t);
            else if (!preferDirectArg && hay.Contains(needle))
                list.Add(t);
        }
        return list;
    }

    string NextUniqueChildName(Transform root, string baseName)
    {
        string name = baseName;
        int idx = 1;
        while (root.Find(name) != null)
        {
            idx++;
            name = baseName + "_" + idx;
        }
        return name;
    }
}
#endif
