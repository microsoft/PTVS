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

using Microsoft.PythonTools.Debugger.Concord.Proxies;
using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;

namespace Microsoft.PythonTools.Debugger.Concord
{
	// This class implements functionality that is logically a part of TraceManager, but has to be implemented on LocalComponent
	// and LocalStackWalkingComponent side due to DKM API location restrictions.
	internal partial class TraceManagerLocalHelper : TraceManagerLocalHelperBase, DkmDataItem
	{
		// There's one of each - StepIn is owned by LocalComponent, StepOut is owned by LocalStackWalkingComponent.
		// See the comment on the latter for explanation on why this is necessary.
		public enum Kind { StepIn, StepOut }

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		public struct PyObject_FieldOffsets
		{
			public readonly long ob_type;

			public PyObject_FieldOffsets(DkmProcess process)
			{
				var fields = StructProxy.GetStructFields<PyObject, PyObject.PyObject_Fields>(process);
				ob_type = fields.ob_type.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		public struct PyVarObject_FieldOffsets
		{
			public readonly long ob_size;

			public PyVarObject_FieldOffsets(DkmProcess process)
			{
				var fields = StructProxy.GetStructFields<PyVarObject, PyVarObject.PyVarObject_Fields>(process);
				ob_size = fields.ob_size.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct PyCodeObject_FieldOffsets
		{
			public readonly long co_varnames, co_filename, co_name;

			public PyCodeObject_FieldOffsets(DkmProcess process)
			{
				var fields = StructProxy.GetStructFields<PyCodeObject, PyCodeObject.Fields>(process);
				co_varnames = fields.co_varnames.Offset;
				co_filename = fields.co_filename.Offset;
				co_name = fields.co_name.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct PyFrameObject_FieldOffsets
		{
			public readonly long f_back, f_code, f_globals, f_locals, f_lineno;

			public PyFrameObject_FieldOffsets(DkmProcess process)
			{
				if (process.GetPythonRuntimeInfo().LanguageVersion <= PythonLanguageVersion.V35)
				{
					var fields = StructProxy.GetStructFields<PyFrameObject, PyFrameObject.Fields_27_35>(process);
					f_back = -1;
					f_code = fields.f_code.Offset;
					f_globals = fields.f_globals.Offset;
					f_locals = fields.f_locals.Offset;
					f_lineno = fields.f_lineno.Offset;
				}
				else
				{
					var fields = StructProxy.GetStructFields<PyFrameObject, PyFrameObject.Fields_36>(process);
					f_back = fields.f_back.Offset;
					f_code = fields.f_code.Offset;
					f_globals = fields.f_globals.Offset;
					f_locals = fields.f_locals.Offset;
					f_lineno = fields.f_lineno.Offset;
				}
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct PyBytesObject_FieldOffsets
		{
			public readonly long ob_sval;

			public PyBytesObject_FieldOffsets(DkmProcess process)
			{
				var fields = StructProxy.GetStructFields<PyBytesObject, PyBytesObject.Fields>(process);
				ob_sval = fields.ob_sval.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct PyUnicodeObject27_FieldOffsets
		{
			public readonly long length, str;

			public PyUnicodeObject27_FieldOffsets(DkmProcess process)
			{
				var fields = StructProxy.GetStructFields<PyUnicodeObject27, PyUnicodeObject27.Fields>(process);
				length = fields.length.Offset;
				str = fields.str.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct PyUnicodeObject33_FieldOffsets
		{
			public readonly long sizeof_PyASCIIObject, sizeof_PyCompactUnicodeObject;
			public readonly long length, state, wstr, wstr_length, data;

			public PyUnicodeObject33_FieldOffsets(DkmProcess process)
			{
				sizeof_PyASCIIObject = StructProxy.SizeOf<PyASCIIObject>(process);
				sizeof_PyCompactUnicodeObject = StructProxy.SizeOf<PyUnicodeObject33>(process);

				var asciiFields = StructProxy.GetStructFields<PyASCIIObject, PyASCIIObject.Fields>(process);
				length = asciiFields.length.Offset;
				state = asciiFields.state.Offset;
				wstr = asciiFields.wstr.Offset;

				var compactFields = StructProxy.GetStructFields<PyCompactUnicodeObject, PyCompactUnicodeObject.Fields>(process);
				wstr_length = compactFields.wstr_length.Offset;

				var unicodeFields = StructProxy.GetStructFields<PyUnicodeObject33, PyUnicodeObject33.Fields>(process);
				data = unicodeFields.data.Offset;
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct FieldOffsets
		{
			public PyObject_FieldOffsets PyObject;
			public PyVarObject_FieldOffsets PyVarObject;
			public PyFrameObject_FieldOffsets PyFrameObject;
			public PyCodeObject_FieldOffsets PyCodeObject;
			public PyBytesObject_FieldOffsets PyBytesObject;
			public PyUnicodeObject27_FieldOffsets PyUnicodeObject27;
			public PyUnicodeObject33_FieldOffsets PyUnicodeObject33;

			public FieldOffsets(DkmProcess process, PythonRuntimeInfo pyrtInfo)
			{
				PyObject = new PyObject_FieldOffsets(process);
				PyVarObject = new PyVarObject_FieldOffsets(process);
				PyFrameObject = new PyFrameObject_FieldOffsets(process);
				PyCodeObject = new PyCodeObject_FieldOffsets(process);
				PyBytesObject = new PyBytesObject_FieldOffsets(process);

				if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27)
				{
					PyUnicodeObject27 = new PyUnicodeObject27_FieldOffsets(process);
					PyUnicodeObject33 = new PyUnicodeObject33_FieldOffsets();
				}
				else
				{
					PyUnicodeObject27 = new PyUnicodeObject27_FieldOffsets();
					PyUnicodeObject33 = new PyUnicodeObject33_FieldOffsets(process);
				}
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct Types
		{
			public ulong PyBytes_Type;
			public ulong PyUnicode_Type;

			public Types(DkmProcess process, PythonRuntimeInfo pyrtInfo)
			{
				PyBytes_Type = PyObject.GetPyType<PyBytesObject>(process).Address;

				if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27)
				{
					PyUnicode_Type = PyObject.GetPyType<PyUnicodeObject27>(process).Address;
				}
				else
				{
					PyUnicode_Type = PyObject.GetPyType<PyUnicodeObject33>(process).Address;
				}
			}
		}

		// Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
		[StructLayout(LayoutKind.Sequential, Pack = 8)]
		private struct FunctionPointers
		{
			public ulong Py_DecRef;
			public ulong PyFrame_FastToLocals;
			public ulong PyRun_StringFlags;
			public ulong PyErr_Fetch;
			public ulong PyErr_Restore;
			public ulong PyErr_Occurred;
			public ulong PyObject_Str;

			public FunctionPointers(DkmProcess process, PythonRuntimeInfo pyrtInfo)
			{
				Py_DecRef = pyrtInfo.DLLs.Python.GetFunctionAddress("Py_DecRef");
				PyFrame_FastToLocals = pyrtInfo.DLLs.Python.GetFunctionAddress("PyFrame_FastToLocals");
				PyRun_StringFlags = pyrtInfo.DLLs.Python.GetFunctionAddress("PyRun_StringFlags");
				PyErr_Fetch = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Fetch");
				PyErr_Restore = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Restore");
				PyErr_Occurred = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Occurred");
				PyObject_Str = pyrtInfo.DLLs.Python.GetFunctionAddress("PyObject_Str");
			}
		}

		// A step-in gate is a function inside the Python interpreter or one of the libaries that may call out
		// to native user code such that it may be a potential target of a step-in operation. For every gate,
		// we record its address in the process, and create a breakpoint. The breakpoints are initially disabled,
		// and only get enabled when a step-in operation is initiated - and then disabled again once it completes.
		private struct StepInGate
		{
			public DkmRuntimeInstructionBreakpoint Breakpoint;
			public StepInGateHandler Handler;
			public bool HasMultipleExitPoints; // see StepInGateAttribute
		}

		/// <summary>
		/// A handler for a step-in gate, run either when a breakpoint at the entry of the gate is hit, or
		/// when a step-in is executed while the gate is the topmost frame on the stack. The handler should
		/// compute any potential runtime exits and pass them to <see cref="OnPotentialRuntimeExit"/>.
		/// </summary>
		/// <param name="useRegisters">
		/// If true, the handler cannot rely on symbolic expression evaluation to compute the values of any
		/// parameters passed to the gate, and should instead retrieve them directly from the CPU registers.
		/// <remarks>
		/// This is currently only true on x64 when entry breakpoint is hit, because x64 uses registers for
		/// argument passing (x86 cdecl uses the stack), and function prolog does not necessarily copy
		/// values to the corresponding stack locations for them - so C++ expression evaluator will produce
		/// incorrect results for arguments, or fail to evaluate them altogether.
		/// </remarks>
		/// </param>
		public delegate void StepInGateHandler(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters);

		public unsafe TraceManagerLocalHelper(DkmProcess process, Kind kind)
		{
			_process = process;
			_pyrtInfo = process.GetPythonRuntimeInfo();

			_traceFunc = _pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("TraceFunc");
			_evalFrameFunc = _pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("EvalFrameFunc");
			_defaultEvalFrameFunc = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<PointerProxy>("DefaultEvalFrameFunc");
			_isTracing = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<ByteProxy>("isTracing");
			_pyTracingPossible = _pyrtInfo.GetRuntimeState()?.ceval.tracing_possible
				?? _pyrtInfo.DLLs.Python.GetStaticVariable<Int32Proxy>("_Py_TracingPossible");

			if (kind == Kind.StepIn)
			{
				var fieldOffsets = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<FieldOffsets>>("fieldOffsets");
				fieldOffsets.Write(new FieldOffsets(process, _pyrtInfo));

				var types = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<Types>>("types");
				types.Write(new Types(process, _pyrtInfo));

				var functionPointers = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<FunctionPointers>>("functionPointers");
				functionPointers.Write(new FunctionPointers(process, _pyrtInfo));

				var stringEquals = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<PointerProxy>("stringEquals");
				if (_pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27)
				{
					stringEquals.Write(_pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("StringEquals27").GetPointer());
				}
				else
				{
					stringEquals.Write(_pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("StringEquals33").GetPointer());
				}

				foreach (var interp in PyInterpreterState.GetInterpreterStates(process))
				{
					if (_pyrtInfo.LanguageVersion >= PythonLanguageVersion.V36)
					{
						RegisterJITTracing(interp);
					}
					foreach (var tstate in interp.GetThreadStates())
					{
						RegisterTracing(tstate);
					}
				}

				_handlers = new PythonDllBreakpointHandlers(this);
				LocalComponent.CreateRuntimeDllFunctionExitBreakpoints(_pyrtInfo.DLLs.Python, "new_threadstate", _handlers.new_threadstate, enable: true);
				LocalComponent.CreateRuntimeDllFunctionExitBreakpoints(_pyrtInfo.DLLs.Python, "PyInterpreterState_New", _handlers.PyInterpreterState_New, enable: true);

				foreach (var methodInfo in _handlers.GetType().GetMethods())
				{
					var stepInAttr = (StepInGateAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(StepInGateAttribute));
					if (stepInAttr != null &&
						(stepInAttr.MinVersion == PythonLanguageVersion.None || _pyrtInfo.LanguageVersion >= stepInAttr.MinVersion) &&
						(stepInAttr.MaxVersion == PythonLanguageVersion.None || _pyrtInfo.LanguageVersion <= stepInAttr.MaxVersion))
					{

						var handler = (StepInGateHandler)Delegate.CreateDelegate(typeof(StepInGateHandler), _handlers, methodInfo);
						AddStepInGate(handler, _pyrtInfo.DLLs.Python, methodInfo.Name, stepInAttr.HasMultipleExitPoints);
					}
				}

				if (_pyrtInfo.DLLs.CTypes != null)
				{
					OnCTypesLoaded(_pyrtInfo.DLLs.CTypes);
				}
			}
		}

		// Indicates that the breakpoint handler is for a Python-to-native step-in gate.
		[AttributeUsage(AttributeTargets.Method)]
		private class StepInGateAttribute : Attribute
		{
			public PythonLanguageVersion MinVersion { get; set; }
			public PythonLanguageVersion MaxVersion { get; set; }

			/// <summary>
			/// If true, this step-in gate function has more than one runtime exit point that can be executed in
			/// a single pass through the body of the function. For example, creating an instance of an object is
			/// a single gate that invokes both tp_new and tp_init sequentially.
			/// </summary>
			public bool HasMultipleExitPoints { get; set; }
		}
	}
}
