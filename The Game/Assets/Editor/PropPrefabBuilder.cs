#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PropPrefabBuilder
{
    const string DEFAULT_OUT_DIR = "Assets/Finn/Props";

    [MenuItem("Tools/Props/Build Prefabs From Selected Models (to Assets/Finn/Props)")]
    public static void BuildDefault() => BuildTo(DEFAULT_OUT_DIR);

    [MenuItem("Tools/Props/Build Prefabs From Selected Models…")]
    public static void BuildPickFolder()
    {
        var abs = EditorUtility.SaveFolderPanel("Choose Output Folder", Application.dataPath, "Props");
        if (string.IsNullOrEmpty(abs)) return;
        var rel = "Assets" + abs.Replace(Application.dataPath, "");
        BuildTo(rel);
    }

    static void BuildTo(string outDir)
    {
        EnsureFolder(outDir);

        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (modelAsset == null) continue; // not a model

            // Make a temporary instance of the imported model so we can inspect hierarchy
            var inst = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;

            // 1) Find the actual content root.
            //    Common patterns: wrapper (no renderers) + single child named "model" (case-insensitive).
            GameObject contentRoot = inst;
            if (!HasAnyRenderer(inst))
            {
                // Prefer a child named "model"
                var m = inst.transform.Find("model") ?? FindCaseInsensitive(inst.transform, "model");
                if (m != null) contentRoot = m.gameObject;
                // Or, if the wrapper has exactly one child, use that
                else if (inst.transform.childCount == 1)
                    contentRoot = inst.transform.GetChild(0).gameObject;
            }

            // 2) Create the final prefab root
            string baseName = Sanitize(modelAsset.name);
            var root = new GameObject(baseName) { isStatic = true };

            // 3) Reparent the content under our new root (keep world transforms)
            contentRoot.transform.SetParent(root.transform, true);

            // If we pulled a child out, destroy the empty wrapper instance to avoid duplicates
            if (inst != null && inst != contentRoot)
                Object.DestroyImmediate(inst);

            // 4) Fit a BoxCollider to the renderers (on the content node, accurate even when rotated)
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                // Combine world-space bounds
                Bounds wb = new Bounds(rends[0].bounds.center, Vector3.zero);
                foreach (var r in rends) wb.Encapsulate(r.bounds);

                // Transform 8 corners into the collider owner's local space
                var colliderOwner = contentRoot.transform; // put collider here (matches mesh transforms)
                var lb = BoundsInLocalSpace(wb, colliderOwner);

                var box = colliderOwner.GetComponent<BoxCollider>();
                if (!box) box = colliderOwner.gameObject.AddComponent<BoxCollider>();
                box.center = lb.center;
                box.size = lb.size;
            }

            // 5) Save prefab
            var filename = AssetDatabase.GenerateUniqueAssetPath($"{outDir}/{baseName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, filename);

            // cleanup temp scene objects
            Object.DestroyImmediate(root);
            if (inst != null) Object.DestroyImmediate(inst);

            Debug.Log($"[Props] Built {filename}");
        }

        AssetDatabase.Refresh();
    }

    // ---------- helpers ----------

    static bool HasAnyRenderer(GameObject go) =>
        go.GetComponentInChildren<Renderer>(true) != null;

    static Transform FindCaseInsensitive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase)) return c;
        }
        return null;
    }

    static Bounds BoundsInLocalSpace(Bounds worldBounds, Transform localSpace)
    {
        // Build the 8 corners of the world AABB
        Vector3 min = worldBounds.min, max = worldBounds.max;
        Vector3[] corners =
        {
            new(min.x, min.y, min.z), new(max.x, min.y, min.z),
            new(min.x, max.y, min.z), new(max.x, max.y, min.z),
            new(min.x, min.y, max.z), new(max.x, min.y, max.z),
            new(min.x, max.y, max.z), new(max.x, max.y, max.z),
        };

        // Encapsulate in local space of the target transform
        var lb = new Bounds(localSpace.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            lb.Encapsulate(localSpace.InverseTransformPoint(corners[i]));
        return lb;
    }

    static string Sanitize(string s)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            s = s.Replace(c.ToString(), "_");
        return s.Trim();
    }

    static void EnsureFolder(string rel)
    {
        if (AssetDatabase.IsValidFolder(rel)) return;
        var parts = rel.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
