using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddPatchController : MonoBehaviour
{

    public DrawingCanvas Canvas;

    public bool TryAddPatch(Vector3 worldSpacePos, bool mirroring)
    {
        return Canvas.TryAddPatchAt(worldSpacePos, mirroring);
    }
}
