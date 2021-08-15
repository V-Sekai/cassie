using UnityEngine;
using VRSketch;


public class SurfacePatch : MonoBehaviour
{
    // Different states, different materials
    //public Material SelectMaterial;
    public Material DeleteSelectMaterial;
    public Material DetailDrawMaterial;

    //private ICycle cycle;
    private int patchID;
    private Material baseMaterial;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private LineRenderer lineRenderer;

    private MeshCollider convexMeshCollider;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        lineRenderer = GetComponent<LineRenderer>();

        convexMeshCollider = GetComponentsInChildren<MeshCollider>()[1]; // hacky way to get effectively the child's collider
        convexMeshCollider.enabled = false;
    }

    public void Create(int patchID, ICycle c, Material mat, Color color, bool debugVis = false, bool displayPatches = true)
    {
        // Associate patch object and cycle
        this.patchID = patchID;
        c.AssociateWithPatch(patchID);

        meshRenderer.material = mat;
        this.baseMaterial = mat;

        if (debugVis)
        {
            int N = 10;
            Vector3[] points = c.GetSamples(N);
            Vector3 displacement = Random.onUnitSphere * 0.01f;
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i] + displacement;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
        }
        else
        {
            lineRenderer.enabled = false;
        }

        if (displayPatches)
        {
            // Create mesh
            (bool success, Mesh mesh) = SurfaceManager.GenerateMesh(c);

            if (success)
            {
                meshFilter.mesh = mesh;
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;

                convexMeshCollider.sharedMesh = null;
                convexMeshCollider.sharedMesh = mesh;
            }
        }
    }

    public void Print()
    {
        Debug.Log("patch " + patchID);
    }

    public int GetID()
    {
        return patchID;
    }

    public bool Project(Vector3 pos, Vector3 dir, out Vector3 posOnPatch, out Vector3 normal)
    {
        RaycastHit hit;
        int layerMask = 1 << 8; // only consider colliders in layer 8 (SurfacePatch)

        Physics.queriesHitBackfaces = true; // otherwise only the front faces can be hit

        posOnPatch = pos;
        normal = Vector3.up;

        if (Physics.Raycast(pos, dir, out hit, 10f, layerMask))
        {
            Debug.DrawRay(pos, dir * hit.distance, Color.yellow);
            if (hit.collider.gameObject.GetInstanceID() == gameObject.GetInstanceID())
            {
                posOnPatch = hit.point;
                normal = hit.normal;
                return true;
            }
            //else
            //    return false;
        }

        RaycastHit inverseHit;
        if (Physics.Raycast(pos, -dir, out inverseHit, 10f, layerMask))
        {
            Debug.DrawRay(pos, -dir * inverseHit.distance, Color.yellow);
            if (inverseHit.collider.gameObject.GetInstanceID() == gameObject.GetInstanceID())
            {
                posOnPatch = inverseHit.point;
                normal = inverseHit.normal;
                return true;
            }
        }
        
        return false;
    }

    public bool ClosestPoint(Vector3 inPos, out Vector3 closestPos)
    {
        //closestPos = Physics.ClosestPoint(inPos, meshCollider, transform.position, transform.rotation);

        // Activates convex mesh collider
        convexMeshCollider.enabled = true;

        // Use the convex version of the mesh collider to determine the projection direction
        Vector3 convexClosest = Physics.ClosestPoint(inPos, convexMeshCollider, transform.position, transform.rotation);

        convexMeshCollider.enabled = false;

        closestPos = inPos;

        Vector3 rayTraceDir = (convexClosest - inPos).normalized;

        return Project(inPos, rayTraceDir, out closestPos, out Vector3 _);
    }

    public bool SurfaceNormal(Vector3 at, out Vector3 normal)
    {
        // Activates convex mesh collider
        convexMeshCollider.enabled = true;

        // Use the convex version of the mesh collider to determine the projection direction
        Vector3 convexClosest = Physics.ClosestPoint(at, convexMeshCollider, transform.position, transform.rotation);

        convexMeshCollider.enabled = false;

        normal = Vector3.up;

        Vector3 rayTraceDir = (convexClosest - at).normalized;

        return Project(at, rayTraceDir, out Vector3 _, out normal);
    }

    public void OnDetailDrawStart()
    {
        //meshRenderer.material = DetailDrawMaterial;
    }

    public void OnDetailDrawStop()
    {
        //meshRenderer.material = baseMaterial;
    }

    public void OnDeleteSelect()
    {
        if (baseMaterial != null && DeleteSelectMaterial != null)
        {
            meshRenderer.material = DeleteSelectMaterial;
        }
    }

    public void OnDeleteDeselect()
    {
        if (baseMaterial != null)
        {
            meshRenderer.material = baseMaterial;
        }
    }

    public int Destroy()
    {
        Destroy(gameObject);
        return patchID;
    }

}
