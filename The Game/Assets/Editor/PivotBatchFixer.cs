#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class PivotBatchFixer
{
    enum PivotMode { Center, FloorCenter, FloorCornerMin }

    [MenuItem("Tools/Geometry/Fix Pivot (make variants)/Center")]
    static void FixCenter() { ProcessSelection(PivotMode.Center); }

    [MenuItem("Tools/Geometry/Fix Pivot (make variants)/Floor Center")]
    static void FixFloorCenter() { ProcessSelection(PivotMode.FloorCenter); }

    [MenuItem("Tools/Geometry/Fix Pivot (make variants)/Floor Corner (Min)")]
    static void FixFloorCorner() { ProcessSelection(PivotMode.FloorCornerMin); }

    static void ProcessSelection(PivotMode mode)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("Select one or more prefabs or model assets in Project.");
            return;
        }

        int ok = 0, skip = 0;
        foreach (var o in objs)
        {
            var path = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(path)) { skip++; continue; }

            // Load prefab contents for editing (works for both prefab assets and model prefabs)
            var root = PrefabUtility.LoadPrefabContents(path);
            if (!HasAnyRenderer(root))
            {
                PrefabUtility.UnloadPrefabContents(root);
                skip++;
                continue;
            }

            // Compute local AABB of all renderers under root
            Vector3 min, max;
            CalcLocalAABB(root.transform, out min, out max);

            Vector3 pivotLocal;
            switch (mode)
            {
                case PivotMode.Center:
                    pivotLocal = (min + max) * 0.5f;
                    break;
                case PivotMode.FloorCenter:
                    pivotLocal = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
                    break;
                default: // FloorCornerMin
                    pivotLocal = min;
                    break;
            }

            // Shift all direct children by -pivot (so root pivot is the chosen point)
            ShiftAllChildren(root.transform, -pivotLocal);

            // Save variant
            var dst = GenerateVariantPath(path);
            PrefabUtility.SaveAsPrefabAsset(root, dst);
            PrefabUtility.UnloadPrefabContents(root);
            ok++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Pivot fix finished. Variants created: {ok}, skipped: {skip}");
    }

    static bool HasAnyRenderer(GameObject go)
    {
        return go.GetComponentsInChildren<Renderer>(true).Length > 0;
    }

    static void CalcLocalAABB(Transform root, out Vector3 min, out Vector3 max)
    {
        bool has = false;
        min = Vector3.zero; max = Vector3.zero;
        var rends = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            var corners = GetWorldCorners(r.bounds);
            for (int i = 0; i < corners.Length; i++)
            {
                var l = root.InverseTransformPoint(corners[i]);
                if (!has) { min = max = l; has = true; }
                else { min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
            }
        }
        if (!has) { min = Vector3.zero; max = Vector3.zero; }
    }

    static Vector3[] GetWorldCorners(Bounds b)
    {
        var c = b.center;
        var e = b.extents;
        return new Vector3[]
        {
            new Vector3(c.x-e.x, c.y-e.y, c.z-e.z),
            new Vector3(c.x-e.x, c.y-e.y, c.z+e.z),
            new Vector3(c.x-e.x, c.y+e.y, c.z-e.z),
            new Vector3(c.x-e.x, c.y+e.y, c.z+e.z),
            new Vector3(c.x+e.x, c.y-e.y, c.z-e.z),
            new Vector3(c.x+e.x, c.y-e.y, c.z+e.z),
            new Vector3(c.x+e.x, c.y+e.y, c.z-e.z),
            new Vector3(c.x+e.x, c.y+e.y, c.z+e.z)
        };
    }

    static void ShiftAllChildren(Transform root, Vector3 delta)
    {
        var children = new List<Transform>();
        for (int i = 0; i < root.childCount; i++) children.Add(root.GetChild(i));
        foreach (var t in children) t.localPosition += delta;
    }

    static string GenerateVariantPath(string srcPath)
    {
        var dir = Path.GetDirectoryName(srcPath);
        var name = Path.GetFileNameWithoutExtension(srcPath);
        var dst = Path.Combine(dir, name + "_fixed.prefab").Replace("\\", "/");
        return AssetDatabase.GenerateUniqueAssetPath(dst);
    }
}
#endif
