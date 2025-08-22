// Assets/Editor/NegateConnectionPointPositions.cs
// Batch-negates ConnectionPoints[].Transform.Position for RoomProperties assets.
// Adds Undo support, lets you target Selected assets or scan the whole project,
// and choose which axes to flip.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NegateConnectionPointPositionsWindow : EditorWindow
{
    private enum Scope { SelectedAssets, WholeProject }

    private Scope scope = Scope.SelectedAssets;
    private bool flipX = true;
    private bool flipY = true;

    [MenuItem("Tools/MapGen/Negate Connection Points...")]
    static void Open() => GetWindow<NegateConnectionPointPositionsWindow>("Negate Connection Points");

    void OnGUI()
    {
        GUILayout.Label("Batch Negate Connection Point Positions", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scope = (Scope)EditorGUILayout.EnumPopup("Scope", scope);
        flipX = EditorGUILayout.ToggleLeft("Negate X", flipX);
        flipY = EditorGUILayout.ToggleLeft("Negate Y", flipY);

        EditorGUILayout.Space();
        if (GUILayout.Button("Preview Affected Assets"))
        {
            var rps = CollectTargets(scope);
            ShowPreview(rps);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!flipX && !flipY))
        {
            if (GUILayout.Button("Apply Negation"))
            {
                var rps = CollectTargets(scope);
                int changed = ApplyNegation(rps, flipX, flipY);
                EditorUtility.DisplayDialog("Done",
                    $"Processed {rps.Count} RoomProperties asset(s).\n" +
                    $"Changed {changed} connection point(s).", "OK");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This modifies the grid positions stored in each RoomProperties.ConnectionPoints[].Transform.Position.\n" +
                                "Undo is supported. Consider version control or a backup before running on the whole project.", MessageType.Info);
    }

    static List<RoomProperties> CollectTargets(Scope s)
    {
        var result = new List<RoomProperties>();
        if (s == Scope.SelectedAssets)
        {
            foreach (var obj in Selection.objects)
            {
                var rp = obj as RoomProperties;
                if (rp) result.Add(rp);
            }

            // If the user selected folders or other assets, search within them too.
            foreach (var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var subGuid in AssetDatabase.FindAssets("t:RoomProperties", new[] { path }))
                    {
                        var subPath = AssetDatabase.GUIDToAssetPath(subGuid);
                        var rp = AssetDatabase.LoadAssetAtPath<RoomProperties>(subPath);
                        if (rp && !result.Contains(rp)) result.Add(rp);
                    }
                }
            }
        }
        else // WholeProject
        {
            foreach (var guid in AssetDatabase.FindAssets("t:RoomProperties"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rp = AssetDatabase.LoadAssetAtPath<RoomProperties>(path);
                if (rp) result.Add(rp);
            }
        }

        return result;
    }

    static void ShowPreview(List<RoomProperties> rps)
    {
        if (rps.Count == 0)
        {
            EditorUtility.DisplayDialog("Preview", "No RoomProperties assets found for the chosen scope.", "OK");
            return;
        }

        var msg = $"Found {rps.Count} RoomProperties asset(s):\n\n";
        int limit = Mathf.Min(20, rps.Count);
        for (int i = 0; i < limit; i++)
        {
            msg += $"- {AssetDatabase.GetAssetPath(rps[i])}\n";
        }
        if (rps.Count > limit) msg += $"…and {rps.Count - limit} more.";
        EditorUtility.DisplayDialog("Preview", msg, "OK");
    }

    static int ApplyNegation(List<RoomProperties> rps, bool flipX, bool flipY)
    {
        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var rp in rps)
            {
                if (rp == null || rp.ConnectionPoints == null) continue;

                Undo.RecordObject(rp, "Negate Connection Points");

                var cps = rp.ConnectionPoints; // array copy by ref
                bool assetModified = false;

                for (int i = 0; i < cps.Length; i++)
                {
                    var c = cps[i];
                    var gt = c.Transform;

                    var pos = gt.Position; // Vector2
                    var newPos = new Vector2(
                        flipX ? -pos.x : pos.x,
                        flipY ? -pos.y : pos.y
                    );

                    if (newPos != pos)
                    {
                        gt.Position = newPos;
                        c.Transform = gt; // assign back to struct
                        cps[i] = c;      // assign back to array
                        changed++;
                        assetModified = true;
                    }
                }

                if (assetModified)
                {
                    // Ensure the serialized array gets the updated struct values
                    rp.ConnectionPoints = cps;
                    EditorUtility.SetDirty(rp);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        return changed;
    }
}
