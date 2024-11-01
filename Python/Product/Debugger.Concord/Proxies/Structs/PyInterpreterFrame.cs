using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    // This was added in commit https://github.com/python/cpython/commit/ae0a2b756255629140efcbe57fc2e714f0267aa3
    [StructProxy(StructName = "_PyInterpreterFrame", MinVersion = PythonLanguageVersion.V311)]
    internal class PyInterpreterFrame : StructProxy {
        internal class Fields {
            public StructField<PointerProxy<PyFunctionObject>> f_func;
            public StructField<PointerProxy<PyObject>> f_globals;
            public StructField<PointerProxy<PyObject>> f_builtins;
            public StructField<PointerProxy<PyObject>> f_locals;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyFrameObject>> frame_obj;
            public StructField<PointerProxy<PyInterpreterFrame>> previous;
            public StructField<PointerProxy<PyObject>> prev_instr; // Not sure PyObject is the right type here
            public StructField<Int32Proxy> stacktop;
            public StructField<BoolProxy> is_entry;
            public StructField<CharProxy> owner;
            public StructField<PointerProxy<ArrayProxy<PyObject>>> localsplus;
        }

        private readonly Fields _fields;

        public PyInterpreterFrame(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }
    }
}
