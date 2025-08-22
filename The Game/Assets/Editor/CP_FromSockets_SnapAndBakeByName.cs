#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditorInternal;

public class CP_FromSockets_SnapAndBake : EditorWindow
{
    // ---------- Socket selection (by prefix) ----------
    string socketPrefix = "Socket";
    bool matchCase = false;
    bool includeInactive = true;
    bool preferDirectChildren = false;

    // ---------- Floor / markers ----------
    string floorChildName = "Floor";
    string markerNamePrefix = "ConnectionPoint_";
    bool tagMarkers = true;
    string markerTag = "ConnectionPoint"; // Create this Tag in Project Settings if you want tag filtering
    float insideMargin = 0.02f;           // meters inside the wall plane
    float upOffset = 0.00f;               // along Floor local +Y

    // ---------- Baking ----------
    bool collectByNamePrefix = true;
    bool collectByTag = true;
    bool autoAssignPrefabByName = true;
    bool enforceDirectionFromName = true; // Use NSWE in names to force inward direction
    bool setRequired = true;
    bool setHasDoor = true;
    float defaultOdds = 1f;

    // ---------- Anchor (positions relative to this) ----------
    string anchorName = "DoorAnchor"; // exact child name
    bool requireAnchor = false;       // if true, skip bake when not found
    bool useAnchorRotationForDir = false; // quantize directions in anchor frame

    [MenuItem("Tools/Rooms/Connection Points: From Sockets (NSWE, Anchor Bake)")]
    static void Open() => GetWindow<CP_FromSockets_SnapAndBake>("CP From Sockets (NSWE+Anchor)");

    void OnGUI()
    {
        EditorGUILayout.LabelField("1) Create/Refresh ConnectionPoint_* Markers (Prefab assets)", EditorStyles.boldLabel);
        socketPrefix = EditorGUILayout.TextField("Socket Prefix (starts with)", socketPrefix);
        matchCase = EditorGUILayout.Toggle("Match Case", matchCase);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        preferDirectChildren = EditorGUILayout.Toggle("Prefer Direct Children", preferDirectChildren);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Floor / Marker placement", EditorStyles.boldLabel);
        floorChildName = EditorGUILayout.TextField("Floor Child Name", floorChildName);
        markerNamePrefix = EditorGUILayout.TextField("Marker Name Prefix", markerNamePrefix);
        insideMargin = EditorGUILayout.FloatField("Inside Margin (m)", insideMargin);
        upOffset = EditorGUILayout.FloatField("Up Offset (+Y of Floor)", upOffset);

        EditorGUILayout.BeginHorizontal();
        tagMarkers = EditorGUILayout.Toggle("Tag Markers", tagMarkers);
        using (new EditorGUI.DisabledScope(!tagMarkers))
        {
            markerTag = EditorGUILayout.TagField("Marker Tag", markerTag);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Create / Refresh markers on SELECTED prefab assets"))
            ProcessPrefabsSelection(CreateOrRefreshMarkersOnPrefab);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2) Bake markers into RoomProperties", EditorStyles.boldLabel);
        collectByNamePrefix = EditorGUILayout.Toggle("Collect by Name Prefix", collectByNamePrefix);
        collectByTag = EditorGUILayout.Toggle("Collect by Tag", collectByTag);
        autoAssignPrefabByName = EditorGUILayout.Toggle("If Prefab null: Auto-assign by same name", autoAssignPrefabByName);
        enforceDirectionFromName = EditorGUILayout.Toggle("Force direction from N/S/E/W name", enforceDirectionFromName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anchor (relative origin for positions)", EditorStyles.miniBoldLabel);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        requireAnchor = EditorGUILayout.Toggle("Require Anchor", requireAnchor);
        useAnchorRotationForDir = EditorGUILayout.Toggle("Use Anchor Rotation For Dir", useAnchorRotationForDir);

        setRequired = EditorGUILayout.Toggle("Set Required = true", setRequired);
        setHasDoor = EditorGUILayout.Toggle("Set HasDoor = true", setHasDoor);
        defaultOdds = EditorGUILayout.Slider("Default Odds", defaultOdds, 0f, 1f);

        if (GUILayout.Button("Bake for SELECTED RoomProperties assets"))
            BakeMarkersIntoRoomProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Creation:\n" +
            " - For each socket named with NSWE suffix (e.g., Socket_N/S/E/W), place ConnectionPoint_<SocketName> at Floor top, " +
            "snap to that wall with Inside Margin, and face inward.\n" +
            "Bake:\n" +
            " - Positions are stored relative to 'Anchor Name' (local X/Z divided by MapGenerator.GRID_SIZE).\n" +
            " - East = -X and West = +X are enforced when 'Force direction from name' is on.\n" +
            "Sockets are never moved.", MessageType.Info);
    }

    // ---------- Pass 1: create/refresh markers on selected prefab assets ----------
    void ProcessPrefabsSelection(System.Func<GameObject, bool> perPrefab)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("[CP NSWE] Nothing selected."); return; }

        int total = 0, modified = 0;
        foreach (var obj in objs)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            Undo.RegisterFullObjectHierarchyUndo(root, "Author ConnectionPoints (NSWE)");
            bool did = perPrefab(root);
            if (did)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                modified++;
            }
            PrefabUtility.UnloadPrefabContents(root);
            total++;
        }
        Debug.Log($"[CP NSWE] Processed {total} prefab(s); modified {modified}.");
    }

    bool CreateOrRefreshMarkersOnPrefab(GameObject root)
    {
        var floor = FindChild(root.transform, floorChildName, includeInactive, preferDirectChildren);
        if (!floor) { Debug.LogWarning($"[CP NSWE] {root.name}: Floor '{floorChildName}' not found."); return false; }

        var sockets = CollectSocketsByPrefix(root.transform, socketPrefix, matchCase, includeInactive, preferDirectChildren);
        if (sockets.Count == 0) { Debug.LogWarning($"[CP NSWE] {root.name}: no sockets starting with '{socketPrefix}'."); return false; }

        float floorTopY;
        if (!TryGetFloorTopWorldY(floor, out floorTopY)) floorTopY = floor.position.y;

        if (!TryGetLocalBoundsXZ(root.transform, floor, out var minL, out var maxL))
        {
            Debug.LogWarning($"[CP NSWE] {root.name}: could not compute floor bounds.");
            return false;
        }

        if (tagMarkers && !TagExists(markerTag))
            Debug.LogWarning($"[CP NSWE] Tag '{markerTag}' does not exist. Create it in Project Settings > Tags, or turn off 'Tag Markers'.");

        int made = 0;
        foreach (var s in sockets)
        {
            string side = GetSideFromName(s.name); // "N","S","E","W",""
            Vector3 pL = root.transform.InverseTransformPoint(new Vector3(s.position.x, floorTopY, s.position.z));

            // Snap to plane based on side (if unknown side, fall back to nearest wall)
            Vector3 inwardWorld;
            if (side == "N")
            {
                pL.z = maxL.z - insideMargin;       // plane +Z
                inwardWorld = -root.transform.forward; // inward -Z
            }
            else if (side == "S")
            {
                pL.z = minL.z + insideMargin;       // plane -Z
                inwardWorld = root.transform.forward;  // inward +Z
            }
            else if (side == "E") // East = left wall (plane -X), inward +X
            {
                pL.x = minL.x + insideMargin;       // plane -X
                inwardWorld = root.transform.right;    // inward +X
            }
            else if (side == "W") // West = right wall (plane +X), inward -X
            {
                pL.x = maxL.x - insideMargin;       // plane +X
                inwardWorld = -root.transform.right;   // inward -X
            }
            else
            {
                // Fallback to nearest wall by distance
                Vector3 tmp = pL;
                float dFront = maxL.z - tmp.z;
                float dBack = tmp.z - minL.z;
                float dRight = maxL.x - tmp.x;
                float dLeft = tmp.x - minL.x;
                int sideIdx = 0; float best = dFront;
                if (dBack < best) { best = dBack; sideIdx = 1; }
                if (dRight < best) { best = dRight; sideIdx = 2; }
                if (dLeft < best) { best = dLeft; sideIdx = 3; }
                switch (sideIdx)
                {
                    case 0: pL.z = maxL.z - insideMargin; inwardWorld = -root.transform.forward; break;
                    case 1: pL.z = minL.z + insideMargin; inwardWorld = root.transform.forward; break;
                    case 2: pL.x = maxL.x - insideMargin; inwardWorld = -root.transform.right; break;
                    default: pL.x = minL.x + insideMargin; inwardWorld = root.transform.right; break;
                }
            }

            Vector3 worldPos = root.transform.TransformPoint(pL) + floor.up * upOffset;
            inwardWorld.y = 0f;
            if (inwardWorld.sqrMagnitude < 1e-8f) inwardWorld = Vector3.forward;
            inwardWorld.Normalize();

            // Idempotent: use exact base name so repeated runs update, not duplicate
            string markerName = markerNamePrefix + s.name;
            var existing = FindChild(root.transform, markerName, true, false);

            Transform m;
            if (existing) m = existing;
            else
            {
                var go = new GameObject(markerName);
                go.transform.SetParent(root.transform, false);
                m = go.transform;
                if (tagMarkers && TagExists(markerTag))
                {
                    try { go.tag = markerTag; } catch { }
                }
            }

            m.position = worldPos;
            m.rotation = Quaternion.LookRotation(inwardWorld, Vector3.up);

            EditorUtility.SetDirty(m.gameObject);
            made++;
        }

        EditorUtility.SetDirty(root);
        return made > 0;
    }

    // ---------- Pass 2: bake markers into RoomProperties ----------
    void BakeMarkersIntoRoomProperties()
    {
        var assets = Selection.objects;
        if (assets == null || assets.Length == 0) { Debug.LogWarning("[CP NSWE] Select RoomProperties assets."); return; }

        int total = 0, modified = 0;
        foreach (var obj in assets)
        {
            var roomProps = obj as RoomProperties;
            if (!roomProps) continue;

            GameObject prefabToUse = roomProps.Prefab;

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
                Debug.LogWarning($"[CP NSWE] {roomProps.name}: RoomProperties.Prefab not assigned and no same-named prefab found.");
                continue;
            }

            var path = AssetDatabase.GetAssetPath(roomProps.Prefab);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[CP NSWE] {roomProps.name}: cannot resolve prefab path."); continue; }

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            // Collect markers by prefix/tag
            var all = root.transform.GetComponentsInChildren<Transform>(includeInactive)
                                     .Where(t => t != root.transform);
            IEnumerable<Transform> markers = all;
            if (collectByNamePrefix) markers = markers.Where(t => t.name.StartsWith(markerNamePrefix));
            if (collectByTag && TagExists(markerTag)) markers = markers.Where(t => t.CompareTag(markerTag));

            var markerList = markers.ToList();
            if (markerList.Count == 0)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning($"[CP NSWE] {roomProps.name}: no markers matched for baking.");
                continue;
            }

            // Resolve anchor (origin)
            Transform anchorTr = FindChild(root.transform, anchorName, includeInactive, preferDirectChildren);
            if (!anchorTr)
            {
                if (requireAnchor)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    Debug.LogWarning($"[CP NSWE] {roomProps.name}: anchor '{anchorName}' not found. Skipping (Require Anchor = true).");
                    continue;
                }
                else
                {
                    Debug.LogWarning($"[CP NSWE] {roomProps.name}: anchor '{anchorName}' not found. Falling back to prefab root.");
                    anchorTr = root.transform;
                }
            }

            // Anchor in room local space
            Vector3 anchorLocalPos = root.transform.InverseTransformPoint(anchorTr.position);

            // Optional: rotate into anchor frame for direction/pos
            Quaternion anchorLocalRot = Quaternion.identity;
            if (useAnchorRotationForDir)
            {
                // Build a root-local rotation that makes anchor.forward become +Z in anchor space
                Vector3 fL = root.transform.InverseTransformDirection(anchorTr.forward);
                fL.y = 0f; if (fL.sqrMagnitude < 1e-6f) fL = Vector3.forward;
                fL.Normalize();
                anchorLocalRot = Quaternion.Inverse(Quaternion.LookRotation(fL, Vector3.up));
            }

            var conns = new List<Connection>(markerList.Count);
            foreach (var m in markerList)
            {
                // Position relative to anchor (room local)
                Vector3 local = root.transform.InverseTransformPoint(m.position);
                local -= anchorLocalPos;
                local.y = 0f;
                if (useAnchorRotationForDir)
                    local = anchorLocalRot * local;

                float G = MapGenerator.GRID_SIZE;
                Vector2 posGrid = new Vector2(local.x / G, local.z / G);

                ExitDirection dir;
                if (enforceDirectionFromName)
                {
                    string side = GetSideFromName(m.name);
                    // Force inward from name with your mapping: East = -X, West = +X
                    if (side == "N") dir = ExitDirection.South; // inward -Z
                    else if (side == "S") dir = ExitDirection.North; // inward +Z
                    else if (side == "E") dir = ExitDirection.East;  // inward +X maps to ExitDirection.East in your enum?
                    else if (side == "W") dir = ExitDirection.West;  // inward -X maps to ExitDirection.West in your enum?
                    else
                    {
                        Vector3 fLocal = root.transform.InverseTransformDirection(m.forward);
                        fLocal.y = 0f;
                        if (useAnchorRotationForDir) fLocal = anchorLocalRot * fLocal;
                        dir = QuantizeToCardinal(fLocal);
                    }
                }
                else
                {
                    Vector3 fLocal = root.transform.InverseTransformDirection(m.forward);
                    fLocal.y = 0f;
                    if (useAnchorRotationForDir) fLocal = anchorLocalRot * fLocal;
                    dir = QuantizeToCardinal(fLocal);
                }

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
            roomProps.ConnectionPoints = conns.ToArray();
            EditorUtility.SetDirty(roomProps);
            AssetDatabase.SaveAssets();

            PrefabUtility.UnloadPrefabContents(root);

            modified++;
            total++;
        }

        Debug.Log($"[CP NSWE] Baked {modified}/{total} RoomProperties asset(s).");
    }

    // ---------- Helpers ----------
    List<Transform> CollectSocketsByPrefix(Transform root, string prefix, bool matchCaseArg, bool includeInactiveArg, bool preferDirectArg)
    {
        var list = new List<Transform>();
        if (string.IsNullOrEmpty(prefix)) return list;
        string needle = matchCaseArg ? prefix : prefix.ToLowerInvariant();

        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
        {
            if (t == root) continue;
            if (preferDirectArg && t.parent != root) continue;

            string hay = matchCaseArg ? t.name : t.name.ToLowerInvariant();
            if (hay.StartsWith(needle)) list.Add(t);
        }
        return list;
    }

    string GetSideFromName(string name)
    {
        // Accept ..._N / ..._S / ..._E / ..._W or containing the words
        string u = name.ToUpperInvariant();
        if (u.EndsWith("_N") || u.Contains("_N_") || u.Contains("NORTH")) return "N";
        if (u.EndsWith("_S") || u.Contains("_S_") || u.Contains("SOUTH")) return "S";
        if (u.EndsWith("_E") || u.Contains("_E_") || u.Contains("EAST")) return "E"; // East = -X plane, inward +X
        if (u.EndsWith("_W") || u.Contains("_W_") || u.Contains("WEST")) return "W"; // West = +X plane, inward -X
        return "";
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
                else { minL = Vector3.Min(minL, min2); maxL = Vector3.Max(maxL, max2); }
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

    // Quantize to your cardinal mapping:
    // North = +Z, South = -Z, West = +X, East = -X
    ExitDirection QuantizeToCardinal(Vector3 fLocal)
    {
        if (Mathf.Abs(fLocal.x) >= Mathf.Abs(fLocal.z))
            return (fLocal.x >= 0f) ? ExitDirection.West : ExitDirection.East;
        else
            return (fLocal.z >= 0f) ? ExitDirection.North : ExitDirection.South;
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

    bool TagExists(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        var tags = InternalEditorUtility.tags;
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
