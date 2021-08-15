using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSketch
{
    public partial class Graph
    {
        // This helper class simply holds a list of cycles, representing the cycles associated with 1 segment
        // It's only there to simplify some common operations in the Graph class, so it is purely internal (no public interface)
        private class SegmentCycles
        {

            private List<Cycle> cycles = new List<Cycle>();

            public SegmentCycles()
            { }

            public void Add(Cycle cycle)
            {
                cycles.Add(cycle);
            }

            public bool IsManifold()
            {
                return cycles.Count < 2;
            }

            public int CyclesCount()
            {
                return cycles.Count;
            }

            public bool Contains(int patchID)
            {
                foreach (var c in cycles)
                {
                    if (c.GetPatchID() == patchID)
                        return true;
                }
                return false;
            }

            public void Remove(Cycle cycle)
            {
                int idxToRemove = -1;

                for (int i = 0; i < cycles.Count; i++)
                {
                    if (cycles[i].Equals(cycle))
                        idxToRemove = i;
                }

                if (idxToRemove != -1)
                    cycles.RemoveAt(idxToRemove);
                else
                    Debug.LogError("failed to remove cycle " + cycle.Print() + " from segment");
            }

            public List<Cycle> Get()
            {
                return cycles;
            }
        }
    }
}
