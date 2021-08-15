using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CASSIEParametersProvider : MonoBehaviour
{

    [SerializeField]
    private CASSIEParameters currentParameters = null;

    public CASSIEParameters Current
    {
        get { return currentParameters; }
    }
}
