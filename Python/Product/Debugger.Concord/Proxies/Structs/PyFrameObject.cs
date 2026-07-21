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

using System.Linq;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal abstract class PyFrameObject : PyVarObject {
        public PyFrameObject(DkmProcess process, ulong address)
            : base(process, address) {
        }


        private static bool IsInEvalFrame(DkmStackWalkFrame frame) {
            var process = frame.Process;
            var pythonInfo = process.GetPythonRuntimeInfo();
            var name = "PyEval_EvalFrameEx";
            ulong addr = 0;
            if (pythonInfo.LanguageVersion > PythonLanguageVersion.V35) {
                name = "_PyEval_EvalFrameDefault";
            }
            addr = pythonInfo.DLLs.Python.GetFunctionAddress(name);
            if (addr == 0) {
                return false;
            }

            var addressMatch = frame.InstructionAddress.IsInSameFunction(process.CreateNativeInstructionAddress(addr));
            var nameMatch = frame.BasicSymbolInfo.MethodName == name;
            return addressMatch || nameMatch;
        }

        public static unsafe PyFrameObject TryCreate(DkmStackWalkFrame frame, int? previousFrameCount) {
            var process = frame.Process;
            if (frame.InstructionAddress == null) {
                return null;
            }
            if (frame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid && !IsInEvalFrame(frame)) {
                return null;
            }

            var framePtrAddress = PyFrameObject.GetFramePtrAddress(frame, previousFrameCount);
            if (framePtrAddress != 0) {
                var pythonInfo = process.GetPythonRuntimeInfo();
                if (pythonInfo.LanguageVersion < PythonLanguageVersion.V311) {
                    return new PyFrameObject310(frame.Process, framePtrAddress);
                }
                return new PyFrameObject311(frame.Process, framePtrAddress);
            }
            return null;
        }

        public abstract PointerProxy<PyFrameObject> f_back { get; }

        public abstract PointerProxy<PyCodeObject> f_code { get; }

        public abstract PointerProxy<PyDictObject> f_globals { get; }

        public abstract PointerProxy<PyDictObject> f_locals { get; }

        public abstract Int32Proxy f_lineno { get; }

        public int ComputeLineNumber(DkmInspectionSession inspectionSession, DkmStackWalkFrame frame, DkmEvaluationFlags flags) {
            var setLineNumber = f_lineno.Read();
            if (setLineNumber != 0) {
                // The frame already has a valid (cached) line number, use it directly.
                return setLineNumber;
            }

            // Compute the line number entirely from debuggee memory: the frame's current
            // instruction offset decoded against the code object's line table. This mirrors
            // CPython's PyFrame_GetLineNumber (return f_lineno if set, else compute from the
            // interpreter frame) and avoids a func-eval per frame. The old approach evaluated
            // PyFrame_GetLineNumber in the debuggee for every Python frame every time execution
            // stopped, which is slow and can hang or crash the debugger - especially for GUI
            // apps that pump messages - on Python 3.11+ where f_lineno is 0 while executing.
            var computedLineNumber = TryComputeLineNumberFromInstruction();
            if (computedLineNumber > 0) {
                return computedLineNumber;
            }

            if (flags != DkmEvaluationFlags.None) {
                // Fallback only: ask the interpreter to compute the line number. This runs
                // code in the debuggee (func-eval), so only do it when stopped at a breakpoint
                // and evaluating the frame. Otherwise it will fail and cause stepping to think
                // the frame we eval is where we should stop.
                // https://github.com/python/cpython/blob/46710ca5f263936a2e36fa5d0f140cf9f50b2618/Objects/frameobject.c#L40-L41
                var evaluator = new CppExpressionEvaluator(inspectionSession, 10, frame, DkmEvaluationFlags.TreatAsExpression);
                var funcAddr = Process.GetPythonRuntimeInfo().DLLs.Python.GetFunctionAddress("PyFrame_GetLineNumber");
                var frameAddr = Address;
                try {
                    setLineNumber = evaluator.EvaluateInt32(string.Format("((int (*)(void *)){0})({1})", funcAddr, frameAddr), DkmEvaluationFlags.EnableExtendedSideEffects);
                } catch (CppEvaluationException) {
                    // This means we can't evaluate right now, just leave as zero
                    setLineNumber = 0;
                }
            }

            return setLineNumber;
        }

        /// <summary>
        /// Attempts to compute the current line number without executing any code in the
        /// debuggee. Returns 0 when unavailable (e.g. Python &lt; 3.11), in which case the
        /// caller falls back to evaluating PyFrame_GetLineNumber in the debuggee.
        /// </summary>
        protected virtual int TryComputeLineNumberFromInstruction() {
            return 0;
        }

        public abstract ArrayProxy<PointerProxy<PyObject>> f_localsplus { get; }

        private static ulong GetFramePtrAddress(DkmStackWalkFrame frame, int? previousFrameCount) {
            // Frame address may already be stored in the frame, check the data.
            if (frame.Data != null && frame.Data.GetDataItem<StackFrameDataItem>() != null) {
                return frame.Data.GetDataItem<StackFrameDataItem>().FramePointerAddress;
            } else {
                // Otherwise we can use the thread state to get the frame pointer.
                var process = frame.Process;
                var tid = frame.Thread.SystemPart.Id;
                PyThreadState tstate = PyThreadState.GetThreadStates(process).FirstOrDefault(ts => ts.thread_id.Read() == tid);
                PyFrameObject pyFrame = tstate.frame.TryRead();
                if (pyFrame != null) {
                    // This pyFrame should be the topmost frame. We need to go down the callstack
                    // based on the number of previous frames that were already found.
                    var numberBack = previousFrameCount != null ? previousFrameCount.Value : 0;
                    while (numberBack > 0 && pyFrame.f_back.Process != null) {
                        pyFrame = pyFrame.f_back.Read();
                        numberBack--;
                    }
                    return pyFrame.Address;
                }
            }

            return 0;
        }
    }
}
