using UnityEngine;
using System.Collections.Generic;
using System.Linq;


// Responsible for sending constraint events to DrawController
// - position constraints (on collision with strokes and grid points)
// - on surface constraints

// Responsible for updating selected stroke and selected patch of Drawing Canvas
// those correspond to the candidates to be deleted, in case of delete button click
public class BrushCollisions : MonoBehaviour
{
    [SerializeField]
    private CASSIEParametersProvider parameters = null;

    public SelectController selectController;
    public DrawController drawController;

    private Collider Collided = null;

    private List<Collider> collidedQueue = new List<Collider>();


    private void Start()
    {
        // Set correct radius for collider, based on what the proximity threshold value is from the parameters
        float desiredRadius = parameters.Current.DefaultScaleProximityThreshold;

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.radius = desiredRadius / transform.lossyScale.x;
    }

    public void QueryCollision()
    {
        SendConstraint();
    }

    private void LateUpdate()
    {
        // Make sure everything in queue is still existing in scene
        // This sucks but it works
        collidedQueue = collidedQueue.Where(obj => obj != null).ToList();
    }

    private void OnTriggerEnter(Collider other)
    {
        // If the brush is already colliding something
        // Add that to the queue

        // Ignore inner collider
        if (other.CompareTag("BrushCollider"))
            return;

        if (Collided)
            collidedQueue.Add(Collided);
        // Trigger change of main collided object
        OnCollidedChange(other);

        //Debug.Log("entering " + other.tag);
    }


    private void OnTriggerExit(Collider other)
    {

        // If brush exited the current main collided object
        // Attempt to fetch the next collided object from queue

        // Ignore inner collider
        if (other.CompareTag("BrushCollider"))
            return;

        //Debug.Log("leaving " + other.tag);

        // Notify of brush leaving patch
        if (other.GetComponent<SurfacePatch>())
            drawController.OnPatchDeselect(gameObject.transform.position);

        if (other == Collided)
        {
            Collided = null;

            int i = 0;
            while (collidedQueue.Count > i)
            {
                // Check if this object still exists
                if (collidedQueue[i])
                {
                    OnCollidedChange(collidedQueue[i]);
                    collidedQueue.RemoveAt(i);
                    break;
                }
                collidedQueue.RemoveAt(i);
                i++;
            }
                
        }
        // Remove this object from queue as we are not colliding it anymore
        else
            collidedQueue.Remove(other);


    }

    private void OnCollidedChange(Collider newCollided)
    {
        //Debug.Log("main collided is now " + newCollided.tag);
        Collided = newCollided;

        // Try to add a constraint for this collision point
        SendConstraint();

        // Notify canvas of patch collision, to add patch constraint if needed
        SurfacePatch patch = newCollided.GetComponent<SurfacePatch>();
        if (patch != null)
        {
            drawController.OnPatchCollide(patch.GetID(), gameObject.transform.position);
            return;
        }

    }

    private void SendConstraint()
    {
        Collider objectCollided = GetPrioritaryCollider();
        Vector3 position = gameObject.transform.position;
        // Try to add a constraint for this collision point
        if (objectCollided != null)
            drawController.AddConstraint(position, objectCollided.gameObject);
    }

    private Collider GetPrioritaryCollider()
    {
        if (Collided == null)
            return null;

        // If we are on a surface patch, check in collided queue and return first found stroke
        if (Collided.CompareTag("SurfacePatch"))
        {
            Collider prioritary = null;
            foreach (var obj in collidedQueue)
            {
                if (obj.CompareTag("FinalStroke"))
                {
                    prioritary = obj;
                }

                if (obj.CompareTag("MirrorPlane") && prioritary == null)
                {
                    prioritary = obj;
                }
            }
            if (prioritary != null)
                return prioritary;
        }

        // Similarly for the mirror plane
        if (Collided.CompareTag("MirrorPlane"))
        {
            //Debug.Log("Collided mirror plane");
            foreach (var obj in collidedQueue)
            {
                if (obj.CompareTag("FinalStroke"))
                {
                    // only change if stroke point is on mirror
                    //Debug.Log("stroke is in queue");
                    //Vector3 onStrokePoint = obj.ClosestPoint(gameObject.transform.position);

                    FinalStroke stroke = obj.GetComponent<FinalStroke>();
                    MirrorPlane plane = Collided.GetComponent<MirrorPlane>();
                    // Check if the curve can serve to create constraints
                    if (stroke != null && plane != null)
                    {
                        Vector3 onStrokePoint = stroke.ClosestPoint(transform.position);
                        float distanceToMirror = Vector3.Distance(onStrokePoint, plane.Project(onStrokePoint));
                        if (distanceToMirror < 10e-6f)
                        {
                            //Debug.Log("found a collision with stroke");
                            return obj;
                        }
                    }

                }
            }
            //Debug.Log("mirror plane is the only collided thing");
        }

        return Collided;
    }

}