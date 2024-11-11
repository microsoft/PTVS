using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_cframe", MinVersion = PythonLanguageVersion.V310, MaxVersion = PythonLanguageVersion.V310)]
    [StructProxy(StructName = "_PyCFrame", MinVersion = PythonLanguageVersion.V311)]
    internal class CFrameProxy : StructProxy {
        internal class Fields {
            [FieldProxy(MaxVersion = PythonLanguageVersion.V311)]
            public StructField<Int32Proxy> use_tracing;
            public StructField<PointerProxy<CFrameProxy>> previous;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<PointerProxy<PyInterpreterFrame>> current_frame;
        }

        private readonly Fields _fields;

        public CFrameProxy(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public Int32Proxy use_tracing {
            get { return GetFieldProxy(_fields.use_tracing); }
        }

        public PointerProxy<PyInterpreterFrame> current_frame => GetFieldProxy(_fields.current_frame);
    }
}
