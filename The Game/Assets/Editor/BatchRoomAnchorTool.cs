// Editor/BatchRoomAnchorTool.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

public class BatchRoomAnchorTool : EditorWindow
{
    // GUI fields
    string anchorName = "DoorAnchor";
    Vector3 anchorLocalPos = Vector3.zero;
    Vector3 anchorLocalEuler = Vector3.zero;
    bool createIfMissing = true;
    bool recenterToAnchor = true;                     // shift all children so anchor ends up at (0,0,0)
    bool updateColliderFromProperties = true;         // uses RoomProfile.Properties + MapGenerator.GRID_SIZE
    float fallbackGridSize = 1f;

    [MenuItem("Tools/Rooms/Batch Room Anchor Tool")]
    static void ShowWindow() => GetWindow<BatchRoomAnchorTool>("Room Anchor Tool");

    void OnEnable()
    {
        fallbackGridSize = TryGetGridSize(fallbackGridSize);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Anchor", EditorStyles.boldLabel);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        anchorLocalPos = EditorGUILayout.Vector3Field("Anchor Local Position", anchorLocalPos);
        anchorLocalEuler = EditorGUILayout.Vector3Field("Anchor Local Rotation (Euler)", anchorLocalEuler);
        createIfMissing = EditorGUILayout.Toggle("Create if missing", createIfMissing);
        recenterToAnchor = EditorGUILayout.Toggle(new GUIContent("Recenter content to anchor",
            "Moves all children by -anchor.position, then zeroes anchor so the door becomes (0,0,0)"), recenterToAnchor);
        updateColliderFromProperties = EditorGUILayout.Toggle(new GUIContent(
            "Update BoxCollider from RoomProperties",
            "Centers collider half a room-deep forward (+Z) and sizes it to the footprint"),
            updateColliderFromProperties);

        EditorGUILayout.Space();

        if (GUILayout.Button("Read Anchor From FIRST Selected"))
            ReadAnchorFromFirstSelected();

        if (GUILayout.Button("Apply to Selected PREFAB ASSETS"))
            ApplyToSelection(prefabAssets: true);

        if (GUILayout.Button("Apply to Selected SCENE INSTANCES"))
            ApplyToSelection(prefabAssets: false);

        EditorGUILayout.HelpBox(
            "Tip: Select one correctly-set room, click 'Read Anchor...', then select all other rooms and click 'Apply'.",
            MessageType.Info);
    }

    // ----- Core -----

    void ApplyToSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("Select prefab assets or scene objects first.");
            return;
        }

        int changed = 0;
        foreach (var obj in objs)
        {
            if (prefabAssets)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (ProcessRoot(root)) changed++;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                if (obj is GameObject go)
                {
                    if (ProcessRoot(go)) changed++;
                }
            }
        }

        Debug.Log($"[Room Anchor Tool] Processed {objs.Length} item(s), changed {changed}.");
    }

    bool ProcessRoot(GameObject root)
    {
        if (!root) return false;
        Undo.RegisterFullObjectHierarchyUndo(root, "Batch Room Anchor Tool");

        // Find or create anchor
        var anchor = FindOrCreateAnchor(root);
        if (!anchor) { Debug.LogWarning($"[{root.name}] No anchor found/created."); return false; }

        // Set transform
        SetAnchorTransform(anchor.transform);

        // Optionally recenter to anchor
        if (recenterToAnchor)
            RecenterToAnchor(root, anchor.transform);

        // Optionally refresh collider from properties
        if (updateColliderFromProperties)
            RefreshColliderFromProperties(root);

        EditorUtility.SetDirty(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        return true;
    }

    GameObject FindOrCreateAnchor(GameObject root)
    {
        // Try by name under root first
        Transform t = root.transform.Find(anchorName);
        if (!t)
        {
            // Try by component anywhere under root
            var marker = root.GetComponentInChildren<RoomAnchor>(true);
            if (marker) t = marker.transform;
        }

        if (!t && createIfMissing)
        {
            var go = new GameObject(string.IsNullOrEmpty(anchorName) ? "DoorAnchor" : anchorName);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<RoomAnchor>();
            t = go.transform;
        }

        return t ? t.gameObject : null;
    }

    void SetAnchorTransform(Transform t)
    {
        t.localPosition = anchorLocalPos;
        t.localRotation = Quaternion.Euler(anchorLocalEuler);
        t.localScale = Vector3.one;
    }

    void RecenterToAnchor(GameObject root, Transform anchor)
    {
        Vector3 offset = anchor.localPosition;
        foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == root.transform) continue;    // keep root in place
            tr.localPosition -= offset;
        }
        anchor.localPosition = Vector3.zero;
    }

    void RefreshColliderFromProperties(GameObject root)
    {
        var rp = root.GetComponent<RoomProfile>();
        var col = root.GetComponent<BoxCollider>();
        if (!col) col = root.AddComponent<BoxCollider>();

        if (!rp || !rp.Properties)
        {
            // Still ensure collider exists so spawns/navmesh work.
            if (col.size == Vector3.zero) col.size = new Vector3(2, 2, 2);
            return;
        }

        float g = TryGetGridSize(fallbackGridSize);
        var size = rp.Properties.Size;

        // center: half the room depth forward in +Z (door at origin)
        var c = col.center;
        c.x = 0f;
        c.z = size.y * g;
        col.center = c;

        // size: footprint in XZ, keep existing Y unless tiny
        var s = col.size;
        s.x = Mathf.Max(0.01f, 2f * size.x * g);
        s.z = Mathf.Max(0.01f, 2f * size.y * g);
        if (s.y < 0.01f) s.y = 3f; // default height if unset
        col.size = s;
    }

    void ReadAnchorFromFirstSelected()
    {
        if (Selection.objects.Length == 0) return;

        // Try prefab asset first
        string path = AssetDatabase.GetAssetPath(Selection.objects[0]);
        GameObject root = null;
        bool loadedPrefab = false;

        if (!string.IsNullOrEmpty(path))
        {
            root = PrefabUtility.LoadPrefabContents(path);
            loadedPrefab = true;
        }
        else if (Selection.objects[0] is GameObject go)
            root = go;

        if (!root) return;

        Transform t = root.transform.Find(anchorName);
        if (!t)
        {
            var marker = root.GetComponentInChildren<RoomAnchor>(true);
            if (marker) t = marker.transform;
        }
        if (t)
        {
            anchorLocalPos = t.localPosition;
            anchorLocalEuler = t.localEulerAngles;
            Repaint();
            Debug.Log($"[Room Anchor Tool] Read anchor from '{root.name}': pos {anchorLocalPos}, rot {anchorLocalEuler}");
        }
        else
        {
            Debug.LogWarning($"[Room Anchor Tool] No anchor named '{anchorName}' (or RoomAnchor) found in '{root.name}'.");
        }

        if (loadedPrefab) PrefabUtility.UnloadPrefabContents(root);
    }

    // Try to grab MapGenerator.GRID_SIZE if available
    static float TryGetGridSize(float fallback)
    {
        var type = GetTypeByName("MapGenerator");
        if (type != null)
        {
            var field = type.GetField("GRID_SIZE", BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(float))
                return (float)field.GetValue(null);
        }
        return fallback <= 0f ? 1f : fallback;
    }

    static System.Type GetTypeByName(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
