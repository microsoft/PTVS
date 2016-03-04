using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    public sealed class CompletionResult {
        private string _completion;
        private PythonMemberType _memberType;
        private readonly string _name, _doc;
        private readonly AP.CompletionValue[] _values;

        internal CompletionResult(string name, PythonMemberType memberType) {
            _name = name;
            _memberType = memberType;
        }

        internal CompletionResult(string name, string completion, string doc, PythonMemberType memberType, AP.CompletionValue[] values) {
            _name = name;
            _memberType = memberType;
            _completion = completion;
            _doc = doc;
            _values = values;
        }

        public string Completion => _completion;
        public string Documentation => _doc;
        public PythonMemberType MemberType => _memberType;
        public string Name => _name;

        internal AP.CompletionValue[] Values => _values;
    }
}
