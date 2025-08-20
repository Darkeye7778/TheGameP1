// Editor/CP_FromSockets_SnapAndBakeByName.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class CP_FromSockets_SnapAndBakeByName : EditorWindow
{
    // -------- Socket selection (by prefix) --------
    string socketPrefix = "Socket";     // sockets must start with this (case sensitive or not)
    bool matchCase = false;
    bool includeInactive = true;
    bool preferDirectChildren = false;

    // -------- Floor / marker placement --------
    string floorChildName = "Floor";
    string markerNamePrefix = "ConnectionPoint_";  // markers will be named ConnectionPoint_* for easy finding
    string markerTag = "ConnectionPoint";          // optional: tag to apply to markers (must already exist)
    bool applyMarkerTag = false;                 // set true if you have created the tag in Project Settings

    float insideMargin = 0.02f; // snap inside this far from the wall plane (meters)
    float upOffset = 0.00f; // along Floor local +Y

    // -------- Baking --------
    bool useNamePrefixWhenBaking = true;
    bool useTagWhenBaking = true;

    bool autoAssignPrefabByName = true; // if RoomProperties.Prefab is null, find prefab with same name
    bool setRequired = true;
    bool setHasDoor = true;
    float defaultOdds = 1f;

    [MenuItem("Tools/Rooms/Connection Points: From Sockets (Snap & Bake by Name)")]
    static void Open() => GetWindow<CP_FromSockets_SnapAndBakeByName>("CP From Sockets");

    void OnGUI()
    {
        EditorGUILayout.LabelField("1) Create/Refresh ConnectionPoint_* Markers (Prefab Assets)", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Socket picking (by prefix):", EditorStyles.miniBoldLabel);
        socketPrefix = EditorGUILayout.TextField("Socket Prefix", socketPrefix);
        matchCase = EditorGUILayout.Toggle("Match Case", matchCase);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        preferDirectChildren = EditorGUILayout.Toggle("Prefer Direct Children", preferDirectChildren);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Floor / Marker placement:", EditorStyles.miniBoldLabel);
        floorChildName = EditorGUILayout.TextField("Floor Child Name", floorChildName);
        markerNamePrefix = EditorGUILayout.TextField("Marker Name Prefix", markerNamePrefix);
        insideMargin = EditorGUILayout.FloatField("Inside Margin (m)", insideMargin);
        upOffset = EditorGUILayout.FloatField("Up Offset (+Y of Floor)", upOffset);

        EditorGUILayout.BeginHorizontal();
        applyMarkerTag = EditorGUILayout.Toggle("Tag Markers", applyMarkerTag);
        using (new EditorGUI.DisabledScope(!applyMarkerTag))
        {
            markerTag = EditorGUILayout.TagField("Marker Tag", markerTag);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Create / Refresh markers on SELECTED prefab assets"))
            ProcessPrefabsSelection(CreateOrRefreshMarkersOnPrefab);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2) Bake Markers -> RoomProperties", EditorStyles.boldLabel);

        useNamePrefixWhenBaking = EditorGUILayout.Toggle("Collect by Name Prefix", useNamePrefixWhenBaking);
        useTagWhenBaking = EditorGUILayout.Toggle("Collect by Tag", useTagWhenBaking);
        autoAssignPrefabByName = EditorGUILayout.Toggle("If Prefab null: Auto-assign by same name", autoAssignPrefabByName);

        setRequired = EditorGUILayout.Toggle("Set Required = true", setRequired);
        setHasDoor = EditorGUILayout.Toggle("Set HasDoor = true", setHasDoor);
        defaultOdds = EditorGUILayout.Slider("Default Odds", defaultOdds, 0f, 1f);

        if (GUILayout.Button("Bake for SELECTED RoomProperties assets"))
            BakeMarkersIntoRoomProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Workflow:\n" +
            " - Select prefab assets -> Create/Refresh markers. For each socket whose name starts with your prefix,\n" +
            "   a ConnectionPoint_* marker is placed at Floor top, snapped to nearest edge, and rotated inward.\n" +
            " - Select RoomProperties assets -> Bake. If Prefab is null, the tool tries to find a prefab with the same name.\n" +
            "   It then finds all markers by name prefix and/or tag and REPLACES ConnectionPoints.", MessageType.Info);
    }

    // ---------- Pass 1: Markers on prefabs ----------
    void ProcessPrefabsSelection(System.Func<GameObject, bool> perPrefab)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("[CP] Nothing selected."); return; }

        int total = 0, modified = 0;
        foreach (var obj in objs)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            Undo.RegisterFullObjectHierarchyUndo(root, "Author ConnectionPoints");
            bool did = perPrefab(root);
            if (did)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                modified++;
            }
            PrefabUtility.UnloadPrefabContents(root);
            total++;
        }
        Debug.Log($"[CP] Processed {total} prefab(s); modified {modified}.");
    }

    bool CreateOrRefreshMarkersOnPrefab(GameObject root)
    {
        var floor = FindChild(root.transform, floorChildName, includeInactive, preferDirectChildren);
        if (!floor) { Debug.LogWarning($"[CP] {root.name}: Floor '{floorChildName}' not found."); return false; }

        var sockets = CollectSocketsByPrefix(root.transform, socketPrefix, matchCase, includeInactive, preferDirectChildren);
        if (sockets.Count == 0) { Debug.LogWarning($"[CP] {root.name}: no sockets starting with '{socketPrefix}'."); return false; }

        // Y at top of floor
        float floorTopY;
        if (!TryGetFloorTopWorldY(floor, out floorTopY)) floorTopY = floor.position.y;

        // Room-local bounds for snapping to edges
        if (!TryGetLocalBoundsXZ(root.transform, floor, out var minL, out var maxL))
        {
            Debug.LogWarning($"[CP] {root.name}: could not compute floor bounds.");
            return false;
        }

        // Optional: validate marker tag
        if (applyMarkerTag && !TagExists(markerTag))
            Debug.LogWarning($"[CP] Tag '{markerTag}' does not exist. Create it in Project Settings > Tags & Layers, or disable 'Tag Markers'.");

        int made = 0;
        foreach (var s in sockets)
        {
            // Project to floor top
            Vector3 worldPos = s.position;
            worldPos.y = floorTopY;

            // Nearest wall side by room-local distance
            Vector3 pL = root.transform.InverseTransformPoint(worldPos);
            float dFront = maxL.z - pL.z; // +Z
            float dBack = pL.z - minL.z; // -Z
            float dRight = maxL.x - pL.x; // +X
            float dLeft = pL.x - minL.x; // -X

            int side = 0; // 0 front +Z, 1 back -Z, 2 right +X, 3 left -X
            float best = dFront;
            if (dBack < best) { best = dBack; side = 1; }
            if (dRight < best) { best = dRight; side = 2; }
            if (dLeft < best) { best = dLeft; side = 3; }

            // Snap to chosen wall plane with inside margin
            switch (side)
            {
                case 0: pL.z = maxL.z - insideMargin; break; // front
                case 1: pL.z = minL.z + insideMargin; break; // back
                case 2: pL.x = maxL.x - insideMargin; break; // right
                default: pL.x = minL.x + insideMargin; break; // left
            }

            worldPos = root.transform.TransformPoint(pL);
            worldPos += floor.up * upOffset;

            // Inward = opposite of wall normal
            Vector3 inward =
                side == 0 ? -root.transform.forward :
                side == 1 ? root.transform.forward :
                side == 2 ? -root.transform.right :
                            root.transform.right;
            inward.y = 0f;
            if (inward.sqrMagnitude < 1e-8f) inward = Vector3.forward;
            inward.Normalize();

            // Create or update marker
            string baseName = markerNamePrefix + s.name;
            string markerName = NextUniqueChildName(root.transform, baseName);
            var existing = FindChild(root.transform, markerName, true, false);

            Transform m;
            if (existing) m = existing;
            else
            {
                var go = new GameObject(markerName);
                go.transform.SetParent(root.transform, false);
                m = go.transform;
                if (applyMarkerTag && TagExists(markerTag))
                {
                    try { go.tag = markerTag; } catch { /* ignore if tag missing */ }
                }
            }

            m.position = worldPos;
            m.rotation = Quaternion.LookRotation(inward, Vector3.up);

            EditorUtility.SetDirty(m.gameObject);
            made++;
        }

        EditorUtility.SetDirty(root);
        return made > 0;
    }

    // ---------- Pass 2: Bake markers into RoomProperties ----------
    void BakeMarkersIntoRoomProperties()
    {
        var assets = Selection.objects;
        if (assets == null || assets.Length == 0) { Debug.LogWarning("[CP] Select RoomProperties assets."); return; }

        int total = 0, modified = 0;

        foreach (var obj in assets)
        {
            var roomProps = obj as RoomProperties;
            if (!roomProps) continue;

            GameObject prefabToUse = roomProps.Prefab;

            // If Prefab is null and auto is enabled, try to resolve by name
            if (!prefabToUse && autoAssignPrefabByName)
            {
                prefabToUse = FindPrefabByExactName(roomProps.name);
                if (prefabToUse)
                {
                    Undo.RecordObject(roomProps, "Assign Prefab by Name");
                    roomProps.Prefab = prefabToUse;
                    EditorUtility.SetDirty(roomProps);
                    AssetDatabase.SaveAssets();
                }
            }

            if (!roomProps.Prefab)
            {
                Debug.LogWarning($"[CP] {roomProps.name}: RoomProperties.Prefab not assigned and no same-named prefab found.");
                continue;
            }

            var path = AssetDatabase.GetAssetPath(roomProps.Prefab);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[CP] {roomProps.name}: cannot resolve prefab path."); continue; }

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            // Collect markers: by prefix and/or tag
            var all = root.transform.GetComponentsInChildren<Transform>(includeInactive)
                        .Where(t => t != root.transform);

            IEnumerable<Transform> markers = all;

            if (useNamePrefixWhenBaking)
                markers = markers.Where(t => t.name.StartsWith(markerNamePrefix));

            if (useTagWhenBaking)
            {
                if (TagExists(markerTag))
                    markers = markers.Where(t => t.CompareTag(markerTag));
                else
                    Debug.LogWarning($"[CP] Tag '{markerTag}' does not exist. Skipping tag filter.");
            }

            var markerList = markers.ToList();
            if (markerList.Count == 0)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning($"[CP] {roomProps.name}: no markers matched for baking.");
                continue;
            }

            // Build Connection[] from markers (inward-facing)
            var conns = new List<Connection>(markerList.Count);
            foreach (var m in markerList)
            {
                Vector3 local = root.transform.InverseTransformPoint(m.position);
                local.y = 0f;

                float G = MapGenerator.GRID_SIZE;
                Vector2 posGrid = new Vector2(local.x / G, local.z / G);

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

            Undo.RecordObject(roomProps, "Bake ConnectionPoints");
            roomProps.ConnectionPoints = conns.ToArray(); // replace
            EditorUtility.SetDirty(roomProps);
            AssetDatabase.SaveAssets();

            PrefabUtility.UnloadPrefabContents(root);

            modified++;
            total++;
        }

        Debug.Log($"[CP] Baked {modified}/{total} RoomProperties asset(s).");
    }

    // ---------- Helpers ----------
    List<Transform> CollectSocketsByPrefix(Transform root, string prefix, bool matchCaseArg, bool includeInactiveArg, bool preferDirectArg)
    {
        var list = new List<Transform>();
        if (string.IsNullOrEmpty(prefix)) return list;

        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
        {
            if (t == root) continue;
            if (preferDirectArg && t.parent != root) continue;

            string name = t.name;
            bool starts = matchCaseArg ? name.StartsWith(prefix) : name.ToLowerInvariant().StartsWith(prefix.ToLowerInvariant());
            if (starts) list.Add(t);
        }
        return list;
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

    bool TryGetLocalBoundsXZ(Transform roomRoot, Transform floor, out Vector3 minL, out Vector3 maxL)
    {
        Matrix4x4 w2l = roomRoot.worldToLocalMatrix;

        // 1) BoxCollider
        var bc = floor.GetComponent<BoxCollider>();
        if (bc)
        {
            var corners = new List<Vector3>(8);
            Vector3 c = bc.center, e = bc.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 local = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                        corners.Add(w2l.MultiplyPoint3x4(floor.TransformPoint(local)));
                    }
            BoundsFromPoints(corners, out minL, out maxL);
            minL.y = 0f; maxL.y = 0f;
            return true;
        }

        // 2) MeshFilter
        var mf = floor.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            var b = mf.sharedMesh.bounds;
            var corners = new List<Vector3>(8);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 local = b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz));
                        corners.Add(w2l.MultiplyPoint3x4(floor.TransformPoint(local)));
                    }
            BoundsFromPoints(corners, out minL, out maxL);
            minL.y = 0f; maxL.y = 0f;
            return true;
        }

        // 3) Combined renderers
        var rends = floor.GetComponentsInChildren<Renderer>(includeInactive);
        if (rends.Length > 0)
        {
            bool first = true; minL = maxL = Vector3.zero;
            foreach (var r in rends)
            {
                var b = r.bounds; // world AABB
                var corners = new Vector3[8];
                int i = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var e = b.extents;
                            Vector3 world = b.center + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                            corners[i++] = roomRoot.worldToLocalMatrix.MultiplyPoint3x4(world);
                        }
                BoundsFromPoints(corners, out var min2, out var max2);
                if (first) { minL = min2; maxL = max2; first = false; }
                else { minL = Vector3.Min(minL, min2); maxL = Vector3.Max(maxL, min2); maxL = Vector3.Max(maxL, max2); }
            }
            minL.y = 0f; maxL.y = 0f;
            return true;
        }

        minL = maxL = Vector3.zero;
        return false;
    }

    void BoundsFromPoints(IEnumerable<Vector3> pts, out Vector3 min, out Vector3 max)
    {
        using (var e = pts.GetEnumerator())
        {
            e.MoveNext();
            min = max = e.Current;
            while (e.MoveNext())
            {
                min = Vector3.Min(min, e.Current);
                max = Vector3.Max(max, e.Current);
            }
        }
    }

    ExitDirection QuantizeToCardinal(Vector3 fLocal)
    {
        // North = +Z, East = +X, South = -Z, West = -X
        if (Mathf.Abs(fLocal.x) >= Mathf.Abs(fLocal.z))
            return fLocal.x >= 0f ? ExitDirection.East : ExitDirection.West;
        else
            return fLocal.z >= 0f ? ExitDirection.North : ExitDirection.South;
    }

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
            return null;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (t.name == name) return t;

        return null;
    }

    string NextUniqueChildName(Transform root, string baseName)
    {
        string n = baseName;
        int i = 1;
        while (root.Find(n) != null) { i++; n = baseName + "_" + i; }
        return n;
    }

    bool TagExists(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        // UnityEditorInternal is OK for editor utilities
        var tags = UnityEditorInternal.InternalEditorUtility.tags;
        for (int i = 0; i < tags.Length; i++) if (tags[i] == tag) return true;
        return false;
    }

    GameObject FindPrefabByExactName(string prefabName)
    {
        string[] guids = AssetDatabase.FindAssets("t:prefab " + prefabName);
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == prefabName)
            {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (obj) return obj;
            }
        }
        return null;
    }
}
#endif
