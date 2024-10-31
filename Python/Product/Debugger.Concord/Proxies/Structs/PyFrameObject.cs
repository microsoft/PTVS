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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Common.Core.OS;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;
using static Microsoft.VisualStudio.Threading.SingleThreadedSynchronizationContext;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "_frame")]
    internal class PyFrameObject : PyVarObject {
        internal class Fields {
            public StructField<PointerProxy<PyFrameObject>> f_back;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<Int32Proxy> f_lineno;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> f_localsplus;
        }

        private readonly Fields _fields;

        public PyFrameObject(DkmProcess process, ulong address)
            : base(process, address) {
            var pythonInfo = process.GetPythonRuntimeInfo();
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject>();
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
                return new PyFrameObject(frame.Process, framePtrAddress);
            }
            return null;
        }

        public PointerProxy<PyFrameObject> f_back {
            get { return GetFieldProxy(_fields.f_back); }
        }

        public PointerProxy<PyCodeObject> f_code {
            get { return GetFieldProxy(_fields.f_code); }
        }

        public PointerProxy<PyDictObject> f_globals {
            get { return GetFieldProxy(_fields.f_globals); }
        }

        public PointerProxy<PyDictObject> f_locals {
            get { return GetFieldProxy(_fields.f_locals); }
        }

        public Int32Proxy f_lineno {
            get { return GetFieldProxy(_fields.f_lineno); }
        }

        public ArrayProxy<PointerProxy<PyObject>> f_localsplus {
            get { return GetFieldProxy(_fields.f_localsplus); }
        }

        private static ulong GetFramePtrAddress(DkmStackWalkFrame frame, int? previousFrameCount) {
            // Frame address may already be stored in the frame, check the data.
            if (frame.Data != null && frame.Data.GetDataItem<StackFrameDataItem>() != null) {
                return frame.Data.GetDataItem<StackFrameDataItem>().FramePointerAddress;
            } else {
                // Otherwise we can use the thread state to get the frame pointer.
                var process = frame.Process;
                var tid = frame.Thread.SystemPart.Id;
                PyThreadState tstate = PyThreadState.GetThreadStates(process).FirstOrDefault(ts => ts.thread_id.Read() == tid);
                PyFrameObject pyFrame = tstate.frame.Read();
                if (pyFrame != null) {
                    // This pyFrame should be the topmost frame. We need to go down the callstack
                    // based on the number of previous frames that were already found.
                    var numberBack = previousFrameCount != null ? previousFrameCount.Value : 0;
                    while (numberBack > 0) {
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
