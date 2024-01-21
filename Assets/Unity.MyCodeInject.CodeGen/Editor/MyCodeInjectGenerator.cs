using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;

namespace MyCodeInject
{
    public class MyCodeInjectGenerator : ILPPInterface
    {
        private readonly List<DiagnosticMessage> _diagnostics = new List<DiagnosticMessage>();
        private PostProcessorAssemblyResolver _mAssemblyResolver;
        private ModuleDefinition _mMyCodeInjectModule;
        private MethodReference _autoCalledHelloMethodRef;
        
        public override ILPPInterface GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.References.Any(
               filePath => Path.GetFileNameWithoutExtension(filePath) == "Unity.MyCodeInject.Runtime");
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly)) return null;

            _diagnostics.Clear();
            var assemblyDefinition = MyCodeInjectHelper.AssemblyDefinitionFor(compiledAssembly, out _mAssemblyResolver);
            _mMyCodeInjectModule = MyCodeInjectHelper.FindBaseModules(assemblyDefinition, _mAssemblyResolver);
            
            AddDiagnosticInfo(_diagnostics, $"assembly {compiledAssembly.Name}");
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                ImportReferences(mainModule);
                foreach (var typeDefinition in mainModule.Types)
                {
                    foreach (var methodDefinition in typeDefinition.Methods)
                    {
                        bool hasMyAttribute = false;
                        foreach (var customAttribute in methodDefinition.CustomAttributes)
                        {
                            if (customAttribute.AttributeType.FullName == typeof(AutoInjectCall).FullName)
                            {
                                hasMyAttribute = true;
                            }
                        }

                        if (hasMyAttribute)
                        {
                            AddDiagnosticInfo(_diagnostics, $"method {methodDefinition.Name}");
                            InjectAutoCode(methodDefinition);
                        }
                    }
                }
                
            }

            return MyCodeInjectHelper.GetResult(assemblyDefinition, _diagnostics);
        }
        void InjectAutoCode(MethodDefinition methodDefinition)
        {
            var processor = methodDefinition.Body.GetILProcessor();
            
            var instructions = new List<Instruction>();
            instructions.Add(processor.Create(OpCodes.Ldarg_0));
            instructions.Add(processor.Create(OpCodes.Call, _autoCalledHelloMethodRef));
            instructions.Add(processor.Create(OpCodes.Nop));
            
            instructions.Reverse();
            instructions.ForEach(instruction => processor.Body.Instructions.Insert(0, instruction));
        }

        static void AddDiagnosticInfo(List<DiagnosticMessage> diagnostics, string info)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Warning,
                MessageData = $" - MyCodeInject: {info}"
            });
        }

        private void ImportReferences(ModuleDefinition moduleDefinition)
        {
            TypeDefinition myCodeInjectBehaviourTypeDef = null;
            foreach (var myCodeInjectTypeDef in _mMyCodeInjectModule.GetAllTypes())
            {
                if (myCodeInjectTypeDef.Name == nameof(BaseMyCodeInjectBehaviour))
                {
                    myCodeInjectBehaviourTypeDef = myCodeInjectTypeDef;
                    break;
                }
            }

            foreach (var methodDef in myCodeInjectBehaviourTypeDef.Methods)
            {
                if (methodDef.Name == nameof(BaseMyCodeInjectBehaviour.AutoCalledHello))
                {
                    _autoCalledHelloMethodRef = moduleDefinition.ImportReference(methodDef);
                    break;
                }
            }
        }
    }
}
