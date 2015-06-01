/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Symbols;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class ModuleManager : DkmDataItem {
        public static DkmResolvedDocument[] FindDocuments(DkmModule module, DkmSourceFileId sourceFileId) {
            DkmDocumentMatchStrength matchStrength;
            if (string.Equals(module.Name, sourceFileId.DocumentName, StringComparison.OrdinalIgnoreCase)) {
                matchStrength = DkmDocumentMatchStrength.FullPath;
            } else {
                // Either the module path is relative, or it's absolute but on a different filesystem (i.e. remote debugging).
                // Walk the local filesystem up starting from source file path, matching it against the module path component
                // by component, stopping once __init__.py is no longer seen on the same level. The intent is to approximate
                // a match on module names by matching the tails of the two paths that contribute to the fully qualified names
                // of the modules.
                string sourcePath = sourceFileId.DocumentName;
                string modulePath = module.Name;
                int levels = 0;
                do {
                    try {
                        string sourceFile = Path.GetFileName(sourcePath);
                        string moduleFile = Path.GetFileName(modulePath);
                        if (!string.Equals(sourceFile, moduleFile, StringComparison.OrdinalIgnoreCase)) {
                            return new DkmResolvedDocument[0];
                        }
                        sourcePath = Path.GetDirectoryName(sourcePath);
                        modulePath = Path.GetDirectoryName(modulePath);
                    } catch (ArgumentException) {
                        return new DkmResolvedDocument[0];
                    }
                    ++levels;
                } while (File.Exists(Path.Combine(sourcePath, "__init__.py")));
                matchStrength = (levels == 1) ? DkmDocumentMatchStrength.FileName : DkmDocumentMatchStrength.SubPath;
            }

            return new[] {
                DkmResolvedDocument.Create(module, module.Name, null, matchStrength, DkmResolvedDocumentWarning.None, false, null)
            };
        }

        private readonly DkmProcess _process;
        private readonly PythonRuntimeInfo _pyrtInfo;

        public ModuleManager(DkmProcess process) {
            _process = process;
            _pyrtInfo = process.GetPythonRuntimeInfo();

            LoadInitialPythonModules();
            LocalComponent.CreateRuntimeDllFunctionBreakpoint(_pyrtInfo.DLLs.Python, "PyCode_New", PythonDllBreakpointHandlers.PyCode_New, enable: true, debugStart: true);
            LocalComponent.CreateRuntimeDllFunctionBreakpoint(_pyrtInfo.DLLs.Python, "PyCode_NewEmpty", PythonDllBreakpointHandlers.PyCode_NewEmpty, enable: true, debugStart: true);
        }

        private void LoadInitialPythonModules() {
            foreach (var interp in PyInterpreterState.GetInterpreterStates(_process)) {
                var modules = interp.modules.TryRead();
                if (modules == null) {
                    continue;
                }

                foreach (var moduleEntry in modules.ReadElements()) {
                    var module = moduleEntry.Value.TryRead() as PyModuleObject;
                    if (module == null) {
                        continue;
                    }

                    var md_dict = module.md_dict.TryRead();
                    if (md_dict == null) {
                        continue;
                    }

                    foreach (var entry in md_dict.ReadElements()) {
                        var name = (entry.Key as IPyBaseStringObject).ToStringOrNull();
                        if (name == "__file__") {
                            var fileName = (entry.Value.TryRead() as IPyBaseStringObject).ToStringOrNull();
                            if (fileName != null && !fileName.EndsWith(".pyd")) {
                                // Unlike co_filename, __file__ usually reflects the actual name of the file from which the module
                                // was created, which will be .pyc rather than .py if it was available, so fix that up.
                                if (fileName.EndsWith(".pyc")) {
                                    fileName = fileName.Substring(0, fileName.Length - 1);
                                }

                                new RemoteComponent.CreateModuleRequest {
                                    ModuleId = Guid.NewGuid(),
                                    FileName = fileName
                                }.SendLower(_process);
                            }
                        }
                    }
                }
            }
        }

        public static DkmInstructionSymbol[] FindSymbols(DkmResolvedDocument resolvedDocument, DkmTextSpan textSpan, string text, out DkmSourcePosition[] symbolLocation) {
            var sourceFileId = DkmSourceFileId.Create(resolvedDocument.DocumentName, null, null, null);
            var resultSpan = new DkmTextSpan(textSpan.StartLine, textSpan.StartLine, 0, 0);
            symbolLocation = new[] { DkmSourcePosition.Create(sourceFileId, resultSpan) };

            var location = new SourceLocation(resolvedDocument.DocumentName, textSpan.StartLine);
            var encodedLocation = location.Encode();
            return new[] { DkmCustomInstructionSymbol.Create(resolvedDocument.Module, Guids.PythonRuntimeTypeGuid, encodedLocation, 0, encodedLocation) };
        }

        public static DkmSourcePosition GetSourcePosition(DkmInstructionSymbol instruction, DkmSourcePositionFlags flags, DkmInspectionSession inspectionSession, out bool startOfLine) {
            var insSym = instruction as DkmCustomInstructionSymbol;
            var loc = new SourceLocation(insSym.AdditionalData);
            startOfLine = true;
            return DkmSourcePosition.Create(DkmSourceFileId.Create(loc.FileName, null, null, null), new DkmTextSpan(loc.LineNumber, loc.LineNumber, 0, 0));
        }

        private class PythonDllBreakpointHandlers {
            public static void PyCode_New(DkmThread thread, ulong frameBase, ulong vframe) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                var filenamePtr = cppEval.EvaluateUInt64("filename");
                var filenameObj = PyObject.FromAddress(process, filenamePtr) as IPyBaseStringObject;
                if (filenameObj == null) {
                    return;
                }

                string filename = filenameObj.ToString();
                if (process.GetPythonRuntimeInstance().GetModuleInstances().Any(mi => mi.FullName == filename)) {
                    return;
                }

                new RemoteComponent.CreateModuleRequest {
                    ModuleId = Guid.NewGuid(),
                    FileName = filename
                }.SendLower(process);
            }

            public static void PyCode_NewEmpty(DkmThread thread, ulong frameBase, ulong vframe) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                ulong filenamePtr = cppEval.EvaluateUInt64("filename");
                if (filenamePtr == 0) {
                    return;
                }

                string filename = new CStringProxy(process, filenamePtr).ReadUnicode();
                if (process.GetPythonRuntimeInstance().GetModuleInstances().Any(mi => mi.FullName == filename)) {
                    return;
                }

                new RemoteComponent.CreateModuleRequest {
                    ModuleId = Guid.NewGuid(),
                    FileName = filename
                }.SendLower(process);
            }
        }
    }
}
