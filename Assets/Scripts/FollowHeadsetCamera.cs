using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowHeadsetCamera : MonoBehaviour
{

    public Transform HeadsetTransform;
    public float DepthOffset;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = HeadsetTransform.position - HeadsetTransform.forward * DepthOffset;
        transform.rotation = HeadsetTransform.rotation;
        
    }
}
