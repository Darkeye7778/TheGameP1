using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class BulkPrefabChildTagger : EditorWindow
{
    enum MatchMode { Contains, Equals, StartsWith, EndsWith, Regex }

    // Search UI
    string nameFilter = "";
    bool matchCase = false;
    MatchMode mode = MatchMode.Contains;

    // Layer UI
    string[] layerNames;
    int layerPopupIndex = 0; // UI index into layerNames (not the numeric layer id)

    // Preview state
    Vector2 scroll;
    List<PreviewItem> preview = new();

    class PreviewItem
    {
        public string path;
        public Hit[] hits;
    }
    class Hit
    {
        public string hierarchyPath;
        public string beforeLayer;
        public string afterLayer;
    }

    [MenuItem("Tools/Bulk Set Child Layer…")]
    static void Open() => GetWindow<BulkPrefabChildTagger>("Bulk Set Child Layer");

    void OnEnable()
    {
        RefreshLayerNames();
    }

    void RefreshLayerNames()
    {
        layerNames = InternalEditorUtility.layers;
        if (layerNames == null || layerNames.Length == 0)
            layerNames = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();

        int idx = System.Array.IndexOf(layerNames, "Default");
        layerPopupIndex = Mathf.Max(0, idx);
    }

    void OnGUI()
    {
        GUILayout.Label("Find child by name -> set its Layer inside prefab assets", EditorStyles.boldLabel);

        // --- Search ---
        GUILayout.Space(4);
        GUILayout.Label("Search", EditorStyles.miniBoldLabel);
        nameFilter = EditorGUILayout.TextField("Name filter", nameFilter);
        using (new EditorGUILayout.HorizontalScope())
        {
            mode = (MatchMode)EditorGUILayout.EnumPopup("Match mode", mode);
            matchCase = EditorGUILayout.ToggleLeft("Match case", matchCase, GUILayout.Width(110));
        }

        // --- Layer ---
        GUILayout.Space(8);
        GUILayout.Label("Layer", EditorStyles.miniBoldLabel);
        if (layerNames == null || layerNames.Length == 0) RefreshLayerNames();
        layerPopupIndex = EditorGUILayout.Popup("Target Layer", layerPopupIndex, layerNames);
        string targetLayerName = layerNames.Length > 0 ? layerNames[layerPopupIndex] : "Default";

        // --- Actions ---
        GUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Matches")) DoPreview(targetLayerName);

            using (new EditorGUI.DisabledScope(preview.Count == 0))
                if (GUILayout.Button("Apply Layer")) Apply(targetLayerName);
        }

        // --- Summary ---
        GUILayout.Space(6);
        int totalHits = preview.Sum(p => p.hits.Length);
        EditorGUILayout.LabelField(
            $"Selection: {Selection.assetGUIDs.Length} * Prefabs with matches: {preview.Count} * Children to change: {totalHits}",
            EditorStyles.miniLabel);

        // --- Preview list ---
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var p in preview)
        {
            if (p.hits.Length == 0) continue;
            EditorGUILayout.LabelField(Path.GetFileName(p.path), EditorStyles.boldLabel);
            foreach (var h in p.hits)
                EditorGUILayout.LabelField($"  * {h.hierarchyPath}   ({h.beforeLayer} -> {h.afterLayer})", EditorStyles.miniLabel);
            GUILayout.Space(6);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox(
            "Tip: if the target layer isn’t listed, add it in Project Settings -> Tags and Layers, then reopen this window.",
            MessageType.Info);
    }

    // ---------------- Core ----------------

    void DoPreview(string targetLayerName)
    {
        preview.Clear();

        if (string.IsNullOrWhiteSpace(nameFilter))
        {
            ShowNotification(new GUIContent("Enter a name filter"));
            return;
        }

        var guids = CollectPrefabGuidsFromSelection();
        if (guids.Count == 0)
        {
            ShowNotification(new GUIContent("Select prefab assets or folders"));
            return;
        }

        var matcher = BuildMatcher();

        try
        {
            int i = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Preview Prefabs", Path.GetFileName(path), (float)i / guids.Count);
                i++;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (!root) continue;

                var hits = new List<Hit>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) // include inactive
                {
                    if (!matcher(t.name)) continue;
                    hits.Add(new Hit
                    {
                        hierarchyPath = GetHierarchyPath(t, root.transform),
                        beforeLayer = LayerMask.LayerToName(t.gameObject.layer),
                        afterLayer = targetLayerName
                    });
                }

                if (hits.Count > 0)
                    preview.Add(new PreviewItem { path = path, hits = hits.ToArray() });

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    void Apply(string targetLayerName)
    {
        if (preview.Count == 0) { ShowNotification(new GUIContent("Run Preview first")); return; }

        int targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer < 0)
        {
            EditorUtility.DisplayDialog("Layer not found",
                $"Layer \"{targetLayerName}\" doesn’t exist.\nCreate it in Project Settings -> Tags and Layers.", "OK");
            return;
        }

        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < preview.Count; i++)
            {
                var item = preview[i];
                EditorUtility.DisplayProgressBar("Applying Layer", Path.GetFileName(item.path), (float)i / preview.Count);

                var root = PrefabUtility.LoadPrefabContents(item.path);
                if (!root) continue;

                // Build quick lookup of matched paths for this prefab
                var targetPaths = new HashSet<string>(item.hits.Select(h => h.hierarchyPath));

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string hp = GetHierarchyPath(t, root.transform);
                    if (!targetPaths.Contains(hp)) continue;

                    t.gameObject.layer = targetLayer;
                    changed++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, item.path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog("Done", $"Changed layer on {changed} object(s) across {preview.Count} prefab(s).", "OK");
    }

    // ---------------- Helpers ----------------

    List<string> CollectPrefabGuidsFromSelection()
    {
        var outGuids = new List<string>();
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path))
                outGuids.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { path }));
            else if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(GameObject))
                outGuids.Add(guid);
        }
        return outGuids.Distinct().ToList();
    }

    System.Func<string, bool> BuildMatcher()
    {
        var comp = matchCase ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;
        string f = nameFilter ?? string.Empty;
        if (!matchCase) f = f.ToLowerInvariant();

        switch (mode)
        {
            case MatchMode.Equals: return s => string.Equals(matchCase ? s : s.ToLowerInvariant(), f, comp);
            case MatchMode.StartsWith: return s => (matchCase ? s : s.ToLowerInvariant()).StartsWith(f, comp);
            case MatchMode.EndsWith: return s => (matchCase ? s : s.ToLowerInvariant()).EndsWith(f, comp);
            case MatchMode.Regex:
                var rx = new Regex(nameFilter, matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                return s => rx.IsMatch(s);
            default: // Contains
                return s => (matchCase ? s : s.ToLowerInvariant()).Contains(f);
        }
    }

    static string GetHierarchyPath(Transform t, Transform root)
    {
        var stack = new Stack<string>();
        var cur = t;
        while (cur && cur != root) { stack.Push(cur.name); cur = cur.parent; }
        if (cur == root) stack.Push(root.name);
        return string.Join("/", stack);
    }
}
