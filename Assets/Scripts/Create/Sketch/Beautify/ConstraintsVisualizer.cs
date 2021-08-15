using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRSketch;

public class ConstraintsVisualizer : MonoBehaviour
{

    [Header("Constraints Display Shader")]
    public Shader shader;

    [Header("Constraints display parameters")]
    public float NodesOpacity = 0.9f;
    public float NodesRadius = 0.003f;

    public Transform canvasTransform;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material material;

    // Shader uniforms indices
    private int _PointRadiusIdx;
    private int _ColorIdx;
    private int _OpacityIdx;
    private int _CanvasToWorldMatrixIdx;

    private float pointRadius;

    


    void Start()
    {
        Debug.Log("started visualizer");
        // Display nodes stuff
        material = new Material(shader);
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = new Mesh();
        meshRenderer.material = material;

        pointRadius = NodesRadius;


        // Shader uniform indices
        _PointRadiusIdx = Shader.PropertyToID("_PointRadius");
        _ColorIdx = Shader.PropertyToID("_Color");
        _OpacityIdx = Shader.PropertyToID("_BaseOpacity");
        _CanvasToWorldMatrixIdx = Shader.PropertyToID("_CanvasToWorldMatrix");

        material.SetFloat(_PointRadiusIdx, pointRadius);

        material.SetColor(_ColorIdx, Color.black);
        material.SetFloat(_OpacityIdx, NodesOpacity);
    }

    private void Update()
    {
        material.SetMatrix(_CanvasToWorldMatrixIdx, canvasTransform.localToWorldMatrix);
    }

    public void Display(List<SerializableConstraint> acceptedConstraints, List<SerializableConstraint> rejectedConstraints)
    {
        int N = acceptedConstraints.Count + rejectedConstraints.Count;

        Mesh nodesMesh = new Mesh();

        if (N > 0)
        {
            Vector3[] positions = new Vector3[N];
            Color[] colors = new Color[N];
            int[] indices = new int[N];

            //for (int i = 0; i < N; i++)
            //    indices[i] = i;

            int i = 0;
            foreach (var c in acceptedConstraints)
            {
                indices[i] = i;
                positions[i] = c.position;
                colors[i] = Color.green;
                i++;
            }
            foreach (var c in rejectedConstraints)
            {
                indices[i] = i;
                positions[i] = c.position;
                colors[i] = Color.red;
                i++;
            }

            nodesMesh.SetVertices(positions);
            nodesMesh.SetColors(colors);
            nodesMesh.SetIndices(indices, MeshTopology.Points, 0);

            material.SetFloat(_PointRadiusIdx, pointRadius * Mathf.Pow(transform.localScale.x, 1f));
            material.SetMatrix(_CanvasToWorldMatrixIdx, canvasTransform.localToWorldMatrix);
        }
        else
        {
            nodesMesh.SetVertices(new Vector3[] { });
            nodesMesh.SetIndices(new int[] { }, MeshTopology.Points, 0);
        }
        meshFilter.mesh = nodesMesh;
    }
}
