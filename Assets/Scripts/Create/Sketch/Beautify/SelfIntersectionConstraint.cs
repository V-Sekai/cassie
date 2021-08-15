using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;

public class SelfIntersectionConstraint : IHardConstraint
{
    private int anchorIdxA;
    private int anchorIdxB;
    private Vector3 AB0;
    private int N;

    public SelfIntersectionConstraint(int anchorIdxA, int anchorIdxB, Vector3 AB0, int N)
    {
        this.anchorIdxA = anchorIdxA;
        this.anchorIdxB = anchorIdxB;
        this.AB0 = AB0;
        this.N = N;
    }

    public (Matrix<float>, Vector<float>) GetBlocks()
    {

        Matrix<float> C = Matrix<float>.Build.Dense(3, 3 * N);
        for (int i = 0; i < 3; i++)
        {
            C[i, 3 * 3 * this.anchorIdxA + i] = 1f;
            C[i, 3 * 3 * this.anchorIdxB + i] = -1f;
        }

        Vector<float> b = Vector<float>.Build.DenseOfArray(Utils.Flatten(this.AB0));

        return (C, b);
    }
}