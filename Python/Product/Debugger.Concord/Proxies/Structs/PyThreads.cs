using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "pythreads", MinVersion = PythonLanguageVersion.V311)]
    internal class PyThreads: StructProxy {
        internal class Fields {
            public StructField<UInt64Proxy> next_unique_id;
            public StructField<PointerProxy<PyThreadState>> head;
            public StructField<Int64Proxy> count;
            public StructField<UInt64Proxy> stacksize;
        }

        private readonly Fields _fields;

        public PyThreads(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyThreadState> head {
            get { return GetFieldProxy(_fields.head); }
        }
    }
}
