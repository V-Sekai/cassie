using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSketch
{
    public partial class Graph
    {
        // This is a private internal class of the Graph class, accessible only through its public interface to outside code
        // This design aims at forbidding the creation of instances by outside code (only the Graph class is authorized to instantiate this class, since it needs to keep track of such objects)
        private class Cycle : ICycle
        {

            public LinkedList<HalfSegment> HalfSegments;
            private int patchID = -1; // Store reference to the patch object in the Unity scene (this is useful when we later want to delete this patch object).
            private int hashCode; // The hashcode is a compact representation of the cycle, which uniquely identifies it by the ordered list of segments that it contains. This is useful to quickly determine if 2 cycles are duplicates.
            private bool userCreated;

            public int HashCode
            {
                get
                {
                    if (updateHashCode)
                        UpdateHashCode();
                    return hashCode;
                }
                private set
                {
                    hashCode = value;
                }
            }
            private bool updateHashCode;

            public Cycle(bool userCreated, LinkedList<HalfSegment> segments)
            {
                HalfSegments = segments;
                this.userCreated = userCreated;
                updateHashCode = true;
            }

            public void RepairAt(Segment oldSegment, Segment newSegment, bool inStrokeOrder)
            {
                LinkedListNode<HalfSegment> oldHS = FindNode(oldSegment);

                if (oldHS == null)
                {
                    Debug.LogError("didn't find old segment to repair at");
                    return;
                }

                //Debug.Log("adding segment " + newSegment.ID + " to cycle " + ID);

                // Create new half segment (it goes in the same orientation as the old half segment)
                HalfSegment newHS = new HalfSegment(newSegment, oldHS.Value.IsReversed);

                // Determine whether we should add the new segment before or after the old segment
                // inStrokeOrder boolean indicates that new segment is added after the old segment relative to the stroke direction
                if (inStrokeOrder)
                {
                    if (oldHS.Value.IsReversed)
                        HalfSegments.AddBefore(oldHS, newHS);
                    else
                        HalfSegments.AddAfter(oldHS, newHS);
                }
                else
                {
                    if (oldHS.Value.IsReversed)
                        HalfSegments.AddAfter(oldHS, newHS);
                    else
                        HalfSegments.AddBefore(oldHS, newHS);
                }

                updateHashCode = true;
            }

            public void Remove(Segment s)
            {
                if (!Contains(s))
                    return;

                LinkedListNode<HalfSegment> toRemove = FindNode(s);

                HalfSegments.Remove(toRemove);

                updateHashCode = true;
            }

            public void AssociateWithPatch(int id)
            {
                this.patchID = id;
            }

            public bool Contains(ISegment s)
            {
                foreach (var hs in HalfSegments)
                {
                    if (hs.GetSegmentID() == s.ID)
                        return true;
                }
                return false;
            }

            public bool Contains(Segment s1, Segment s2)
            {
                // Check if cycle contains those segments consecutively
                LinkedListNode<HalfSegment> hs1 = FindNode(s1);

                if (hs1 == null)
                    return false;

                if ((hs1.Next != null && hs1.Next.Value.GetSegmentID() == s2.ID)
                    || (hs1.Next == null && HalfSegments.First.Value.GetSegmentID() == s2.ID)
                    || (hs1.Previous != null && hs1.Previous.Value.GetSegmentID() == s2.ID)
                    || (hs1.Previous == null) && HalfSegments.Last.Value.GetSegmentID() == s2.ID)
                    return true;

                return false;
            }

            public int GetPatchID()
            {
                return this.patchID;
            }

            public SerializablePatch Serialize()
            {
                return new SerializablePatch(patchID, !userCreated, GetStrokeIDs());
            }

            public Vector3[] GetNodesPosition()
            {
                Vector3[] points = new Vector3[HalfSegments.Count];
                int i = 0;
                foreach (var segment in HalfSegments)
                {
                    points[i] = segment.GetStart().Position;
                    i++;
                }
                return points;
            }

            public Vector3[] GetSamples(float targetEdgeLength)
            {
                List<Vector3> points = new List<Vector3>();
                foreach (var segment in HalfSegments)
                {
                    points.AddRange(segment.GetSamples(targetEdgeLength));
                }
                return points.ToArray();
            }

            public string Print()
            {
                return "Cycle with " + HalfSegments.Count + " segments";
            }

            public override int GetHashCode()
            {
                return HashCode;
            }

            public override bool Equals(object other)
            {
                if (other as Cycle != null)
                    return ((Cycle)other).HashCode == HashCode;
                return false;
            }

            private List<int> GetStrokeIDs()
            {
                List<int> strokeIDs = new List<int>();
                foreach (var s in HalfSegments)
                {
                    strokeIDs.Add(s.GetStrokeID());
                }
                return strokeIDs;
            }

            private LinkedListNode<HalfSegment> FindNode(Segment s)
            {
                // Check if cycle contains those segments consecutively
                LinkedListNode<HalfSegment> hs = HalfSegments.First;
                while (hs != null)
                {
                    if (hs.Value.GetSegmentID() == s.ID)
                    {
                        return hs;
                    }
                    hs = hs.Next;
                }
                return null;
            }

            private void UpdateHashCode()
            {
                //Debug.Log("updating hash code");
                List<int> segmentsID = new List<int>();

                string ids = "IDs ";

                foreach (var halfSegment in HalfSegments)
                {
                    segmentsID.Add(halfSegment.GetSegmentID());
                }

                segmentsID.Sort();

                int hash = 0x2D2816FE;
                foreach (int element in segmentsID)
                {
                    hash = unchecked(31 * hash +
                        EqualityComparer<int>.Default.GetHashCode(element));
                    ids += element + " ";
                }

                this.hashCode = hash;
                //Debug.Log("hashCode is " + this.hashCode + " , for IDs: " + ids);
                this.updateHashCode = false;
            }
        }
    }
}
