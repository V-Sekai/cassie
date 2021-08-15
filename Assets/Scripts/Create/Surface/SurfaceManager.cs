using System.Collections.Generic;
using UnityEngine;
using VRSketch;
using System.Runtime.InteropServices;
using System;

public class SurfaceManager : MonoBehaviour
{

    public GameObject SurfacePrefab;
    public Material SurfacePatchMaterial;
    public DrawingCanvas Canvas;
    public Camera mainCamera; // for view direction, as a fall back on surface projection
    public InputController inputController; // need this link to send surface patch log info...

    public bool DebugVisualization = false;
    public bool DisplayPatches = true;

    private Dictionary<int, SurfacePatch> _surfacePatches = new Dictionary<int, SurfacePatch>();
    private int zFightingOffsetIdx;
    private float zFightingOffsetBase;
    private int _patchID = 0;


    // TRIANGULATION API
    [DllImport("Triangulation_dll")]
    private static extern bool Triangulate(double[] boundary, int nB, float targetEdgeLength, ref IntPtr vertices, ref IntPtr faces, ref int nV, ref int nF);

    [DllImport("Triangulation_dll")]
    private static extern void CleanUp(ref IntPtr vertices, ref IntPtr faces);

    private void Awake()
    {
        IntPtr verticesPtr = IntPtr.Zero;
        IntPtr facesPtr = IntPtr.Zero;

        int nF = 0;
        int nV = 0;
        float targetEdgeLength = 0.02f;
        int nB = 1;
        double[] boundary = new double[] { 0.0, 0.0, 0.0 };

        bool success = Triangulate(boundary, nB, targetEdgeLength, ref verticesPtr, ref facesPtr, ref nV, ref nF);

        // Clean up unmanaged memory
        CleanUp(ref verticesPtr, ref facesPtr);
    }

    private void Start()
    {
        zFightingOffsetIdx = Shader.PropertyToID("_ZFightingOffset");
        zFightingOffsetBase = SurfacePatchMaterial.GetFloat(zFightingOffsetIdx);
    }

    private void OnDestroy()
    {
        // Reset surface patch material
        SurfacePatchMaterial.SetFloat(zFightingOffsetIdx, zFightingOffsetBase);
    }

    public void AddPatch(ICycle cycle)
    {
        GameObject patchObject = Canvas.Create(SurfacePrefab, Primitive.Surface);
        SurfacePatch patch = patchObject.GetComponent<SurfacePatch>();
        //patch.SetMaterial(SurfacePatchMaterial);
        patch.Create(_patchID, cycle, SurfacePatchMaterial, FewColors.Get(_surfacePatches.Count), debugVis: DebugVisualization, displayPatches: DisplayPatches);
        _surfacePatches.Add(_patchID, patch);
        _patchID++;

        // Log action
        inputController.OnPatchAdd(cycle.Serialize());
    }

    public int DeletePatch(int cycleID)
    {
        if (_surfacePatches.TryGetValue(cycleID, out SurfacePatch toDelete))
        {
            _surfacePatches.Remove(cycleID);

            // Log action (already logged in input controller, if patch was deleted by user)
            //inputController.OnPatchDelete(cycleID);

            return toDelete.Destroy();
        }
        Debug.Log("didn't find patch " + cycleID);
        return -1;
    }

    public void Scale(float newScale)
    {
        // Update z fighting offset value in shader
        SurfacePatchMaterial.SetFloat(zFightingOffsetIdx, zFightingOffsetBase * newScale);
    }

    public bool ProjectOnPatch(int patchID, Vector3 pos, out Vector3 posOnPatch, bool canvasSpace = false)
    {
        if (_surfacePatches.TryGetValue(patchID, out SurfacePatch patch))
        {
            Vector3 inPos = pos;
            if (canvasSpace)
            {
                inPos = Canvas.transform.TransformPoint(pos);
            }
            //bool success = patch.Project(inPos, dir, out posOnPatch);
            bool success = patch.ClosestPoint(inPos, out posOnPatch);
            if (!success)
            {
                // Attempt projecting along view direction
                Vector3 dir = (inPos - mainCamera.transform.position).normalized;
                success = patch.Project(inPos, dir, out posOnPatch, out Vector3 _);
                //Debug.Log("projected along view dir instead");
            }
            if (success && canvasSpace)
                posOnPatch = Canvas.transform.InverseTransformPoint(posOnPatch);
            return success;
        }
        //Debug.Log("patch " + patchID + " not found");
        posOnPatch = Vector3.zero;
        return false;
    }

    public bool BoundsPatch(int patchID, ISegment s)
    {
        if (_surfacePatches.TryGetValue(patchID, out SurfacePatch patch))
        {
            // Query graph to figure out if the cycle is bound by segment s
            return Canvas.Graph.SegmentBoundsCycle(s, patchID);
            //return patch.IsBoundBy(s);
        }
        else
            return false;
    }

    public void OnDetailDrawStart(int patchID)
    {
        if (_surfacePatches.TryGetValue(patchID, out SurfacePatch patch))
            patch.OnDetailDrawStart();
    }

    public void OnDetailDrawStop(int patchID)
    {
        if (_surfacePatches.TryGetValue(patchID, out SurfacePatch patch))
            patch.OnDetailDrawStop();
    }

    public bool GetNormal(int patchID, Vector3 at, out Vector3 normal, bool canvasSpace = false)
    {
        normal = Vector3.up;
        if (_surfacePatches.TryGetValue(patchID, out SurfacePatch patch))
        {
            Vector3 atPos = at;
            if (canvasSpace)
            {
                atPos = Canvas.transform.TransformPoint(at);
            }
            if (patch.SurfaceNormal(atPos, out normal))
            {
                if (canvasSpace)
                    normal = Canvas.transform.InverseTransformDirection(normal);
                return true;
            }
        }
        return false;
    }

    public static (bool, Mesh) GenerateMesh(ICycle cycle)
    {
        Mesh mesh = new Mesh();
        float targetEdgeLength = 0.02f;

        // Get boundary vertices from cycle
        Vector3[] boundaryVec3 = cycle.GetSamples(targetEdgeLength: targetEdgeLength);

        // Triangulate
        int nB = boundaryVec3.Length;
        double[] boundary = new double[3 * nB];

        // Prevent empty boundary triangulation
        if (nB == 0)
            return (false, mesh);

        for (int i = 0; i < nB; i++)
        {
            boundary[3 * i + 0] = boundaryVec3[i].x;
            boundary[3 * i + 1] = boundaryVec3[i].y;
            boundary[3 * i + 2] = boundaryVec3[i].z;
        }

        IntPtr verticesPtr = IntPtr.Zero;
        IntPtr facesPtr = IntPtr.Zero;

        int nF = 0;
        int nV = 0;

        // Triangulate patch with code from [An Algorithm for Triangulating Multiple 3D Polygons, Ming Zou et al., 2013]
        // and refine the geometry by subdivision and smoothing with CGAL
        // we wrapped this code in a DLL, which is provided in the repo, check out repo readme to find the source of our wrapper
        bool success = Triangulate(boundary, nB, targetEdgeLength, ref verticesPtr, ref facesPtr, ref nV, ref nF);

        Debug.Log($"[MESHING] patch {cycle.GetPatchID()}, success = {success}, face count = {nF}");

        if (success)
        {

            // IntPtr to arrays
            // Load the results into a managed array.
            double[] vertices = new double[nV * 3];
            int[] faces = new int[nF * 3];
            Marshal.Copy(verticesPtr
                , vertices
                , 0
                , nV * 3);

            Marshal.Copy(facesPtr
                , faces
                , 0
                , nF * 3);

            Vector3[] meshVertices = new Vector3[nV] ;

            for (int i = 0; i < nV; i++)
            {
                meshVertices[i] = new Vector3(
                    (float)vertices[3 * i + 0],
                    (float)vertices[3 * i + 1],
                    (float)vertices[3 * i + 2]
                    );
            }

            //mesh.SetVertices(meshVertices.ToList());
            mesh.vertices = meshVertices;
            mesh.SetIndices(faces, MeshTopology.Triangles, 0);
        }

        // Clean up unmanaged memory
        CleanUp(ref verticesPtr, ref facesPtr);


        return (success, mesh);
    }

}
