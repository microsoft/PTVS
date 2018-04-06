// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class PartialFunctionInfo : AnalysisValue {
        private readonly IAnalysisSet _function;
        private readonly IAnalysisSet[] _args;
        private readonly NameExpression[] _keywordArgNames;
        private readonly IPythonProjectEntry _declProjEntry;
        private readonly int _declVersion;
        private IAnalysisSet _argsTuple;
        private IAnalysisSet _keywordsDict;

        public PartialFunctionInfo(ProjectEntry declProjEntry, IAnalysisSet function, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            _declProjEntry = declProjEntry;
            _declVersion = _declProjEntry.AnalysisVersion;
            _function = function;
            _args = args;
            _keywordArgNames = keywordArgNames;
        }

        public override IPythonProjectEntry DeclaringModule => _declProjEntry;
        public override int DeclaringVersion => _declVersion;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (Push()) {
                try {
                    var newArgs = _args.Take(_args.Length - _keywordArgNames.Length)
                        .Concat(args.Take(args.Length - keywordArgNames.Length))
                        .Concat(_args.Skip(_args.Length - _keywordArgNames.Length))
                        .Concat(args.Skip(args.Length - keywordArgNames.Length))
                        .ToArray();

                    var newKwArgs = _keywordArgNames.Concat(keywordArgNames).ToArray();

                    return _function.Call(node, unit, newArgs, newKwArgs);
                } finally {
                    Pop();
                }
            }
            return AnalysisSet.Empty;
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                int skipCount = _args.Length - _keywordArgNames.Length;
                var skipNames = new HashSet<string>(_keywordArgNames.Select(n => n.Name));

                foreach (var overload in _function.SelectMany(f => f.Overloads)) {
                    yield return overload.WithNewParameters(
                        overload.Parameters.Skip(skipCount).Where(p => !skipNames.Contains(p.Name)).ToArray()
                    );
                }
            }
        }

        public override string ShortDescription {
            get {
                return "partial";
            }
        }

        public override string Description {
            get {
                var sb = new StringBuilder();
                sb.Append(ShortDescription);
                sb.Append('(');

                if (_function.Count == 1) {
                    sb.Append(_function.First().ToString());
                } else {
                    sb.Append('{');
                    sb.Append(string.Join(", ", _function));
                    sb.Append('}');
                }

                for (int i = 0; i < _args.Length; ++i) {
                    sb.Append(", ");
                    int j = i - _args.Length + _keywordArgNames.Length;
                    if (j >= 0) {
                        sb.Append(_keywordArgNames[j].Name);
                        sb.Append('=');
                    }
                    var arg = _args[i];
                    if (arg.Count == 1) {
                        sb.Append(arg.First().ToString());
                    } else {
                        sb.Append('{');
                        sb.Append(string.Join(", ", arg));
                        sb.Append('}');
                    }
                }

                sb.Append(")");
                return sb.ToString();
            }
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = AnalysisSet.Empty;

            if (name == "func") {
                AddReference(node, unit);
                return _function;
            } else if (name == "args") {
                AddReference(node, unit);
                if (_argsTuple == null) {
                    _argsTuple = new SequenceInfo(_args.Take(_args.Length - _keywordArgNames.Length)
                        .Select(v => {
                            var vd = new VariableDef();
                            vd.AddTypes(unit, v, false, DeclaringModule);
                            return vd;
                        }).ToArray(),
                        unit.State.ClassInfos[BuiltinTypeId.Tuple],
                        node,
                        unit.ProjectEntry
                    );
                }
                return _argsTuple;
            } else if (name == "keywords") {
                AddReference(node, unit);
                if (_keywordsDict == null) {
                    var dict = new DictionaryInfo(unit.ProjectEntry, node);
                    _keywordsDict = dict;
                    for (int i = 0; i < _keywordArgNames.Length; ++i) {
                        int j = i + _args.Length - _keywordArgNames.Length;
                        if (j >= 0 && j < _args.Length) {
                            dict._keysAndValues.AddTypes(
                                unit,
                                unit.State.GetConstant(_keywordArgNames[i].Name),
                                _args[j],
                                false
                            );
                        }
                    }
                }
                return _keywordsDict;
            }
            return res;
        }
    }
}
