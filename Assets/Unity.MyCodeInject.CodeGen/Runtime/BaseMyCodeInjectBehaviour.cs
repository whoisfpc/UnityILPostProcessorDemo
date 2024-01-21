using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyCodeInject
{
    public abstract class BaseMyCodeInjectBehaviour : MonoBehaviour
    {
        public void AutoCalledHello()
        {
            Debug.Log("Hello from auto call method");
        }
    }
}
