// Editor/CollidersToParentTool.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CollidersToParentTool : EditorWindow
{
    // Where the visible meshes live (child)
    string childSourceName = "default";

    // Where the MeshCollider should be (parent). Leave blank to use parent of childSourceName.
    string targetParentName = "Model"; // set "" to auto = child.parent

    // Options
    string environmentLayerName = "Environment";
    bool includeInactive = true;
    bool removeChildMeshColliders = true;
    bool replaceExistingOnTarget = true;

    [MenuItem("Tools/Rooms/Colliders -> Parent (Combine)")]
    static void Open() => GetWindow<CollidersToParentTool>("Colliders -> Parent");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Source/Target", EditorStyles.boldLabel);
        childSourceName = EditorGUILayout.TextField("Child source name", childSourceName);
        targetParentName = EditorGUILayout.TextField(new GUIContent("Target parent name (optional)", "If empty, uses the parent of the source child"), targetParentName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        environmentLayerName = EditorGUILayout.TextField("Environment layer", environmentLayerName);
        includeInactive = EditorGUILayout.Toggle("Include inactive", includeInactive);
        removeChildMeshColliders = EditorGUILayout.Toggle("Remove child MeshColliders", removeChildMeshColliders);
        replaceExistingOnTarget = EditorGUILayout.Toggle("Replace existing collider on target", replaceExistingOnTarget);

        EditorGUILayout.Space();
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS"))
            ProcessSelection(prefabAssets: true);

        if (GUILayout.Button("Process SELECTED SCENE OBJECTS"))
            ProcessSelection(prefabAssets: false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Finds child named `childSourceName` (e.g. 'default'), combines all MeshFilters under it into ONE mesh in the target parent's local space (e.g. 'Model'), " +
            "and assigns that mesh to a MeshCollider on the parent. No runtime scripts are added.", MessageType.Info);
    }

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

                Undo.RegisterFullObjectHierarchyUndo(root, "Colliders -> Parent");
                bool did = ProcessRoot(root);
                if (did) { PrefabUtility.SaveAsPrefabAsset(root, path); changed++; }
                PrefabUtility.UnloadPrefabContents(root);
                total++;
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Colliders -> Parent");
                if (ProcessRoot(go)) changed++;
                total++;
            }
        }

        Debug.Log($"[CollidersToParent] Processed {total} object(s); modified {changed}.");
    }

    bool ProcessRoot(GameObject root)
    {
        // Find the source child (where meshes live)
        var sources = FindChildrenByName(root.transform, childSourceName, includeInactive).ToList();
        if (sources.Count == 0)
        {
            Debug.LogWarning($"[CollidersToParent] '{childSourceName}' not found under '{root.name}'. Skipping.");
            return false;
        }

        // Determine target parent for each source
        int envLayer = LayerMask.NameToLayer(environmentLayerName);
        if (envLayer < 0) envLayer = 0;

        bool modified = false;

        // Group sources by target parent (usually each source's parent or a named target under root)
        var groups = new Dictionary<Transform, List<Transform>>();

        foreach (var src in sources)
        {
            Transform target = null;

            if (!string.IsNullOrEmpty(targetParentName))
                target = FindChildByName(root.transform, targetParentName, includeInactive);

            if (!target) target = src.parent; // default: parent of the source

            if (!target)
            {
                Debug.LogWarning($"[CollidersToParent] No target parent for source '{src.name}' in '{root.name}'. Skipping this source.");
                continue;
            }

            if (!groups.TryGetValue(target, out var list))
            {
                list = new List<Transform>();
                groups[target] = list;
            }
            list.Add(src);
        }

        foreach (var kv in groups)
        {
            var target = kv.Key;
            var srcRoots = kv.Value;

            // Combine all MeshFilters under the source roots into one mesh in target local space
            if (BuildCombinedColliderOnTarget(target, srcRoots, envLayer))
            {
                modified = true;

                if (removeChildMeshColliders)
                {
                    foreach (var s in srcRoots) RemoveMeshCollidersUnder(s, includeInactive);
                }
            }
        }

        if (modified)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }

        return modified;
    }

    // Combine all meshes under srcRoots into a single mesh in target local space, assign to MeshCollider on target
    bool BuildCombinedColliderOnTarget(Transform target, List<Transform> srcRoots, int envLayer)
    {
        var mfs = new List<MeshFilter>();
        foreach (var s in srcRoots)
            mfs.AddRange(s.GetComponentsInChildren<MeshFilter>(includeInactive));

        if (mfs.Count == 0) return false;

        var combines = new List<CombineInstance>(mfs.Count);
        var toTarget = target.worldToLocalMatrix;

        foreach (var mf in mfs)
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;

            var ci = new CombineInstance
            {
                mesh = mesh,
                transform = toTarget * mf.transform.localToWorldMatrix
            };
            combines.Add(ci);
        }

        if (combines.Count == 0) return false;

        var combined = new Mesh
        {
            name = "BakedCol_CombinedToParent",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        combined.CombineMeshes(combines.ToArray(), true, true, false);

        var mc = target.GetComponent<MeshCollider>();
        if (!mc) mc = target.gameObject.AddComponent<MeshCollider>();
        else if (replaceExistingOnTarget) mc.sharedMesh = null; // force recook

        mc.sharedMesh = combined;
        mc.convex = false;

        if (envLayer >= 0) target.gameObject.layer = envLayer;

        EditorUtility.SetDirty(target.gameObject);
        return true;
    }

    static IEnumerable<Transform> FindChildrenByName(Transform root, string name, bool includeInactiveArg)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (t.name == name) yield return t;
    }

    static Transform FindChildByName(Transform root, string name, bool includeInactiveArg)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var t = root.Find(name);
        if (t) return t;
        foreach (var x in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (x.name == name) return x;
        return null;
    }

    static void RemoveMeshCollidersUnder(Transform root, bool includeInactiveArg)
    {
        var colls = root.GetComponentsInChildren<MeshCollider>(includeInactiveArg);
        foreach (var c in colls) Undo.DestroyObjectImmediate(c);
    }
}
#endif
