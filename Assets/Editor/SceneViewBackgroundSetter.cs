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
        if (sceneView.camera != null)
        {
            sceneView.camera.clearFlags = CameraClearFlags.SolidColor;
            sceneView.camera.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Editing grey (darker grey)
        }
    }
}