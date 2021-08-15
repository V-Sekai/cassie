using UnityEngine;
using System.Collections;

namespace VRSketch
{
    public interface ICycle
    {
        Vector3[] GetNodesPosition();
        Vector3[] GetSamples(float targetEdgeLength);
        string Print();
        int GetPatchID();
        SerializablePatch Serialize();

        void AssociateWithPatch(int patchID);

        bool Contains(ISegment s);
    }
}
