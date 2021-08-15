using UnityEngine;
using System.Collections;
using MathNet.Numerics.LinearAlgebra;
using VRSketch;


public class FidelityEnergy
{

    private float p_factor;
    private float t_factor;

    private int N;
    private float[] tangentNorms2;

    public FidelityEnergy(Vector3[] B0, float w_p, float w_t, float displacement_normalizer)
    {
        this.N = B0.Length;

        this.p_factor = w_p / N;
        this.t_factor = w_t / (N - 1);

        // Normalizing term for position variation
        float L = 10e-4f; // arbitrary small constant

        // Compute all initial squared tangent lengths
        this.tangentNorms2 = new float[N - 1];
        for (int i = 0; i < N - 1; i++)
        {
            float dist = Vector3.Distance(B0[i + 1], B0[0]);
            if (dist > L)
                L = dist;
            this.tangentNorms2[i] = Vector3.Dot(B0[i] - B0[i + 1],
                                                B0[i] - B0[i + 1]);
        }
        //this.p_factor /= (L * L);
        //float small = 0.02f;
        this.p_factor /= (displacement_normalizer * displacement_normalizer);
    }

    public Matrix<float> GetBlock()
    {
        Matrix<float> A = Matrix<float>.Build.Dense(3 * N, 3 * N);

        for (int i = 0; i < N; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                A[3 * i + k, 3 * i + k] += 2 * p_factor;
                if (i > 0 && tangentNorms2[i - 1] > Constants.eps)
                {
                    A[3 * i + k, 3 * i + k] += 2 * t_factor / tangentNorms2[i - 1];
                    A[3 * i + k, 3 * (i - 1) + k] -= 2 * t_factor / tangentNorms2[i - 1];
                }
                if (i < N - 1 && tangentNorms2[i] > Constants.eps)
                {
                    A[3 * i + k, 3 * i + k] += 2 * t_factor / tangentNorms2[i];
                    A[3 * i + k, 3 * (i + 1) + k] -= 2 * t_factor / tangentNorms2[i];
                }
            }
        }

        return A;
    }

    public float Compute(Vector3[] X)
    {
        float Ep = 0f;
        float Et = 0f;

        for (int i = 0; i < N; i++)
        {
            Ep += Vector3.Dot(X[i], X[i]);
            if (i < N - 1 && tangentNorms2[i] > Constants.eps)
            {
                Et += Vector3.Dot(X[i] - X[i + 1], X[i] - X[i + 1]) / this.tangentNorms2[i];
            }
        }

        float energy = p_factor * Ep + t_factor * Et;

        return energy;
    }
    
}
