using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "PyCodeObject", MaxVersion = PythonLanguageVersion.V310)]
    [PyType(MaxVersion = PythonLanguageVersion.V310, VariableName = "PyCode_Type")]
    internal class PyCodeObject310 : PyCodeObject {
        public class Fields {
            public StructField<Int32Proxy> co_nlocals;
            public StructField<PointerProxy<PyTupleObject>> co_names;
            public StructField<PointerProxy<PyTupleObject>> co_varnames;
            public StructField<PointerProxy<PyTupleObject>> co_freevars;
            public StructField<PointerProxy<PyTupleObject>> co_cellvars;
            public StructField<PointerProxy<IPyBaseStringObject>> co_filename;
            public StructField<PointerProxy<IPyBaseStringObject>> co_name;
            public StructField<Int32Proxy> co_firstlineno;
        }

        private readonly Fields _fields;

        public override Int32Proxy co_nlocals => GetFieldProxy(_fields.co_nlocals);

        public override PointerProxy<PyTupleObject> co_names => GetFieldProxy(_fields.co_names);

        public override IWritableDataProxy<PyTupleObject> co_varnames => GetFieldProxy(_fields.co_varnames);

        public override IWritableDataProxy<PyTupleObject> co_freevars => GetFieldProxy(_fields.co_freevars);

        public override IWritableDataProxy<PyTupleObject> co_cellvars => GetFieldProxy(_fields.co_cellvars);

        public override PointerProxy<IPyBaseStringObject> co_filename => GetFieldProxy(_fields.co_filename);

        public override PointerProxy<IPyBaseStringObject> co_name => GetFieldProxy(_fields.co_name);

        public override Int32Proxy co_firstlineno => GetFieldProxy(_fields.co_firstlineno);

        public PyCodeObject310(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyCodeObject310>();
        }

    }
}
