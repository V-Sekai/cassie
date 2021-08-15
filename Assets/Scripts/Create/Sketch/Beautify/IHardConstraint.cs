using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;

public interface IHardConstraint
{
    (Matrix<float>, Vector<float>) GetBlocks();
}