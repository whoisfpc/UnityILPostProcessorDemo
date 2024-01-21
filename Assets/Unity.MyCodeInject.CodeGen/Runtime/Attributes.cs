using System;

namespace MyCodeInject
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoInjectCall : Attribute
    {
    }
}