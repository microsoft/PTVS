// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Debugger.Concord
{
	// TODO: remove VS 2010 workaround by merging LocalComponent and LocalStackWalkingComponent together.
	//
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
			: base(Guids.LocalStackWalkingComponentGuid)
		{
		}

		void IDkmModuleInstanceLoadNotification.OnModuleInstanceLoad(DkmModuleInstance moduleInstance, DkmWorkList workList, DkmEventDescriptorS eventDescriptor)
		{
			var process = moduleInstance.Process;

			var engines = process.DebugLaunchSettings.EngineFilter;
			if (engines == null || !engines.Contains(Guids.PythonDebugEngineGuid))
			{
				return;
			}

			if (moduleInstance.RuntimeInstance == process.GetNativeRuntimeInstance())
			{
				new LocalComponent.NativeModuleInstanceLoadedNotification { ModuleInstanceId = moduleInstance.UniqueId }.SendLower(process);
			}
		}

		[DataContract]
		[MessageTo(Guids.LocalStackWalkingComponentId)]
		internal class BeforeCreatePythonRuntimeNotification : MessageBase<BeforeCreatePythonRuntimeNotification>
		{
			[DataMember]
			public Guid PythonDllModuleInstanceId { get; set; }

			[DataMember]
			public Guid DebuggerHelperDllModuleInstanceId { get; set; }

			public override void Handle(DkmProcess process)
			{
				var pyrtInfo = process.GetPythonRuntimeInfo();
				var nativeModules = process.GetNativeRuntimeInstance().GetNativeModuleInstances();

				pyrtInfo.DLLs.Python = nativeModules.Single(mi => mi.UniqueId == PythonDllModuleInstanceId);

				if (DebuggerHelperDllModuleInstanceId != Guid.Empty)
				{
					pyrtInfo.DLLs.DebuggerHelper = nativeModules.Single(mi => mi.UniqueId == DebuggerHelperDllModuleInstanceId);
				}
			}
		}

		unsafe void IDkmRuntimeInstanceLoadNotification.OnRuntimeInstanceLoad(DkmRuntimeInstance runtimeInstance, DkmEventDescriptor eventDescriptor)
		{
			if (runtimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid)
			{
				Debug.Fail("OnRuntimeInstanceLoad notification for a non-Python runtime.");
				throw new NotSupportedException();
			}

			var process = runtimeInstance.Process;
			process.SetDataItem(DkmDataCreationDisposition.CreateNew, new TraceManagerLocalHelper(process, TraceManagerLocalHelper.Kind.StepOut));
		}

		[DataContract]
		[MessageTo(Guids.LocalStackWalkingComponentId)]
		internal class BeginStepOutNotification : MessageBase<BeginStepOutNotification>
		{
			[DataMember]
			public Guid ThreadId { get; set; }

			public override void Handle(DkmProcess process)
			{
				var thread = process.GetThreads().Single(t => t.UniqueId == ThreadId);

				var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
				if (traceHelper == null)
				{
					Debug.Fail("LocalStackWalkingComponent received a BeginStepOutNotification, but there's no TraceManagerLocalHelper to handle it.");
					throw new InvalidOperationException();
				}

				traceHelper.OnBeginStepOut(thread);
			}
		}

		[DataContract]
		[MessageTo(Guids.LocalStackWalkingComponentId)]
		internal class StepCompleteNotification : MessageBase<StepCompleteNotification>
		{
			public override void Handle(DkmProcess process)
			{

				var traceHelper = process.GetDataItem<TraceManagerLocalHelper>();
				if (traceHelper == null)
				{
					Debug.Fail("LocalStackWalkingComponent received a StepCompleteNotification, but there's no TraceManagerLocalHelper to handle it.");
					throw new InvalidOperationException();
				}

				traceHelper.OnStepComplete();
			}
		}
	}
}
