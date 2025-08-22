#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MarkersFromPrefixOffset : EditorWindow
{
    string floorChildName = "Floor";
    string anchorName = "DoorAnchor";
    string sourcePrefix = ".ConnectionPoint_";
    string markerPrefix = "ConnectionPoint_";
    float inwardOffset = 0.3f;
    bool forwardOutward = true;
    bool includeInactive = true;
    bool deleteExistingMarkersWithTargetPrefix = false;

    [MenuItem("Tools/Rooms/Markers From Prefix Offset (Prefabs)")]
    static void Open() => GetWindow<MarkersFromPrefixOffset>("Markers From Prefix Offset");

    void OnGUI()
    {
        floorChildName = EditorGUILayout.TextField("Floor Child Name", floorChildName);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        sourcePrefix = EditorGUILayout.TextField("Source Prefix", sourcePrefix);
        markerPrefix = EditorGUILayout.TextField("Marker Prefix", markerPrefix);
        inwardOffset = EditorGUILayout.FloatField("Inward Offset (m)", inwardOffset);
        forwardOutward = EditorGUILayout.Toggle("Marker Forward Points Outward", forwardOutward);
        includeInactive = EditorGUILayout.Toggle("Include Inactive Children", includeInactive);
        deleteExistingMarkersWithTargetPrefix = EditorGUILayout.Toggle("Delete Existing Target Markers", deleteExistingMarkersWithTargetPrefix);
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS")) ProcessSelection();
    }

    void ProcessSelection()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("[MarkersFromPrefix] Nothing selected."); return; }
        int total = 0, modified = 0;
        foreach (var o in objs)
        {
            var path = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root) continue;
            Undo.RegisterFullObjectHierarchyUndo(root, "Markers From Prefix Offset");
            bool did = ProcessRoot(root);
            if (did) { PrefabUtility.SaveAsPrefabAsset(root, path); modified++; }
            PrefabUtility.UnloadPrefabContents(root);
            total++;
        }
        Debug.Log($"[MarkersFromPrefix] Processed {total}, modified {modified}.");
    }

    bool ProcessRoot(GameObject root)
    {
        var floor = FindChild(root.transform, floorChildName, includeInactive);
        if (!floor) { Debug.LogWarning($"[MarkersFromPrefix] {root.name}: Floor '{floorChildName}' not found."); return false; }
        var anchor = FindChild(root.transform, anchorName, includeInactive);
        if (!anchor) { Debug.LogWarning($"[MarkersFromPrefix] {root.name}: Anchor '{anchorName}' not found."); return false; }
        if (!TryGetLocalBoundsXZ(root.transform, floor.transform, out var minL, out var maxL))
        { Debug.LogWarning($"[MarkersFromPrefix] {root.name}: Could not compute floor bounds."); return false; }

        if (deleteExistingMarkersWithTargetPrefix)
        {
            var olds = root.GetComponentsInChildren<Transform>(includeInactive).Where(t => t != root.transform && t.name.StartsWith(markerPrefix)).ToArray();
            foreach (var t in olds) Undo.DestroyObjectImmediate(t.gameObject);
        }

        var sources = root.GetComponentsInChildren<Transform>(includeInactive)
                          .Where(t => t != root.transform && t.name.StartsWith(sourcePrefix)).ToList();
        if (sources.Count == 0) return false;

        float anchorY = anchor.position.y;
        var walls = new[]
        {
            new Wall{ side=Side.North, fixedCoord=maxL.z, outward=root.transform.forward },
            new Wall{ side=Side.South, fixedCoord=minL.z, outward=-root.transform.forward },
            new Wall{ side=Side.East,  fixedCoord=maxL.x, outward=root.transform.right },
            new Wall{ side=Side.West,  fixedCoord=minL.x, outward=-root.transform.right },
        };

        int made = 0, updated = 0;
        foreach (var s in sources)
        {
            Vector3 pL = root.transform.worldToLocalMatrix.MultiplyPoint3x4(s.position);
            Wall nearest = walls[0];
            float best = float.PositiveInfinity;
            foreach (var w in walls)
            {
                float d = (w.IsZ ? Mathf.Abs(pL.z - w.fixedCoord) : Mathf.Abs(pL.x - w.fixedCoord));
                if (d < best) { best = d; nearest = w; }
            }

            if (nearest.IsZ) pL.z = nearest.fixedCoord; else pL.x = nearest.fixedCoord;

            Vector3 outwardW = nearest.outward; outwardW.y = 0f; outwardW.Normalize();
            Vector3 inwardW = -outwardW;

            Vector3 pW = root.transform.localToWorldMatrix.MultiplyPoint3x4(pL);
            pW.y = anchorY;
            pW += inwardW * Mathf.Max(0f, inwardOffset);

            string markerName = markerPrefix + s.name.Substring(sourcePrefix.Length);
            var existing = FindChild(root.transform, markerName, includeInactive);
            Transform m = existing ? existing : new GameObject(markerName).transform;
            if (!existing) m.SetParent(root.transform, false);

            Vector3 fwd = forwardOutward ? outwardW : inwardW;
            var rot = Quaternion.LookRotation(fwd, Vector3.up);

            bool posChanged = !Approximately(m.position, pW);
            bool rotChanged = Quaternion.Angle(m.rotation, rot) > 0.01f;
            m.position = pW;
            m.rotation = rot;
            EditorUtility.SetDirty(m.gameObject);
            if (posChanged || rotChanged) { if (existing) updated++; else made++; }
        }

        if (made + updated > 0)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        Debug.Log($"[MarkersFromPrefix] {root.name}: created {made}, updated {updated}.");
        return (made + updated) > 0;
    }

    enum Side { North, South, East, West }
    struct Wall
    {
        public Side side;
        public float fixedCoord;
        public Vector3 outward;
        public bool IsZ => (side == Side.North || side == Side.South);
    }

    Transform FindChild(Transform root, string name, bool includeInactiveArg)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var t = root.Find(name);
        if (t) return t;
        foreach (var x in root.GetComponentsInChildren<Transform>(includeInactiveArg))
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
                        pts.Add(roomRoot.worldToLocalMatrix.MultiplyPoint3x4(floor.TransformPoint(c + new Vector3(e.x * sx, e.y * sy, e.z * sz))));
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
                        pts.Add(roomRoot.worldToLocalMatrix.MultiplyPoint3x4(floor.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz)))));
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
        switch (i)
        {
            case 0: return new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);
            case 1: return new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);
            case 2: return new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);
            case 3: return new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);
            case 4: return new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);
            case 5: return new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);
            case 6: return new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);
            default: return new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);
        }
    }

    bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 1e-6f;
}
#endif
