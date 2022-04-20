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

using System.Diagnostics;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal interface IPyBoolObject : IPyObject {
        bool ToBoolean();
    }

    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyIntObject")]
    [PyType(MaxVersion = PythonLanguageVersion.V27, VariableName = "PyBool_Type")]
    internal class PyBoolObject27 : PyIntObject, IPyBoolObject {
        private class ValuesHolder : DkmDataItem {
            public readonly PyBoolObject27 False, True;

            public ValuesHolder(DkmProcess process) {
                var pythonDll = process.GetPythonRuntimeInfo().DLLs.Python;

                False = pythonDll.GetStaticVariable<PyBoolObject27>("_Py_ZeroStruct");
                True = pythonDll.GetStaticVariable<PyBoolObject27>("_Py_TrueStruct");
            }
        }

        public PyBoolObject27(DkmProcess process, ulong address)
            : base(process, address, checkType: false) {
            CheckPyType<PyBoolObject27>();
        }

        public static PyBoolObject27 Create(DkmProcess process, bool value) {
            var values = process.GetOrCreateDataItem(() => new ValuesHolder(process));
            return value ? values.True : values.False;
        }

        public bool ToBoolean() {
            return ToInt32() != 0;
        }

        public override void Repr(ReprBuilder builder) {
            builder.Append(ToBoolean() ? "True" : "False");
        }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V33, StructName = "PyLongObject")]
    [PyType(MinVersion = PythonLanguageVersion.V33, VariableName = "PyBool_Type")]
    internal class PyBoolObject33 : PyLongObject, IPyBoolObject {
        private class ValuesHolder : DkmDataItem {
            public readonly PyBoolObject33 False, True;

            public ValuesHolder(DkmProcess process) {
                var pythonDll = process.GetPythonRuntimeInfo().DLLs.Python;

                False = pythonDll.GetStaticVariable<PyBoolObject33>("_Py_FalseStruct");
                True = pythonDll.GetStaticVariable<PyBoolObject33>("_Py_TrueStruct");
            }
        }

        public PyBoolObject33(DkmProcess process, ulong address)
            : base(process, address, checkType: false) {
            CheckPyType<PyBoolObject33>();
        }

        public static PyBoolObject33 Create(DkmProcess process, bool value) {
            var values = process.GetOrCreateDataItem(() => new ValuesHolder(process));
            return value ? values.True : values.False;
        }

        public bool ToBoolean() {
            var values = Process.GetOrCreateDataItem(() => new ValuesHolder(Process));
            if (this == values.False) {
                return false;
            } else if (this == values.True) {
                return true;
            } else {
                return ToBigInteger() != 0;
            }
        }

        public override void Repr(ReprBuilder builder) {
            builder.Append(ToBoolean() ? "True" : "False");
        }
    }
}
