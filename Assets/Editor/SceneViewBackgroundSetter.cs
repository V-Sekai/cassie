using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class SceneViewBackgroundSetter
{
    static SceneViewBackgroundSetter()
    {
        SceneView.duringSceneGui += SetBackgroundColor;
    }

    private static void SetBackgroundColor(SceneView sceneView)
    {
        sceneView.background = new Color(0.4f, 0.4f, 0.4f, 1f); // Editing grey (darker grey)
    }
}