// Editor/PlaceAnchorOnFloorTopFrontHalvesWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PlaceAnchorOnFloorTopFrontHalvesWindow : EditorWindow
{
    // Names
    string floorName = "Floor";
    string anchorName = "DoorAnchor";
    string createUnderParentName = ""; // optional; blank = root

    // Find options
    bool includeInactive = true;
    bool preferDirectChildren = false;
    bool createAnchorIfMissing = true;

    // Which "front" axis relative to Floor's local space
    enum FrontAxis { PositiveZ, PositiveX }
    FrontAxis frontAxis = FrontAxis.PositiveZ;

    // Horizontal placement across the width
    enum HalfChoice { Center, LeftHalfCenter, RightHalfCenter }
    HalfChoice halfChoice = HalfChoice.Center;

    // How to compute the bounds of the floor
    enum BoundsMode { FromBoxCollider, FromMeshBoundsLocal, FromCombinedRenderers }
    BoundsMode boundsMode = BoundsMode.FromBoxCollider;

    // Offsets (applied after we find the top-front-half-center)
    float upOffset = 0f;          // along floor's local +Y
    float forwardOffset = 0f;     // along local front (+Z or +X)
    float lateralFineOffset = 0f; // small nudge along lateral axis (left/right)
    Vector3 extraWorldOffset = Vector3.zero; // final world offset if needed

    // Placement
    bool useWorldSpace = true; // recommended

    [MenuItem("Tools/Rooms/Place Anchor On Floor TOP-FRONT (Halves)")]
    static void Open() => GetWindow<PlaceAnchorOnFloorTopFrontHalvesWindow>("Anchor -> Floor Top-Front (Halves)");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Names", EditorStyles.boldLabel);
        floorName = EditorGUILayout.TextField("Floor Child Name", floorName);
        anchorName = EditorGUILayout.TextField("Anchor Name", anchorName);
        createUnderParentName = EditorGUILayout.TextField(new GUIContent("Create Anchor Under (optional)", "Blank = prefab root"), createUnderParentName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Find Options", EditorStyles.boldLabel);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        preferDirectChildren = EditorGUILayout.Toggle("Prefer Direct Children", preferDirectChildren);
        createAnchorIfMissing = EditorGUILayout.Toggle("Create Anchor If Missing", createAnchorIfMissing);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Top-Front Definition", EditorStyles.boldLabel);
        frontAxis = (FrontAxis)EditorGUILayout.EnumPopup("Front Axis (Floor local)", frontAxis);
        boundsMode = (BoundsMode)EditorGUILayout.EnumPopup("Bounds Source", boundsMode);
        halfChoice = (HalfChoice)EditorGUILayout.EnumPopup("Across Width", halfChoice);
        EditorGUILayout.HelpBox(
            "Front Axis sets which direction is 'front' in the floor's local space.\n" +
            "Across Width chooses Center, Left half-center, or Right half-center.\n" +
            "Bounds Source controls how we read the floor size:\n" +
            "- FromBoxCollider: use a BoxCollider on the Floor (best if present)\n" +
            "- FromMeshBoundsLocal: use MeshFilter.sharedMesh.bounds\n" +
            "- FromCombinedRenderers: combine all renderers under Floor (robust fallback)",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Offsets", EditorStyles.boldLabel);
        upOffset = EditorGUILayout.FloatField(new GUIContent("Up Offset (+Y)"), upOffset);
        forwardOffset = EditorGUILayout.FloatField(new GUIContent("Forward Offset (+Front)"), forwardOffset);
        lateralFineOffset = EditorGUILayout.FloatField(new GUIContent("Lateral Fine Offset"), lateralFineOffset);
        extraWorldOffset = EditorGUILayout.Vector3Field(new GUIContent("Extra World Offset"), extraWorldOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        useWorldSpace = EditorGUILayout.Toggle(new GUIContent("Use World Space", "Recommended"), useWorldSpace);

        EditorGUILayout.Space();
        if (GUILayout.Button("Process SELECTED PREFAB ASSETS"))
            ProcessSelection(prefabAssets: true);

        if (GUILayout.Button("Process SELECTED SCENE OBJECTS"))
            ProcessSelection(prefabAssets: false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Example: For a 20.3 x 0.03 x 20.3 floor (X x Y x Z) with front = +Z, the left/right half centers are at X = +/- (width/4) = +/- 5.075 from centerline.\n" +
            "This tool computes that automatically from your chosen bounds source.",
            MessageType.Info);
    }

    // -------- main --------
    void ProcessSelection(bool prefabAssets)
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            Debug.LogWarning("[PlaceAnchorTopFrontHalves] Nothing selected.");
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

                Undo.RegisterFullObjectHierarchyUndo(root, "Place Anchor On Floor TOP-FRONT (Halves)");
                if (ProcessRoot(root)) { PrefabUtility.SaveAsPrefabAsset(root, path); modified++; }
                PrefabUtility.UnloadPrefabContents(root);
                total++;
            }
            else if (obj is GameObject go)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Place Anchor On Floor TOP-FRONT (Halves)");
                if (ProcessRoot(go)) modified++;
                total++;
            }
        }

        Debug.Log($"[PlaceAnchorTopFrontHalves] Processed {total} item(s); modified {modified}.");
    }

    bool ProcessRoot(GameObject root)
    {
        var floor = FindChild(root.transform, floorName, includeInactive, preferDirectChildren);
        if (!floor)
        {
            Debug.LogWarning($"[PlaceAnchorTopFrontHalves] {root.name}: Floor '{floorName}' not found.");
            return false;
        }

        // Compute world position at top-front-half-center
        Vector3 worldPos = ComputeTopFrontHalfCenterWorld(floor);
        worldPos += extraWorldOffset;

        // Find or create anchor
        var anchor = FindChild(root.transform, anchorName, includeInactive, preferDirectChildren);
        if (!anchor && createAnchorIfMissing)
        {
            Transform parent = string.IsNullOrEmpty(createUnderParentName)
                ? root.transform
                : (FindChild(root.transform, createUnderParentName, includeInactive, preferDirectChildren) ?? root.transform);

            var go = new GameObject(anchorName);
            go.transform.SetParent(parent, false);
            anchor = go.transform;
        }

        if (!anchor)
        {
            Debug.LogWarning($"[PlaceAnchorTopFrontHalves] {root.name}: Anchor '{anchorName}' not found and create is disabled.");
            return false;
        }

        if (useWorldSpace)
            anchor.position = worldPos;
        else
            anchor.localPosition = (anchor.parent ? anchor.parent : root.transform).InverseTransformPoint(worldPos);

        EditorUtility.SetDirty(anchor.gameObject);
        EditorUtility.SetDirty(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
        return true;
    }

    // -------- computation --------
    Vector3 ComputeTopFrontHalfCenterWorld(Transform floor)
    {
        // Axes in floor local space
        Vector3 localUp = Vector3.up;
        Vector3 localFwd = (frontAxis == FrontAxis.PositiveZ) ? Vector3.forward : Vector3.right;
        Vector3 localLat = (frontAxis == FrontAxis.PositiveZ) ? Vector3.right : Vector3.forward;

        // Try chosen bounds mode, with fallbacks
        switch (boundsMode)
        {
            case BoundsMode.FromBoxCollider:
                {
                    var bc = floor.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        float halfUp = bc.size.y * 0.5f;
                        float halfFront = (frontAxis == FrontAxis.PositiveZ) ? bc.size.z * 0.5f : bc.size.x * 0.5f;
                        float quarterLat = ((frontAxis == FrontAxis.PositiveZ) ? bc.size.x : bc.size.z) * 0.25f;

                        float latOffset = 0f;
                        if (halfChoice == HalfChoice.LeftHalfCenter) latOffset = -quarterLat;
                        if (halfChoice == HalfChoice.RightHalfCenter) latOffset = +quarterLat;
                        latOffset += lateralFineOffset;

                        Vector3 localPoint = bc.center
                                           + localUp * (halfUp + upOffset)
                                           + localFwd * (halfFront + forwardOffset)
                                           + localLat * latOffset;

                        return floor.TransformPoint(localPoint);
                    }
                    // fallback
                    goto case BoundsMode.FromMeshBoundsLocal;
                }

            case BoundsMode.FromMeshBoundsLocal:
                {
                    var mf = floor.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mb = mf.sharedMesh.bounds; // in floor local space
                        float halfUp = mb.extents.y;
                        float halfFront = (frontAxis == FrontAxis.PositiveZ) ? mb.extents.z : mb.extents.x;
                        float quarterLat = ((frontAxis == FrontAxis.PositiveZ) ? mb.extents.x : mb.extents.z) * 0.5f; // extents is half-size => quarter = extents * 0.5

                        float latOffset = 0f;
                        if (halfChoice == HalfChoice.LeftHalfCenter) latOffset = -quarterLat;
                        if (halfChoice == HalfChoice.RightHalfCenter) latOffset = +quarterLat;
                        latOffset += lateralFineOffset;

                        Vector3 localPoint = mb.center
                                           + localUp * (halfUp + upOffset)
                                           + localFwd * (halfFront + forwardOffset)
                                           + localLat * latOffset;

                        return floor.TransformPoint(localPoint);
                    }
                    // fallback
                    goto case BoundsMode.FromCombinedRenderers;
                }

            case BoundsMode.FromCombinedRenderers:
            default:
                {
                    var rends = floor.GetComponentsInChildren<Renderer>(includeInactive);
                    if (rends.Length > 0)
                    {
                        // Combine renderer bounds in FLOOR LOCAL space
                        Matrix4x4 w2l = floor.worldToLocalMatrix;
                        bool first = true;
                        Vector3 minL = Vector3.zero, maxL = Vector3.zero;

                        for (int i = 0; i < rends.Length; i++)
                        {
                            var b = rends[i].bounds;
                            // iterate 8 corners
                            for (int c = 0; c < 8; c++)
                            {
                                Vector3 corner = GetBoundsCorner(b, c);
                                Vector3 pL = w2l.MultiplyPoint3x4(corner);
                                if (first) { minL = maxL = pL; first = false; }
                                else
                                {
                                    minL = Vector3.Min(minL, pL);
                                    maxL = Vector3.Max(maxL, pL);
                                }
                            }
                        }

                        // Top (max Y), Front (max along front axis), Lateral center for half choice
                        float topY = maxL.y;
                        float frontCoord = (frontAxis == FrontAxis.PositiveZ) ? maxL.z : maxL.x;
                        float minLat = (frontAxis == FrontAxis.PositiveZ) ? minL.x : minL.z;
                        float maxLat = (frontAxis == FrontAxis.PositiveZ) ? maxL.x : maxL.z;
                        float centerLat = 0.5f * (minLat + maxLat);

                        float targetLat = centerLat;
                        if (halfChoice == HalfChoice.LeftHalfCenter) targetLat = 0.5f * (minLat + centerLat);
                        if (halfChoice == HalfChoice.RightHalfCenter) targetLat = 0.5f * (centerLat + maxLat);
                        targetLat += lateralFineOffset;

                        Vector3 localPoint;
                        if (frontAxis == FrontAxis.PositiveZ)
                            localPoint = new Vector3(targetLat, topY + upOffset, frontCoord + forwardOffset);
                        else
                            localPoint = new Vector3(frontCoord + forwardOffset, topY + upOffset, targetLat);

                        return floor.TransformPoint(localPoint);
                    }
                    // last resort: use pivot
                    Vector3 fallback = floor.position
                                     + floor.up * upOffset
                                     + ((frontAxis == FrontAxis.PositiveZ) ? floor.forward : floor.right) * forwardOffset
                                     + ((frontAxis == FrontAxis.PositiveZ) ? floor.right : floor.forward) * lateralFineOffset;
                    return fallback;
                }
        }
    }

    static Vector3 GetBoundsCorner(Bounds b, int index)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;
        switch (index)
        {
            case 0: return new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);
            case 1: return new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);
            case 2: return new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);
            case 3: return new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);
            case 4: return new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);
            case 5: return new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);
            case 6: return new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);
            default: return new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);
        }
    }

    // helpers
    Transform FindChild(Transform root, string name, bool includeInactiveArg, bool preferDirect)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (preferDirect)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                if (ch.name == name) return ch;
            }
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactiveArg))
            if (t.name == name) return t;

        return null;
    }
}
#endif

