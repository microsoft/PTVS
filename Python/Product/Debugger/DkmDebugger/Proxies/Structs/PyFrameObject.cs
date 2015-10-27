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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using Microsoft.PythonTools.Debugger;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyFrameObject : PyObject {
        public class Fields {
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<Int32Proxy> f_lineno;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> f_localsplus;
        }

        private readonly Fields _fields;

        public PyFrameObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject>();
        }

        public static unsafe PyFrameObject TryCreate(DkmStackWalkFrame frame) {
            var process = frame.Process;
            var PyEval_EvalFrameEx = process.CreateNativeInstructionAddress(process.GetPythonRuntimeInfo().DLLs.Python.GetFunctionAddress("PyEval_EvalFrameEx"));

            if (frame.InstructionAddress == null) {
                return null;
            } 
            if (frame.RuntimeInstance.Id.RuntimeType != Guids.PythonRuntimeTypeGuid && !frame.InstructionAddress.IsInSameFunction(PyEval_EvalFrameEx)) {
                return null;
            }

            var cppLanguage = DkmLanguage.Create("C++", new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.CppLanguageGuid));
            var inspectionSession = DkmInspectionSession.Create(process, null);
            var inspectionContext = DkmInspectionContext.Create(inspectionSession, process.GetNativeRuntimeInstance(), frame.Thread, 0,
                DkmEvaluationFlags.TreatAsExpression | DkmEvaluationFlags.NoSideEffects, DkmFuncEvalFlags.None, 10, cppLanguage, null);

            CppExpressionEvaluator cppEval;
            try {
                cppEval = new CppExpressionEvaluator(inspectionContext, frame);
            } catch (ArgumentException) {
                Debug.Fail("Failed to create C++ expression evaluator while obtaining PyFrameObject from a native frame.");
                return null;
            }

            ulong framePtr;
            try {
                framePtr = cppEval.EvaluateUInt64("f");
            } catch (CppEvaluationException) {
                Debug.Fail("Failed to evaluate the 'f' parameter to PyEval_EvalFrameEx while obtaining PyFrameObject from a native frame.");
                return null;
            }

            return new PyFrameObject(frame.Process, framePtr);
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
    }
}
