#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AutoDetectDoorsFromColliders : EditorWindow
{
    string floorChildName = "Floor";
    string anchorName = "DoorAnchor";
    string markerPrefix = "ConnectionPoint_";
    bool tagMarkers = false;
    string markerTag = "ConnectionPoint";

    float bandHeight = 4.0f;
    float sampleStep = 0.10f;
    float verticalStep = 0.25f;
    float minDoorWidth = 0.80f;
    float inwardNudge = 0.02f;
    float probeDepth = 1.25f;
    LayerMask wallMask = ~0;
    bool useBoxCast = true;
    Vector3 boxHalfExtents = new Vector3(0.05f, 0.125f, 0.05f);

    bool drawDebug = true;
    Color openColor = new Color(0f, 1f, 0f, 0.6f);
    Color hitColor = new Color(1f, 0f, 0f, 0.6f);

    [MenuItem("Tools/Rooms/Auto-Detect Doors From Colliders")]
    static void Open() => GetWindow<AutoDetectDoorsFromColliders>("Auto-Detect Doors");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Conventions", EditorStyles.boldLabel);
        floorChildName = EditorGUILayout.TextField("Floor Child Name", floorChildName);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        markerPrefix = EditorGUILayout.TextField("Marker Name Prefix", markerPrefix);

        using (new EditorGUILayout.HorizontalScope())
        {
            tagMarkers = EditorGUILayout.Toggle("Tag Markers", tagMarkers, GUILayout.Width(110));
            using (new EditorGUI.DisabledScope(!tagMarkers))
                markerTag = EditorGUILayout.TagField("Marker Tag", markerTag);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Scan Settings", EditorStyles.boldLabel);
        bandHeight = EditorGUILayout.FloatField("Band Height (+Y from anchor)", bandHeight);
        sampleStep = EditorGUILayout.FloatField("Horizontal Sample Step (m)", sampleStep);
        verticalStep = EditorGUILayout.FloatField("Vertical Sample Step (m)", verticalStep);
        minDoorWidth = EditorGUILayout.FloatField("Min Door Width (m)", minDoorWidth);
        inwardNudge = EditorGUILayout.FloatField("Inward Nudge (m)", inwardNudge);
        probeDepth = EditorGUILayout.FloatField("Probe Depth (m)", probeDepth);
        wallMask = LayerMaskField("Wall LayerMask", wallMask);

        useBoxCast = EditorGUILayout.Toggle(new GUIContent("Use BoxCast (recommended)"), useBoxCast);
        if (useBoxCast)
            boxHalfExtents = EditorGUILayout.Vector3Field("Box Half Extents", boxHalfExtents);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        drawDebug = EditorGUILayout.Toggle("Draw Debug", drawDebug);
        openColor = EditorGUILayout.ColorField("Open Sample Color", openColor);
        hitColor = EditorGUILayout.ColorField("Blocked Sample Color", hitColor);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS"))
            ProcessSelectionPrefabs();
    }

    void ProcessSelectionPrefabs()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("[AutoDetectDoors] Nothing selected."); return; }

        int total = 0, modified = 0;
        foreach (var obj in objs)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;

            Undo.RegisterFullObjectHierarchyUndo(root, "Auto-Detect Doors From Colliders");
            bool did = ProcessRoot(root);
            if (did) { PrefabUtility.SaveAsPrefabAsset(root, path); modified++; }
            PrefabUtility.UnloadPrefabContents(root);
            total++;
        }
        Debug.Log($"[AutoDetectDoors] Processed {total} prefab(s), modified {modified}.");
    }

    bool ProcessRoot(GameObject root)
    {
        var floor = FindChild(root.transform, floorChildName);
        if (!floor) { Debug.LogWarning($"[AutoDetectDoors] {root.name}: Floor '{floorChildName}' not found."); return false; }

        var anchor = FindChild(root.transform, anchorName);
        if (!anchor) { Debug.LogWarning($"[AutoDetectDoors] {root.name}: Anchor '{anchorName}' not found."); return false; }

        int deleted = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;
            if (t.name.StartsWith(markerPrefix))
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                deleted++;
            }
        }

        if (!TryGetLocalBoundsXZ(root.transform, floor, out var minL, out var maxL))
        {
            Debug.LogWarning($"[AutoDetectDoors] {root.name}: Could not compute floor bounds.");
            return deleted > 0;
        }

        float y0 = anchor.transform.position.y;
        float y1 = y0 + Mathf.Max(0.01f, bandHeight);

        var walls = new List<WallSpec>
        {
            new WallSpec { side = WallSide.North, fixedCoord = maxL.z, spanMin = minL.x, spanMax = maxL.x, inward = -root.transform.forward },
            new WallSpec { side = WallSide.South, fixedCoord = minL.z, spanMin = minL.x, spanMax = maxL.x, inward =  root.transform.forward },
            new WallSpec { side = WallSide.East,  fixedCoord = maxL.x, spanMin = minL.z, spanMax = maxL.z, inward = -root.transform.right  },
            new WallSpec { side = WallSide.West,  fixedCoord = minL.x, spanMin = minL.z, spanMax = maxL.z, inward =  root.transform.right   },
        };

        int created = 0;
        foreach (var w in walls)
            created += DetectOnWall(root.transform, w, y0, y1);

        if (deleted > 0 || created > 0)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        Debug.Log($"[AutoDetectDoors] {root.name}: deleted {deleted}, created {created}");
        return (deleted + created) > 0;
    }

    int DetectOnWall(Transform roomRoot, WallSpec w, float y0, float y1)
    {
        int made = 0;
        Vector3 inward = w.inward; inward.y = 0f;
        if (inward.sqrMagnitude < 1e-8f) inward = Vector3.forward;
        inward.Normalize();

        float length = Mathf.Max(0f, w.spanMax - w.spanMin);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, sampleStep)));
        var openMask = new bool[steps];

        for (int i = 0; i < steps; i++)
        {
            float t = (steps == 1) ? 0.5f : (float)i / (steps - 1);
            Vector3 pL = w.IsZ
                ? new Vector3(Mathf.Lerp(w.spanMin, w.spanMax, t), 0f, w.fixedCoord)
                : new Vector3(w.fixedCoord, 0f, Mathf.Lerp(w.spanMin, w.spanMax, t));

            Vector3 baseW = roomRoot.TransformPoint(pL);
            Vector3 mid = new Vector3(baseW.x, 0.5f * (y0 + y1), baseW.z) + inward * inwardNudge;

            bool blocked = false;
            for (float y = y0; y <= y1; y += Mathf.Max(0.01f, verticalStep))
            {
                Vector3 start = new Vector3(mid.x, y, mid.z);
                if (useBoxCast)
                {
                    if (Physics.BoxCast(start, boxHalfExtents, -inward, out _, Quaternion.identity, probeDepth, wallMask, QueryTriggerInteraction.Collide))
                    { blocked = true; break; }
                }
                else
                {
                    if (Physics.Raycast(start, -inward, out _, probeDepth, wallMask, QueryTriggerInteraction.Collide))
                    { blocked = true; break; }
                }
            }
            openMask[i] = !blocked;
            if (drawDebug)
            {
                Color c = blocked ? hitColor : openColor;
                Vector3 end = mid + (-inward) * probeDepth;
                Handles.color = c;
                Handles.DrawAAPolyLine(3f, mid, end);
            }
        }

        var spans = ExtractOpenSpans(openMask, steps, length);
        foreach (var s in spans)
        {
            if (s.width < minDoorWidth) continue;
            float tMid = s.tCenter;
            Vector3 centerL = w.IsZ
                ? new Vector3(Mathf.Lerp(w.spanMin, w.spanMax, tMid), 0f, w.fixedCoord)
                : new Vector3(w.fixedCoord, 0f, Mathf.Lerp(w.spanMin, w.spanMax, tMid));
            Vector3 centerW = roomRoot.TransformPoint(centerL) + inward * inwardNudge;
            centerW.y = 0.5f * (y0 + y1);

            string name = markerPrefix + $"{w.side}_{Mathf.RoundToInt(tMid * 1000)}";
            var existing = FindChild(roomRoot, name);
            Transform m = existing ? existing : new GameObject(name).transform;
            if (!existing) m.SetParent(roomRoot, false);

            if (tagMarkers) { try { m.gameObject.tag = markerTag; } catch { } }

            m.position = centerW;
            m.rotation = Quaternion.LookRotation(inward, Vector3.up);
            EditorUtility.SetDirty(m.gameObject);
            made++;
        }
        return made;
    }

    enum WallSide { North, South, East, West }
    class WallSpec
    {
        public WallSide side;
        public float fixedCoord;
        public float spanMin;
        public float spanMax;
        public Vector3 inward;
        public bool IsZ => (side == WallSide.North || side == WallSide.South);
    }

    struct Span { public float tCenter; public float width; }
    List<Span> ExtractOpenSpans(bool[] open, int steps, float worldLength)
    {
        var list = new List<Span>();
        int i = 0;
        while (i < steps)
        {
            if (!open[i]) { i++; continue; }
            int j = i;
            while (j < steps && open[j]) j++;
            float t0 = (steps == 1) ? 0f : (float)i / (steps - 1);
            float t1 = (steps == 1) ? 1f : (float)(j - 1) / (steps - 1);
            float width = Mathf.Abs(t1 - t0) * worldLength;
            float tMid = 0.5f * (t0 + t1);
            list.Add(new Span { tCenter = tMid, width = width });
            i = j;
        }
        return list;
    }

    Transform FindChild(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var t = root.Find(name);
        if (t) return t;
        foreach (var x in root.GetComponentsInChildren<Transform>(true))
            if (x.name == name) return x;
        return null;
    }

    bool TryGetLocalBoundsXZ(Transform roomRoot, Transform floor, out Vector3 minL, out Vector3 maxL)
    {
        var bc = floor.GetComponent<BoxCollider>();
        if (bc)
        {
            var pts = new List<Vector3>(8);
            Vector3 c = bc.center, e = bc.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 lp = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                        pts.Add(roomRoot.worldToLocalMatrix.MultiplyPoint3x4(floor.TransformPoint(lp)));
                    }
            BoundsFromPoints(pts, out minL, out maxL); minL.y = 0f; maxL.y = 0f; return true;
        }

        var mf = floor.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            var b = mf.sharedMesh.bounds;
            var pts = new List<Vector3>(8);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 lp = b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz));
                        pts.Add(roomRoot.worldToLocalMatrix.MultiplyPoint3x4(floor.TransformPoint(lp)));
                    }
            BoundsFromPoints(pts, out minL, out maxL); minL.y = 0f; maxL.y = 0f; return true;
        }

        var rends = floor.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            bool first = true; minL = maxL = Vector3.zero;
            foreach (var r in rends)
            {
                var b = r.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 wp = BoundsCorner(b, c);
                    Vector3 lp = roomRoot.worldToLocalMatrix.MultiplyPoint3x4(wp);
                    if (first) { minL = maxL = lp; first = false; }
                    else { minL = Vector3.Min(minL, lp); maxL = Vector3.Max(maxL, lp); }
                }
            }
            minL.y = 0f; maxL.y = 0f; return true;
        }

        minL = maxL = Vector3.zero; return false;
    }

    void BoundsFromPoints(IEnumerable<Vector3> pts, out Vector3 min, out Vector3 max)
    {
        using var e = pts.GetEnumerator();
        e.MoveNext(); min = max = e.Current;
        while (e.MoveNext()) { min = Vector3.Min(min, e.Current); max = Vector3.Max(max, e.Current); }
    }

    Vector3 BoundsCorner(Bounds b, int i)
    {
        var c = b.center; var e = b.extents;
        return i switch
        {
            0 => new Vector3(c.x - e.x, c.y - e.y, c.z - e.z),
            1 => new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
            2 => new Vector3(c.x - e.x, c.y + e.y, c.z - e.z),
            3 => new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
            4 => new Vector3(c.x - e.x, c.y - e.y, c.z + e.z),
            5 => new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
            6 => new Vector3(c.x - e.x, c.y + e.y, c.z + e.z),
            _ => new Vector3(c.x + e.x, c.y + e.y, c.z + e.z)
        };
    }

    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        string[] layers = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();
        for (int i = 0; i < layers.Length; i++) if (string.IsNullOrEmpty(layers[i])) layers[i] = $"Layer {i}";
        int mask = EditorGUILayout.MaskField(label, selected.value, layers);
        selected.value = mask;
        return selected;
    }
}
#endif
