using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;

public class TangentConstraint : ISoftConstraint
{
    private int idxA;
    private int idxB;
    private Vector3 T0;
    private Matrix<float> Tcross;
    private int N;

    public TangentConstraint(int ctrlPtIdx, Vector3 t_target, Vector3[] B0)
    {
        this.N = B0.Length;
        if (ctrlPtIdx < this.N - 1)
        {
            this.idxA = ctrlPtIdx;
            this.idxB = ctrlPtIdx + 1;
        }
        else
        {
            this.idxA = ctrlPtIdx - 1;
            this.idxB = ctrlPtIdx;
        }

        this.Tcross = Utils.CrossMatrix(t_target);
        this.T0 = B0[this.idxB] - B0[this.idxA];
    }

    public (Matrix<float>, Vector<float>) GetBlocks()
    {

        Matrix<float> A = Matrix<float>.Build.Dense(3 * N, 3 * N);
        Vector<float> b = Vector<float>.Build.Dense(3 * N);

        // It is checked before hand that T0 is a non null vector
        float factor = 2f / Mathf.Max(Vector3.Dot(T0, T0), Constants.eps);
        Vector<float> b_i = - factor * Tcross * Vector<float>.Build.Dense(Utils.Flatten(T0));

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                A[3 * this.idxA + i, 3 * this.idxA + j] = - factor * Tcross[i, j];
                A[3 * this.idxA + i, 3 * this.idxB + j] = factor * Tcross[i, j];

                A[3 * this.idxB + i, 3 * this.idxB + j] = - factor * Tcross[i, j];
                A[3 * this.idxB + i, 3 * this.idxA + j] = factor * Tcross[i, j];

            }
            b[3 * this.idxA + i] = b_i[i];
            b[3 * this.idxB + i] = - b_i[i];

        }

        return (A, b);
    }
}