using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Project {
    class VersionCookie : IAnalysisCookie {
        public readonly int Version;

        public VersionCookie(int version) {
            Version = version;
        }
    }
}
