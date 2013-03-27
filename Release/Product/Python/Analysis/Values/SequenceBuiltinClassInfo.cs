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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized ClassInfo for sequence types.
    /// </summary>
    abstract class SequenceBuiltinClassInfo : BuiltinClassInfo {
        protected readonly INamespaceSet _indexTypes;

        public SequenceBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
            var seqType = classObj as IPythonSequenceType;
            if (seqType != null && seqType.IndexTypes != null) {
                _indexTypes = projectState.GetNamespacesFromObjects(seqType.IndexTypes).GetInstanceType();
            } else {
                _indexTypes = NamespaceSet.Empty;
            }
        }

        internal INamespaceSet IndexTypes {
            get { return _indexTypes; }
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                var res = unit.Scope.GetOrMakeNodeValue(
                    node,
                    (node_) => MakeFromIndexes(node_, unit.ProjectEntry)
                ) as SequenceInfo;

                List<INamespaceSet> seqTypes = new List<INamespaceSet>();
                foreach (var type in args[0]) {
                    SequenceInfo seqInfo = type as SequenceInfo;
                    if (seqInfo != null) {
                        for (int i = 0; i < seqInfo.IndexTypes.Length; i++) {
                            if (seqTypes.Count == i) {
                                seqTypes.Add(seqInfo.IndexTypes[i].Types);
                            } else {
                                seqTypes[i] = seqTypes[i].Union(seqInfo.IndexTypes[i].Types);
                            }
                        }
                    } else {
                        var defaultIndexType = type.GetIndex(node, unit, ProjectState.GetConstant(0));
                        if (seqTypes.Count == 0) {
                            seqTypes.Add(defaultIndexType);
                        } else {
                            seqTypes[0] = seqTypes[0].Union(defaultIndexType);
                        }
                    }
                }

                res.AddTypes(unit, seqTypes.ToArray());

                return res;
            }

            return base.Call(node, unit, args, keywordArgNames);
        }

        private static string GetInstanceShortDescription(Namespace ns) {
            var bci = ns as BuiltinClassInfo;
            if (bci != null) {
                return bci.Instance.ShortDescription;
            }
            return ns.ShortDescription;
        }

        protected string MakeDescription(string typeName) {
            if (_indexTypes == null || _indexTypes.Count == 0) {
                return typeName;
            } else if (_indexTypes.Count == 1) {
                return typeName + " of " + GetInstanceShortDescription(_indexTypes.First());
            } else if (_indexTypes.Count < 4) {
                return typeName + " of {" + string.Join(", ", _indexTypes.Select(GetInstanceShortDescription)) + "}";
            } else {
                return typeName + " of multiple types";
            }
        }

        public override string ShortDescription {
            get {
                return MakeDescription(_type.Name);
            }
        }

        public abstract SequenceInfo MakeFromIndexes(Node node, ProjectEntry entry);
    }
}
