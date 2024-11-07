using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using static Microsoft.PythonTools.Debugger.Concord.Proxies.Structs.PyFrameObject310;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_frame", MinVersion = PythonLanguageVersion.V311)]
    [PyType(MinVersion = PythonLanguageVersion.V311, VariableName = "PyFrame_Type")]
    internal class PyFrameObject311 : PyFrameObject {
        internal class Fields {
            public StructField<PointerProxy<PyFrameObject>> f_back;
            public StructField<PointerProxy<PyInterpreterFrame>> f_frame;
            public StructField<PointerProxy<PyObject>> f_trace;
            public StructField<Int32Proxy> f_lineno;
            public StructField<CharProxy> f_trace_lines;
            public StructField<CharProxy> f_trace_opcodes;
            public StructField<CharProxy> f_fast_as_locals;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> _f_frame_data;
        }

        private readonly Fields _fields;

        public PyFrameObject311(DkmProcess process, ulong address)
            : base(process, address) {
            var pythonInfo = process.GetPythonRuntimeInfo();
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject311>();
        }

        private PointerProxy<PyInterpreterFrame> f_frame {
            get { return GetFieldProxy(_fields.f_frame); }
        }

        private PyInterpreterFrame GetFrame() {
            return f_frame.TryRead();
        }

        public override PointerProxy<PyFrameObject> f_back => GetFieldProxy(_fields.f_back);

        public override PointerProxy<PyCodeObject> f_code => GetFrame().f_code;

        public override PointerProxy<PyDictObject> f_globals => GetFrame().f_globals;

        public override PointerProxy<PyDictObject> f_locals => GetFrame().f_locals;

        public override Int32Proxy f_lineno => GetFieldProxy(_fields.f_lineno);

        public override ArrayProxy<PointerProxy<PyObject>> f_localsplus => GetFrame().f_localsplus;
    }
}
