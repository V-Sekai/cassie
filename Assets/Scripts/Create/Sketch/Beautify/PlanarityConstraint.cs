using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;

public class PlanarityConstraint : ISoftConstraint
{
    private Vector<float> normal;
    private Vector<float>[] T0;
    private int N;
    private float[] tangentNorms2;

    public PlanarityConstraint(Vector3 normal, Vector3[] B0)
    {
        this.N = B0.Length;
        this.normal = Vector<float>.Build.Dense(Utils.Flatten(normal));

        // Compute all initial squared tangent lengths
        this.tangentNorms2 = new float[N - 1];
        this.T0 = new Vector<float>[N - 1];
        for (int i = 0; i < N - 1; i++)
        {
            this.tangentNorms2[i] = Vector3.Dot(B0[i] - B0[i + 1],
                                                B0[i] - B0[i + 1]);

            this.T0[i] = Vector<float>.Build.Dense(Utils.Flatten(B0[i + 1] - B0[i]));
        }
    }

    public (Matrix<float>, Vector<float>) GetBlocks()
    {

        Matrix<float> A = Matrix<float>.Build.Dense(3 * N, 3 * N);
        Vector<float> b = Vector<float>.Build.Dense(3 * N);

        Matrix<float> NN = normal.ToColumnMatrix() * normal.ToRowMatrix();

        float factor = 2f / (N - 1);

        for (int k = 0; k < N; k++)
        {
            Vector<float> b_k = Vector<float>.Build.Dense(3);

            if (k < N - 1 && !Mathf.Approximately(tangentNorms2[k], 0f))
                b_k += factor * NN * T0[k] / tangentNorms2[k];
            if (k > 0 && !Mathf.Approximately(tangentNorms2[k - 1], 0f))
                b_k += -factor * NN * T0[k - 1] / tangentNorms2[k - 1];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (k < N - 1)
                    {
                        A[3 * k + i, 3 * k + j] += factor * NN[i, j] / tangentNorms2[k];
                        A[3 * k + i, 3 * (k + 1) + j] += -factor * NN[i, j] / tangentNorms2[k];
                    }
                    if (k > 0)
                    {
                        A[3 * k + i, 3 * k + j] += factor * NN[i, j] / tangentNorms2[k - 1];
                        A[3 * k + i, 3 * (k - 1) + j] += -factor * NN[i, j] / tangentNorms2[k - 1];
                    }
                }
                b[3 * k + i] = b_k[i];
            }

        }

        return (A, b);
    }
}