#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SetLocalPositionTool : EditorWindow
{

    Vector3 targetLocalPos = Vector3.zero;
    bool affectX = true, affectY = true, affectZ = true;
    bool recurseChildren = false;
    bool includeInactive = true;
    bool operateOnRootsOnly = true;

    [MenuItem("Tools/Rooms/Set Local Position (No Rotation Change)")]
    static void Open() => GetWindow<SetLocalPositionTool>("Set Local Position");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Set LOCAL Position (rotation & scale unchanged)", EditorStyles.boldLabel);
        targetLocalPos = EditorGUILayout.Vector3Field("Target Local Position", targetLocalPos);

        EditorGUILayout.BeginHorizontal();
        affectX = EditorGUILayout.ToggleLeft("Affect X", affectX, GUILayout.Width(90));
        affectY = EditorGUILayout.ToggleLeft("Affect Y", affectY, GUILayout.Width(90));
        affectZ = EditorGUILayout.ToggleLeft("Affect Z", affectZ, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        recurseChildren = EditorGUILayout.Toggle("Process Children (Recursive)", recurseChildren);
        includeInactive = EditorGUILayout.Toggle("Include Inactive Children", includeInactive);
        operateOnRootsOnly = EditorGUILayout.Toggle(new GUIContent("Operate on Roots Only (Scene)",
            "When on, only top-level selections are processed in the scene to avoid double-moving nested objects."), operateOnRootsOnly);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply to Selected PREFAB ASSETS"))
            Apply(prefabAssets: true);

        if (GUILayout.Button("Apply to Selected SCENE OBJECTS"))
            Apply(prefabAssets: false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Door Anchor helper", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates (or moves) a child named 'DoorAnchor' at the target local position on each selected root. " +
                                "Does NOT modify other children. Rotation/scale of everything stays the same.", MessageType.Info);

        if (GUILayout.Button("Create/Move 'DoorAnchor' To Target (Prefabs)"))
            CreateOrMoveDoorAnchor(prefabAssets: true);

        if (GUILayout.Button("Create/Move 'DoorAnchor' To Target (Scene)"))
            CreateOrMoveDoorAnchor(prefabAssets: false);

        EditorGUILayout.Space();
        if (GUILayout.Button("Zero To (0,0,0)"))
            targetLocalPos = Vector3.zero;
    }

    void Apply(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[SetLocalPosition] Nothing selected.");
            return;
        }

        int changed = 0;
        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root)
                {
                    Undo.RegisterFullObjectHierarchyUndo(root, "Set Local Position (Prefab)");
                    if (ProcessRoot(root)) changed++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            else if (obj is GameObject go)
            {

                if (operateOnRootsOnly && go.transform.parent != null) continue;

                Undo.RegisterFullObjectHierarchyUndo(go, "Set Local Position (Scene)");
                if (ProcessRoot(go)) changed++;
            }
        }

        Debug.Log($"[SetLocalPosition] Processed {objs.Length} item(s); modified {changed}.");
    }

    bool ProcessRoot(GameObject root)
    {
        if (!root) return false;
        int mods = 0;

        Transform[] transforms;
        if (recurseChildren)
            transforms = root.GetComponentsInChildren<Transform>(includeInactive);
        else
            transforms = new[] { root.transform };

        foreach (var t in transforms)
        {
            var before = t.localPosition;
            var after = new Vector3(
                affectX ? targetLocalPos.x : before.x,
                affectY ? targetLocalPos.y : before.y,
                affectZ ? targetLocalPos.z : before.z
            );

            if (after != before)
            {
                t.localPosition = after;
                mods++;
            }
        }

        if (mods > 0)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            return true;
        }
        return false;
    }

    void CreateOrMoveDoorAnchor(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[SetLocalPosition] Nothing selected.");
            return;
        }

        int changed = 0;
        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root)
                {
                    Undo.RegisterFullObjectHierarchyUndo(root, "Move DoorAnchor (Prefab)");
                    if (CreateOrMoveAnchorUnder(root.transform)) changed++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            else if (obj is GameObject go)
            {
                if (operateOnRootsOnly && go.transform.parent != null) continue;

                Undo.RegisterFullObjectHierarchyUndo(go, "Move DoorAnchor (Scene)");
                if (CreateOrMoveAnchorUnder(go.transform)) changed++;
            }
        }

        Debug.Log($"[SetLocalPosition] DoorAnchor placed/updated on {changed} object(s).");
    }

    bool CreateOrMoveAnchorUnder(Transform root)
    {
        var anchor = root.Find("DoorAnchor");
        if (!anchor)
        {
            var go = new GameObject("DoorAnchor");
            go.transform.SetParent(root, false);
            anchor = go.transform;
        }

        var before = anchor.localPosition;
        var newPos = new Vector3(
            affectX ? targetLocalPos.x : before.x,
            affectY ? targetLocalPos.y : before.y,
            affectZ ? targetLocalPos.z : before.z
        );

        if (newPos != before)
        {
            anchor.localPosition = newPos;
            EditorUtility.SetDirty(root.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            return true;
        }
        return false;
    }
}
#endif