using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger;
using Microsoft.PythonTools.Common.Parsing;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_frame", MaxVersion = PythonLanguageVersion.V310)]
    [PyType(MaxVersion = PythonLanguageVersion.V310, VariableName = "PyFrame_Type")]
    internal class PyFrameObject310 : PyFrameObject {
        internal class Fields {
            public StructField<PointerProxy<PyFrameObject>> f_back;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<PointerProxy<PyObject>> f_trace;
            public StructField<Int32Proxy> f_lineno;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> f_localsplus;
        }

        private readonly Fields _fields;

        public override PointerProxy<PyFrameObject> f_back => GetFieldProxy(_fields.f_back);

        public override PointerProxy<PyCodeObject> f_code => GetFieldProxy(_fields.f_code);

        public override PointerProxy<PyDictObject> f_globals => GetFieldProxy(_fields.f_globals);

        public override PointerProxy<PyDictObject> f_locals => GetFieldProxy(_fields.f_locals);

        public override Int32Proxy f_lineno => GetFieldProxy(_fields.f_lineno);

        public override ArrayProxy<PointerProxy<PyObject>> f_localsplus => GetFieldProxy(_fields.f_localsplus);

        public PyFrameObject310(DkmProcess process, ulong address)
            : base(process, address) {
            var pythonInfo = process.GetPythonRuntimeInfo();
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject310>();
        }
    }
}
