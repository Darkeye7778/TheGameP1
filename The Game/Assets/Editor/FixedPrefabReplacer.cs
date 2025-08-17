#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FixedPrefabReplacer
{
    static readonly string[] kFixedSuffixes = {
        "_fixed.prefab",
        "_origin_floor.prefab",
        "_origin_center.prefab",
        "_origin_min.prefab"
    };

    [MenuItem("Tools/Geometry/Replace Originals With Fixed Variants")]
    static void ReplaceWithFixed()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("Select one or more prefabs/model prefabs in Project."); return; }

        int replaced = 0, wrapped = 0, skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var o in objs)
            {
                var origPath = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(origPath)) { skipped++; continue; }

                string fixedPath, originalPath;
                ResolvePaths(origPath, out originalPath, out fixedPath);

                if (string.IsNullOrEmpty(fixedPath) || !File.Exists(fixedPath))
                {
                    Debug.LogWarning($"[ReplaceWithFixed] No fixed variant found for {origPath}.");
                    skipped++;
                    continue;
                }

                var ext = Path.GetExtension(originalPath).ToLowerInvariant();
                if (ext == ".prefab")
                {
                    var fixedRoot = PrefabUtility.LoadPrefabContents(fixedPath);
                    fixedRoot.name = Path.GetFileNameWithoutExtension(originalPath);
                    PrefabUtility.SaveAsPrefabAsset(fixedRoot, originalPath);
                    PrefabUtility.UnloadPrefabContents(fixedRoot);
                    replaced++;
                }
                else
                {
                    string wrapperPath = UniqueSiblingPath(originalPath, "_WRAPPED.prefab");
                    CreateWrapperPrefab(wrapperPath, fixedPath, Path.GetFileNameWithoutExtension(originalPath));
                    Debug.LogWarning($"[ReplaceWithFixed] {originalPath} is a model asset; created wrapper: {wrapperPath}. Update references if needed.");
                    wrapped++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Replace Originals: replaced={replaced}, wrapped={wrapped}, skipped={skipped}");
    }

    [MenuItem("Tools/Geometry/Wrap Originals With Empty Parent (using Fixed child)")]
    static void WrapWithParent()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("Select one or more prefabs/model prefabs in Project."); return; }

        int overwrote = 0, created = 0, skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var o in objs)
            {
                var origPath = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(origPath)) { skipped++; continue; }

                string fixedPath, originalPath;
                ResolvePaths(origPath, out originalPath, out fixedPath);

                if (string.IsNullOrEmpty(fixedPath) || !File.Exists(fixedPath))
                {
                    Debug.LogWarning($"[WrapWithParent] No fixed variant found for {origPath}.");
                    skipped++;
                    continue;
                }

                var ext = Path.GetExtension(originalPath).ToLowerInvariant();
                if (ext == ".prefab")
                {
                    CreateWrapperPrefab(originalPath, fixedPath, Path.GetFileNameWithoutExtension(originalPath));
                    overwrote++;
                }
                else
                {
                    string wrapperPath = UniqueSiblingPath(originalPath, "_WRAPPED.prefab");
                    CreateWrapperPrefab(wrapperPath, fixedPath, Path.GetFileNameWithoutExtension(originalPath));
                    created++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Wrap Originals: overwrote={overwrote}, created={created}, skipped={skipped}");
    }

    static void ResolvePaths(string selectedPath, out string originalPath, out string fixedPath)
    {
        originalPath = selectedPath;
        fixedPath = null;

        string dir = Path.GetDirectoryName(selectedPath).Replace("\\", "/");
        string name = Path.GetFileNameWithoutExtension(selectedPath);
        string ext = Path.GetExtension(selectedPath).ToLowerInvariant();

        foreach (var suf in kFixedSuffixes)
        {
            if (name.EndsWith(suf.Replace(".prefab", "")))
            {
                string core = name.Substring(0, name.Length - suf.Replace(".prefab", "").Length);
                string tryPrefab = Combine(dir, core + ".prefab");
                string trySame = Combine(dir, core + ext);
                originalPath = File.Exists(tryPrefab) ? tryPrefab : trySame;
                fixedPath = selectedPath;
                return;
            }
        }

        foreach (var suf in kFixedSuffixes)
        {
            string candidate = Combine(dir, name + suf);
            if (File.Exists(candidate)) { fixedPath = candidate; return; }
        }
    }

    static string Combine(string a, string b) => (a + "/" + b).Replace("\\", "/");

    static string UniqueSiblingPath(string originalPath, string suffix)
    {
        var dir = Path.GetDirectoryName(originalPath);
        var core = Path.GetFileNameWithoutExtension(originalPath);
        var basePath = Path.Combine(dir, core + suffix).Replace("\\", "/");
        return AssetDatabase.GenerateUniqueAssetPath(basePath);
    }

    static void CreateWrapperPrefab(string savePath, string fixedPrefabPath, string rootName)
    {
        var wrapperRoot = new GameObject(rootName);
        var fixedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fixedPrefabPath);
        if (!fixedAsset)
        {
            Object.DestroyImmediate(wrapperRoot);
            Debug.LogError($"[CreateWrapperPrefab] Cannot load fixed prefab at {fixedPrefabPath}");
            return;
        }
        var child = PrefabUtility.InstantiatePrefab(fixedAsset, wrapperRoot.transform) as GameObject;
        if (child)
        {
            child.name = fixedAsset.name;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }
        PrefabUtility.SaveAsPrefabAsset(wrapperRoot, savePath);
        Object.DestroyImmediate(wrapperRoot);
    }
}
#endif
