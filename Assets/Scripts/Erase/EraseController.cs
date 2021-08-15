using UnityEngine;
using VRSketch;

public class EraseController : MonoBehaviour
{
    public DrawingCanvas canvas;

    public void Clear()
    {
        canvas.Clear();
    }

    public bool TryDelete(Vector3 inputPos, out InteractionType interactionType, out int elementID, bool mirror = false)
    {
        return canvas.DeleteSelected(inputPos, out interactionType, out elementID, mirror);
    }
}
