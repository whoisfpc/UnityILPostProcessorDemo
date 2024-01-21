using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace MyCodeInject
{
    public static class MyCodeInjectHelper
    {
        private const string MyCodeInjectModuleName = "Unity.MyCodeInject.Runtime.dll";
        
        internal static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out PostProcessorAssemblyResolver resolver)
        {
            resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = resolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            //apparently, it will happen that when we ask to resolve a type that lives inside Unity.Entities, and we
            //are also postprocessing Unity.Entities, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //unity.entities itself as well.
            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }
        
        internal static ILPostProcessResult GetResult(AssemblyDefinition assemblyDefinition, List<DiagnosticMessage> diagnostics)
        {
            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }
        
        private static void SearchForBaseModulesRecursive(AssemblyDefinition assemblyDefinition, PostProcessorAssemblyResolver assemblyResolver, ref ModuleDefinition myCodeInjectModule, HashSet<string> visited)
        {

            foreach (var module in assemblyDefinition.Modules)
            {
                if (module == null)
                {
                    continue;
                }

                if (myCodeInjectModule == null && module.Name == MyCodeInjectModuleName)
                {
                    myCodeInjectModule = module;
                    return;
                }
            }

            foreach (var assemblyNameReference in assemblyDefinition.MainModule.AssemblyReferences)
            {
                if (assemblyNameReference == null)
                {
                    continue;
                }
                if (visited.Contains(assemblyNameReference.Name))
                {
                    continue;
                }

                visited.Add(assemblyNameReference.Name);

                var assembly = assemblyResolver.Resolve(assemblyNameReference);
                if (assembly == null)
                {
                    continue;
                }
                SearchForBaseModulesRecursive(assembly, assemblyResolver, ref myCodeInjectModule, visited);

                if (myCodeInjectModule != null)
                {
                    return;
                }
            }
        }

        internal static ModuleDefinition FindBaseModules(AssemblyDefinition assemblyDefinition, PostProcessorAssemblyResolver assemblyResolver)
        {
            ModuleDefinition myCodeInjectModule = null;
            var visited = new HashSet<string>();
            SearchForBaseModulesRecursive(assemblyDefinition, assemblyResolver, ref myCodeInjectModule, visited);

            return myCodeInjectModule;
        }
    }
}