// Editor/AddRootCubeWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public class AddRootCubeWindow : EditorWindow
{
    // Visuals
    bool addRenderer = true;
    Material cubeMaterial = null;

    // Collider (on ROOT)
    bool addBoxCollider = true;
    bool useExistingRootBoxColliderIfFound = true;
    bool colliderIsTrigger = false;
    string colliderLayerName = "Environment"; // leave empty to not change the layer

    // Cube geometry baked into the mesh (root transform is not changed)
    Vector3 cubeSize = new Vector3(1, 1, 1);
    Vector3 cubeOffset = Vector3.zero;       // local offset from root
    Vector3 cubeRotationEuler = Vector3.zero; // baked rotation (degrees) around offset

    // Name used for the mesh sub-asset on prefab assets
    const string MeshName = "RootHelperCubeMesh";

    [MenuItem("Tools/Rooms/Add/Update ROOT Cube")]
    static void Open() => GetWindow<AddRootCubeWindow>("Root Cube (Base)");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Root Cube (Base of Prefab)", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
        addRenderer = EditorGUILayout.Toggle("Add MeshRenderer", addRenderer);
        cubeMaterial = (Material)EditorGUILayout.ObjectField("Material (optional)", cubeMaterial, typeof(Material), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Collider (on ROOT)", EditorStyles.boldLabel);
        addBoxCollider = EditorGUILayout.Toggle("Add/Update BoxCollider", addBoxCollider);
        useExistingRootBoxColliderIfFound = EditorGUILayout.Toggle("Prefer existing BoxCollider on root", useExistingRootBoxColliderIfFound);
        colliderIsTrigger = EditorGUILayout.Toggle("Collider is Trigger", colliderIsTrigger);
        colliderLayerName = EditorGUILayout.TextField(new GUIContent("Set Layer (optional)", "Leave blank to not change"), colliderLayerName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cube Geometry (baked)", EditorStyles.boldLabel);
        cubeSize = EditorGUILayout.Vector3Field("Size", cubeSize);
        cubeOffset = EditorGUILayout.Vector3Field("Offset (local)", cubeOffset);
        cubeRotationEuler = EditorGUILayout.Vector3Field("Rotation (Euler, baked)", cubeRotationEuler);

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply to SELECTED PREFAB ASSETS"))
            ProcessSelection(prefabAssets: true);

        if (GUILayout.Button("Apply to SELECTED SCENE OBJECTS"))
            ProcessSelection(prefabAssets: false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Adds/updates a cube directly on the PREFAB ROOT (no child objects).\n" +
            "- Builds a fresh mesh with baked size/offset/rotation each time.\n" +
            "- On prefab assets, stores the mesh as a sub-asset named '" + MeshName + "'.\n" +
            "- Optionally adds/updates a BoxCollider on the root matching the cube.",
            MessageType.Info);
    }

    void ProcessSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[RootCube] Nothing selected.");
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

                Undo.RegisterFullObjectHierarchyUndo(root, "Add/Update ROOT Cube");
                if (ApplyToRoot(root, prefabPath: path))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    modified++;
                }
                PrefabUtility.UnloadPrefabContents(root);
                total++;
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Add/Update ROOT Cube");
                if (ApplyToRoot(go, prefabPath: null)) modified++;
                total++;
            }
        }

        Debug.Log($"[RootCube] Processed {total} object(s); modified {modified}.");
    }

    bool ApplyToRoot(GameObject root, string prefabPath)
    {
        bool changed = false;

        // Create a fresh mesh for this run
        var newMesh = GenerateCubeMesh(cubeSize, cubeOffset, cubeRotationEuler);

        // If editing a prefab asset, add the mesh as a sub-asset first (so the object is persistent)
        Mesh oldSubAsset = null;
        if (!string.IsNullOrEmpty(prefabPath))
        {
            // Find any existing sub-asset we previously created (same name)
            var all = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
            oldSubAsset = all.FirstOrDefault(a => a is Mesh && a.name == MeshName) as Mesh;

            newMesh.name = MeshName;
            AssetDatabase.AddObjectToAsset(newMesh, prefabPath);
            // Do not import right now; SaveAssets after reassignments
        }

        // Ensure MeshFilter/Renderer on ROOT (no children)
        MeshFilter mf = root.GetComponent<MeshFilter>();
        MeshRenderer mr = root.GetComponent<MeshRenderer>();

        if (addRenderer)
        {
            if (!mf) { mf = root.AddComponent<MeshFilter>(); changed = true; }
            if (!mr) { mr = root.AddComponent<MeshRenderer>(); changed = true; }
            if (cubeMaterial && mr.sharedMaterial != cubeMaterial) { mr.sharedMaterial = cubeMaterial; changed = true; }
            if (mf.sharedMesh != newMesh) { mf.sharedMesh = newMesh; changed = true; }
        }
        else
        {
            // If you don't want visuals, it's okay to still hold the mesh in MF for reference (optional).
            if (!mf) { mf = root.AddComponent<MeshFilter>(); changed = true; }
            if (mf.sharedMesh != newMesh) { mf.sharedMesh = newMesh; changed = true; }
            if (mr) { DestroyImmediate(mr); changed = true; }
        }

        // Now that the new mesh is assigned, safely remove the old sub-asset (if any)
        if (!string.IsNullOrEmpty(prefabPath) && oldSubAsset != null && oldSubAsset != newMesh)
        {
            AssetDatabase.RemoveObjectFromAsset(oldSubAsset);
        }

        // Save sub-asset changes (no ImportAsset to avoid destroying instances we just created)
        if (!string.IsNullOrEmpty(prefabPath))
        {
            AssetDatabase.SaveAssets();
        }

        // BoxCollider on ROOT (center/size match baked cube)
        if (addBoxCollider)
        {
            BoxCollider bc = null;
            if (useExistingRootBoxColliderIfFound)
                bc = root.GetComponent<BoxCollider>();

            if (!bc) { bc = root.AddComponent<BoxCollider>(); changed = true; }

            bc.center = cubeOffset;
            bc.size = new Vector3(Mathf.Abs(cubeSize.x), Mathf.Abs(cubeSize.y), Mathf.Abs(cubeSize.z));
            bc.isTrigger = colliderIsTrigger;

            if (!string.IsNullOrEmpty(colliderLayerName))
            {
                int layer = LayerMask.NameToLayer(colliderLayerName);
                if (layer >= 0 && root.layer != layer) { root.layer = layer; changed = true; }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        return changed;
    }

    // Build a fresh cube mesh with baked size/offset/rotation
    Mesh GenerateCubeMesh(Vector3 size, Vector3 offset, Vector3 euler)
    {
        var mesh = new Mesh { name = MeshName };

        // 8-vertex cube (centered at origin), then scale/rotate/offset
        Vector3[] verts =
        {
            new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f),
            new Vector3( 0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f),
            new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f),
            new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
        };

        int[] tris =
        {
            0,2,1, 0,3,2, // back
            4,5,6, 4,6,7, // front
            4,0,1, 4,1,5, // bottom
            3,7,6, 3,6,2, // top
            4,7,3, 4,3,0, // left
            1,2,6, 1,6,5  // right
        };

        var rot = Quaternion.Euler(euler);
        Vector3 sz = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        for (int i = 0; i < verts.Length; i++)
        {
            var v = Vector3.Scale(verts[i], sz);
            v = rot * v;
            v += offset;
            verts[i] = v;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
#endif
