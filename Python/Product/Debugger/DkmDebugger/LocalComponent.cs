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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Dia;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Native;
using Microsoft.VisualStudio.Debugger.Symbols;

namespace Microsoft.PythonTools.DkmDebugger {
    public class LocalComponent :
        ComponentBase,
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
                    // When debugging dumps, we don't need the helper, and don't care about it even if it's loaded in the dump.
                    return;
                } else if (process.GetPythonRuntimeInstance() != null) {
                    return;
                }

                var pyrtInfo = process.GetPythonRuntimeInfo();
                var moduleInstance = process.GetNativeRuntimeInstance().GetNativeModuleInstances().Single(mi => mi.UniqueId == ModuleInstanceId);
                if (PythonDLLs.GetPythonLanguageVersion(moduleInstance) != Parsing.PythonLanguageVersion.None) {
                    pyrtInfo.DLLs.Python = moduleInstance;
                    for (int i = 0; i < 2; ++i) {
                        if (moduleInstance.TryGetSymbols() != null) {
                            if (process.LivePart == null) {
                                // If debugging crash dumps, runtime can be created as soon as Python symbols are resolved.
                                CreatePythonRuntimeInstance(process);
                            } else {
                                // If not, we need to check for debugger helper DLL as well, and inject it if it isn't there yet.
                                if (pyrtInfo.DLLs.DebuggerHelper != null) {
                                    CreatePythonRuntimeInstance(process);
                                } else {
                                    DebugAttach.AttachDkm(process.LivePart.Id);
                                }
                            }
                            return;
                        }

                        moduleInstance.TryLoadSymbols();
                    }

                    var warnMsg = DkmCustomMessage.Create(process.Connection, process, Guid.Empty, (int)VsPackageMessage.WarnAboutPythonSymbols, moduleInstance.Name, null);
                    warnMsg.SendToVsService(Guids.CustomDebuggerEventHandlerGuid, IsBlocking: true);
                } else if (PythonDLLs.DebuggerHelperNames.Contains(moduleInstance.Name)) {
                    pyrtInfo.DLLs.DebuggerHelper = moduleInstance;
                    moduleInstance.TryLoadSymbols();
                    if (pyrtInfo.DLLs.Python != null && pyrtInfo.DLLs.Python.TryGetSymbols() != null) {
                        CreatePythonRuntimeInstance(process);
                    }
                } else if (PythonDLLs.CTypesNames.Contains(moduleInstance.Name)) {
                    moduleInstance.TryLoadSymbols();
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
            if (nativeModuleInstance != null && PythonDLLs.CTypesNames.Contains(moduleInstance.Name)) {
                pyrtInfo.DLLs.CTypes = nativeModuleInstance;

                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper != null) {
                    traceHelper.OnCTypesLoaded(nativeModuleInstance);
                }
            }

            if (process.GetPythonRuntimeInstance() != null) {
                return;
            }

            if (pyrtInfo.DLLs.Python != null && pyrtInfo.DLLs.Python.TryGetSymbols() != null) {
                if (process.LivePart == null || pyrtInfo.DLLs.DebuggerHelper != null) {
                    CreatePythonRuntimeInstance(process);
                } else {
                    DebugAttach.AttachDkm(process.LivePart.Id);
                }
            }
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
            if (process.LivePart != null) {
                process.SetDataItem(DkmDataCreationDisposition.CreateNew, new TraceManagerLocalHelper(process, TraceManagerLocalHelper.Kind.StepIn));
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
            completionRoutine(new DkmEvaluateExpressionAsyncResult(DkmFailedEvaluationResult.Create(
                inspectionContext, stackFrame, expression.Text, expression.Text,
                "Evaluation of arbitrary Python expressions is not supported when native debugging is enabled.",
                DkmEvaluationResultFlags.Invalid,
                null)));
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
            public override void Handle(DkmProcess process) {
                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper == null) {
                    Debug.Fail("LocalComponent received a BeginStepInNotification, but there is no TraceManagerLocalHelper to handle it.");
                    throw new InvalidOperationException();
                }

                traceHelper.OnBeginStepIn();
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

        public static DkmRuntimeBreakpoint CreateRuntimeDllFunctionBreakpoint(DkmNativeModuleInstance moduleInstance, string funcName, RuntimeDllBreakpointHandler handler, bool enable = false, bool debugStart = false) {
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

        public static DkmRuntimeBreakpoint[] CreateRuntimeDllFunctionExitBreakpoints(DkmNativeModuleInstance moduleInstance, string funcName, RuntimeDllBreakpointHandler handler, bool enable = false) {
            var process = moduleInstance.Process;
            var runtimeBreakpoints = process.GetOrCreateDataItem(() => new RuntimeDllBreakpoints());

            var funcSym = moduleInstance.GetSymbols().GetSymbol(SymTagEnum.SymTagFunction, funcName);
            var funcEnds = funcSym.GetSymbols(SymTagEnum.SymTagFuncDebugEnd, null).ToArray();
            if (funcEnds.Length == 0) {
                Debug.Fail("Cannot set exit breakpoint for function " + funcName + " because it has no FuncDebugEnd symbols.");
                throw new NotSupportedException();
            }

            var bps = new List<DkmRuntimeBreakpoint>();
            foreach (var funcEnd in funcEnds) {
                if (funcEnd.locationType != (uint)DiaLocationType.LocIsStatic) {
                    Debug.Fail("Cannot set exit breakpoint for function " + funcName + " because it has a non-static FuncDebugEnd symbol.");
                    throw new NotSupportedException();
                }

                ulong addr = moduleInstance.BaseAddress + funcEnd.relativeVirtualAddress;
                var bp = process.CreateBreakpoint(Guids.LocalComponentGuid, addr);
                if (enable) {
                    bp.Enable();
                }
                bps.Add(bp);

                runtimeBreakpoints.Handlers.Add(bp.UniqueId, handler);
            }

            return bps.ToArray();
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
    }
}
