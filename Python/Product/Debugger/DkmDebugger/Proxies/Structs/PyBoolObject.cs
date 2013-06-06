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
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
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

        public override string ToString() {
            return ToBoolean() ? "True" : "False";
        }

        protected override string Repr(Func<PyObject, string> repr) {
            return ToString();
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

        public override string ToString() {
            return ToBoolean() ? "True" : "False";
        }

        protected override string Repr(Func<PyObject, string> repr) {
            return ToString();
        }
    }
}
