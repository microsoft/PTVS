using Microsoft.PythonTools.Debugger.Concord;
using Microsoft.PythonTools.Debugger.Concord.Proxies;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;
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
	internal class TraceManagerLocalHelperBase
	{

		private readonly DkmProcess _process;
		private readonly PythonRuntimeInfo _pyrtInfo;
		private readonly PythonDllBreakpointHandlers _handlers;
		private readonly DkmNativeInstructionAddress _traceFunc;
		private readonly DkmNativeInstructionAddress _evalFrameFunc;
		private readonly PointerProxy _defaultEvalFrameFunc;
		private readonly Int32Proxy _pyTracingPossible;
		private readonly ByteProxy _isTracing;

		private readonly List<StepInGate> _stepInGates = new List<StepInGate>();

		// Breakpoints corresponding to the native functions outside of Python runtime that can potentially
		// be called by Python. These lists are dynamically filled for every new step operation, when one of 
		// the Python DLL breakpoints above is hit. They are cleared after that step operation completes.
		private readonly List<DkmRuntimeBreakpoint> _stepInTargetBreakpoints = new List<DkmRuntimeBreakpoint>();
		private readonly List<DkmRuntimeBreakpoint> _stepOutTargetBreakpoints = new List<DkmRuntimeBreakpoint>();

		public void OnBeginStepIn(DkmThread thread)
		{
			var frameInfo = new RemoteComponent.GetCurrentFrameInfoRequest { ThreadId = thread.UniqueId }.SendLower(thread.Process);

			var workList = DkmWorkList.Create(null);
			var topFrame = thread.GetTopStackFrame();
			var curAddr = (topFrame != null) ? topFrame.InstructionAddress as DkmNativeInstructionAddress : null;

			foreach (var gate in _stepInGates)
			{
				gate.Breakpoint.Enable();

				// A step-in may happen when we are stopped inside a step-in gate function. For example, when the gate function
				// calls out to user code more than once, and the user then steps out from the first call; we're now inside the
				// gate, but the runtime exit breakpoints for that gate have been cleared after the previous step-in completed. 
				// To correctly handle this scenario, we need to check whether we're inside a gate with multiple exit points, and
				// if so, call the associated gate handler (as it the entry breakpoint for the gate is hit) so that it re-enables
				// the runtime exit breakpoints for that gate.
				if (gate.HasMultipleExitPoints && curAddr != null)
				{
					var addr = (DkmNativeInstructionAddress)gate.Breakpoint.InstructionAddress;
					if (addr.IsInSameFunction(curAddr))
					{
						gate.Handler(thread, frameInfo.FrameBase, frameInfo.VFrame, useRegisters: false);
					}
				}
			}
		}

		public void OnBeginStepOut(DkmThread thread)
		{
			// When we're stepping out while in Python code, there are two possibilities. Either the stack looks like this:
			//
			//   PythonFrame1
			//   PythonFrame2
			//
			// or else it looks like this:
			//
			//   PythonFrame
			//   [Native to Python transition]
			//   NativeFrame
			//
			// In both cases, we use native breakpoints on the return address to catch the end of step-out operation.
			// For Python-to-native step-out, this is the only option. For Python-to-Python, it would seem that TraceFunc
			// can detect it via PyTrace_RETURN, but it doesn't actually know whether the return is to Python or to
			// native at the point where it's reported - and, in any case, we need to let PyEval_EvalFrameEx to return
			// before reporting the completion of that step-out (otherwise we will show the returning frame in call stack).

			// Find the destination for step-out by walking the call stack and finding either the first native frame
			// outside of Python and helper DLLs, or the second Python frame.
			var inspectionSession = DkmInspectionSession.Create(_process, null);
			var frameFormatOptions = new DkmFrameFormatOptions(DkmVariableInfoFlags.None, DkmFrameNameFormatOptions.None, DkmEvaluationFlags.None, 10000, 10);
			var stackContext = DkmStackContext.Create(inspectionSession, thread, DkmCallStackFilterOptions.None, frameFormatOptions, null, null);
			DkmStackFrame frame = null;
			for (int pyFrameCount = 0; pyFrameCount != 2;)
			{
				DkmStackFrame[] frames = null;
				var workList = DkmWorkList.Create(null);
				stackContext.GetNextFrames(workList, 1, (result) => { frames = result.Frames; });
				workList.Execute();
				if (frames == null || frames.Length != 1)
				{
					return;
				}
				frame = frames[0];

				var frameModuleInstance = frame.ModuleInstance;
				if (frameModuleInstance is DkmNativeModuleInstance &&
					frameModuleInstance != _pyrtInfo.DLLs.Python &&
					frameModuleInstance != _pyrtInfo.DLLs.DebuggerHelper &&
					frameModuleInstance != _pyrtInfo.DLLs.CTypes)
				{
					break;
				}
				else if (frame.RuntimeInstance != null && frame.RuntimeInstance.Id.RuntimeType == Guids.PythonRuntimeTypeGuid)
				{
					++pyFrameCount;
				}
			}

			var nativeAddr = frame.InstructionAddress as DkmNativeInstructionAddress;
			if (nativeAddr == null)
			{
				var customAddr = frame.InstructionAddress as DkmCustomInstructionAddress;
				if (customAddr == null)
				{
					return;
				}

				var loc = new SourceLocation(customAddr.AdditionalData, thread.Process);
				nativeAddr = loc.NativeAddress;
				if (nativeAddr == null)
				{
					return;
				}
			}

			var bp = DkmRuntimeInstructionBreakpoint.Create(Guids.PythonStepTargetSourceGuid, thread, nativeAddr, false, null);
			bp.Enable();

			_stepOutTargetBreakpoints.Add(bp);
		}

		public void OnCTypesLoaded(DkmNativeModuleInstance moduleInstance)
		{
			AddStepInGate(_handlers._call_function_pointer, moduleInstance, "_call_function_pointer", hasMultipleExitPoints: false);
		}

		public void OnStepComplete()
		{
			foreach (var gate in _stepInGates)
			{
				gate.Breakpoint.Disable();
			}

			foreach (var bp in _stepInTargetBreakpoints)
			{
				bp.Close();
			}
			_stepInTargetBreakpoints.Clear();

			foreach (var bp in _stepOutTargetBreakpoints)
			{
				bp.Close();
			}
			_stepOutTargetBreakpoints.Clear();
		}

		public unsafe void RegisterJITTracing(PyInterpreterState istate)
		{
			Debug.Assert(_pyrtInfo.LanguageVersion >= PythonLanguageVersion.V36);

			var current = istate.eval_frame.Read();
			if (current != _evalFrameFunc.GetPointer())
			{
				_defaultEvalFrameFunc.Write(current);
				istate.eval_frame.Write(_evalFrameFunc.GetPointer());
			}
		}

		public unsafe void RegisterTracing(PyThreadState tstate)
		{
			tstate.use_tracing.Write(1);
			tstate.c_tracefunc.Write(_traceFunc.GetPointer());
			_pyTracingPossible.Write(_pyTracingPossible.Read() + 1);
			_isTracing.Write(1);
		}

		private void AddStepInGate(StepInGateHandler handler, DkmNativeModuleInstance module, string funcName, bool hasMultipleExitPoints)
		{
			var gate = new StepInGate
			{
				Handler = handler,
				HasMultipleExitPoints = hasMultipleExitPoints,
				Breakpoint = LocalComponent.CreateRuntimeDllFunctionBreakpoint(module, funcName,
					(thread, frameBase, vframe, retAddr) => handler(thread, frameBase, vframe, useRegisters: thread.Process.Is64Bit()))
			};
			_stepInGates.Add(gate);
		}

		// Sets a breakpoint on a given function pointer, that represents some code outside of the Python DLL that can potentially
		// be invoked as a result of the current step-in operation (in which case it is the step-in target).
		private void OnPotentialRuntimeExit(DkmThread thread, ulong funcPtr)
		{
			if (funcPtr == 0)
			{
				return;
			}

			if (_pyrtInfo.DLLs.Python.ContainsAddress(funcPtr))
			{
				return;
			}
			else if (_pyrtInfo.DLLs.DebuggerHelper != null && _pyrtInfo.DLLs.DebuggerHelper.ContainsAddress(funcPtr))
			{
				return;
			}
			else if (_pyrtInfo.DLLs.CTypes != null && _pyrtInfo.DLLs.CTypes.ContainsAddress(funcPtr))
			{
				return;
			}

			var bp = _process.CreateBreakpoint(Guids.PythonStepTargetSourceGuid, funcPtr);
			bp.Enable();

			_stepInTargetBreakpoints.Add(bp);
		}
	}
}