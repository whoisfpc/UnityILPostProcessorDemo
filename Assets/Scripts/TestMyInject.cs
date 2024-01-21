using System.Collections;
using System.Collections.Generic;
using MyCodeInject;
using UnityEngine;

public class TestMyInject : BaseMyCodeInjectBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ToWorld();
    }

    [AutoInjectCall]
    private void ToWorld()
    {
        Debug.Log("World");
    }
}
