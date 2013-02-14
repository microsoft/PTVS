/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    struct ArgumentSet {
        public readonly INamespaceSet[] Args;

        public ArgumentSet(INamespaceSet[] args) {
            this.Args = args;
        }

        public INamespaceSet SequenceArgs {
            get {
                return Args[Args.Length - 2];
            }
        }

        public INamespaceSet DictArgs {
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

        public static ArgumentSet FromArgs(FunctionDefinition node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgs) {
            // TODO: Warn when a keyword argument is provided and it maps to
            // something which is also a positional argument:
            // def f(a, b, c):
            //    print a, b, c
            //
            // f(2, 3, a=42)

            int kwArgsOffset = args.Length - keywordArgs.Length;

            var seqArgs = NamespaceSet.EmptyUnion;
            var dictArgs = NamespaceSet.EmptyUnion;
            int listArgsIndex = -1;
            int dictArgsIndex = -1;

            int argCount = node.Parameters.Count;
            var newArgs = new INamespaceSet[argCount + 2];
            for (int i = 0; i < node.Parameters.Count; ++i) {
                if (node.Parameters[i].Kind == ParameterKind.List) {
                    listArgsIndex = i;
                } else if (node.Parameters[i].Kind == ParameterKind.Dictionary) {
                    dictArgsIndex = i;
                }
                newArgs[i] = NamespaceSet.Empty;
            }

            for (int i = 0; i < kwArgsOffset; ++i) {
                if (i < argCount && (listArgsIndex < 0 || i < listArgsIndex)) {
                    newArgs[i] = newArgs[i].Union(args[i]);
                } else if (listArgsIndex >= 0) {
                    foreach (var ns in args[i]) {
                        var sseq = ns as StarArgsSequenceInfo;
                        if (sseq != null && i < node.Parameters.Count && sseq._node == node.Parameters[i]) {
                            seqArgs = seqArgs.Add(unit.ProjectState._tupleType.Instance);
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
                    if (listArgsIndex >= 0) {
                        foreach (var ns in args[i]) {
                            var sseq = ns as StarArgsSequenceInfo;
                            if (sseq != null) {
                                seqArgs = seqArgs.Union(sseq.IndexTypes.SelectMany(def => def.TypesNoCopy));
                            } else {
                                newArgs[listArgsIndex] = newArgs[listArgsIndex].Add(ns);
                            }
                        }
                    }
                } else if (name.Equals("**", StringComparison.Ordinal)) {
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

            var limits = unit.ProjectState.Limits;
            for (int i = 0; i < argCount; ++i) {
                newArgs[i] = ReduceArgs(newArgs[i], limits.NormalArgumentTypes);
            }
            newArgs[argCount] = ReduceArgs(seqArgs, limits.ListArgumentTypes);
            newArgs[argCount + 1] = ReduceArgs(dictArgs, limits.DictArgumentTypes);

            return new ArgumentSet(newArgs);
        }

        private static INamespaceSet ReduceArgs(INamespaceSet args, int limit) {
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
