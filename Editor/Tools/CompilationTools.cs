using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for accessing compilation status and errors.
    /// </summary>
    [McpToolProvider]
    public class CompilationTools
    {
        private static readonly List<CompilationError> _compilationErrors = new List<CompilationError>();
        private static bool _isInitialized;

        static CompilationTools()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_isInitialized) return;
            
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            
            _isInitialized = true;
        }

        private static void OnCompilationStarted(object context)
        {
            lock (_compilationErrors)
            {
                _compilationErrors.Clear();
            }
        }

        private static void OnCompilationFinished(object context)
        {
            // Compilation finished - errors are already captured
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            lock (_compilationErrors)
            {
                foreach (var message in messages)
                {
                    if (message.type == CompilerMessageType.Error || message.type == CompilerMessageType.Warning)
                    {
                        _compilationErrors.Add(new CompilationError
                        {
                            type = message.type.ToString(),
                            message = message.message,
                            file = message.file,
                            line = message.line,
                            column = message.column,
                            assemblyPath = assemblyPath
                        });
                    }
                }
            }
        }

        [McpTool("unity_get_compilation_status", "Get the current compilation status and any errors")]
        public static object GetCompilationStatus(string argsJson)
        {
            Initialize();
            
            List<CompilationError> errors;
            lock (_compilationErrors)
            {
                errors = new List<CompilationError>(_compilationErrors);
            }
            
            // Separate errors and warnings
            var errorList = new List<CompilationError>();
            var warningList = new List<CompilationError>();
            
            foreach (var e in errors)
            {
                if (e.type == "Error")
                    errorList.Add(e);
                else
                    warningList.Add(e);
            }
            
            return new CompilationStatusResult
            {
                isCompiling = EditorApplication.isCompiling,
                hasErrors = errorList.Count > 0,
                errorCount = errorList.Count,
                warningCount = warningList.Count,
                errors = errorList.ToArray(),
                warnings = warningList.ToArray()
            };
        }

        [McpTool("unity_recompile_scripts", "Force recompilation of all scripts")]
        public static object RecompileScripts(string argsJson)
        {
            if (EditorApplication.isCompiling)
            {
                return new { error = "Compilation is already in progress" };
            }
            
            CompilationPipeline.RequestScriptCompilation();
            
            return new RecompileResult
            {
                success = true,
                message = "Script recompilation requested"
            };
        }

        [McpTool("unity_get_assemblies", "Get information about project assemblies")]
        public static object GetAssemblies(string argsJson)
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var assemblyInfos = new List<AssemblyInfo>();
            
            foreach (var asm in assemblies)
            {
                var sourceFiles = new List<string>();
                foreach (var file in asm.sourceFiles)
                {
                    sourceFiles.Add(file);
                }
                
                assemblyInfos.Add(new AssemblyInfo
                {
                    name = asm.name,
                    outputPath = asm.outputPath,
                    sourceFileCount = asm.sourceFiles.Length,
                    sourceFiles = sourceFiles.Count <= 20 ? sourceFiles.ToArray() : null, // Limit for large assemblies
                    flags = asm.flags.ToString()
                });
            }
            
            return new GetAssembliesResult
            {
                assemblyCount = assemblyInfos.Count,
                assemblies = assemblyInfos.ToArray()
            };
        }

        #region Data Types

        [Serializable]
        public class CompilationError
        {
            public string type;
            public string message;
            public string file;
            public int line;
            public int column;
            public string assemblyPath;
        }

        [Serializable]
        public class CompilationStatusResult
        {
            public bool isCompiling;
            public bool hasErrors;
            public int errorCount;
            public int warningCount;
            public CompilationError[] errors;
            public CompilationError[] warnings;
        }

        [Serializable]
        public class RecompileResult
        {
            public bool success;
            public string message;
        }

        [Serializable]
        public class AssemblyInfo
        {
            public string name;
            public string outputPath;
            public int sourceFileCount;
            public string[] sourceFiles;
            public string flags;
        }

        [Serializable]
        public class GetAssembliesResult
        {
            public int assemblyCount;
            public AssemblyInfo[] assemblies;
        }

        #endregion
    }
}
