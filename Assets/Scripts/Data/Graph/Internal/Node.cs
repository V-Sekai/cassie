using System;
using System.Collections.Generic;
using UnityEngine;


namespace VRSketch
{

    public partial class Graph
    {
        // This is a private internal class of the Graph class, accessible only through its public interface to outside code
        // This design aims at forbidding the creation of instances by outside code (only the Graph class is authorized to instantiate this class, since it needs to keep track of such objects)
        private class Node : INode
        {
            public int ID { get; }
            public LinkedList<Segment> Neighbors { get; private set; }
            public int IncidentCount { get { return Neighbors.Count; } }
            public Vector3 Position { get; }
            public Vector3 Normal { get; private set; } = Vector3.zero;
            public bool IsSharp { get; private set; } = false;

            public Node(Vector3 position, int ID)
            {
                this.ID = ID;
                this.Neighbors = new LinkedList<Segment>();
                this.Position = position;
            }

            public Vector3[] GetNeighbors()
            {
                Vector3[] tangents = new Vector3[Neighbors.Count];
                int i = 0;
                foreach (var segment in Neighbors)
                {
                    tangents[i] = segment.GetTangentAt(this);
                    i++;
                }
                return tangents;
            }

            // Get next segment in ccw order around the normal
            public ISegment GetNext(ISegment s)
            {
                LinkedListNode<Segment> segmentRef = Neighbors.Find((Segment)s);
                if (segmentRef == null)
                {
                    Debug.LogError("Segment " + s.ID + " not found at node");
                    return null;
                }

                return segmentRef.Next != null ? segmentRef.Next.Value : Neighbors.First.Value;
            }

            // Get previous segment in ccw order around the normal
            public ISegment GetPrevious(ISegment s)
            {
                LinkedListNode<Segment> segmentRef = Neighbors.Find((Segment)s);
                if (segmentRef == null)
                {
                    Debug.LogError("Segment " + s.ID + " not found at node");
                    return null;
                }

                return segmentRef.Previous != null ? segmentRef.Previous.Value : Neighbors.Last.Value;
            }

            public ISegment GetInPlane(ISegment s, Vector3 N, bool next)
            {
                // Sort segments around normal N (all those that project well in this plane)
                // and take next from s

                Segment nextSegment = null;

                float projMax = 0f;
                Segment bestInPlane = null;

                foreach (var other in Neighbors)
                {
                    if (other.Equals(s))
                        continue;

                    Vector3 x0 = ProjectOnPlane(s.GetTangentAt(this), N);
                    Vector3 y0 = Vector3.Cross(x0, N); // left hand rule for unity cross fct

                    Vector3 tangent_s = ProjectOnPlane(other.GetTangentAt(this), N, normalize: false);
#if UNITY_EDITOR
                    Debug.Log($"[CYCLE DETECTION] at node {ID}, segment {other.ID} projected in plane has magnitude: {tangent_s.magnitude}");
#endif
                    // This segment does not align well with the plane,
                    // we ignore it in our choice of next segment
                    // but still store it, in case none of the neighboring segments project well in the plane.
                    if (tangent_s.magnitude < 0.7f)
                    {
                        if (tangent_s.magnitude > projMax)
                        {
                            bestInPlane = other;
                            projMax = tangent_s.magnitude;
                        }
                        continue;
                    }
                    else
                        tangent_s = tangent_s.normalized;

                    float y_s = Vector3.Dot(tangent_s, y0);
                    float x_s = Vector3.Dot(tangent_s, x0);
//#if UNITY_EDITOR
//                    Debug.Log("[CYCLE DETECTION] x_s = " + x_s + " y_s = " + y_s);
//#endif

                    // Initialize next
                    if (nextSegment == null)
                    {
                        nextSegment = other;
                        continue;
                    }
                    Vector3 next_tangent = nextSegment.GetTangentAt(this);

                    // Check whether this segment is between s and next
                    if (Vector3.Dot(next_tangent, y0) >= 0)
                    {
                        if (next)
                        {
                            if (y_s > 0 && Vector3.Dot(next_tangent, x0) < x_s)
                                nextSegment = other;
                        }
                        else
                        {
                            if (y_s <= 0 || Vector3.Dot(next_tangent, x0) > x_s)
                                nextSegment = other;
                        }
                    }
                    else
                    {
                        if (next)
                        {
                            if (y_s >= 0 || Vector3.Dot(next_tangent, x0) > x_s)
                                nextSegment = other;
                        }
                        else
                        {
                            if (y_s < 0 && Vector3.Dot(next_tangent, x0) < x_s)
                                nextSegment = other;
                        }
                    }

                }

                // Default: no segment projected well in plane..
                // we just take the one that projected "best", ie had the biggest projection magnitude
                if (nextSegment == null)
                {
                    nextSegment = bestInPlane;
                }

                //Debug.Log("found as next segment: " + nextSegment.ID);

                return nextSegment;

            }

            public SerializableNode Serialize()
            {
                List<int> segmentsID = new List<int>();

                foreach (var s in Neighbors)
                    segmentsID.Add(s.ID);

                SerializableNode nodeData = new SerializableNode(ID, Position, segmentsID);
                return nodeData;
            }


            private void UpdateNormal()
            {
                // Compute normal defined by best fitting plane to neighbouring segments
                if (Neighbors.Count < 2)
                    Normal = Vector3.zero;
                else
                {
                    // Normal is well defined, we can compute it
                    Vector3[] tangents = GetNeighbors();

                    Vector3 newNormal = Vector3.zero;
                    // If there are only 2 collinear segments at this node, the normal is zero
                    if (tangents.Length > 2 || Vector3.Cross(tangents[0], tangents[1]).magnitude > 0.1f)
                    {

                        (Plane bestPlane, float err) = Utils.FitPlane(Position, tangents);

                        if (bestPlane == null)
                        {
                            //Debug.Log("[NODE] only near collinear tangents at this node, using overall directions");
                            int i = 0;
                            Vector3[] pseudoTangents = new Vector3[Neighbors.Count];
                            foreach (var s in Neighbors)
                            {
                                Vector3 oppositeEndpoint = s.GetOpposite(this) != null ? s.GetOpposite(this).Position : s.GetPointAt((s.GetParam(this) + 0.5f) % 1);
                                pseudoTangents[i] = (oppositeEndpoint - this.Position).normalized;
                                i++;
                            }

                            (bestPlane, err) = Utils.FitPlane(Position, pseudoTangents);
                        }
                        //Debug.Log("[NODE] err = " + err);

                        // A node is classified as sharp if the max error (err) is bigger than cos(pi/2 - pi/6) = 0.5
                        // we compute the error as the dot product of a vector with the plane normal
                        if (bestPlane != null && err > 0.5f)
                        {
                            //Debug.Log("[NODE] is sharp");
                            IsSharp = true;
                        }
                        else
                            IsSharp = false;

                        if (bestPlane != null)
                            newNormal = bestPlane.n;
                    }

                    Normal = newNormal;
                }
            }

            public void AddSegment(ISegment s)
            {
                Neighbors.AddLast((Segment)s);

                // Recompute normal
                UpdateNormal();

                if (Normal.magnitude > 0f)
                {
                    SortSegments();
                }

            }

            public void RemoveSegment(ISegment s)
            {
                Neighbors.Remove((Segment)s);
                UpdateNormal();

                if (Normal.magnitude > 0f)
                {
                    SortSegments();
                }
            }

            public void ClearNeighbors()
            {
                Neighbors.Clear();
            }


            public bool TryRemove()
            {
                // Node is not necessary if it is purely linking 2 segments of the same stroke and nothing else
                if (Neighbors.Count == 2 && Neighbors.First.Value.Stroke.Equals(Neighbors.Last.Value.Stroke))
                {
                    // Remove this node from stroke, ie mend its neighboring segments
                    ISegment s = Neighbors.First.Value;
                    bool canMend = s.Stroke.MendSegments(this, Neighbors.First.Value, Neighbors.Last.Value);
                    // Remove all segments from node
                    if (canMend)
                    {
                        ClearNeighbors();
                        return true;
                    }
                    else
                        return false;
                }
                else if (Neighbors.Count == 0)
                    return true;
                else
                    return false;
            }

            private Vector3 ProjectOnPlane(Vector3 v)
            {
                if (Normal.magnitude > 0)
                    return (v - Vector3.Dot(v, Normal) * Normal).normalized;
                else
                    return v;
            }

            private static Vector3 ProjectOnPlane(Vector3 v, Vector3 N, bool normalize = true)
            {
                return normalize ? (v - Vector3.Dot(v, N) * N).normalized : (v - Vector3.Dot(v, N) * N);
            }

            private void SortSegments()
            {
                // Sort all segments, in counter-clockwise order around node normal
                // Update neighbors linked list

                if (Neighbors.Count <= 2)
                    return;

                LinkedList<Segment> sortedSegments = new LinkedList<Segment>();
                sortedSegments.AddFirst((Segment)Neighbors.First.Value); // choose a first segment as origin

                Neighbors.RemoveFirst(); // Exclude this one from subsequent sorting


                foreach (var s in Neighbors)
                {
                    LinkedListNode<Segment> neighbor = sortedSegments.First;

                    Vector3 x0 = ProjectOnPlane(neighbor.Value.GetTangentAt(this));
                    Vector3 y0 = Vector3.Cross(x0, Normal); // left hand rule for unity cross fct

                    Vector3 tangent_s = ProjectOnPlane(s.GetTangentAt(this));
                    float y_s = Vector3.Dot(tangent_s, y0);
                    float x_s = Vector3.Dot(tangent_s, x0);

                    bool isAfterCurrent = true;
                    while (neighbor.Next != null && isAfterCurrent)
                    {
                        neighbor = neighbor.Next;
                        Vector3 tangent_neighbor = ProjectOnPlane(neighbor.Value.GetTangentAt(this));

                        float x_s_corrected = x_s;
                        float y_s_corrected = y_s;

                        // Special case where the tangents are equal: take overall segment direction
                        if (Vector3.Dot(tangent_neighbor, tangent_s) > 0.99f)
                        {
                            //Debug.Log("tangents are equal at node, taking overall direction of segment " + neighbor.Value.ID + " " + s.ID);
                            tangent_neighbor = (neighbor.Value.GetOpposite(this).Position - this.Position).normalized;

                            // Figure out where to take a sample along the segment in case if the opposite node is not defined yet
                            Vector3 s_opposite_endpoint = s.GetOpposite(this) != null ? s.GetOpposite(this).Position : s.GetPointAt((s.GetParam(this) + 0.5f) % 1);

                            Vector3 tangent_s_corrected = (s_opposite_endpoint - this.Position).normalized;

                            x_s_corrected = Vector3.Dot(tangent_s_corrected, x0);
                            y_s_corrected = Vector3.Dot(tangent_s_corrected, y0);
                        }

                        if (Vector3.Dot(tangent_neighbor, y0) >= 0)
                        {
                            if (y_s_corrected > 0 && Vector3.Dot(tangent_neighbor, x0) < x_s_corrected)
                                isAfterCurrent = false;
                        }
                        else
                        {
                            if (y_s_corrected >= 0 || Vector3.Dot(tangent_neighbor, x0) > x_s_corrected)
                                isAfterCurrent = false;
                        }
                    }
                    // Insert current segment at the right place
                    if (!isAfterCurrent)
                        sortedSegments.AddBefore(neighbor, s);
                    else
                        sortedSegments.AddLast(s);
                }

                Neighbors = sortedSegments;
            }

        }
    }

}