﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace MyCodeInject
{
    internal class PostProcessorAssemblyResolver : IAssemblyResolver
    {
        private readonly string[] _assemblyReferences;
        private readonly Dictionary<string, AssemblyDefinition> _assemblyCache = new Dictionary<string, AssemblyDefinition>();
        private readonly ICompiledAssembly _compiledAssembly;
        private AssemblyDefinition _selfAssembly;

        public PostProcessorAssemblyResolver(ICompiledAssembly compiledAssembly)
        {
            _compiledAssembly = compiledAssembly;
            _assemblyReferences = compiledAssembly.References;
        }

        public void Dispose() { }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            lock (_assemblyCache)
            {
                if (name.Name == _compiledAssembly.Name)
                {
                    return _selfAssembly;
                }

                var fileName = FindFile(name);
                if (fileName == null)
                {
                    return null;
                }

                var lastWriteTime = File.GetLastWriteTime(fileName);
                var cacheKey = $"{fileName}{lastWriteTime}";
                if (_assemblyCache.TryGetValue(cacheKey, out var result))
                {
                    return result;
                }

                parameters.AssemblyResolver = this;

                var ms = MemoryStreamFor(fileName);
                var pdb = $"{fileName}.pdb";
                if (File.Exists(pdb))
                {
                    parameters.SymbolStream = MemoryStreamFor(pdb);
                }

                var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                _assemblyCache.Add(cacheKey, assemblyDefinition);

                return assemblyDefinition;
            }
        }

        private string FindFile(AssemblyNameReference name)
        {
            var fileName = _assemblyReferences.FirstOrDefault(r => Path.GetFileName(r) == $"{name.Name}.dll");
            if (fileName != null)
            {
                return fileName;
            }

            // perhaps the type comes from an exe instead
            fileName = _assemblyReferences.FirstOrDefault(r => Path.GetFileName(r) == $"{name.Name}.exe");
            if (fileName != null)
            {
                return fileName;
            }

            //Unfortunately the current ICompiledAssembly API only provides direct references.
            //It is very much possible that a postprocessor ends up investigating a type in a directly
            //referenced assembly, that contains a field that is not in a directly referenced assembly.
            //if we don't do anything special for that situation, it will fail to resolve.  We should fix this
            //in the ILPostProcessing API. As a workaround, we rely on the fact here that the indirect references
            //are always located next to direct references, so we search in all directories of direct references we
            //got passed, and if we find the file in there, we resolve to it.
            return _assemblyReferences
                .Select(Path.GetDirectoryName)
                .Distinct()
                .Select(parentDir => Path.Combine(parentDir, $"{name.Name}.dll"))
                .FirstOrDefault(File.Exists);
        }

        private static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byteArray = new byte[fileStream.Length];
                var readLength = fileStream.Read(byteArray, 0, (int)fileStream.Length);
                if (readLength != fileStream.Length)
                {
                    throw new InvalidOperationException("File read length is not full length of file.");
                }

                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                {
                    throw;
                }

                Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                Thread.Sleep(waitTime);

                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
        {
            _selfAssembly = assemblyDefinition;
        }
    }
}
