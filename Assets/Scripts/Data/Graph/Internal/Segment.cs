using Curve;
using System.Collections.Generic;
using UnityEngine;

namespace VRSketch
{
    public partial class Graph
    {
        // This is a private internal class of the Graph class, accessible only through its public interface to outside code
        // This design aims at forbidding the creation of instances by outside code (only the Graph class is authorized to instantiate this class, since it needs to keep track of such objects)
        private class Segment : ISegment
        {
            public FinalStroke Stroke { get; }
            private Node[] _endpoints = new Node[2];
            private float[] _onCurveParams = new float[2];
            public int ID { get; }


            public Segment(int ID, FinalStroke s, float start, float end, Node startNode, Node endNode)
            {
                this.ID = ID;
                Stroke = s;
                SetStart(start, startNode);
                SetEnd(end, endNode);
            }


            public void SetStart(float param, Node node)
            {
                Replace(0, param, node);
            }

            public void SetStart(Node node)
            {
                SetStart(_onCurveParams[0], node);
            }

            public void SetEnd(float param, Node node)
            {
                Replace(1, param, node);
            }

            public void SetEnd(Node node)
            {
                SetEnd(_onCurveParams[1], node);
            }

            public INode GetStartNode()
            {
                return _endpoints[0];
            }

            public INode GetEndNode()
            {
                return _endpoints[1];
            }

            public float GetStartParam()
            {
                return _onCurveParams[0];
            }

            public float GetEndParam()
            {
                return _onCurveParams[1];
            }

            public Vector3 GetPointAt(float u)
            {
                float t = Mathf.Lerp(_onCurveParams[0],
                                     _onCurveParams[1],
                                     u);
                return Stroke.Curve.GetPoint(t);
            }

            public float GetApproxLength()
            {
                return Stroke.Curve.LengthBetween(_onCurveParams[0], _onCurveParams[1]);
            }

            public int GetSamplesCount(float targetEdgeLength)
            {
                int result;
                int min = 8;
                int max = 50;
                if (Stroke.Curve as BezierCurve != null)
                {
                    result = Mathf.Clamp((int)Mathf.Floor(((BezierCurve)Stroke.Curve).LengthBetween(_onCurveParams[0], _onCurveParams[1]) / targetEdgeLength), min, max);
                }
                else
                {
                    result = Mathf.Clamp((int)Mathf.Floor(Vector3.Distance(GetStartNode().Position, GetEndNode().Position) / targetEdgeLength), min, max);
                }
                //result = Mathf.Max(result, (int)Mathf.Floor(Stroke.Curve.GetLength() / targetEdgeLength));

                return result;
            }

            public void Delete()
            {
                // Clean up segment connexion to its _endpoints
                foreach (var node in _endpoints)
                {
                    node.RemoveSegment(this);
                }

            }

            public INode GetOpposite(INode from)
            {
                return _endpoints[Other(WhichEndpoint(from))];
            }

            public float GetParam(INode from)
            {
                return _onCurveParams[WhichEndpoint(from)];
            }

            public bool IsInReverse(INode from)
            {
                return WhichEndpoint(from) == 1;
            }

            public (INode, float) GetClosest(Vector3 position)
            {
                return (Vector3.Distance(_endpoints[0].Position, position) < Vector3.Distance(_endpoints[1].Position, position)) ?
                    (_endpoints[0], _onCurveParams[0]) :
                    (_endpoints[1], _onCurveParams[1]);
            }

            public Vector3 GetTangentAt(INode endpoint)
            {
                int end = WhichEndpoint(endpoint);
                float u = _onCurveParams[end];
                float orientation = end == 0 ? 1 : -1;
                return orientation * Stroke.Curve.GetTangent(u);
            }

            public float EvaluateEndpointNormals(Node to)
            {
                int idx = WhichEndpoint(to);
                Node from = _endpoints[Other(idx)];
                Vector3 transported = Stroke.ParallelTransport(from.Normal, _onCurveParams[Other(idx)], _onCurveParams[idx]);
                return Vector3.Dot(transported, to.Normal);
            }

            public Vector3 ProjectInPlane(INode n, Vector3 normal)
            {
                Vector3 v = GetTangentAt(n);
                Vector3 proj = (v - Vector3.Dot(v, normal) * normal);

                // If the tangent does not project well in this plane, we take the overall segment direction instead (the vector between both endpoint nodes)
                if (proj.magnitude < 0.7f)
                {
                    //Debug.Log("taking overall direction instead of tangent at node");
                    int nodeIdx = WhichEndpoint(n);
                    Vector3 v2 = (_endpoints[Other(nodeIdx)].Position - n.Position).normalized;

                    Vector3 proj2 = (v2 - Vector3.Dot(v2, normal) * normal);

                    if (proj2.magnitude > proj.magnitude)
                    {
                        //Debug.Log("overall direction is better");
                        return proj2.normalized;
                    }

                }

                return proj.normalized;
            }

            public Vector3 Transport(Vector3 v, INode to)
            {
                int toIdx = WhichEndpoint(to);
                int fromIdx = Other(toIdx);
                return Stroke.ParallelTransport(v, _onCurveParams[fromIdx], _onCurveParams[toIdx]);
            }

            public Vector3 Transport(Vector3 v, float fromParam, float toParam)
            {
                return Stroke.ParallelTransport(v, fromParam, toParam);
            }

            public Vector3 GetMidpoint()
            {
                return (_endpoints[0].Position + _endpoints[1].Position) / 2f;
            }

            private int WhichEndpoint(INode node)
            {
                return node.Equals(_endpoints[0]) ? 0 : 1;
            }

            private int Other(int idx)
            {
                return (idx + 1) % 2;
            }

            private void Replace(int idx, float param, Node node)
            {
                _onCurveParams[idx] = param;

                if (node != null)
                {
                    if (this._endpoints[idx] != null)
                    {
                        // Remove segment from node
                        this._endpoints[idx].RemoveSegment(this);
                    }

                    // Set new node and add segment to new node
                    this._endpoints[idx] = node;
                    node.AddSegment(this);
                }
            }
        }
    }

}