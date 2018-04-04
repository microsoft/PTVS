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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    struct ArgumentSet {
        public readonly IAnalysisSet[] Args;
        public readonly IAnalysisSet SequenceArgs;
        public readonly IAnalysisSet DictArgs;
        public readonly IReadOnlyDictionary<PythonVariable, IAnalysisSet> Closure;

        public ArgumentSet(IAnalysisSet[] args, IAnalysisSet sequenceArgs, IAnalysisSet dictArgs, IReadOnlyDictionary<PythonVariable, IAnalysisSet> closure) {
            Args = args;
            SequenceArgs = sequenceArgs ?? AnalysisSet.Empty;
            DictArgs = dictArgs ?? AnalysisSet.Empty;
            Closure = closure;
        }

        public int Count => Args.Length;

        public override string ToString() {
            return string.Join(", ", Args.Take(Count).Select(a => a.ToString())) +
                (SequenceArgs.Any() ? ", *" + SequenceArgs.ToString() : "") +
                (DictArgs.Any() ? ", **" + DictArgs.ToString() : "");
        }

        public static ArgumentSet FromArgs(FunctionDefinition node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgs) {
            // TODO: Warn when a keyword argument is provided and it maps to
            // something which is also a positional argument:
            // def f(a, b, c):
            //    print a, b, c
            //
            // f(2, 3, a=42)

            int kwArgsOffset = args.Length - keywordArgs.Length;

            var seqArgs = AnalysisSet.EmptyUnion;
            var dictArgs = AnalysisSet.EmptyUnion;
            int listArgsIndex = -1;
            int dictArgsIndex = -1;

            int argCount = node.Parameters.Count;
            var newArgs = new IAnalysisSet[argCount];
            for (int i = 0; i < node.Parameters.Count; ++i) {
                if (node.Parameters[i].Kind == ParameterKind.List) {
                    listArgsIndex = i;
                } else if (node.Parameters[i].Kind == ParameterKind.Dictionary) {
                    dictArgsIndex = i;
                }
                newArgs[i] = AnalysisSet.Empty;
            }

            int lastPositionFilled = -1;

            for (int i = 0; i < kwArgsOffset; ++i) {
                if (i < argCount && (listArgsIndex < 0 || i < listArgsIndex)) {
                    newArgs[i] = newArgs[i].Union(args[i]);
                    lastPositionFilled = i;
                } else if (listArgsIndex >= 0) {
                    foreach (var ns in args[i]) {
                        var sseq = ns as StarArgsSequenceInfo;
                        if (sseq != null && i < node.Parameters.Count && sseq._node == node.Parameters[i]) {
                            seqArgs = seqArgs.Add(unit.State.ClassInfos[BuiltinTypeId.Tuple].Instance);
                        } else {
                            seqArgs = seqArgs.Add(ns);
                        }
                    }
                } else {
                    // TODO: Warn about extra parameters
                }
            }

            for (int i = kwArgsOffset; i < args.Length; ++i) {
                if (i - kwArgsOffset >= keywordArgs.Length || keywordArgs[i - kwArgsOffset] == null) {
                    continue;
                }

                var name = keywordArgs[i - kwArgsOffset].Name;
                if (name.Equals("*", StringComparison.Ordinal)) {
                    foreach (var ns in args[i]) {
                        SequenceInfo seq;
                        StarArgsSequenceInfo sseq;
                        if ((sseq = ns as StarArgsSequenceInfo) != null) {
                            seqArgs = seqArgs.Union(sseq.IndexTypes.SelectMany(def => def.TypesNoCopy));
                        } else if ((seq = ns as SequenceInfo) != null) {
                            for (int j = 0; j < seq.IndexTypes.Length; ++j) {
                                int k = lastPositionFilled + j + 1;
                                if (k < node.Parameters.Count && node.Parameters[k].Kind == ParameterKind.Normal) {
                                    newArgs[k] = newArgs[k].Union(seq.IndexTypes[j].TypesNoCopy);
                                } else if (listArgsIndex >= 0) {
                                    seqArgs = seqArgs.Union(seq.IndexTypes[j].TypesNoCopy);
                                } else {
                                    // TODO: Warn about extra parameters
                                }
                            }
                        } else if (listArgsIndex >= 0) {
                            newArgs[listArgsIndex] = newArgs[listArgsIndex].Add(ns);
                        }
                    }
                } else if (name.Equals("**", StringComparison.Ordinal)) {
                    foreach (var dict in args[i].OfType<DictionaryInfo>()) {
                        foreach (var kv in dict._keysAndValues.KeyValueTypes) {
                            var paramName = kv.Key.GetConstantValueAsString();
                            if (string.IsNullOrEmpty(paramName)) {
                                continue;
                            }

                            for (int j = 0; j < argCount; ++j) {
                                if (node.Parameters[j].Name.Equals(paramName, StringComparison.Ordinal)) {
                                    newArgs[j] = newArgs[j].Union(kv.Value);
                                    break;
                                }
                            }
                        }
                    }

                    if (dictArgsIndex >= 0) {
                        foreach (var ns in args[i]) {
                            var sdict = ns as StarArgsDictionaryInfo;
                            if (sdict != null) {
                                dictArgs = dictArgs.Union(sdict._keysAndValues.AllValueTypes);
                            } else {
                                newArgs[dictArgsIndex] = newArgs[dictArgsIndex].Add(ns);
                            }
                        }
                    }
                } else {
                    bool foundParam = false;
                    for (int j = 0; j < argCount; ++j) {
                        if (node.Parameters[j].Name.Equals(name, StringComparison.Ordinal)) {
                            newArgs[j] = newArgs[j].Union(args[i]);
                            foundParam = true;
                            break;
                        }
                    }
                    if (!foundParam && dictArgsIndex >= 0) {
                        dictArgs = dictArgs.Union(args[i]);
                    }
                }
            }

            var limits = unit.State.Limits;
            for (int i = 0; i < argCount; ++i) {
                newArgs[i] = ReduceArgs(newArgs[i], limits.NormalArgumentTypes);
            }

            var set = new ArgumentSet(
                newArgs,
                ReduceArgs(seqArgs, limits.ListArgumentTypes),
                ReduceArgs(dictArgs, limits.DictArgumentTypes),
                null
            );

            return set;
        }

        private static IAnalysisSet ReduceArgs(IAnalysisSet args, int limit) {
            while (args.Count > limit) {
                var newArgs = args.AsStrongerUnion();
                if (ReferenceEquals(newArgs, args)) {
                    return args;
                }
                args = newArgs;
            }
            return args;
        }
    }
}
