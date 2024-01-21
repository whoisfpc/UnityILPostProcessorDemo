# UnityILPostProcessorDemo
使用Cecil和unity的ILPostProcessor，在代码编译后修改代码

## 说明
使用cecil修改已经编译好的代码，让打了`[AutoInjectCall]`属性的方法，在执行自己的逻辑前，自动额外执行
基类的`AutoCalledHello`方法。

- 注意：因为Unity的限制，想要使用`ILPostProcessor`和相关机制，必须创建一个AssemblyDefine，并且命名格式为`Unity.XXXX.CodeGen`

## 参考
https://qiita.com/sune2/items/bb23de9364f966ada933  
https://forum.unity.com/threads/how-does-unity-do-codegen-and-why-cant-i-do-it-myself.853867/#post-5646937