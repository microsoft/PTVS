using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_import_state", MinVersion = PythonLanguageVersion.V312)]
    internal class ImportState : StructProxy {
        internal class Fields {
            public StructField<PointerProxy<PyDictObject>> modules;
        }

        private readonly Fields _fields;

        public ImportState(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyDictObject> modules {
            get { return GetFieldProxy(_fields.modules); }
        }
    }
}
