#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BulkRemoveChildrenWindow : EditorWindow
{
    // Search
    string prefix = "Helper";          // the prefix to look for at the beginning of the name
    bool caseInsensitive = true;
    bool includeInactive = true;
    bool allowMultiplePrefixes = true; // separate with ';' (e.g., "Helper;Temp;BakedCol_")
    bool ignoreRootMatches = true;     // never delete the selected root itself
    bool dryRun = true;                // preview first

    [MenuItem("Tools/Rooms/Bulk Remove Children (Prefix)")]
    static void Open() => GetWindow<BulkRemoveChildrenWindow>("Remove Children (Prefix)");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Remove children whose name STARTS WITH the prefix", EditorStyles.boldLabel);
        prefix = EditorGUILayout.TextField("Prefix (or 'a;b;c')", prefix);
        caseInsensitive = EditorGUILayout.Toggle("Case Insensitive", caseInsensitive);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        allowMultiplePrefixes = EditorGUILayout.Toggle("Allow Multiple Prefixes (';')", allowMultiplePrefixes);
        ignoreRootMatches = EditorGUILayout.Toggle("Ignore Root Matches", ignoreRootMatches);
        dryRun = EditorGUILayout.Toggle("Dry Run (preview only)", dryRun);

        EditorGUILayout.Space();
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS")) ProcessSelection(true);
        if (GUILayout.Button("Process SELECTED SCENE OBJECTS")) ProcessSelection(false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Select prefab assets (Project) or scene objects (Hierarchy), then run.\n" +
            "Removes any child at any depth whose name starts with the given prefix(es).\n" +
            "Deletes deepest-first. Use Dry Run to preview.", MessageType.Info);
    }

    void ProcessSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[RemoveByPrefix] Nothing selected.");
            return;
        }

        int total = 0, modified = 0;

        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (!root) continue;

                Undo.RegisterFullObjectHierarchyUndo(root, "Bulk Remove Children (Prefix)");

                bool did = ProcessRoot(root);
                if (did && !dryRun) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);

                if (did) modified++;
                total++;
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Bulk Remove Children (Prefix)");
                if (ProcessRoot(go)) modified++;
                total++;
            }
        }

        Debug.Log($"[RemoveByPrefix] Processed {total} object(s); modified {modified}.");
    }

    bool ProcessRoot(GameObject root)
    {
        if (root == null) return false;

        var prefixes = ParsePrefixes(prefix, allowMultiplePrefixes);
        if (prefixes.Count == 0)
        {
            Debug.LogWarning("[RemoveByPrefix] Empty prefix.");
            return false;
        }

        var comp = caseInsensitive ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal;

        // Gather matches (exclude root if requested)
        var all = root.GetComponentsInChildren<Transform>(includeInactive);
        var victims = new List<GameObject>();

        foreach (var t in all)
        {
            if (ignoreRootMatches && t == root.transform) continue;

            var name = t.name;
            if (StartsWithAny(name, prefixes, comp))
                victims.Add(t.gameObject);
        }

        if (victims.Count == 0)
        {
            Debug.Log($"[RemoveByPrefix] {root.name}: no matches.");
            return false;
        }

        // Delete deepest-first
        victims.Sort((a, b) => GetDepth(b.transform).CompareTo(GetDepth(a.transform)));

        foreach (var go in victims)
        {
            string path = GetFullPath(go.transform);
            if (dryRun)
                Debug.Log($"[RemoveByPrefix] Would remove: {path}");
            else
            {
                Debug.Log($"[RemoveByPrefix] Removing: {path}");
                Undo.DestroyObjectImmediate(go);
            }
        }

        if (!dryRun)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }

        return true;
    }

    // Helpers
    List<string> ParsePrefixes(string raw, bool allowMulti)
    {
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        if (!allowMulti) return new List<string> { raw.Trim() };
        return raw.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    bool StartsWithAny(string name, List<string> prefs, System.StringComparison comp)
    {
        for (int i = 0; i < prefs.Count; i++)
            if (name.StartsWith(prefs[i], comp)) return true;
        return false;
    }

    int GetDepth(Transform t)
    {
        int d = 0; while (t && t.parent != null) { d++; t = t.parent; }
        return d;
    }

    string GetFullPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack.ToArray());
    }
}
#endif
