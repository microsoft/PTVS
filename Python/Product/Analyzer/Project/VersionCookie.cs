using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Project {
    sealed class VersionCookie : IAnalysisCookie {
        public readonly Dictionary<int, VersionInfo> ParsedVersions;

        public VersionCookie(Dictionary<int, VersionInfo> versions) {
            ParsedVersions = versions;
        }
    }

    sealed class VersionInfo {
        public readonly int Version;
        public readonly PythonAst Ast;

        public VersionInfo(int version, PythonAst ast) {
            Version = version;
            Ast = ast;
        }
    }
}
