using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if(!Application.isPlaying)
            return;
        if(GUILayout.Button("Iterate"))
            MapGenerator.Instance.Iterate();
    }
}
