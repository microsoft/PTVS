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
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    struct ArgumentSet {
        public readonly IAnalysisSet[] Args;

        public ArgumentSet(IAnalysisSet[] args) {
            this.Args = args;
        }

        public IAnalysisSet SequenceArgs {
            get {
                return Args[Args.Length - 2];
            }
        }

        public IAnalysisSet DictArgs {
            get {
                return Args[Args.Length - 1];
            }
        }
        public int Count {
            get {
                return Args.Length - 2;
            }
        }

        public int CombinationCount {
            get {
                return Args.Take(Count).Where(y => y.Count >= 2).Aggregate(1, (x, y) => x * y.Count);
            }
        }

        public override string ToString() {
            return string.Join(", ", Args.Take(Count).Select(a => a.ToString())) +
                (SequenceArgs.Any() ? ", *" + SequenceArgs.ToString() : "") +
                (DictArgs.Any() ? ", **" + DictArgs.ToString() : "");
        }

        public static bool AreCompatible(ArgumentSet x, ArgumentSet y) {
            return x.Args.Length == y.Args.Length;
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

            int argCount = node.ParametersInternal.Length;
            var newArgs = new IAnalysisSet[argCount + 2];
            for (int i = 0; i < node.ParametersInternal.Length; ++i) {
                if (node.ParametersInternal[i].Kind == ParameterKind.List) {
                    listArgsIndex = i;
                } else if (node.ParametersInternal[i].Kind == ParameterKind.Dictionary) {
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
                        if (ns is StarArgsSequenceInfo sseq && i < node.ParametersInternal.Length && sseq._node == node.ParametersInternal[i]) {
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
                                if (k < node.ParametersInternal.Length && node.ParametersInternal[k].Kind == ParameterKind.Normal) {
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
                                if (node.ParametersInternal[j].Name.Equals(paramName, StringComparison.Ordinal)) {
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
                        if (node.ParametersInternal[j].Name.Equals(name, StringComparison.Ordinal)) {
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
            newArgs[argCount] = ReduceArgs(seqArgs, limits.ListArgumentTypes);
            newArgs[argCount + 1] = ReduceArgs(dictArgs, limits.DictArgumentTypes);

            return new ArgumentSet(newArgs);
        }

        private static IAnalysisSet ReduceArgs(IAnalysisSet args, int limit) {
            for (int j = 0; j <= UnionComparer.MAX_STRENGTH; ++j) {
                if (args.Count > limit) {
                    args = args.AsUnion(j);
                } else {
                    break;
                }
            }
            return args;
        }
    }
}
