using UnityEngine;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;

public interface ISoftConstraint
{
    (Matrix<float>, Vector<float>) GetBlocks();
}