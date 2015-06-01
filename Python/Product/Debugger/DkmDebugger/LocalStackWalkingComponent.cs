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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;

namespace Microsoft.PythonTools.DkmDebugger {
    // In VS 2010, a component with level below 9996005 cannot invoke stack walking APIs, and a component above that level will have other functionality
    // not working properly. So, this component implements those operations that should logically be a part of LocalComponent, but which require a stack
    // walk - in particular, setting breakpoints for step-out.
    //
    // In addition, being the highest-level component, it also decides when Python runtime should be created by observing module/symbol load events.
    // This is because module loading notifications go in order from lowest-level to highest-level components, so once this component receives them,
    // it means that all other components have those modules in the module list of the process already, and will be able to locate them in response
    // to runtime load notification.
    public class LocalStackWalkingComponent :
        ComponentBase,
        IDkmModuleInstanceLoadNotification,
        IDkmRuntimeInstanceLoadNotification
    {

        public LocalStackWalkingComponent()
            : base(Guids.LocalStackWalkingComponentGuid) {
        }

        void IDkmModuleInstanceLoadNotification.OnModuleInstanceLoad(DkmModuleInstance moduleInstance, DkmWorkList workList, DkmEventDescriptorS eventDescriptor) {
            var process = moduleInstance.Process;

            var engines = process.DebugLaunchSettings.EngineFilter;
            if (engines == null || !engines.Contains(AD7Engine.DebugEngineGuid)) {
                return;
            }

            if (moduleInstance.RuntimeInstance == process.GetNativeRuntimeInstance()) {
                new LocalComponent.NativeModuleInstanceLoadedNotification { ModuleInstanceId = moduleInstance.UniqueId }.SendLower(process);
            }
        }

        [DataContract]
        [MessageTo(Guids.LocalStackWalkingComponentId)]
        internal class BeforeCreatePythonRuntimeNotification : MessageBase<BeforeCreatePythonRuntimeNotification> {
            [DataMember]
            public Guid PythonDllModuleInstanceId { get; set; }

            [DataMember]
            public Guid DebuggerHelperDllModuleInstanceId { get; set; }

            public override void Handle(DkmProcess process) {
                var pyrtInfo = process.GetPythonRuntimeInfo();
                var nativeModules = process.GetNativeRuntimeInstance().GetNativeModuleInstances();

                pyrtInfo.DLLs.Python = nativeModules.Single(mi => mi.UniqueId == PythonDllModuleInstanceId);

                if (DebuggerHelperDllModuleInstanceId != Guid.Empty) {
                    pyrtInfo.DLLs.DebuggerHelper = nativeModules.Single(mi => mi.UniqueId == DebuggerHelperDllModuleInstanceId);
                }
            }
        }

        unsafe void IDkmRuntimeInstanceLoadNotification.OnRuntimeInstanceLoad(DkmRuntimeInstance runtimeInstance, DkmEventDescriptor eventDescriptor) {
            if (runtimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid) {
                Debug.Fail("OnRuntimeInstanceLoad notification for a non-Python runtime.");
                throw new NotSupportedException();
            }

            var process = runtimeInstance.Process;
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, new TraceManagerLocalHelper(process, TraceManagerLocalHelper.Kind.StepOut));
        }

        [DataContract]
        [MessageTo(Guids.LocalStackWalkingComponentId)]
        internal class BeginStepOutNotification : MessageBase<BeginStepOutNotification> {
            [DataMember]
            public Guid ThreadId { get; set; }

            public override void Handle(DkmProcess process) {
                var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);

                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper == null) {
                    Debug.Fail("LocalStackWalkingComponent received a BeginStepOutNotification, but there's no TraceManagerLocalHelper to handle it.");
                    throw new InvalidOperationException();
                }

                traceHelper.OnBeginStepOut(thread);
            }
        }

        [DataContract]
        [MessageTo(Guids.LocalStackWalkingComponentId)]
        internal class StepCompleteNotification : MessageBase<StepCompleteNotification> {
            public override void Handle(DkmProcess process) {

                var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
                if (traceHelper == null) {
                    Debug.Fail("LocalStackWalkingComponent received a StepCompleteNotification, but there's no TraceManagerLocalHelper to handle it.");
                    throw new InvalidOperationException();
                }

                traceHelper.OnStepComplete();
            }
        }
    }
}
