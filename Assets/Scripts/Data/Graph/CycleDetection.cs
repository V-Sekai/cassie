using UnityEngine;
using System.Collections.Generic;

namespace VRSketch
{

    public static class CycleDetection
    {

        public static bool DetectCycle(Graph g, ISegment startSegment, INode startNode, out LinkedList<HalfSegment> cycle, bool reversed = false)
        {
            //Cycle cycle = new Cycle(userCreated: false);
            cycle = new LinkedList<HalfSegment>();

            INode currentNode = startNode;
            ISegment currentSegment = startSegment;

            INode nextNode = null;
            ISegment nextSegment = null;

            // this node is the target to reach to close the cycle
            INode oppositeNode = startSegment.GetOpposite(startNode);

            // Particular case of a simple closed loop
            if (oppositeNode.Equals(startNode))
            {
                cycle.AddLast(new HalfSegment(currentSegment, false));
                return true;
            }

            if (currentNode.IncidentCount < 2 || oppositeNode.IncidentCount < 2)
                return false;

            Vector3 currentNormal = Vector3.zero;

#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] start search from segment {startSegment.ID}, node {startNode.ID}.");
#endif

            // Search for a counter clockwise cycle
            while (currentNode.IncidentCount > 1)
            {
#if UNITY_EDITOR
                Debug.Log("[CYCLE DETECTION] current segment ID: " + currentSegment.ID);
#endif

                // Check if path is still eulerian
                if (Contains(cycle, currentSegment))
                {
                    //Debug.Log("path would not be eulerian");
                    break;
                }


                // Check if path is still manifold
                if (g.ExistingCyclesCount(currentSegment) >= 2)
                    break;

                // Is this node trivial?
                // (a node is "trivial" if it only has 2 neighboring segments, in which case we don't need to choose which segment should be the next one since there is only 1 option)
                if (currentNode.IncidentCount > 2)
                {

                    Vector3 transportedCurrentNormal = currentSegment.Transport(currentNormal, currentNode);

                    // Non trivial node
                    // if non sharp: normal is well defined, use sorted neighbors at node to determine next segment
                    // if sharp: choose neighbor segment at node that is the most planar wrt current normal transported to node

                    if (currentNode.IsSharp && currentNormal.magnitude > 0.9f)
                    {
#if UNITY_EDITOR
                        Debug.Log("[CYCLE DETECTION] sharp node: get " + (reversed ? "next segment in plane" : "previous segment in plane"));
#endif
                        if (!reversed)
                            nextSegment = currentNode.GetInPlane(currentSegment, transportedCurrentNormal, next: false);
                        else
                            nextSegment = currentNode.GetInPlane(currentSegment, transportedCurrentNormal, next: true);
                    }
                    else
                    {
#if UNITY_EDITOR
                        Debug.Log("[CYCLE DETECTION] non sharp node: get " + (reversed ? "next segment" : "previous segment"));
#endif
                        if (!reversed)
                            nextSegment = currentNode.GetPrevious(currentSegment);
                        else
                            nextSegment = currentNode.GetNext(currentSegment);
                    }

                    nextNode = nextSegment.GetOpposite(currentNode);


                    Vector3 nodeNormal;

                    // If the current node is sharp, we can't simply use its normal to continue
                    // So we determine it in one of two ways:
                    //     - if there is no valid transported normal from previous nodes (meaning that we started from a sharp node), we set the normal as the cross product between current and next segment
                    //     - if there is a valid transported normal, we simply use that
                    if (currentNode.IsSharp)
                    {
                        if (transportedCurrentNormal.magnitude < 0.1f)
                        {
                            // Take cross product between current and next segment
                            nodeNormal = Vector3.Cross(currentSegment.GetTangentAt(currentNode), nextSegment.GetTangentAt(currentNode)).normalized;
                            if (reversed)
                                nodeNormal = -nodeNormal;
                        }
                        else
                        {
                            // Take transported current normal

                            nodeNormal = transportedCurrentNormal;
                            //Debug.Log("node normal before rotation" + nodeNormal);

                            // The "transportedCurrentNormal" corresponds to parallel transport along the previous segment,
                            // We additionally rotate the normal at the node, extending the parallel transport across this discontinuity between smooth curves
                            nodeNormal = TransportAcrossNode(currentNode, currentSegment, nextSegment, nodeNormal);


                        }

                    }
                    else
                    {
                        // if the node is not sharp, we simply use its normal thereafter
                        nodeNormal = currentNode.Normal;
                    }

                    // Update normal
                    currentNormal = nodeNormal;

                    // If next node has a well defined normal,
                    // are node normals consistently oriented between current node and next node?

                    // This determines in which order (clockwise or counter clockwise) we should go around the next node
                    // We store this state by updating the variable "reversed"

                    if (nextNode.IncidentCount > 2 && !nextNode.IsSharp)
                    {
                        ShouldReverse(ref reversed, currentNormal, nextNode, nextSegment);
                    }
                }
                else
                {
                    // Trivial node case

                    //Debug.Log("trivial node");

                    // next segment and next node are trivial
                    nextSegment = currentNode.GetNext(currentSegment);
                    nextNode = nextSegment.GetOpposite(currentNode);

                    // normal may be ill-defined at this node, so whenever possible, use "currentNormal" which is the normal from previous node
                    if (currentNormal.magnitude > 0.9f)
                    {
                        // If current normal is well defined, parallel transport it along segment
                        // and just use that

                        // Parallel transport last known normal
                        currentNormal = currentSegment.Transport(currentNormal, currentNode);

                        currentNormal = TransportAcrossNode(currentNode, currentSegment, nextSegment, currentNormal);

                    }
                    else
                    {
                        // Try to define normal depending on current segments
                        Vector3 nTrivial = Vector3.Cross(currentSegment.GetTangentAt(currentNode), nextSegment.GetTangentAt(currentNode));
                        if (nTrivial.magnitude > 0.5f)
                        {
                            currentNormal = nTrivial.normalized;
                            //reversed = false;
                            //Debug.Log("trivial node normal: " + currentNormal.ToString("F3"));
                        }

                    }


                    if (currentNormal.magnitude > 0.9f)
                    {
                        // If next node has a well defined normal,
                        // update "reversed": are node normals consistently oriented between current normal and normal at next node?
                        if (nextNode.IncidentCount > 2 && !nextNode.IsSharp)
                            ShouldReverse(ref reversed, currentNormal, nextNode, nextSegment);
                    }
                }

                bool reversedSegment = currentSegment.IsInReverse(currentNode);

                cycle.AddLast(new HalfSegment(currentSegment, reversedSegment));

                currentSegment = nextSegment;
                currentNode = nextNode;
            }

#if UNITY_EDITOR
            Debug.Log($"[CYCLE DETECTION] end search from segment {startSegment.ID}, node {startNode.ID}.");
#endif

            INode finalNode = currentSegment.GetOpposite(currentNode);

            if (finalNode.Equals(oppositeNode) && currentSegment.Equals(startSegment) && cycle.Count > 1)
            {
                return true;
            }

            return false;
        }


        public static bool DetectCycle(Graph g, Vector3 inputPos, bool lookAtNonManifold, out LinkedList<HalfSegment> cycle)
        {
            cycle = new LinkedList<HalfSegment>();

            // Attempt to find a cycle for which the patch center would lie close to inputPos
            // First find the closest segment
            ISegment closest = g.FindClosestSegment(inputPos, lookAtNonManifold);

            if (closest == null)
                return false;

            // Check if the segment is a loop
            if (closest.GetStartNode().Equals(closest.GetEndNode()))
            {
                cycle.AddLast(new HalfSegment(closest, false));
                return true;
            }

            // From the inputPos and the segment endpoints, we can fit a plane
            //(Plane p, float d) = Utils.FitPlane(new Vector3[] { inputPos, closest.GetStartNode().Position, closest.GetEndNode().Position });
            //Vector3 planeNormal = Vector3.Cross((closest.GetStartNode().Position - inputPos).normalized, (closest.GetEndNode().Position - inputPos).normalized);
            //Debug.Log("plane normal " + planeNormal);

            // Start from closest segment and try to walk graph
            // By always choosing next segment as the one that lies closest to the input position
            INode startNode = closest.GetStartNode();
            INode currentNode = startNode;
            ISegment currentSegment = closest;

            int MaxNonCollinearSegments = 10;
            int counter = 0;

            while (counter <= MaxNonCollinearSegments)
            {
#if UNITY_EDITOR
                Debug.Log("current segment ID: " + currentSegment.ID);
#endif

                // Check if path is still eulerian
                if (Contains(cycle, currentSegment))
                {
                    //Debug.Log("path would not be eulerian");
                    break;
                }

                // Add segment to cycle
                bool reversedSegment = !currentSegment.IsInReverse(currentNode);
                cycle.AddLast(new HalfSegment(currentSegment, reversedSegment));

                // Decide on next segment
                currentNode = currentSegment.GetOpposite(currentNode);

                ISegment nextSegment = g.FindClosestAmongNeighbors(inputPos, currentNode, currentSegment, lookAtNonManifold);

                // Didn't find a suitable next segment
                if (nextSegment == null)
                    break;

                // If current segment and next segment are not collinear, increment counter
                if (Vector3.Dot(currentSegment.GetTangentAt(currentNode), nextSegment.GetTangentAt(currentNode)) < 0.8f)
                    counter++;

                currentSegment = nextSegment;
            }

            if (counter > MaxNonCollinearSegments)
                Debug.Log("couldn't find a short enough path");

            if (currentNode.Equals(startNode) && currentSegment.Equals(closest) && cycle.Count > 1)
            {
                return true;
            }

            return false;
        }


        private static bool Contains(LinkedList<HalfSegment> cycle, ISegment s)
        {
            foreach (var hs in cycle)
            {
                if (hs.GetSegmentID() == s.ID)
                    return true;
            }
            return false;
        }


        private static void ShouldReverse(ref bool reversed, Vector3 currentNormal, INode nextNode, ISegment segment)
        {
            Vector3 transportedCurrentNormal = segment.Transport(currentNormal, nextNode);
            float endpointsAngle = Vector3.Dot(transportedCurrentNormal, nextNode.Normal);
            //Debug.Log("Angle between endpoint normals: " + endpointsAngle);

            // If both normals do not have a clear agreement (parallel transporting one to the location of the other does not align them well)
            // We are in the case where the curve lies on the imagined surface with significant geodesic torsion => we can't rely on comparing the normals to disambiguate orientation

            // So we fall back to this simple trick that seems to help a bit, but honestly isn't so good (hence not described in paper)
            if (Mathf.Abs(endpointsAngle) < 0.5f)
            {
                //Debug.Log("orientation is ambiguous at this node");

                // We hesitate between choosing one of those segments
                ISegment nextAtNode = nextNode.GetNext(segment);
                ISegment prevAtNode = nextNode.GetPrevious(segment);

                // Sort those 2 segments in the plane defined by the transported current normal, instead of relying on ordering around the normal at nextNode

                // Project segments in plane defined by the transported current normal, and sort
                Vector3 projNext = nextAtNode.ProjectInPlane(nextNode, transportedCurrentNormal);
                Vector3 projPrev = prevAtNode.ProjectInPlane(nextNode, transportedCurrentNormal);

                Vector3 x0 = segment.ProjectInPlane(nextNode, transportedCurrentNormal);
                Vector3 y0 = Vector3.Cross(x0, transportedCurrentNormal); // left hand rule for unity cross fct

                double thetaNext = (Mathf.Atan2(Vector3.Dot(projNext, y0), Vector3.Dot(projNext, x0)) + 2 * Mathf.PI) % (2 * Mathf.PI);
                //Debug.Log("theta next:f " + thetaNext);
                double thetaPrev = (Mathf.Atan2(Vector3.Dot(projPrev, y0), Vector3.Dot(projPrev, x0)) + 2 * Mathf.PI) % (2 * Mathf.PI);
                //Debug.Log("theta prev: " + thetaPrev);

                if (thetaNext > thetaPrev)
                    reversed = !reversed;
            }
            else
            {
                if (endpointsAngle < 0)
                    reversed = !reversed;
            }
        }

        private static Vector3 TransportAcrossNode(INode node, ISegment prevSegment, ISegment nextSegment, Vector3 normal)
        {

            // Parallel transport between segments, across a node
            // (works the same as for parallel transport along a curve,
            // we transport "across the node" by taking tangents from the 2 curves)

            Vector3 prev_tangent = -prevSegment.GetTangentAt(node);
            Vector3 tangent = nextSegment.GetTangentAt(node);
            var axis = Vector3.Cross(prev_tangent, tangent);

            if (axis.magnitude > float.Epsilon)
            {
                axis.Normalize();

                float dot = Vector3.Dot(prev_tangent, tangent);

                // clamp for floating pt errors
                float theta = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));

                return Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis) * normal;
            }

            return normal;
        }
    }
}