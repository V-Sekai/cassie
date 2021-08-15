using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;

public class PositionConstraint : IHardConstraint
{
    private int ctrlPtIdx;
    private Vector3 d;
    private int N;

    public PositionConstraint(int ctrlPtIdx, Vector3 d, int N)
    {
        this.ctrlPtIdx = ctrlPtIdx;
        this.d = d;
        this.N = N;
    }

    public (Matrix<float>, Vector<float>) GetBlocks()
    {

        Matrix<float> C = Matrix<float>.Build.Dense(3, 3 * N);
        for (int i = 0; i < 3; i++)
        {
            C[i, 3 * this.ctrlPtIdx + i] = 1f;
        }

        Vector<float> b = Vector<float>.Build.DenseOfArray(Utils.Flatten(this.d));

        return (C, b);
    }
}