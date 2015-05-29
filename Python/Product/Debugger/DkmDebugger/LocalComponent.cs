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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Dia;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Evaluation.IL;
using Microsoft.VisualStudio.Debugger.Native;
using Microsoft.VisualStudio.Debugger.Symbols;

namespace Microsoft.PythonTools.DkmDebugger {
    public class LocalComponent :
        ComponentBase,
#if DEV14_OR_LATER
        IDkmIntrinsicFunctionEvaluator140,
#endif
        IDkmModuleSymbolsLoadedNotification,
        IDkmRuntimeInstanceLoadNotification,
        IDkmCallStackFilter,
        IDkmLanguageFrameDecoder,
        IDkmLanguageExpressionEvaluator,
        IDkmCustomVisualizer,
        IDkmSymbolCompilerIdQuery,
        IDkmSymbolDocumentCollectionQuery,
        IDkmSymbolDocumentSpanQuery,
        IDkmSymbolQuery {

        public LocalComponent()
            : base(Guids.LocalComponentGuid) {
        }

        private static void CreatePythonRuntimeInstance(DkmProcess process) {
            var pyrtInfo = process.GetPythonRuntimeInfo();
            var pythonDllId = pyrtInfo.DLLs.Python.UniqueId;
            var debuggerHelperDllId = pyrtInfo.DLLs.DebuggerHelper != null ? pyrtInfo.DLLs.DebuggerHelper.UniqueId : Guid.Empty;

            new LocalStackWalkingComponent.BeforeCreatePythonRuntimeNotification {
                PythonDllModuleInstanceId = pythonDllId,
                DebuggerHelperDllModuleInstanceId = debuggerHelperDllId
            }.SendHigher(process);

            new RemoteComponent.CreatePythonRuntimeRequest {
                PythonDllModuleInstanceId = pythonDllId,
                DebuggerHelperDllModuleInstanceId = debuggerHelperDllId
            }.SendLower(process);
        }

        private class HelperDllInjectionDataHolder : DkmDataItem {
            public DkmThread SuspendedThread { get; set; }
        }

        private static string GetPyInitializeObjectFile(PythonLanguageVersion version) {
            switch (version) {
                case PythonLanguageVersion.V27:
                case PythonLanguageVersion.V33:
                case PythonLanguageVersion.V34:
                    return "pythonrun.obj";
                case PythonLanguageVersion.V35:
                    return "pylifecycle.obj";
                default:
                    Debug.Fail("Unsupported Python version");
                    return string.Empty;
            }
        }

        private static void InjectHelperDll(DkmProcess process) {
            var injectionData = process.GetDataItem<HelperDllInjectionDataHolder>();
            if (injectionData != null) {
                // Injection is already in progress.
                return;
            }

            injectionData = new HelperDllInjectionDataHolder();
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, injectionData);

            var pyrtInfo = process.GetPythonRuntimeInfo();

            // Loading the helper is done via CreateRemoteThread(LoadLibrary), which is inherently asynchronous.
            // On the other hand, we will not handle breakpoints until it is loaded - they won't even be bound.
            // If any Python code is running in the meantime, this may cause us to skip breakpoints, which is
            // very surprising in the run (F5) scenario, as the user expects all preset breakpoints to be hit.
            // To fix that, we need block the Python interpreter loop until the helper is fully loaded.
            //
            // Pausing all threads is not a good way to do this, because one of the threads may be holding the
            // loader lock, which will prevent the helper from loading and result in a deadlock. So instead,
            // block at a known location at the beginning of PyInitialize_Ex, and only freeze the thread that
            // calls it - this is sufficient to prevent execution of Python code in run scenario before helper
            // is loaded.
            //
            // For attach-to-running-process scenario, we do nothing because the attach itself is inherently
            // asynchronous, and so there's no user expectation that breakpoints light up instantly.

            // If Python is already initialized, this is attach-to-running-process - don't block.
            var initialized = pyrtInfo.DLLs.Python.GetStaticVariable<Int32Proxy>(
                "initialized",
                GetPyInitializeObjectFile(pyrtInfo.LanguageVersion)
            );
            if (initialized.Read() == 0) {
                // When Py_InitializeEx is hit, suspend the thread.
                DkmRuntimeBreakpoint makePendingCallsBP = null;
                makePendingCallsBP = CreateRuntimeDllExportedFunctionBreakpoint(pyrtInfo.DLLs.Python, "Py_InitializeEx", (thread, frameBase, vFrame) => {
                    makePendingCallsBP.Close();
                    if (process.GetPythonRuntimeInstance() == null) {
                        thread.Suspend(true);
                        injectionData.SuspendedThread = thread;
                    }
                });
                makePendingCallsBP.Enable();
            }

            // Inject the helper DLL; OnHelperDllInitialized will resume the thread once the DLL is loaded and initialized.
            DebugAttach.AttachDkm(process.LivePart.Id);
        }

        private static void OnHelperDllInitialized(DkmNativeModuleInstance moduleInstance) {
            var process = moduleInstance.Process;
            var pyrtInfo = process.GetPythonRuntimeInfo();
            pyrtInfo.DLLs.DebuggerHelper = moduleInstance;

            if (pyrtInfo.DLLs.Python != null && pyrtInfo.DLLs.Python.HasSymbols()) {
                CreatePythonRuntimeInstance(process);
            }

            // If there was a suspended thread, resume it.
            var injectionData = process.GetDataItem<HelperDllInjectionDataHolder>();
            if (injectionData != null && injectionData.SuspendedThread != null) {
                injectionData.SuspendedThread.Resume(true);
            }
        }

        // Normally we would just want to handle IDkmModuleInstanceLoadNotification.OnModuleInstanceLoad. However, if we create Python runtime
        // in response to OnModuleInstanceLoad, this will happen before LocalStackWalkingComponent (being a higher-level component) has seen
        // the newly loaded module, and it will be unable to reference the Python & debugger helper DLLs in its initialization code. For this
        // reason, let LocalStackWalkingComponent handle OnModuleInstanceLoad, and notify us using this message when it does - this way, we only
        // create the runtime when all components on all levels can access the module instances and their symbols.
        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class NativeModuleInstanceLoadedNotification : MessageBase<NativeModuleInstanceLoadedNotification> {
            [DataMember]
            public Guid ModuleInstanceId { get; set; }

            public override void Handle(DkmProcess process) {
                if (process.LivePart == null) {
                    // When debugging dumps, there's no stepping or live expression evaluation. Hence, we don't
                    // need the helper DLL nor _ctypes.pyd for anything, and even if they are loaded in the dump,
                    // we don't care about them at all.
                    return;
                }

                var pyrtInfo = process.GetPythonRuntimeInfo();
                var moduleInstance = process.GetNativeRuntimeInstance().GetNativeModuleInstances().Single(mi => mi.UniqueId == ModuleInstanceId);

                if (pyrtInfo.DLLs.CTypes == null && PythonDLLs.CTypesNames.Contains(moduleInstance.Name)) {
                    moduleInstance.TryLoadSymbols();
                    if (moduleInstance.HasSymbols()) {
                        pyrtInfo.DLLs.CTypes = moduleInstance;

                        var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                        if (traceHelper != null) {
                            traceHelper.OnCTypesLoaded(moduleInstance);
                        }
                    }
                }

                if (process.GetPythonRuntimeInstance() != null) {
                    return;
                }

                if (PythonDLLs.GetPythonLanguageVersion(moduleInstance) != PythonLanguageVersion.None) {
                    pyrtInfo.DLLs.Python = moduleInstance;
                    for (int i = 0; i < 2; ++i) {
                        if (moduleInstance.HasSymbols()) {
                            if (IsModuleCompiledWithPGO(moduleInstance)) {
                                pyrtInfo.DLLs.Python = null;
                                var pgoWarnMsg = DkmCustomMessage.Create(process.Connection, process, Guid.Empty, (int)VsPackageMessage.WarnAboutPGO, moduleInstance.Name, null);
                                pgoWarnMsg.SendToVsService(Guids.CustomDebuggerEventHandlerGuid, IsBlocking: true);
                                return;
                            }

                            if (process.LivePart == null) {
                                // If debugging crash dumps, runtime can be created as soon as Python symbols are resolved.
                                CreatePythonRuntimeInstance(process);
                            } else {
                                // If not, we need to check for debugger helper DLL as well, and inject it if it isn't there yet.
                                if (pyrtInfo.DLLs.DebuggerHelper != null) {
                                    CreatePythonRuntimeInstance(process);
                                } else {
                                    InjectHelperDll(process);
                                }
                            }
                            return;
                        }

                        moduleInstance.TryLoadSymbols();
                    }

                    var symWarnMsg = DkmCustomMessage.Create(process.Connection, process, Guid.Empty, (int)VsPackageMessage.WarnAboutPythonSymbols, moduleInstance.Name, null);
                    symWarnMsg.SendToVsService(Guids.CustomDebuggerEventHandlerGuid, IsBlocking: true);
                } else if (PythonDLLs.DebuggerHelperNames.Contains(moduleInstance.Name)) {
                    moduleInstance.TryLoadSymbols();

                    // When the module is reported is loaded, it is not necessarily fully initialized yet - it is possible to get into a state
                    // where its import table is not processed yet. If we register TraceFunc and it gets called by Python when in that state,
                    // we'll get a crash as soon as any imported WinAPI function is called. So check whether DllMain has already run - if it
                    // is, we're good to go, and if not, set a breakpoint on a hook that will be called once it is run, and defer runtime
                    // creation until that breakpoint is hit.

                    bool isInitialized = moduleInstance.GetExportedStaticVariable<ByteProxy>("isInitialized").Read() != 0;
                    if (isInitialized) {
                        OnHelperDllInitialized(moduleInstance);
                    } else {
                        DkmRuntimeBreakpoint initBP = null;
                        initBP = CreateRuntimeDllExportedFunctionBreakpoint(moduleInstance, "OnInitialized", (thread, frameBase, vFrame) => {
                            initBP.Close();
                            OnHelperDllInitialized(moduleInstance);
                        });
                        initBP.Enable();
                    }
                }
            }
        }

        void IDkmModuleSymbolsLoadedNotification.OnModuleSymbolsLoaded(DkmModuleInstance moduleInstance, DkmModule module, bool isReload, DkmWorkList workList, DkmEventDescriptor eventDescriptor) {
            var process = moduleInstance.Process;

            var engines = process.DebugLaunchSettings.EngineFilter;
            if (engines == null || !engines.Contains(AD7Engine.DebugEngineGuid)) {
                return;
            }

            var pyrtInfo = process.GetPythonRuntimeInfo();

            var nativeModuleInstance = moduleInstance as DkmNativeModuleInstance;
            if (nativeModuleInstance != null) {
                if (PythonDLLs.CTypesNames.Contains(moduleInstance.Name)) {
                    pyrtInfo.DLLs.CTypes = nativeModuleInstance;

                    var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                    if (traceHelper != null) {
                        traceHelper.OnCTypesLoaded(nativeModuleInstance);
                    }
                } else if (PythonDLLs.GetPythonLanguageVersion(nativeModuleInstance) != PythonLanguageVersion.None) {
                    if (IsModuleCompiledWithPGO(moduleInstance)) {
                        pyrtInfo.DLLs.Python = null;
                        var pgoWarnMsg = DkmCustomMessage.Create(process.Connection, process, Guid.Empty, (int)VsPackageMessage.WarnAboutPGO, moduleInstance.Name, null);
                        pgoWarnMsg.SendToVsService(Guids.CustomDebuggerEventHandlerGuid, IsBlocking: true);
                        return;
                    }
                }
            }
            
            if (process.GetPythonRuntimeInstance() != null) {
                return;
            }

            if (pyrtInfo.DLLs.Python != null && pyrtInfo.DLLs.Python.HasSymbols()) {
                if (process.LivePart == null || pyrtInfo.DLLs.DebuggerHelper != null) {
                    CreatePythonRuntimeInstance(process);
                } else {
                    InjectHelperDll(process);
                }
            }
        }

        // For PGO-enabled binaries, their symbol information is unreliable, often in dangerous ways (e.g. FuncDebugStart/End is basically garbage
        // for split functions, and locals can be messed up), so we do not support Python built with PGO (currently only 2.7.3 and below).
        private static bool IsModuleCompiledWithPGO(DkmModuleInstance moduleInstance) {
            using (var moduleSym = moduleInstance.GetSymbols()) {
                var compSyms = moduleSym.Object.GetSymbols(SymTagEnum.SymTagCompiland, null);
                try {
                    foreach (var compSym in compSyms) {
                        var blockSyms = compSym.Object.GetSymbols(SymTagEnum.SymTagBlock, null);
                        try {
                            foreach (var blockSym in blockSyms) {
                                using (var parentSym = ComPtr.Create(blockSym.Object.lexicalParent)) {
                                    uint blockStart = blockSym.Object.relativeVirtualAddress;
                                    uint funcStart = parentSym.Object.relativeVirtualAddress;
                                    uint funcEnd = funcStart + (uint)parentSym.Object.length;
                                    if (blockStart < funcStart || blockStart >= funcEnd) {
                                        return true;
                                    }
                                }
                            }
                        } finally {
                            foreach (var blockSym in blockSyms) {
                                blockSym.Dispose();
                            }
                        }
                    }
                } finally {
                    foreach (var funcSym in compSyms) {
                        funcSym.Dispose();
                    }
                }
            }
            return false;
        }

        unsafe void IDkmRuntimeInstanceLoadNotification.OnRuntimeInstanceLoad(DkmRuntimeInstance runtimeInstance, DkmEventDescriptor eventDescriptor) {
            if (runtimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("OnRuntimeInstanceLoad notification for a non-Python runtime.");
                throw new NotSupportedException();
            }

            var process = runtimeInstance.Process;
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, new ModuleManager(process));
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, new CallStackFilter(process));
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, new ExpressionEvaluator(process));
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, new PyObjectAllocator(process));

            var exceptionManager = process.GetOrCreateDataItem(() => new ExceptionManagerLocalHelper(process));
            exceptionManager.OnPythonRuntimeInstanceLoaded();

            if (process.LivePart != null) {
                process.SetDataItem(DkmDataCreationDisposition.CreateNew, new TraceManagerLocalHelper(process, TraceManagerLocalHelper.Kind.StepIn));
            }

            // If both local and remote components are actually in the same process, they share the same DebuggerOptions, so no need to propagate it.
            if (process.Connection.Flags.HasFlag(DkmTransportConnectionFlags.MarshallingRequired)) {
                process.SetDataItem(DkmDataCreationDisposition.CreateNew, new DebuggerOptionsPropagator(process));
            }
        }

        DkmStackWalkFrame[] IDkmCallStackFilter.FilterNextFrame(DkmStackContext stackContext, DkmStackWalkFrame input) {
            if (input == null) {
                return null;
            }

            var filter = input.Process.GetDataItem<CallStackFilter>();
            try {
                if (filter != null) {
                    return filter.FilterNextFrame(stackContext, input);
                }
            } catch (DkmException) {
            }
            return new[] { input };
        }

        void IDkmLanguageFrameDecoder.GetFrameName(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmVariableInfoFlags argumentFlags, DkmCompletionRoutine<DkmGetFrameNameAsyncResult> completionRoutine) {
            if (frame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("GetFrameName called on a non-Python frame.");
                throw new NotSupportedException();
            }

            var filter = frame.Process.GetDataItem<CallStackFilter>();
            if (filter == null) {
                Debug.Fail("GetFrameName called, but no instance of CallStackFilter is there to handle it.");
                throw new InvalidOperationException();
            }

            filter.GetFrameName(inspectionContext, workList, frame, argumentFlags, completionRoutine);
        }

        void IDkmLanguageFrameDecoder.GetFrameReturnType(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmCompletionRoutine<DkmGetFrameReturnTypeAsyncResult> completionRoutine) {
            if (frame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("GetFrameReturnType called on a non-Python frame.");
                throw new NotSupportedException();
            }

            var filter = frame.Process.GetDataItem<CallStackFilter>();
            if (filter == null) {
                Debug.Fail("GetFrameReturnType called, but no instance of CallStackFilter exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            filter.GetFrameReturnType(inspectionContext, workList, frame, completionRoutine);
        }

        void IDkmLanguageExpressionEvaluator.EvaluateExpression(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmLanguageExpression expression, DkmStackWalkFrame stackFrame, DkmCompletionRoutine<DkmEvaluateExpressionAsyncResult> completionRoutine) {
            if (stackFrame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("EvaluateExpression called on a non-Python frame.");
                throw new NotSupportedException();
            }

            var ee = stackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("EvaluateExpression called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            ee.EvaluateExpression(inspectionContext, workList, expression, stackFrame, completionRoutine);
        }

        void IDkmLanguageExpressionEvaluator.GetFrameLocals(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame stackFrame, DkmCompletionRoutine<DkmGetFrameLocalsAsyncResult> completionRoutine) {
            if (stackFrame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("GetFrameLocals called on a non-Python frame.");
                throw new NotSupportedException();
            }

            var ee = stackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("GetFrameLocals called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            ee.GetFrameLocals(inspectionContext, workList, stackFrame, completionRoutine);            
        }

        void IDkmLanguageExpressionEvaluator.GetChildren(DkmEvaluationResult result, DkmWorkList workList, int initialRequestSize, DkmInspectionContext inspectionContext, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completionRoutine) {
            var ee = result.StackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("GetChildren called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            ee.GetChildren(result, workList, initialRequestSize, inspectionContext, completionRoutine);
        }

        void IDkmLanguageExpressionEvaluator.GetFrameArguments(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame frame, DkmCompletionRoutine<DkmGetFrameArgumentsAsyncResult> completionRoutine) {
            throw new NotImplementedException();
        }

        void IDkmLanguageExpressionEvaluator.GetItems(DkmEvaluationResultEnumContext enumContext, DkmWorkList workList, int startIndex, int count, DkmCompletionRoutine<DkmEvaluationEnumAsyncResult> completionRoutine) {
            var ee = enumContext.StackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("GetItems called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            ee.GetItems(enumContext, workList, startIndex, count, completionRoutine);
        }

        string IDkmLanguageExpressionEvaluator.GetUnderlyingString(DkmEvaluationResult result) {
            var ee = result.StackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("GetUnderlyingString called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            return ee.GetUnderlyingString(result);
        }

        void IDkmLanguageExpressionEvaluator.SetValueAsString(DkmEvaluationResult result, string value, int timeout, out string errorText) {
            var ee = result.StackFrame.Process.GetDataItem<ExpressionEvaluator>();
            if (ee == null) {
                Debug.Fail("SetValueAsString called, but no instance of ExpressionEvaluator exists in this DkmProcess to handle it.");
                throw new InvalidOperationException();
            }

            ee.SetValueAsString(result, value, timeout, out errorText);
        }

        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class AsyncBreakReceivedNotification : MessageBase<AsyncBreakReceivedNotification> {
            [DataMember]
            public Guid ThreadId { get; set; }

            public override void Handle(DkmProcess process) {
                var ee = process.GetDataItem<ExpressionEvaluator>();
                if (ee != null) {
                    var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                    ee.OnAsyncBreakComplete(thread);
                }
            }
        }

        void IDkmCustomVisualizer.EvaluateVisualizedExpression(DkmVisualizedExpression visualizedExpression, out DkmEvaluationResult resultObject) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            natVis.EvaluateVisualizedExpression(visualizedExpression, out resultObject);
        }

        void IDkmCustomVisualizer.GetChildren(DkmVisualizedExpression visualizedExpression, int initialRequestSize, DkmInspectionContext inspectionContext, out DkmChildVisualizedExpression[] initialChildren, out DkmEvaluationResultEnumContext enumContext) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            natVis.GetChildren(visualizedExpression, initialRequestSize, inspectionContext, out initialChildren, out enumContext);
        }

        void IDkmCustomVisualizer.GetItems(DkmVisualizedExpression visualizedExpression, DkmEvaluationResultEnumContext enumContext, int startIndex, int count, out DkmChildVisualizedExpression[] items) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            natVis.GetItems(visualizedExpression, enumContext, startIndex, count, out items);
        }

        string IDkmCustomVisualizer.GetUnderlyingString(DkmVisualizedExpression visualizedExpression) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            return natVis.GetUnderlyingString(visualizedExpression);
        }

        void IDkmCustomVisualizer.SetValueAsString(DkmVisualizedExpression visualizedExpression, string value, int timeout, out string errorText) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            natVis.SetValueAsString(visualizedExpression, value, timeout, out errorText);
        }

        void IDkmCustomVisualizer.UseDefaultEvaluationBehavior(DkmVisualizedExpression visualizedExpression, out bool useDefaultEvaluationBehavior, out DkmEvaluationResult defaultEvaluationResult) {
            var natVis = visualizedExpression.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            natVis.UseDefaultEvaluationBehavior(visualizedExpression, out useDefaultEvaluationBehavior, out defaultEvaluationResult);
        }

#if DEV14_OR_LATER 

        DkmILEvaluationResult[] IDkmIntrinsicFunctionEvaluator140.Execute(DkmILExecuteIntrinsic executeIntrinsic, DkmILContext iLContext, DkmCompiledILInspectionQuery inspectionQuery, DkmILEvaluationResult[] arguments, ReadOnlyCollection<DkmCompiledInspectionQuery> subroutines, out DkmILFailureReason failureReason) {
            var natVis = iLContext.StackFrame.Process.GetOrCreateDataItem(() => new PyObjectNativeVisualizer());
            return natVis.Execute(executeIntrinsic, iLContext, inspectionQuery, arguments, subroutines, out failureReason);
        }

#endif

        DkmCompilerId IDkmSymbolCompilerIdQuery.GetCompilerId(DkmInstructionSymbol instruction, DkmInspectionSession inspectionSession) {
            return new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.PythonLanguageGuid);
        }

        DkmResolvedDocument[] IDkmSymbolDocumentCollectionQuery.FindDocuments(DkmModule module, DkmSourceFileId sourceFileId) {
            if (module.CompilerId.LanguageId != Guids.PythonLanguageGuid) {
                Debug.Fail("Non-Python module passed to FindDocuments.");
                throw new NotSupportedException();
            }

            return ModuleManager.FindDocuments(module, sourceFileId);
        }

        DkmInstructionSymbol[] IDkmSymbolDocumentSpanQuery.FindSymbols(DkmResolvedDocument resolvedDocument, DkmTextSpan textSpan, string text, out DkmSourcePosition[] symbolLocation) {
            if (resolvedDocument.Module.CompilerId.LanguageId != Guids.PythonLanguageGuid) {
                Debug.Fail("Non-Python module passed to FindSymbols.");
                throw new NotSupportedException();
            }

            return ModuleManager.FindSymbols(resolvedDocument, textSpan, text, out symbolLocation);
        }

        DkmSourcePosition IDkmSymbolQuery.GetSourcePosition(DkmInstructionSymbol instruction, DkmSourcePositionFlags flags, DkmInspectionSession inspectionSession, out bool startOfLine) {
            return ModuleManager.GetSourcePosition(instruction, flags, inspectionSession, out startOfLine);
        }

        object IDkmSymbolQuery.GetSymbolInterface(DkmModule module, Guid interfaceID) {
            throw new NotImplementedException();
        }

        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class BeginStepInNotification : MessageBase<BeginStepInNotification> {
            [DataMember]
            public Guid ThreadId { get; set; }

            public override void Handle(DkmProcess process) {
                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper == null) {
                    Debug.Fail("LocalComponent received a BeginStepInNotification, but there is no TraceManagerLocalHelper to handle it.");
                    throw new InvalidOperationException();
                }

                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                traceHelper.OnBeginStepIn(thread);
            }
        }

        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class StepCompleteNotification : MessageBase<StepCompleteNotification> {
            public override void Handle(DkmProcess process) {
                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper == null) {
                    Debug.Fail("LocalComponent received a StepCompleteNotification, but there is no TraceManagerLocalHelper to handle it.");
                    throw new InvalidOperationException();
                }

                traceHelper.OnStepComplete();
            }
        }

        public delegate void RuntimeDllBreakpointHandler(DkmThread thread, ulong frameBase, ulong vframe);

        private class RuntimeDllBreakpoints : DkmDataItem {
            public readonly Dictionary<Guid, RuntimeDllBreakpointHandler> Handlers = new Dictionary<Guid, RuntimeDllBreakpointHandler>();
        }

        public static DkmRuntimeInstructionBreakpoint CreateRuntimeDllFunctionBreakpoint(DkmNativeModuleInstance moduleInstance, string funcName, RuntimeDllBreakpointHandler handler, bool enable = false, bool debugStart = false) {
            var process = moduleInstance.Process;
            var runtimeBreakpoints = process.GetOrCreateDataItem(() => new RuntimeDllBreakpoints());

            var addr = moduleInstance.GetFunctionAddress(funcName, debugStart);
            var bp = process.CreateBreakpoint(Guids.LocalComponentGuid, addr);
            if (enable) {
                bp.Enable();
            }
            runtimeBreakpoints.Handlers.Add(bp.UniqueId, handler);
            return bp;
        }

        public static DkmRuntimeBreakpoint CreateRuntimeDllExportedFunctionBreakpoint(DkmNativeModuleInstance moduleInstance, string funcName, RuntimeDllBreakpointHandler handler, bool enable = false) {
            var process = moduleInstance.Process;
            var runtimeBreakpoints = process.GetOrCreateDataItem(() => new RuntimeDllBreakpoints());

            var addr = moduleInstance.GetExportedFunctionAddress(funcName);
            var bp = DkmRuntimeInstructionBreakpoint.Create(Guids.LocalComponentGuid, null, addr, false, null);
            if (enable) {
                bp.Enable();
            }
            runtimeBreakpoints.Handlers.Add(bp.UniqueId, handler);
            return bp;
        }

        public static DkmRuntimeBreakpoint[] CreateRuntimeDllFunctionExitBreakpoints(DkmNativeModuleInstance moduleInstance, string funcName, RuntimeDllBreakpointHandler handler, bool enable = false) {
            var process = moduleInstance.Process;
            var runtimeBreakpoints = process.GetOrCreateDataItem(() => new RuntimeDllBreakpoints());

            using (var moduleSym = moduleInstance.GetSymbols()) 
            using (var funcSym = moduleSym.Object.GetSymbol(SymTagEnum.SymTagFunction, funcName)) {
                var funcEnds = funcSym.Object.GetSymbols(SymTagEnum.SymTagFuncDebugEnd, null);
                try {
                    if (funcEnds.Length == 0) {
                        Debug.Fail("Cannot set exit breakpoint for function " + funcName + " because it has no FuncDebugEnd symbols.");
                        throw new NotSupportedException();
                    }

                    var bps = new List<DkmRuntimeBreakpoint>();
                    foreach (var funcEnd in funcEnds) {
                        if (funcEnd.Object.locationType != (uint)DiaLocationType.LocIsStatic) {
                            Debug.Fail("Cannot set exit breakpoint for function " + funcName + " because it has a non-static FuncDebugEnd symbol.");
                            throw new NotSupportedException();
                        }

                        ulong addr = moduleInstance.BaseAddress + funcEnd.Object.relativeVirtualAddress;
                        var bp = process.CreateBreakpoint(Guids.LocalComponentGuid, addr);
                        if (enable) {
                            bp.Enable();
                        }
                        bps.Add(bp);

                        runtimeBreakpoints.Handlers.Add(bp.UniqueId, handler);
                    }

                    return bps.ToArray();
                } finally {
                    foreach (var funcEnd in funcEnds) {
                        funcEnd.Dispose();
                    }
                }
            }
        }

        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class HandleBreakpointRequest : MessageBase<HandleBreakpointRequest> {
            [DataMember]
            public Guid BreakpointId { get; set; }

            [DataMember]
            public Guid ThreadId { get; set; }

            [DataMember]
            public ulong FrameBase { get; set; }

            [DataMember]
            public ulong VFrame { get; set; }

            public override void Handle(DkmProcess process) {
                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                var runtimeBreakpoints = process.GetDataItem<RuntimeDllBreakpoints>();
                RuntimeDllBreakpointHandler handler;
                if (runtimeBreakpoints.Handlers.TryGetValue(BreakpointId, out handler)) {
                    handler(thread, FrameBase, VFrame);
                } else {
                    Debug.Fail("LocalComponent received a HandleBreakpointRequest for a breakpoint that it does not know about.");
                }
            }
        }

        [DataContract]
        [MessageTo(Guids.LocalComponentId)]
        internal class MonitorExceptionsRequest : MessageBase<MonitorExceptionsRequest> {
            [DataMember]
            public bool MonitorExceptions { get; set; }

            public override void Handle(DkmProcess process) {
                var exceptionHelper = process.GetOrCreateDataItem(() => new ExceptionManagerLocalHelper(process));
                exceptionHelper.MonitorExceptions = MonitorExceptions;
            }
        }
    }
}
