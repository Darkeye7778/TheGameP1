#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BatchRoomPrefabMaker
{
    [MenuItem("Tools/Rooms/Make/Wrap As Room (overwrite same name)")]
    public static void Run()
    {
        var sel = Selection.objects;
        if (sel == null || sel.Length == 0) { Debug.LogWarning("Select prefabs or model assets in Project."); return; }

        int done = 0, skipped = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var o in sel)
            {
                var path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path)) { skipped++; continue; }

                var ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".prefab")
                {
                    OverwritePrefabInPlace(path);
                    done++;
                }
                else
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) { skipped++; continue; }

                    var prefabPath = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".prefab")
                                     .Replace("\\", "/");

                    CreateOrOverwritePrefabFromModel(model, prefabPath);
                    done++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        Debug.Log($"Room wrapper complete. Overwritten/created={done}, skipped={skipped}");
    }

    static void OverwritePrefabInPlace(string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var rootName = Path.GetFileNameWithoutExtension(prefabPath);
            root.name = rootName;

            var rp = root.GetComponent<RoomProfile>() ?? root.AddComponent<RoomProfile>();
            var bc = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();

            var modelChild = FindChild(root.transform, "Model");
            if (modelChild == null)
            {
                modelChild = new GameObject("Model").transform;
                modelChild.SetParent(root.transform, false);

                var moveList = new List<Transform>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    var c = root.transform.GetChild(i);
                    if (c == modelChild) continue;
                    if (c.name == "DoorAnchor") continue;
                    moveList.Add(c);
                }
                foreach (var t in moveList) t.SetParent(modelChild, true);
            }

            var door = FindChild(root.transform, "DoorAnchor");
            if (door == null)
            {
                var go = new GameObject("DoorAnchor");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            FitBoxColliderToChildren(bc, modelChild, root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CreateOrOverwritePrefabFromModel(GameObject modelAsset, string savePath)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(savePath));
        try
        {
            var rp = root.AddComponent<RoomProfile>();
            var bc = root.AddComponent<BoxCollider>();

            var door = new GameObject("DoorAnchor");
            door.transform.SetParent(root.transform, false);

            var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
            if (modelInstance != null)
            {
                modelInstance.name = "Model";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
            }

            FitBoxColliderToChildren(bc, modelInstance != null ? modelInstance.transform : root.transform, root.transform);

            // Overwrite if exists, else create
            PrefabUtility.SaveAsPrefabAsset(root, savePath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        return null;
    }

    static void FitBoxColliderToChildren(BoxCollider bc, Transform sourceRoot, Transform prefabRoot)
    {
        if (!bc) return;
        var rends = sourceRoot ? sourceRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];

        if (rends.Length == 0)
        {
            bc.center = Vector3.zero;
            bc.size = Vector3.one;
            return;
        }

        bool has = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        foreach (var r in rends)
        {
            var b = r.bounds;
            var c = b.center; var e = b.extents;
            Vector3[] corners = {
                new Vector3(c.x-e.x, c.y-e.y, c.z-e.z),
                new Vector3(c.x-e.x, c.y-e.y, c.z+e.z),
                new Vector3(c.x-e.x, c.y+e.y, c.z-e.z),
                new Vector3(c.x-e.x, c.y+e.y, c.z+e.z),
                new Vector3(c.x+e.x, c.y-e.y, c.z-e.z),
                new Vector3(c.x+e.x, c.y-e.y, c.z+e.z),
                new Vector3(c.x+e.x, c.y+e.y, c.z-e.z),
                new Vector3(c.x+e.x, c.y+e.y, c.z+e.z)
            };
            for (int i = 0; i < 8; i++)
            {
                var lp = prefabRoot.InverseTransformPoint(corners[i]);
                if (!has) { min = max = lp; has = true; }
                else { min = Vector3.Min(min, lp); max = Vector3.Max(max, lp); }
            }
        }
        bc.center = (min + max) * 0.5f;
        bc.size = (max - min);
    }
}
#endif
