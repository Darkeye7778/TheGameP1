#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class CopyDoorAnchorFromFirstSelected : EditorWindow
{
    string anchorName = "DoorAnchor";
    bool createIfMissing = true;
    bool copyLocalRotation = false;  // toggle if you ever want to match rotation too
    bool copyLocalScale = false;     // rarely needed
    bool includeInactive = true;

    // captured from the "source" (first selected)
    Vector3 sourceLocalPos;
    Quaternion sourceLocalRot = Quaternion.identity;
    Vector3 sourceLocalScale = Vector3.one;
    bool haveSource = false;

    [MenuItem("Tools/Rooms/Copy DoorAnchor From First Selected")]
    static void Open() => GetWindow<CopyDoorAnchorFromFirstSelected>("Copy DoorAnchor");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Copy DoorAnchor (local) from FIRST selected to the rest", EditorStyles.boldLabel);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        createIfMissing = EditorGUILayout.Toggle("Create Anchor If Missing", createIfMissing);
        includeInactive = EditorGUILayout.Toggle("Include Inactive Children", includeInactive);
        copyLocalRotation = EditorGUILayout.Toggle("Also Copy Local Rotation", copyLocalRotation);
        copyLocalScale = EditorGUILayout.Toggle("Also Copy Local Scale", copyLocalScale);

        EditorGUILayout.Space();

        if (GUILayout.Button("Read Anchor From FIRST Selected"))
        {
            haveSource = TryReadFromFirstSelected(out sourceLocalPos, out sourceLocalRot, out sourceLocalScale);
            if (haveSource)
                Debug.Log($"[CopyDoorAnchor] Source anchor read: localPos {sourceLocalPos}, localRot {sourceLocalRot.eulerAngles}, localScale {sourceLocalScale}");
        }

        using (new EditorGUI.DisabledScope(!haveSource))
        {
            if (GUILayout.Button("Apply to Selected PREFAB ASSETS (skip first)"))
                ApplyToSelection(prefabAssets: true);

            if (GUILayout.Button("Apply to Selected SCENE OBJECTS (skip first)"))
                ApplyToSelection(prefabAssets: false);
        }

        if (haveSource)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Captured Source Values (read-only)", EditorStyles.miniBoldLabel);
            EditorGUILayout.Vector3Field("localPosition", sourceLocalPos);
            EditorGUILayout.Vector3Field("localEuler", sourceLocalRot.eulerAngles);
            EditorGUILayout.Vector3Field("localScale", sourceLocalScale);
        }

        EditorGUILayout.HelpBox(
            "1) Select a source room (prefab asset or scene object) that already has a DoorAnchor.\n" +
            "2) Click 'Read Anchor From FIRST Selected'.\n" +
            "3) Add more rooms to the selection (the source must stay first in the selection order).\n" +
            "4) Click one of the Apply buttons.\n\n" +
            "If a target is missing the anchor, it will be created under the root and then positioned.",
            MessageType.Info);
    }

    // --- Core ---

    bool TryReadFromFirstSelected(out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        pos = Vector3.zero; rot = Quaternion.identity; scale = Vector3.one;

        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[CopyDoorAnchor] Nothing selected.");
            return false;
        }

        // prefer prefab asset path if available
        string path = AssetDatabase.GetAssetPath(objs[0]);
        GameObject root = null;
        bool loaded = false;

        if (!string.IsNullOrEmpty(path))
        {
            root = PrefabUtility.LoadPrefabContents(path);
            loaded = true;
        }
        else if (objs[0] is GameObject go)
        {
            root = go;
        }

        if (!root)
        {
            Debug.LogWarning("[CopyDoorAnchor] First selected is not a prefab or scene GameObject.");
            return false;
        }

        var anchor = FindAnchor(root.transform, anchorName, includeInactive);
        if (!anchor)
        {
            if (loaded) PrefabUtility.UnloadPrefabContents(root);
            Debug.LogWarning($"[CopyDoorAnchor] Anchor '{anchorName}' not found under '{root.name}'.");
            return false;
        }

        pos = anchor.localPosition;
        rot = anchor.localRotation;
        scale = anchor.localScale;

        if (loaded) PrefabUtility.UnloadPrefabContents(root);
        return true;
    }

    void ApplyToSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length < 2)
        {
            Debug.LogWarning("[CopyDoorAnchor] Select a source first, then one or more targets.");
            return;
        }

        int changed = 0, total = 0;

        for (int i = 1; i < objs.Length; i++) // skip first (the source)
        {
            var obj = objs[i];

            if (prefabAssets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root)
                {
                    Undo.RegisterFullObjectHierarchyUndo(root, "Copy DoorAnchor (Prefab)");
                    if (CopyToRoot(root)) changed++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    total++;
                }
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Copy DoorAnchor (Scene)");
                if (CopyToRoot(go)) changed++;
                total++;
            }
        }

        Debug.Log($"[CopyDoorAnchor] Applied to {total} target(s); modified {changed}.");
    }

    bool CopyToRoot(GameObject root)
    {
        if (!root) return false;

        var anchor = FindAnchor(root.transform, anchorName, includeInactive);

        if (!anchor && createIfMissing)
        {
            var go = new GameObject(anchorName);
            go.transform.SetParent(root.transform, false);
            anchor = go.transform;
        }

        if (!anchor)
        {
            Debug.LogWarning($"[CopyDoorAnchor] '{anchorName}' missing under '{root.name}' and 'Create' is off. Skipped.");
            return false;
        }

        var beforePos = anchor.localPosition;
        var beforeRot = anchor.localRotation;
        var beforeScale = anchor.localScale;

        anchor.localPosition = sourceLocalPos;
        if (copyLocalRotation) anchor.localRotation = sourceLocalRot;
        if (copyLocalScale) anchor.localScale = sourceLocalScale;

        bool changed = (anchor.localPosition != beforePos) ||
                       (copyLocalRotation && anchor.localRotation != beforeRot) ||
                       (copyLocalScale && anchor.localScale != beforeScale);

        if (changed)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        return changed;
    }

    static Transform FindAnchor(Transform root, string name, bool includeInactive)
    {
        // exact child by name (top-level)
        var t = root.Find(name);
        if (t) return t;

        // fallback: search anywhere under root (keeps nested setups working)
        foreach (var tr in root.GetComponentsInChildren<Transform>(includeInactive))
            if (tr.name == name) return tr;

        return null;
    }
}
#endif
