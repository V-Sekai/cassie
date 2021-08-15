using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloseBrushCollisions : MonoBehaviour
{
    public SelectController selectController;

    private float colliderRadius;

    private void Start()
    {
        colliderRadius = transform.lossyScale.x * GetComponent<SphereCollider>().radius;
    }

    private void OnTriggerEnter(Collider other)
    {
        selectController.OnDeleteCollision(other);
    }

    private void OnTriggerExit(Collider other)
    {
        selectController.OnDeleteCollisionExit(other);

        // Look for potential other collided stuff
        Collider[] collided = Physics.OverlapSphere(transform.position, colliderRadius);
        if (collided.Length > 0)
            selectController.OnDeleteCollision(collided[0]);
    }
}
