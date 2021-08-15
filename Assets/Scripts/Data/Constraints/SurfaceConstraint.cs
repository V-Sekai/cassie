using Curve;
using UnityEngine;

public class SurfaceConstraint
{
    public int PatchID { get; }
    public Vector3 StartPosition { get; } // in canvas space
    //public float StartTime { get; }
    public bool LeftMidStroke { get; private set; } = false;
    public Vector3 EndPosition { get; private set; } // in canvas space
    //public float EndTime { get; private set; }

    public SurfaceConstraint(int patchID, Vector3 startPosition)
    {
        this.PatchID = patchID;
        this.StartPosition = startPosition;
        //this.StartTime = Time.time;
    }

    public void Leave(Vector3 position)
    {
        this.LeftMidStroke = true;
        this.EndPosition = position;
        //this.EndTime = Time.time;
    }

}