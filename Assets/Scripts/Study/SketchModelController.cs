using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRSketch;

public class SketchModelController : MonoBehaviour
{
    public List<GameObject> availableModels;
    public List<GameObject> availableExamples;
    //public Camera sceneCamera;
    public Material modelMaterial;
    public Material exampleMaterial;

    private GameObject currentModel;
    private GameObject currentExample;
    private int _FocusPointIdx;

    private void Start()
    {
        _FocusPointIdx = Shader.PropertyToID("_FocusPoint");
    }

    public void UpdateHandPos(Vector3 pos)
    {
        // Update focus point position
        modelMaterial.SetVector(_FocusPointIdx, pos);
    }

    public bool SetModel(SketchModel model, Vector3 origin)
    {
        if ((int)model < availableModels.Count)
        {
            GameObject newModelPrefab = availableModels[(int)model];
            if (newModelPrefab == null)
                return false;

            // Instantiate new model
            GameObject newModel = Instantiate(newModelPrefab, Vector3.zero, Quaternion.identity, this.transform);
            newModel.transform.localPosition = origin;
            newModel.transform.localRotation = Quaternion.identity;
            newModel.transform.localScale = Vector3.one;

            // Set material
            foreach(var el in newModel.GetComponentsInChildren< Renderer >())
            {
                el.material = modelMaterial;
            }

            // Destroy old model
            if (currentModel != null)
                Destroy(currentModel);

            currentModel = newModel;

            return true;
        }
        else
            return false;
    }

    public bool ShowExample(SketchModel model, Vector3 origin)
    {
        if ((int)model < availableExamples.Count)
        {
            GameObject newModelPrefab = availableExamples[(int)model];
            if (newModelPrefab == null)
                return false;

            // Instantiate new model
            GameObject newModel = Instantiate(newModelPrefab, Vector3.zero, Quaternion.identity, this.transform);
            newModel.transform.localPosition = origin;
            newModel.transform.localRotation = Quaternion.identity;
            newModel.transform.localScale = Vector3.one;

            // Set material
            foreach (var el in newModel.GetComponentsInChildren<Renderer>())
            {
                el.material = exampleMaterial;
            }

            currentExample = newModel;

            return true;
        }
        else
            return false;
    }

    public void HideExample()
    {
        // Destroy old model
        if (currentExample != null)
            Destroy(currentExample);
    }

    public void HideModel()
    {
        // Destroy old model
        if (currentModel != null)
            Destroy(currentModel);
    }
}
