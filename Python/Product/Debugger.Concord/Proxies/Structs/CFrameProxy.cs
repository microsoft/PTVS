using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    // This change was introduced here: https://github.com/python/cpython/commit/9e7b2076fb4380987ad0262c4c0ca900b06475ad#diff-c22186367cbe20233e843261998dc027ae5f1f8c0d2e778abfa454ae74cc59deR1613
    [StructProxy(StructName = "_cframe", MinVersion = PythonLanguageVersion.V310)]
    internal class CFrameProxy : StructProxy {
        internal class Fields {
            public StructField<Int32Proxy> use_tracing;
            public StructField<PointerProxy<CFrameProxy>> previous;
        }

        private readonly Fields _fields;

        public CFrameProxy(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public Int32Proxy use_tracing {
            get { return GetFieldProxy(_fields.use_tracing); }
        }
    }
}
