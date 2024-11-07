using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "PyFunctionObject", MinVersion = PythonLanguageVersion.V310)]
    internal class PyFunctionObject : PyObject {
        internal class Fields {
            public StructField<PointerProxy<PyObject>> func_globals;
            public StructField<PointerProxy<PyObject>> func_builtins;
            public StructField<PointerProxy<PyObject>> func_name;
            public StructField<PointerProxy<PyObject>> func_qualname;
            public StructField<PointerProxy<PyObject>> func_code;
            public StructField<PointerProxy<PyObject>> func_defaults;
            public StructField<PointerProxy<PyObject>> func_kwdefaults;
            public StructField<PointerProxy<PyObject>> func_closure;
            public StructField<PointerProxy<PyObject>> func_doc;
            public StructField<PointerProxy<PyObject>> func_dict;
            public StructField<PointerProxy<PyObject>> func_weakreflist;
            public StructField<PointerProxy<PyObject>> func_module;
            public StructField<PointerProxy<PyObject>> func_annotations;
            public StructField<PointerProxy<UInt64Proxy>> vectorcall;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<UInt32Proxy> func_version;
        }

        private readonly Fields _fields;

        public PyFunctionObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyFunctionObject>();
        }
    }
}
