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
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    // This class implements functionality that is logically a part of TraceManager, but has to be implemented on LocalComponent
    // and LocalStackWalkingComponent side due to DKM API location restrictions.
    internal class TraceManagerLocalHelper : DkmDataItem {
        // There's one of each - StepIn is owned by LocalComponent, StepOut is owned by LocalStackWalkingComponent.
        // See the comment on the latter for explanation on why this is necessary.
        public enum Kind { StepIn, StepOut }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PyObject_FieldOffsets {
            public readonly long ob_type;

            public PyObject_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyObject, PyObject.PyObject_Fields>(process);
                ob_type = fields.ob_type.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct PyVarObject_FieldOffsets {
            public readonly long ob_size;

            public PyVarObject_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyVarObject, PyVarObject.PyVarObject_Fields>(process);
                ob_size = fields.ob_size.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyCodeObject_FieldOffsets {
            public readonly long co_varnames, co_filename, co_name;

            public PyCodeObject_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyCodeObject, PyCodeObject.Fields>(process);
                co_varnames = fields.co_varnames.Offset;
                co_filename = fields.co_filename.Offset;
                co_name = fields.co_name.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyFrameObject_FieldOffsets {
            public readonly long f_code, f_globals, f_locals, f_lineno;

            public PyFrameObject_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyFrameObject, PyFrameObject.Fields>(process);
                f_code = fields.f_code.Offset;
                f_globals = fields.f_globals.Offset;
                f_locals = fields.f_locals.Offset;
                f_lineno = fields.f_lineno.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyBytesObject_FieldOffsets {
            public readonly long ob_sval;

            public PyBytesObject_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyBytesObject, PyBytesObject.Fields>(process);
                ob_sval = fields.ob_sval.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyUnicodeObject27_FieldOffsets {
            public readonly long length, str;

            public PyUnicodeObject27_FieldOffsets(DkmProcess process) {
                var fields = StructProxy.GetStructFields<PyUnicodeObject27, PyUnicodeObject27.Fields>(process);
                length = fields.length.Offset;
                str = fields.str.Offset;
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct PyUnicodeObject33_FieldOffsets {
            public readonly long sizeof_PyASCIIObject, sizeof_PyCompactUnicodeObject;
            public readonly long length, state, wstr, wstr_length, data;

            public PyUnicodeObject33_FieldOffsets(DkmProcess process) {
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
        private struct FieldOffsets {
            public PyObject_FieldOffsets PyObject;
            public PyVarObject_FieldOffsets PyVarObject;
            public PyFrameObject_FieldOffsets PyFrameObject;
            public PyCodeObject_FieldOffsets PyCodeObject;
            public PyBytesObject_FieldOffsets PyBytesObject;
            public PyUnicodeObject27_FieldOffsets PyUnicodeObject27;
            public PyUnicodeObject33_FieldOffsets PyUnicodeObject33;

            public FieldOffsets(DkmProcess process, PythonRuntimeInfo pyrtInfo) {
                PyObject = new PyObject_FieldOffsets(process);
                PyVarObject = new PyVarObject_FieldOffsets(process);
                PyFrameObject = new PyFrameObject_FieldOffsets(process);
                PyCodeObject = new PyCodeObject_FieldOffsets(process);
                PyBytesObject = new PyBytesObject_FieldOffsets(process);

                if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                    PyUnicodeObject27 = new PyUnicodeObject27_FieldOffsets(process);
                    PyUnicodeObject33 = new PyUnicodeObject33_FieldOffsets();
                } else {
                    PyUnicodeObject27 = new PyUnicodeObject27_FieldOffsets();
                    PyUnicodeObject33 = new PyUnicodeObject33_FieldOffsets(process);
                }
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct Types {
            public ulong PyBytes_Type;
            public ulong PyUnicode_Type;

            public Types(DkmProcess process, PythonRuntimeInfo pyrtInfo) {
                PyBytes_Type = PyObject.GetPyType<PyBytesObject>(process).Address;

                if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                    PyUnicode_Type = PyObject.GetPyType<PyUnicodeObject27>(process).Address;
                } else {
                    PyUnicode_Type = PyObject.GetPyType<PyUnicodeObject33>(process).Address;
                }
            }
        }

        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct FunctionPointers {
            public ulong Py_DecRef;
            public ulong PyFrame_FastToLocals;
            public ulong PyRun_StringFlags;
            public ulong PyErr_Fetch;
            public ulong PyErr_Restore;
            public ulong PyErr_Occurred;
            public ulong PyObject_Str;

            public FunctionPointers(DkmProcess process, PythonRuntimeInfo pyrtInfo) {
                Py_DecRef = pyrtInfo.DLLs.Python.GetFunctionAddress("Py_DecRef");
                PyFrame_FastToLocals = pyrtInfo.DLLs.Python.GetFunctionAddress("PyFrame_FastToLocals");
                PyRun_StringFlags = pyrtInfo.DLLs.Python.GetFunctionAddress("PyRun_StringFlags");
                PyErr_Fetch = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Fetch");
                PyErr_Restore = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Restore");
                PyErr_Occurred = pyrtInfo.DLLs.Python.GetFunctionAddress("PyErr_Occurred");
                PyObject_Str = pyrtInfo.DLLs.Python.GetFunctionAddress("PyObject_Str");
            }
        }

        private readonly DkmProcess _process;
        private readonly PythonRuntimeInfo _pyrtInfo;
        private readonly PythonDllBreakpointHandlers _handlers;
        private readonly DkmNativeInstructionAddress _traceFunc;
        private readonly UInt32Proxy _pyTracingPossible;

        // A step-in gate is a function inside the Python interpreter or one of the libaries that may call out
        // to native user code such that it may be a potential target of a step-in operation. For every gate,
        // we record its address in the process, and create a breakpoint. The breakpoints are initially disabled,
        // and only get enabled when a step-in operation is initiated - and then disabled again once it completes.
        private struct StepInGate {
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

        private readonly List<StepInGate> _stepInGates = new List<StepInGate>();

        // Breakpoints corresponding to the native functions outside of Python runtime that can potentially
        // be called by Python. These lists are dynamically filled for every new step operation, when one of 
        // the Python DLL breakpoints above is hit. They are cleared after that step operation completes.
        private readonly List<DkmRuntimeBreakpoint> _stepInTargetBreakpoints = new List<DkmRuntimeBreakpoint>();
        private readonly List<DkmRuntimeBreakpoint> _stepOutTargetBreakpoints = new List<DkmRuntimeBreakpoint>();

        public unsafe TraceManagerLocalHelper(DkmProcess process, Kind kind) {
            _process = process;
            _pyrtInfo = process.GetPythonRuntimeInfo();

            _traceFunc = _pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("TraceFunc");
            _pyTracingPossible = _pyrtInfo.DLLs.Python.GetStaticVariable<UInt32Proxy>("_Py_TracingPossible");

            if (kind == Kind.StepIn) {
                var fieldOffsets = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<FieldOffsets>>("fieldOffsets");
                fieldOffsets.Write(new FieldOffsets(process, _pyrtInfo));

                var types = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<Types>>("types");
                types.Write(new Types(process, _pyrtInfo));

                var functionPointers = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<CliStructProxy<FunctionPointers>>("functionPointers");
                functionPointers.Write(new FunctionPointers(process, _pyrtInfo));

                var stringEquals = _pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<PointerProxy>("stringEquals");
                if (_pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                    stringEquals.Write(_pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("StringEquals27").GetPointer());
                } else {
                    stringEquals.Write(_pyrtInfo.DLLs.DebuggerHelper.GetExportedFunctionAddress("StringEquals33").GetPointer());
                }

                foreach (var interp in PyInterpreterState.GetInterpreterStates(process)) {
                    foreach (var tstate in interp.GetThreadStates()) {
                        RegisterTracing(tstate);
                    }
                }

                _handlers = new PythonDllBreakpointHandlers(this);
                LocalComponent.CreateRuntimeDllFunctionExitBreakpoints(_pyrtInfo.DLLs.Python, "new_threadstate", _handlers.new_threadstate, enable: true);

                foreach (var methodInfo in _handlers.GetType().GetMethods()) {
                    var stepInAttr = (StepInGateAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(StepInGateAttribute));
                    if (stepInAttr != null &&
                        (stepInAttr.MinVersion == PythonLanguageVersion.None || _pyrtInfo.LanguageVersion >= stepInAttr.MinVersion) &&
                        (stepInAttr.MaxVersion == PythonLanguageVersion.None || _pyrtInfo.LanguageVersion <= stepInAttr.MaxVersion)) {

                        var handler = (StepInGateHandler)Delegate.CreateDelegate(typeof(StepInGateHandler), _handlers, methodInfo);
                        AddStepInGate(handler, _pyrtInfo.DLLs.Python, methodInfo.Name, stepInAttr.HasMultipleExitPoints);
                    }
                }

                if (_pyrtInfo.DLLs.CTypes != null) {
                    OnCTypesLoaded(_pyrtInfo.DLLs.CTypes);
                }
            }
        }

        private void AddStepInGate(StepInGateHandler handler, DkmNativeModuleInstance module, string funcName, bool hasMultipleExitPoints) {
            var gate = new StepInGate {
                Handler = handler,
                HasMultipleExitPoints = hasMultipleExitPoints,
                Breakpoint = LocalComponent.CreateRuntimeDllFunctionBreakpoint(module, funcName,
                    (thread, frameBase, vframe) => handler(thread, frameBase, vframe, useRegisters: thread.Process.Is64Bit()))
            };
            _stepInGates.Add(gate);
        }

        public void OnCTypesLoaded(DkmNativeModuleInstance moduleInstance) {
            AddStepInGate(_handlers._call_function_pointer, moduleInstance, "_call_function_pointer", hasMultipleExitPoints: false);
        }

        public unsafe void RegisterTracing(PyThreadState tstate) {
            tstate.use_tracing.Write(1);
            tstate.c_tracefunc.Write(_traceFunc.GetPointer());

            _pyTracingPossible.Write(_pyTracingPossible.Read() + 1);
        }

        public void OnBeginStepIn(DkmThread thread) {
            var frameInfo = new RemoteComponent.GetCurrentFrameInfoRequest { ThreadId = thread.UniqueId }.SendLower(thread.Process);

            var workList = DkmWorkList.Create(null);
            var topFrame = thread.GetTopStackFrame();
            var curAddr = (topFrame != null) ? topFrame.InstructionAddress as DkmNativeInstructionAddress : null;

            foreach (var gate in _stepInGates) {
                gate.Breakpoint.Enable();

                // A step-in may happen when we are stopped inside a step-in gate function. For example, when the gate function
                // calls out to user code more than once, and the user then steps out from the first call; we're now inside the
                // gate, but the runtime exit breakpoints for that gate have been cleared after the previous step-in completed. 
                // To correctly handle this scenario, we need to check whether we're inside a gate with multiple exit points, and
                // if so, call the associated gate handler (as it the entry breakpoint for the gate is hit) so that it re-enables
                // the runtime exit breakpoints for that gate.
                if (gate.HasMultipleExitPoints && curAddr != null) {
                    var addr = (DkmNativeInstructionAddress)gate.Breakpoint.InstructionAddress;
                    if (addr.IsInSameFunction(curAddr)) {
                        gate.Handler(thread, frameInfo.FrameBase, frameInfo.VFrame, useRegisters: false);
                    }
                }
            }
        }

        public void OnBeginStepOut(DkmThread thread) {
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
            for (int pyFrameCount = 0; pyFrameCount != 2; ) {
                DkmStackFrame[] frames = null;
                var workList = DkmWorkList.Create(null);
                stackContext.GetNextFrames(workList, 1, (result) => { frames = result.Frames; });
                workList.Execute();
                if (frames == null || frames.Length != 1) {
                    return;
                }
                frame = frames[0];

                var frameModuleInstance = frame.ModuleInstance;
                if (frameModuleInstance is DkmNativeModuleInstance &&
                    frameModuleInstance != _pyrtInfo.DLLs.Python &&
                    frameModuleInstance != _pyrtInfo.DLLs.DebuggerHelper &&
                    frameModuleInstance != _pyrtInfo.DLLs.CTypes) {
                    break;
                } else if (frame.RuntimeInstance != null && frame.RuntimeInstance.Id.RuntimeType == Guids.PythonRuntimeTypeGuid) {
                    ++pyFrameCount;
                }
            }

            var nativeAddr = frame.InstructionAddress as DkmNativeInstructionAddress;
            if (nativeAddr == null) {
                var customAddr = frame.InstructionAddress as DkmCustomInstructionAddress;
                if (customAddr == null) {
                    return;
                }

                var loc = new SourceLocation(customAddr.AdditionalData, thread.Process);
                nativeAddr = loc.NativeAddress;
                if (nativeAddr == null) {
                    return;
                }
            }

            var bp = DkmRuntimeInstructionBreakpoint.Create(Guids.PythonStepTargetSourceGuid, thread, nativeAddr, false, null);
            bp.Enable();

            _stepOutTargetBreakpoints.Add(bp);
        }

        public void OnStepComplete() {
            foreach (var gate in _stepInGates) {
                gate.Breakpoint.Disable();
            }

            foreach (var bp in _stepInTargetBreakpoints) {
                bp.Close();
            }
            _stepInTargetBreakpoints.Clear();

            foreach (var bp in _stepOutTargetBreakpoints) {
                bp.Close();
            }
            _stepOutTargetBreakpoints.Clear();
        }

        // Sets a breakpoint on a given function pointer, that represents some code outside of the Python DLL that can potentially
        // be invoked as a result of the current step-in operation (in which case it is the step-in target).
        private void OnPotentialRuntimeExit(DkmThread thread, ulong funcPtr) {
            if (funcPtr == 0) {
                return;
            }

            if (_pyrtInfo.DLLs.Python.ContainsAddress(funcPtr)) {
                return;
            } else if (_pyrtInfo.DLLs.DebuggerHelper != null && _pyrtInfo.DLLs.DebuggerHelper.ContainsAddress(funcPtr)) {
                return;
            } else if (_pyrtInfo.DLLs.CTypes != null && _pyrtInfo.DLLs.CTypes.ContainsAddress(funcPtr)) {
                return;
            }

            var bp = _process.CreateBreakpoint(Guids.PythonStepTargetSourceGuid, funcPtr);
            bp.Enable();

            _stepInTargetBreakpoints.Add(bp);
        }

        // Indicates that the breakpoint handler is for a Python-to-native step-in gate.
        [AttributeUsage(AttributeTargets.Method)]
        private class StepInGateAttribute : Attribute {
            public PythonLanguageVersion MinVersion { get; set; }
            public PythonLanguageVersion MaxVersion { get; set; }

            /// <summary>
            /// If true, this step-in gate function has more than one runtime exit point that can be executed in
            /// a single pass through the body of the function. For example, creating an instance of an object is
            /// a single gate that invokes both tp_new and tp_init sequentially.
            /// </summary>
            public bool HasMultipleExitPoints { get; set; }
        }

        private class PythonDllBreakpointHandlers {
            private readonly TraceManagerLocalHelper _owner;

            public PythonDllBreakpointHandlers(TraceManagerLocalHelper owner) {
                _owner = owner;
            }

            public void new_threadstate(DkmThread thread, ulong frameBase, ulong vframe) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                // Addressing this local by name does not work for release builds, so read the return value directly from the register instead.
                var tstate = new PyThreadState(thread.Process, cppEval.EvaluateReturnValueUInt64());
                if (tstate == null) {
                    return;
                }

                _owner.RegisterTracing(tstate);
            }

            // This step-in gate is not marked [StepInGate] because it doesn't live in pythonXX.dll, and so we register it manually.
            public void _call_function_pointer(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);
                ulong pProc = cppEval.EvaluateUInt64(useRegisters ? "@rdx" : "pProc");
                _owner.OnPotentialRuntimeExit(thread, pProc);
            }

            [StepInGate]
            public void call_function(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                int oparg = cppEval.EvaluateInt32(useRegisters ? "@rdx" : "oparg");

                int na = oparg & 0xff;
                int nk = (oparg >> 8) & 0xff;
                int n = na + 2 * nk;

                ulong func = cppEval.EvaluateUInt64(
                    "*((*(PyObject***){0}) - {1} - 1)",
                    useRegisters ? "@rcx" : "pp_stack",
                    n);
                var obj = PyObject.FromAddress(process, func);
                ulong ml_meth = cppEval.EvaluateUInt64(
                    "((PyObject*){0})->ob_type == &PyCFunction_Type ? ((PyCFunctionObject*){0})->m_ml->ml_meth : 0",
                    func);

                _owner.OnPotentialRuntimeExit(thread, ml_meth);
            }

            [StepInGate]
            public void PyCFunction_Call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                ulong ml_meth = cppEval.EvaluateUInt64(
                    "((PyObject*){0})->ob_type == &PyCFunction_Type ? ((PyCFunctionObject*){0})->m_ml->ml_meth : 0",
                    useRegisters ? "@rcx" : "func");
                _owner.OnPotentialRuntimeExit(thread, ml_meth);
            }

            [StepInGate]
            public void getset_get(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string descrVar = useRegisters ? "((PyGetSetDescrObject*)@rcx)" : "descr";

                ulong get = cppEval.EvaluateUInt64(descrVar + "->d_getset->get");
                _owner.OnPotentialRuntimeExit(thread, get);
            }

            [StepInGate]
            public void getset_set(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string descrVar = useRegisters ? "((PyGetSetDescrObject*)@rcx)" : "descr";

                ulong set = cppEval.EvaluateUInt64(descrVar + "->d_getset->set");
                _owner.OnPotentialRuntimeExit(thread, set);
            }

            [StepInGate(HasMultipleExitPoints = true)]
            public void type_call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string typeVar = useRegisters ? "((PyTypeObject*)@rcx)" : "type";

                ulong tp_new = cppEval.EvaluateUInt64(typeVar + "->tp_new");
                _owner.OnPotentialRuntimeExit(thread, tp_new);

                ulong tp_init = cppEval.EvaluateUInt64(typeVar + "->tp_init");
                _owner.OnPotentialRuntimeExit(thread, tp_init);
            }

            [StepInGate]
            public void PyType_GenericNew(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string typeVar = useRegisters ? "((PyTypeObject*)@rcx)" : "type";

                ulong tp_alloc = cppEval.EvaluateUInt64(typeVar + "->tp_alloc");
                _owner.OnPotentialRuntimeExit(thread, tp_alloc);
            }

            [StepInGate]
            public void PyObject_Print(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string opVar = useRegisters ? "((PyObject*)@rcx)" : "op";

                ulong tp_print = cppEval.EvaluateUInt64(opVar + "->ob_type->tp_print");
                _owner.OnPotentialRuntimeExit(thread, tp_print);
            }

            [StepInGate]
            public void PyObject_GetAttrString(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_getattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattr");
                _owner.OnPotentialRuntimeExit(thread, tp_getattr);
            }

            [StepInGate]
            public void PyObject_SetAttrString(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_setattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattr");
                _owner.OnPotentialRuntimeExit(thread, tp_setattr);
            }

            [StepInGate]
            public void PyObject_GetAttr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_getattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattr");
                _owner.OnPotentialRuntimeExit(thread, tp_getattr);

                ulong tp_getattro = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattro");
                _owner.OnPotentialRuntimeExit(thread, tp_getattro);
            }

            [StepInGate]
            public void PyObject_SetAttr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_setattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattr");
                _owner.OnPotentialRuntimeExit(thread, tp_setattr);

                ulong tp_setattro = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattro");
                _owner.OnPotentialRuntimeExit(thread, tp_setattro);
            }

            [StepInGate]
            public void PyObject_Repr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_repr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_repr");
                _owner.OnPotentialRuntimeExit(thread, tp_repr);
            }

            [StepInGate]
            public void PyObject_Hash(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_hash = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_hash");
                _owner.OnPotentialRuntimeExit(thread, tp_hash);
            }

            [StepInGate]
            public void PyObject_Call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string funcVar = useRegisters ? "((PyObject*)@rcx)" : "func";

                ulong tp_call = cppEval.EvaluateUInt64(funcVar + "->ob_type->tp_call");
                _owner.OnPotentialRuntimeExit(thread, tp_call);
            }

            [StepInGate]
            public void PyObject_Str(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

                ulong tp_str = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_str");
                _owner.OnPotentialRuntimeExit(thread, tp_str);
            }

            [StepInGate(MaxVersion = PythonLanguageVersion.V27, HasMultipleExitPoints = true)]
            public void do_cmp(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
                string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

                ulong tp_compare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_compare");
                _owner.OnPotentialRuntimeExit(thread, tp_compare1);

                ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

                ulong tp_compare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_compare");
                _owner.OnPotentialRuntimeExit(thread, tp_compare2);

                ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
            }

            [StepInGate(MaxVersion = PythonLanguageVersion.V27, HasMultipleExitPoints = true)]
            public void PyObject_RichCompare(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
                string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

                ulong tp_compare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_compare");
                _owner.OnPotentialRuntimeExit(thread, tp_compare1);

                ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

                ulong tp_compare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_compare");
                _owner.OnPotentialRuntimeExit(thread, tp_compare2);

                ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
            }

            [StepInGate(MinVersion = PythonLanguageVersion.V33, HasMultipleExitPoints = true)]
            public void do_richcompare(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
                string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

                ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

                ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
                _owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
            }

            [StepInGate]
            public void PyObject_GetIter(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string oVar = useRegisters ? "((PyObject*)@rcx)" : "o";

                ulong tp_iter = cppEval.EvaluateUInt64(oVar + "->ob_type->tp_iter");
                _owner.OnPotentialRuntimeExit(thread, tp_iter);
            }

            [StepInGate]
            public void PyIter_Next(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string iterVar = useRegisters ? "((PyObject*)@rcx)" : "iter";

                ulong tp_iternext = cppEval.EvaluateUInt64(iterVar + "->ob_type->tp_iternext");
                _owner.OnPotentialRuntimeExit(thread, tp_iternext);
            }

            [StepInGate]
            public void builtin_next(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters) {
                var process = thread.Process;
                var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

                string argsVar = useRegisters ? "((PyTupleObject*)@rdx)" : "((PyTupleObject*)args)";

                ulong tp_iternext = cppEval.EvaluateUInt64(argsVar + "->ob_item[0]->ob_type->tp_iternext");
                _owner.OnPotentialRuntimeExit(thread, tp_iternext);
            }
        }
    }
}
