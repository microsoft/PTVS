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
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_builtins;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyFrameObject>> frame_obj;
            public StructField<PointerProxy<PyInterpreterFrame>> previous;
            public StructField<PointerProxy<PyObject>> prev_instr; // Not sure PyObject is the right type here
            public StructField<Int32Proxy> stacktop;
            public StructField<BoolProxy> is_entry;
            public StructField<CharProxy> owner;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> localsplus;
        }

        private readonly Fields _fields;

        public PyInterpreterFrame(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
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

        public ArrayProxy<PointerProxy<PyObject>> f_localsplus {
            get { return GetFieldProxy(_fields.localsplus); }
        }

        public PointerProxy<PyFrameObject> frame_obj {
            get { return GetFieldProxy(_fields.frame_obj); }
        }

    }
}
