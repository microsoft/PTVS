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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Exceptions;
using Microsoft.VisualStudio.Debugger.Native;
using Microsoft.VisualStudio.Debugger.Stepping;
using Microsoft.VisualStudio.Debugger.Symbols;

namespace Microsoft.PythonTools.DkmDebugger {
    public class RemoteComponent :
        ComponentBase,
        IDkmModuleInstanceLoadNotification,
        IDkmRuntimeMonitorBreakpointHandler,
        IDkmRuntimeBreakpointReceived,
        IDkmRuntimeStepper,
        IDkmExceptionController,
        IDkmExceptionFormatter,
        IDkmExceptionManager,
        IDkmAsyncBreakCompleteReceived {

        public RemoteComponent()
            : base(Guids.RemoteComponentGuid) {
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class CreatePythonRuntimeRequest : MessageBase<CreatePythonRuntimeRequest> {
            [DataMember]
            public Guid PythonDllModuleInstanceId { get; set; }

            [DataMember]
            public Guid DebuggerHelperDllModuleInstanceId { get; set; }

            public override void Handle(DkmProcess process) {
                var pyrtInfo = process.GetPythonRuntimeInfo();
                var nativeModules = process.GetNativeRuntimeInstance().GetNativeModuleInstances();

                pyrtInfo.DLLs.Python = nativeModules.Single(mi => mi.UniqueId == PythonDllModuleInstanceId);
                pyrtInfo.DLLs.Python.FlagAsTransitionModule();

                if (DebuggerHelperDllModuleInstanceId != Guid.Empty) {
                    pyrtInfo.DLLs.DebuggerHelper = nativeModules.Single(mi => mi.UniqueId == DebuggerHelperDllModuleInstanceId);
                    pyrtInfo.DLLs.DebuggerHelper.FlagAsTransitionModule();

                    process.SetDataItem(DkmDataCreationDisposition.CreateNew, new TraceManager(process));
                }

                var runtimeId = new DkmRuntimeInstanceId(Guids.PythonRuntimeTypeGuid, 0);
                var runtimeInstance = DkmCustomRuntimeInstance.Create(process, runtimeId, null);
                new CreateModuleRequest { ModuleId = Guids.UnknownPythonModuleGuid }.Handle(process);
            }
        }

        void IDkmModuleInstanceLoadNotification.OnModuleInstanceLoad(DkmModuleInstance moduleInstance, DkmWorkList workList, DkmEventDescriptorS eventDescriptor) {
            var nativeModuleInstance = moduleInstance as DkmNativeModuleInstance;
            if (nativeModuleInstance != null && PythonDLLs.CTypesNames.Contains(moduleInstance.Name)) {
                var pyrtInfo = moduleInstance.Process.GetPythonRuntimeInfo();
                pyrtInfo.DLLs.CTypes = nativeModuleInstance;
                nativeModuleInstance.FlagAsTransitionModule();
            }
        }

        void IDkmRuntimeMonitorBreakpointHandler.DisableRuntimeBreakpoint(DkmRuntimeBreakpoint runtimeBreakpoint) {
            var traceManager = runtimeBreakpoint.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("DisableRuntimeBreakpoint called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }
            traceManager.RemoveBreakpoint(runtimeBreakpoint);
        }

        void IDkmRuntimeMonitorBreakpointHandler.EnableRuntimeBreakpoint(DkmRuntimeBreakpoint runtimeBreakpoint) {
            var bp = runtimeBreakpoint as DkmRuntimeInstructionBreakpoint;
            if (bp == null) {
                Debug.Fail("Non-Python breakpoint passed to EnableRuntimeBreakpoint.");
                throw new NotImplementedException();
            }

            var instrAddr = bp.InstructionAddress as DkmCustomInstructionAddress;
            if (instrAddr == null || instrAddr.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("Non-Python breakpoint passed to EnableRuntimeBreakpoint.");
                throw new NotImplementedException();
            }

            var traceManager = bp.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("EnableRuntimeBreakpoint called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }
            
            var loc = new SourceLocation(instrAddr.AdditionalData);
            bp.SetDataItem(DkmDataCreationDisposition.CreateNew, loc);
            traceManager.AddBreakpoint(bp);
        }

        void IDkmRuntimeMonitorBreakpointHandler.TestRuntimeBreakpoint(DkmRuntimeBreakpoint runtimeBreakpoint) {
            var traceManager = runtimeBreakpoint.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("TestRuntimeBreakpoint called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }
        }

        void IDkmRuntimeBreakpointReceived.OnRuntimeBreakpointReceived(DkmRuntimeBreakpoint runtimeBreakpoint, DkmThread thread, bool hasException, DkmEventDescriptorS eventDescriptor) {
            if (runtimeBreakpoint.SourceId == Guids.LocalComponentGuid) {
                ulong retAddr, frameBase, vframe;
                thread.GetCurrentFrameInfo(out retAddr, out frameBase, out vframe);
                new LocalComponent.HandleBreakpointRequest {
                    BreakpointId = runtimeBreakpoint.UniqueId,
                    ThreadId = thread.UniqueId,
                    FrameBase = frameBase,
                    VFrame = vframe
                }.SendHigher(thread.Process);
            } else if (runtimeBreakpoint.SourceId == Guids.PythonTraceManagerSourceGuid || runtimeBreakpoint.SourceId == Guids.PythonStepTargetSourceGuid) {
                var traceManager = runtimeBreakpoint.Process.GetDataItem<TraceManager>();
                if (traceManager != null) {
                    traceManager.OnNativeBreakpointHit(runtimeBreakpoint, thread);
                }
            } else {
                Debug.Fail("RemoteComponent received a notification for a breakpoint that it does not know how to handle.");
                throw new ArgumentException();
            }
        }

        void IDkmRuntimeStepper.AfterSteppingArbitration(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, DkmStepArbitrationReason reason, DkmRuntimeInstance newControllingRuntimeInstance) {
        }

        void IDkmRuntimeStepper.BeforeEnableNewStepper(DkmRuntimeInstance runtimeInstance, DkmStepper stepper) {
            var traceManager = runtimeInstance.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("OnNewControllingRuntimeInstance called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }

            traceManager.BeforeEnableNewStepper(runtimeInstance, stepper);
        }

        void IDkmRuntimeStepper.NotifyStepComplete(DkmRuntimeInstance runtimeInstance, DkmStepper stepper) {
        }

        void IDkmRuntimeStepper.OnNewControllingRuntimeInstance(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, DkmStepArbitrationReason reason, DkmRuntimeInstance controllingRuntimeInstance) {
            var traceManager = runtimeInstance.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("OnNewControllingRuntimeInstance called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }

            traceManager.CancelStep(stepper);
        }

        bool IDkmRuntimeStepper.OwnsCurrentExecutionLocation(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, DkmStepArbitrationReason reason) {
            if (!DebuggerOptions.UsePythonStepping) {
                return false;
            }

            var process = runtimeInstance.Process;
            var pyrtInfo = process.GetPythonRuntimeInfo();
            if (pyrtInfo.DLLs.Python == null) {
                return false;
            }

            var thread = stepper.Thread;
            var ip = thread.GetCurrentRegisters(new DkmUnwoundRegister[0]).GetInstructionPointer();

            return pyrtInfo.DLLs.Python.ContainsAddress(ip) ||
                (pyrtInfo.DLLs.DebuggerHelper != null && pyrtInfo.DLLs.DebuggerHelper.ContainsAddress(ip)) ||
                (pyrtInfo.DLLs.CTypes != null && pyrtInfo.DLLs.CTypes.ContainsAddress(ip));
        }

        void IDkmRuntimeStepper.Step(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, DkmStepArbitrationReason reason) {
            var traceManager = runtimeInstance.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("Step called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }

            traceManager.Step(stepper, reason);
        }

        bool IDkmRuntimeStepper.StepControlRequested(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, DkmStepArbitrationReason reason, DkmRuntimeInstance callingRuntimeInstance) {
            return true;
        }

        void IDkmRuntimeStepper.StopStep(DkmRuntimeInstance runtimeInstance, DkmStepper stepper) {
            var traceManager = runtimeInstance.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("StopStep called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }

            traceManager.CancelStep(stepper);
        }

        void IDkmRuntimeStepper.TakeStepControl(DkmRuntimeInstance runtimeInstance, DkmStepper stepper, bool leaveGuardsInPlace, DkmStepArbitrationReason reason, DkmRuntimeInstance callingRuntimeInstance) {
            var traceManager = runtimeInstance.Process.GetDataItem<TraceManager>();
            if (traceManager == null) {
                Debug.Fail("TakeStepControl called before TraceMananger is initialized.");
                throw new InvalidOperationException();
            }

            traceManager.CancelStep(stepper);
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class CreateModuleRequest : MessageBase<CreateModuleRequest> {
            [DataMember]
            public Guid ModuleId { get; set; }

            [DataMember]
            public string FileName { get; set; }

            public CreateModuleRequest() {
                FileName = "";
            }

            public override void Handle(DkmProcess process) {
                var pythonRuntime = process.GetPythonRuntimeInstance();
                if (pythonRuntime == null) {
                    return;
                }

                string moduleName;
                if (ModuleId == Guids.UnknownPythonModuleGuid) {
                    moduleName = "<unknown>";
                } else {
                    try {
                        moduleName = Path.GetFileName(FileName);
                    } catch (ArgumentException) {
                        moduleName = FileName;
                    }
                }

                var module = DkmModule.Create(new DkmModuleId(ModuleId, Guids.PythonSymbolProviderGuid), FileName,
                    new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.PythonLanguageGuid), process.Connection, null);
                var moduleInstance = DkmCustomModuleInstance.Create(moduleName, FileName, 0, pythonRuntime, null, null, DkmModuleFlags.None,
                    DkmModuleMemoryLayout.Unknown, 0, 0, 0, "Python", false, null, null, null);
                moduleInstance.SetModule(module, true);
            }
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class RaiseExceptionRequest : MessageBase<RaiseExceptionRequest> {
            [DataMember]
            public Guid ThreadId { get; set; }

            [DataMember]
            public string Name { get; set; }

            [DataMember]
            public byte[] AdditionalInformation { get; set; }

            public override void Handle(DkmProcess process) {
                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                var excInfo = DkmCustomExceptionInformation.Create(
                    process.GetPythonRuntimeInstance(), Guids.PythonExceptionCategoryGuid, thread, null, Name, 0,
                    DkmExceptionProcessingStage.Thrown | DkmExceptionProcessingStage.UserVisible | DkmExceptionProcessingStage.UserCodeSearch,
                    null, new ReadOnlyCollection<byte>(AdditionalInformation));
                excInfo.OnDebugMonitorException();
            }
        }

        bool IDkmExceptionController.CanModifyProcessing(DkmExceptionInformation exception) {
            return false;
        }

        void IDkmExceptionController.SquashProcessing(DkmExceptionInformation exception) {
            throw new NotImplementedException();
        }

        string IDkmExceptionFormatter.GetAdditionalInformation(DkmExceptionInformation exception) {
            var em = exception.Process.GetOrCreateDataItem(() => new ExceptionManager(exception.Process));
            return em.GetAdditionalInformation(exception);
        }

        string IDkmExceptionFormatter.GetDescription(DkmExceptionInformation exception) {
            var em = exception.Process.GetOrCreateDataItem(() => new ExceptionManager(exception.Process));
            return em.GetDescription(exception);
        }

        void IDkmExceptionManager.AddExceptionTrigger(DkmProcess process, Guid sourceId, DkmExceptionTrigger trigger) {
            var em = process.GetOrCreateDataItem(() => new ExceptionManager(process));
            em.AddExceptionTrigger(process, sourceId, trigger);
        }

        void IDkmExceptionManager.ClearExceptionTriggers(DkmProcess process, Guid sourceId) {
            var em = process.GetOrCreateDataItem(() => new ExceptionManager(process));
            em.ClearExceptionTriggers(process, sourceId);
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class EndFuncEvalExecutionRequest : MessageBase<EndFuncEvalExecutionRequest> {
            [DataMember]
            public Guid ThreadId { get; set; }

            public override void Handle(DkmProcess process) {
                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                thread.EndFuncEvalExecution(DkmFuncEvalFlags.None);
            }
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class AbortingEvalExecutionRequest : MessageBase<AbortingEvalExecutionRequest> {
            public override void Handle(DkmProcess process) {
                process.AbortingFuncEvalExecution(DkmFuncEvalFlags.None);
            }
        }

        void IDkmAsyncBreakCompleteReceived.OnAsyncBreakCompleteReceived(DkmProcess process, DkmAsyncBreakStatus status, DkmThread thread, DkmEventDescriptorS eventDescriptor) {
            new LocalComponent.AsyncBreakReceivedNotification { ThreadId = thread.UniqueId }.SendHigher(process);
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class GetCurrentFrameInfoRequest : MessageBase<GetCurrentFrameInfoRequest, GetCurrentFrameInfoResponse> {
            [DataMember]
            public Guid ThreadId { get; set; }

            public override GetCurrentFrameInfoResponse Handle(DkmProcess process) {
                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);
                var response = new GetCurrentFrameInfoResponse();
                thread.GetCurrentFrameInfo(out response.RetAddr, out response.FrameBase, out response.VFrame);
                return response;
            }
        }

        [DataContract]
        internal class GetCurrentFrameInfoResponse {
            [DataMember]
            public ulong RetAddr, FrameBase, VFrame;
        }

        [DataContract]
        [MessageTo(Guids.RemoteComponentId)]
        internal class SetDebuggerOptions : MessageBase<SetDebuggerOptions> {
            [DataMember]
            public bool ShowNativePythonFrames, UsePythonStepping, ShowCppViewNodes, ShowPythonViewNodes;

            public override void Handle(DkmProcess process) {
                DebuggerOptions.ShowNativePythonFrames = ShowNativePythonFrames;
                DebuggerOptions.UsePythonStepping = UsePythonStepping;
                DebuggerOptions.ShowCppViewNodes = ShowCppViewNodes;
                DebuggerOptions.ShowPythonViewNodes = ShowPythonViewNodes;
            }
        }
    }
}
