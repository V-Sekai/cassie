using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VRSketch;
using System;

namespace Curve
{
    public class LineCurve : Curve
    {
        Vector3 A;
        Vector3 B;

        public LineCurve(Vector3 A, Vector3 B, float weightA, float weightB) : base(new List<float> { weightA, weightB }, false)
        {
            this.A = A;
            this.B = B;
        }


        public override Vector3 GetPoint(float t)
        {
            return Vector3.Lerp(A, B, t);
        }

        public override float GetWeight(float t)
        {
            return Mathf.Lerp(weights[0], weights[1], t);
        }

        public override PointOnCurve Project(Vector3 point)
        {
            Vector3 direction = Vector3.Normalize(B - A);
            Vector3 proj = A;

            if (Vector3.Dot(point - A, direction) <= 0)
            {
                proj = A;
                //Debug.Log("projection before A");
            }

            else if (Vector3.Dot(point - B, direction) >= 0)
            {
                proj = B;
                //Debug.Log("projection after B");
            }
            else
            {
                proj = Utils.ProjectOnLine(A, direction, point);
                //Debug.Log("projection between A and B");
            }

            float t = Mathf.Clamp(Vector3.Distance(proj, A) / Vector3.Distance(A, B), 0f, 1f);
            

            return new PointOnCurve(t, proj, direction, Vector3.zero);
        }

        public override PointOnCurve GetPointOnCurve(float t)
        {
            return new PointOnCurve(t, GetPoint(t), Vector3.Normalize(B - A), Vector3.zero);
        }

        public override Vector3 ParallelTransport(Vector3 v, float from, float to)
        {
            return v;
        }

        public override bool IsValid(float size_threshold)
        {
            return Vector3.Distance(A, B) > size_threshold;
        }

        public override List<Vector3> GetControlPoints()
        {
            return new List<Vector3>() { A, B };
        }

        public override List<List<Vector3>> GetControlPointsBetween(float from, float to)
        {
            return new List<List<Vector3>>()
            {
                new List<Vector3>() { GetPoint(to), GetPoint(from) }
            };
        }

        public override Reparameterization? CutAt(float t, bool throwBefore, float snapToExistingAnchorThreshold)
        {
            // Check if should split
            Vector3 P = GetPoint(t);
            if (Vector3.Distance(A, P) < snapToExistingAnchorThreshold || Vector3.Distance(B, P) < snapToExistingAnchorThreshold)
            {
                return new Reparameterization(0f, 1f);
            }

            float oldLength = Vector3.Distance(A, B);
            if (throwBefore)
            {
                A = P;
                return new Reparameterization(t, oldLength / Vector3.Distance(A, B));
            }

            else
            {
                B = P;
                return new Reparameterization(0f, oldLength / Vector3.Distance(A, B));
            }
                
        }

        public override float LengthBetween(float tA, float tB)
        {
            return Vector3.Distance(GetPoint(tA), GetPoint(tB));
        }


        public (IntersectionConstraint[], MirrorPlaneConstraint[]) Constrain(
            Constraint[] constraintCandidates, Vector3[] OrthoDirections, float angular_proximity_threshold, float proximity_threshold,
            out List<SerializableConstraint> appliedConstraints, out List<SerializableConstraint> rejectedConstraints)
        {
            Vector3 direction = Vector3.Normalize(B - A);
            float length = Vector3.Distance(A, B);
            float endpointSnapThreshold = proximity_threshold;

            List<IntersectionConstraint> intersections = new List<IntersectionConstraint>();
            List<MirrorPlaneConstraint> mirrorPlaneConstraints = new List<MirrorPlaneConstraint>();
            appliedConstraints = new List<SerializableConstraint>();
            rejectedConstraints = new List<SerializableConstraint>();

            if (constraintCandidates.Length > 2)
            {
                //Debug.Log("[LINE] constraining to 2 constraints out of " + constraintCandidates.Length);
                //Debug.Log("[LINE] constraint " + constraintCandidates[0].Position + " type :" + (constraintCandidates[0] as IntersectionConstraint != null ? " intersection" : constraintCandidates[0] as MirrorPlaneConstraint != null ? " mirror" : " none"));
                //Debug.Log("[LINE] constraint " + constraintCandidates[constraintCandidates.Length - 1].Position + " type :" + (constraintCandidates[constraintCandidates.Length - 1] as IntersectionConstraint != null ? " intersection" : constraintCandidates[constraintCandidates.Length - 1] as MirrorPlaneConstraint != null ? " mirror" : " none"));

                // Find 2 best constraints and constrain to those
                // They are the pair of points further from each other
                // Assume that the first one is correct, find the last one as the furthest from that one
                Constraint cA = constraintCandidates[0];
                int idx_A = 0;
                Constraint cB = constraintCandidates[0];
                int idx_B = 0;
                float distAB = 0f;
                for (int i = 1; i < constraintCandidates.Length; i++)
                {
                    float dist_i = Vector3.Distance(cA.Position, constraintCandidates[i].Position);
                    if (dist_i > distAB)
                    {
                        cB = constraintCandidates[i];
                        idx_B = i;
                        distAB = dist_i;
                    }
                }
                // Most probably first and last, as they are sorted in the order they are encountered
                (bool constrainedA, bool constrainedB) = ConstrainTo(cA, cB, OrthoDirections, angular_proximity_threshold, proximity_threshold);

                // Record constraints data
                SerializableConstraint cA_ser = Serialize(cA);
                SerializableConstraint cB_ser = Serialize(cB);

                if (constrainedA)
                    appliedConstraints.Add(cA_ser);
                else
                    rejectedConstraints.Add(cB_ser);

                if (constrainedB)
                    appliedConstraints.Add(cB_ser);
                else
                    rejectedConstraints.Add(cB_ser);


                for (int i = 0; i < constraintCandidates.Length; i++)
                {
                    if (i != idx_A && i != idx_B)
                    {
                        rejectedConstraints.Add(Serialize(constraintCandidates[i]));
                    }
                }

            }

            if (constraintCandidates.Length == 0)
            {
                //Debug.Log("[LINE] no constraints");
                // If close to an orthogonal direction, snap to it
                if (SnapDirToOrtho(OrthoDirections, angular_proximity_threshold, out Vector3 newDir))
                {
                    Vector3 newB = A + newDir * length;
                    if (Vector3.Distance(newB, B) < proximity_threshold)
                    {
                        B = newB;
                        //Debug.Log("snap line to direction: " + newDir);
                    }
                }
            }

            if (constraintCandidates.Length == 1)
            {
                //Debug.Log("[LINE] 1 constraint");
                //Debug.Log("[LINE] constraint " + constraintCandidates[0].Position + " type :" + (constraintCandidates[0] as IntersectionConstraint != null ? " intersection" : constraintCandidates[0] as MirrorPlaneConstraint != null ? " mirror" : " none"));
                ConstrainTo(constraintCandidates[0], OrthoDirections, angular_proximity_threshold, proximity_threshold);
                // Record constraints data
                appliedConstraints.Add(Serialize(constraintCandidates[0]));
            }
            if (constraintCandidates.Length == 2)
            {
                //Debug.Log("[LINE] 2 constraints");
                //Debug.Log("[LINE] constraint " + constraintCandidates[0].Position + " type :" + (constraintCandidates[0] as IntersectionConstraint != null ? " intersection" : constraintCandidates[0] as MirrorPlaneConstraint != null ? " mirror" : " none"));
                //Debug.Log("[LINE] constraint " + constraintCandidates[1].Position + " type :" + (constraintCandidates[1] as IntersectionConstraint != null ? " intersection" : constraintCandidates[1] as MirrorPlaneConstraint != null ? " mirror" : " none"));
                (bool constrainedA, bool constrainedB) = ConstrainTo(constraintCandidates[0], constraintCandidates[1], OrthoDirections, angular_proximity_threshold, proximity_threshold);

                // Record constraints data
                SerializableConstraint cA_ser = Serialize(constraintCandidates[0]);

                SerializableConstraint cB_ser = Serialize(constraintCandidates[1]);

                if (constrainedA)
                    appliedConstraints.Add(cA_ser);
                else
                    rejectedConstraints.Add(cA_ser);

                if (constrainedB)
                    appliedConstraints.Add(cB_ser);
                else
                    rejectedConstraints.Add(cB_ser);
            }

            direction = Vector3.Normalize(B - A);

            foreach (var constraint in constraintCandidates)
            {
                if (constraint as IntersectionConstraint != null)
                {
                    //Debug.Log("potential intersection at " + constraint.Position.ToString("F3"));
                    //Debug.Log("distance: " + Vector3.Distance(constraint.Position, Utils.ProjectOnLine(A, direction, constraint.Position)));
                    if (Vector3.Distance(constraint.Position, Utils.ProjectOnLine(A, direction, constraint.Position)) < proximity_threshold * 0.1f)
                    {
                        //Debug.Log("accept intersection");
                        IntersectionConstraint intersection = (IntersectionConstraint)constraint;
                        intersection.ProjectOn(this);
                        intersections.Add(intersection);
                    }
                }
                if (constraint as MirrorPlaneConstraint != null)
                {
                    if (Vector3.Distance(constraint.Position, Utils.ProjectOnLine(A, direction, constraint.Position)) < proximity_threshold * 0.1f)
                    {
                        //Debug.Log("accept intersection");
                        MirrorPlaneConstraint intersection = (MirrorPlaneConstraint)constraint;
                        intersection.ProjectOn(this);
                        mirrorPlaneConstraints.Add(intersection);
                    }
                }
            }
            return (intersections.ToArray(), mirrorPlaneConstraints.ToArray());
        }

        public static LineCurve ProjectOnPlane(LineCurve inputCurve, VRSketch.Plane plane, out float score)
        {
            Vector3 A = plane.Project(inputCurve.A);
            Vector3 B = plane.Project(inputCurve.B);
            // Mean distance between points and mirror
            score = 0.5f * (Mathf.Abs(Vector3.Dot(plane.n, (A - inputCurve.A))) * 0.5f + Math.Abs(Vector3.Dot(plane.n, B - inputCurve.B)) * 0.5f);

            return new LineCurve(A, B, inputCurve.weights[0], inputCurve.weights[1]);
        }


        public static LineCurve Mirror(LineCurve inputCurve, VRSketch.Plane mirrorPlane, out float score)
        {
            Vector3 A = mirrorPlane.Mirror(inputCurve.A);
            Vector3 B = mirrorPlane.Mirror(inputCurve.B);

            score = (Mathf.Abs(Vector3.Dot(mirrorPlane.n, (A - inputCurve.A))) + Mathf.Abs(Vector3.Dot(mirrorPlane.n, B - inputCurve.B))) * 0.5f;

            return new LineCurve(A, B, inputCurve.weights[0], inputCurve.weights[1]);
        }


        private (bool, bool) ConstrainTo(Constraint constraintA, Constraint constraintB, Vector3[] OrthoDirections, float angular_proximity_threshold, float proximity_threshold)
        {
            Vector3 direction = Vector3.Normalize(B - A);
            float length = Vector3.Distance(A, B);
            float endpointSnapThreshold = proximity_threshold;


            Vector3 P1 = constraintA.Position;
            Vector3 P2 = constraintB.Position;

            // Beware of case where P1 and P2 are very close
            if (Vector3.Distance(P1, P2) < proximity_threshold * 0.1f)
            {
                //Debug.Log("rejecting one of the constraints because they're too close");
                // Keep intersection constraint, as much as possible
                if (constraintA as IntersectionConstraint == null && constraintB as IntersectionConstraint != null)
                {
                    ConstrainTo(constraintB, OrthoDirections, angular_proximity_threshold, proximity_threshold);
                    return (false, true);
                }

                else
                {
                    ConstrainTo(constraintA, OrthoDirections, angular_proximity_threshold, proximity_threshold);
                    return (true, false);
                }
            }


            if ((Vector3.Dot(P1 - A, direction) < 0) || (Vector3.Distance(P1, A) < endpointSnapThreshold))
            {
                A = P1;
            }
            else
            {
                // Project A on line (P1 B)
                A = Utils.ProjectOnLine(P1, Vector3.Normalize(B - P1), A);
            }

            if ((Vector3.Dot(P2 - B, direction) > 0) || (Vector3.Distance(P2, B) < endpointSnapThreshold))
            {
                B = P2;
            }
            else
            {
                // Project B on line (A P2)
                B = Utils.ProjectOnLine(A, Vector3.Normalize(P2 - A), B);
            }

            return (true, true);
        }

        private void ConstrainTo(Constraint constraint, Vector3[] OrthoDirections, float angular_proximity_threshold, float proximity_threshold)
        {
            Vector3 direction = Vector3.Normalize(B - A);
            float length = Vector3.Distance(A, B);
            float endpointSnapThreshold = proximity_threshold;

            Vector3 P = constraint.Position;
            //Vector3 P_proj = Utils.ProjectOnLine(P, direction, A);

            //if (Vector3.Distance(P, P_proj) > 2f * proximity_threshold)
            //{
            //    // Abort, constraint is too far
            //    return;
            //}

            // If P is not on segment AB, snap closest point A or B to P
            if ((Vector3.Dot(P - A, direction) < 0) || (Vector3.Distance(P, A) < endpointSnapThreshold))
            {
                A = P;
                // If close to an orthogonal direction, snap to it
                if (SnapDirToOrtho(OrthoDirections, angular_proximity_threshold, out Vector3 newDir))
                {
                    Vector3 newB = A + newDir * length;
                    if (Vector3.Distance(newB, B) < proximity_threshold)
                    {
                        B = newB;
                        //Debug.Log("snap line to direction: " + newDir);
                    }
                }

            }

            else if ((Vector3.Dot(P - B, direction) > 0) || (Vector3.Distance(P, B) < endpointSnapThreshold))
            {
                B = P;
                // If close to an orthogonal direction, snap to it
                if (SnapDirToOrtho(OrthoDirections, angular_proximity_threshold, out Vector3 newDir))
                {
                    Vector3 newA = B - newDir * length;
                    if (Vector3.Distance(newA, A) < proximity_threshold)
                    {
                        A = newA;
                        //Debug.Log("snap line to direction: " + newDir);
                    }
                }
            }
            else
            {
                // Translate line so that it passes through the point
                Vector3 translation = (P - A) - direction * Vector3.Dot(P - A, direction);
                A += translation;
                B += translation;

                // If close to an orthogonal direction, snap to it
                if (SnapDirToOrtho(OrthoDirections, angular_proximity_threshold, out Vector3 newDir))
                {
                    Vector3 newA = P - newDir * Vector3.Distance(P, A);
                    Vector3 newB = P + newDir * Vector3.Distance(P, B);
                    if (Vector3.Distance(newA, A) < proximity_threshold && Vector3.Distance(newB, B) < proximity_threshold)
                    {
                        A = newA;
                        B = newB;
                        //Debug.Log("snap line to direction: " + newDir);
                    }
                }
            }
        }

        private bool SnapDirToOrtho(Vector3[] OrthoDirections, float angular_proximity, out Vector3 newDirection)
        {
            Vector3 direction = (B - A).normalized;
            // If close to an orthogonal direction, snap to it
            foreach (var ax in OrthoDirections)
            {
                if (Mathf.Abs(Vector3.Dot(direction, ax)) > Mathf.Abs(Mathf.Cos(angular_proximity)))
                {
                    newDirection = Vector3.Dot(direction, ax) > 0 ? ax : -ax;
                    return true;
                }
            }
            newDirection = direction;
            return false;
        }

        private SerializableConstraint Serialize(Constraint c)
        {
            float eps = 10e-6f;
            return new SerializableConstraint(
                        c.Position,
                        c as IntersectionConstraint != null,
                        isAtExistingNode: c as IntersectionConstraint != null ? ((IntersectionConstraint)c).IsAtNode : false,
                        isAtNewEndpoint: Vector3.Distance(c.Position, A) < eps || Vector3.Distance(c.Position, B) < eps,
                        alignTangents: false
                        );
        }

    }

}