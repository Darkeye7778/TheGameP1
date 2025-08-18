#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class FlipDoorAnchors180 : EditorWindow
{
    string anchorName = "DoorAnchor";
    bool includeInactive = true;

    [MenuItem("Tools/Rooms/Flip DoorAnchors 180° Y")]
    static void Open() => GetWindow<FlipDoorAnchors180>("Flip DoorAnchors");

    void OnGUI()
    {
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        includeInactive = EditorGUILayout.Toggle("Include Inactive Children", includeInactive);

        if (GUILayout.Button("Flip on Selected PREFAB ASSETS"))
            Apply(prefabAssets: true);

        if (GUILayout.Button("Flip on Selected SCENE OBJECTS"))
            Apply(prefabAssets: false);

        EditorGUILayout.HelpBox("Rotates each DoorAnchor's localRotation by 180° around Y. No other transforms changed.", MessageType.Info);
    }

    void Apply(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) { Debug.LogWarning("Nothing selected."); return; }

        int changed = 0, scanned = 0;
        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (!root) continue;

                Undo.RegisterFullObjectHierarchyUndo(root, "Flip DoorAnchors 180°");
                if (FlipUnder(root.transform)) changed++;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                scanned++;
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Flip DoorAnchors 180°");
                if (FlipUnder(go.transform)) changed++;
                scanned++;
            }
        }
        Debug.Log($"[FlipDoorAnchors] Scanned {scanned}, flipped {changed}.");
    }

    bool FlipUnder(Transform root)
    {
        bool any = false;
        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive))
        {
            if (t.name != anchorName) continue;
            t.localRotation = Quaternion.AngleAxis(180f, Vector3.up) * t.localRotation;
            EditorUtility.SetDirty(t.gameObject);
            any = true;
        }
        if (any)
        {
            EditorUtility.SetDirty(root.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        return any;
    }
}
#endif
