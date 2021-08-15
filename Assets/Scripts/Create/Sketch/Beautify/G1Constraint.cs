using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;

public class G1Constraint : IHardConstraint
{

    private float[] leftTangentNorms;
    private float[] rightTangentNorms;
    private bool[] isG1;
    private int N;
    private int Nb;

    public int nJoints { get; private set; } = 0;
    //private bool closed;
    //private float startTangentNorm;
    //private float endTangentNorm;

    public G1Constraint(Vector3[] controlPoints)
    {
        N = controlPoints.Length;
        Nb = N / 3; // Number of individual beziers
        //closed = isClosed;

        // Compute tangent norms
        leftTangentNorms = new float[Nb - 1];
        rightTangentNorms = new float[Nb - 1];
        isG1 = new bool[Nb - 1];

        for (int i = 0; i < Nb - 1; i++)
        {
            int anchor_idx = 3 * (i + 1);
            leftTangentNorms[i] = Vector3.Distance(controlPoints[anchor_idx], controlPoints[anchor_idx - 1]);
            rightTangentNorms[i] = Vector3.Distance(controlPoints[anchor_idx], controlPoints[anchor_idx + 1]);

            // Is this a G1 joint?
            if (Vector3.Cross(controlPoints[anchor_idx] - controlPoints[anchor_idx - 1], controlPoints[anchor_idx] - controlPoints[anchor_idx + 1]).magnitude > 10e-6f)
            {
                Debug.Log("non G1 joint");
                isG1[i] = false;
            }
            else
            {
                isG1[i] = true;
                nJoints++;
            }

        }

    }

    public (Matrix<float>, Vector<float>) GetBlocks()
    {
        //int N_lines = 3 * (Nb - 1);
        int N_lines = 3 * nJoints;
        int i_c = 0;
        Matrix<float> C = Matrix<float>.Build.Dense(N_lines, 3 * N);
        for (int i = 0; i < Nb - 1; i++)
        {
            if (isG1[i])
            {
                int anchor_idx = 3 * (i + 1);
                for (int k = 0; k < 3; k++)
                {
                    // Careful of degenerate case where one tangent is practically null
                    if (leftTangentNorms[i] > Constants.eps && rightTangentNorms[i] > Constants.eps)
                    {
                        C[3 * i_c + k, 3 * anchor_idx + k] = (1 / leftTangentNorms[i]) + (1 / rightTangentNorms[i]);
                        C[3 * i_c + k, 3 * (anchor_idx - 1) + k] = -(1 / leftTangentNorms[i]);
                        C[3 * i_c + k, 3 * (anchor_idx + 1) + k] = -(1 / rightTangentNorms[i]);
                    }
                }
                i_c++;
            }

        }

        Vector<float> b = Vector<float>.Build.Dense(N_lines);

        return (C, b);
    }
}