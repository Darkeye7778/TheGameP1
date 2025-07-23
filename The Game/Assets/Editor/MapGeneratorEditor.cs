
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
        
        if(GUILayout.Button("Clean Up"))
            MapGenerator.Instance.Cleanup();
        
        if(GUILayout.Button("Regenerate"))
        {
            MapGenerator.Instance.CustomSeed = 0;
            MapGenerator.Instance.Generate();
        }
        
        if(GUILayout.Button("Regenerate (Same Seed)"))
        {
            MapGenerator.Instance.CustomSeed = MapGenerator.Instance.Seed;
            MapGenerator.Instance.Generate();
        }
    }
}
