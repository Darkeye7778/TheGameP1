// RecenterRooms.cs
using UnityEditor;
using UnityEngine;

public static class RecenterRooms
{
    [MenuItem("Tools/Rooms/Recenter Prefab *Assets* To DoorAnchor")]
    static void RecenterPrefabAssets()
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            DoRecenter(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Rooms/Recenter *Instances* To DoorAnchor")]
    static void RecenterInstances()
    {
        foreach (var go in Selection.gameObjects) DoRecenter(go);
    }

    static void DoRecenter(GameObject root)
    {
        var anchor = root.GetComponentInChildren<RoomAnchor>(true);
        if (!anchor) { Debug.LogWarning($"No RoomAnchor under {root.name}"); return; }

        Vector3 offset = anchor.transform.localPosition;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;   // keep root in place
            t.localPosition -= offset;           // shift content so door becomes (0,0,0)
        }
        anchor.transform.localPosition = Vector3.zero;

        // Optional: nudge the collider center forward by half the room depth
        var rp = root.GetComponent<RoomProfile>();
        var col = root.GetComponent<BoxCollider>();
        if (rp && col)
        {
            float g = MapGenerator.GRID_SIZE;
            col.center = new Vector3(0f, col.center.y, rp.Properties.Size.y * g);
            col.size = new Vector3(2f * rp.Properties.Size.x * g, col.size.y, 2f * rp.Properties.Size.y * g);
        }

        EditorUtility.SetDirty(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
    }
}
