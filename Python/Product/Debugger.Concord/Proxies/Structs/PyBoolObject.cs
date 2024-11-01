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

    [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "PyLongObject")]
    [PyType(MinVersion = PythonLanguageVersion.V39, VariableName = "PyBool_Type")]
    internal class PyBoolObject : PyLongObject, IPyBoolObject {
        private class ValuesHolder : DkmDataItem {
            public readonly PyBoolObject False, True;

            public ValuesHolder(DkmProcess process) {
                var pythonDll = process.GetPythonRuntimeInfo().DLLs.Python;

                False = pythonDll.GetStaticVariable<PyBoolObject>("_Py_FalseStruct");
                True = pythonDll.GetStaticVariable<PyBoolObject>("_Py_TrueStruct");
            }
        }

        public PyBoolObject(DkmProcess process, ulong address)
            : base(process, address, checkType: false) {
            CheckPyType<PyBoolObject>();
        }

        public static PyBoolObject Create(DkmProcess process, bool value) {
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
