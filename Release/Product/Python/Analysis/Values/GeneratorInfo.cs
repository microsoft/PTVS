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

using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a generator instance - either constructed using a generator expression or
    /// by calling a function definition which contains yield expressions.
    /// </summary>
    internal class GeneratorInfo : BuiltinInstanceInfo {
        private GeneratorNextBoundBuiltinMethodInfo _nextMethod;
        private GeneratorSendBoundBuiltinMethodInfo _sendMethod;
        private readonly Node _node;
        public readonly VariableDef Yields;
        public readonly VariableDef Sends;
        public readonly VariableDef Returns;

        public GeneratorInfo(PythonAnalyzer projectState, Node node)
            : base(projectState.ClassInfos[BuiltinTypeId.Generator]) {
            _node = node;
            Yields = new VariableDef();
            Sends = new VariableDef();
            Returns = new VariableDef();
        }

        private GeneratorNextBoundBuiltinMethodInfo NextMethod {
            get {
                if (_nextMethod == null) {
                    INamespaceSet nextMeth;
                    string nextName = (ProjectState.LanguageVersion.Is3x()) ? "__next__" : "next";
                    if (TryGetMember(nextName, out nextMeth)) {
                        _nextMethod = new GeneratorNextBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)nextMeth.First());
                    }
                }
                return _nextMethod;
            }
        }

        private GeneratorSendBoundBuiltinMethodInfo SendMethod {
            get {
                if (_sendMethod == null) {
                    INamespaceSet sendMeth;
                    if (TryGetMember("send", out sendMeth)) {
                        _sendMethod = new GeneratorSendBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)sendMeth.First());
                    }
                }
                return _sendMethod;
            }
        }


        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            switch(name) {
                case "next":
                    if (NextMethod != null && unit.ProjectState.LanguageVersion.Is2x()) {
                        return NextMethod.SelfSet;
                    }
                    break;
                case "__next__":
                    if (NextMethod != null && unit.ProjectState.LanguageVersion.Is3x()) {
                        return NextMethod.SelfSet;
                    }
                    break;
                case "send":
                    if (SendMethod != null) {
                        return SendMethod.SelfSet;
                    }
                    break;
            }
            
            return base.GetMember(node, unit, name);
        }

        public override INamespaceSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override INamespaceSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            Yields.AddDependency(unit);

            return Yields.Types;
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            base.AddReference(node, analysisUnit);
        }

        public void AddYield(Node node, AnalysisUnit unit, INamespaceSet yieldValue, bool enqueue = true) {
            Yields.MakeUnionStrongerIfMoreThan(ProjectState.Limits.YieldTypes, yieldValue);
            Yields.AddTypes(unit, yieldValue, enqueue);
        }

        public void AddReturn(Node node, AnalysisUnit unit, INamespaceSet returnValue, bool enqueue = true) {
            Returns.MakeUnionStrongerIfMoreThan(ProjectState.Limits.ReturnTypes, returnValue);
            Returns.AddTypes(unit, returnValue, enqueue);
        }

        public void AddSend(Node node, AnalysisUnit unit, INamespaceSet sendValue, bool enqueue = true) {
            Sends.AddTypes(unit, sendValue, enqueue);
        }

        public void AddYieldFrom(Node node, AnalysisUnit unit, INamespaceSet yieldsFrom, bool enqueue = true) {
            foreach (var ns in yieldsFrom) {
                AddYield(node, unit, ns.GetEnumeratorTypes(node, unit), enqueue);
                var gen = ns as GeneratorInfo;
                if (gen != null) {
                    gen.AddSend(node, unit, Sends.Types, enqueue);
                    AddReturn(node, unit, gen.Returns.Types, enqueue);
                }
            }
        }

        private const int REQUIRED_STRENGTH = 1;

        //public override bool UnionEquals(Namespace ns, int strength) {
        //    if (strength < REQUIRED_STRENGTH) {
        //        return Equals(ns);
        //    }

        //    var sgi = ns as SimpleGeneratorInfo;
        //    if (sgi != null) {
        //        return true;
        //    }

        //    var gi = ns as GeneratorInfo;
        //    if (gi == null) {
        //        return false;
        //    }

        //    return _node == gi._node;
        //}

        //public override int UnionHashCode(int strength) {
        //    if (strength < REQUIRED_STRENGTH) {
        //        return GetHashCode();
        //    }
        //    return ClassInfo.GetHashCode();
        //}

        //internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
        //    if (strength < REQUIRED_STRENGTH) {
        //        return this;
        //    }

        //    var sgi = ns as SimpleGeneratorInfo;
        //    if (sgi != null) {
        //        Yields.CopyTo(sgi.Yields);
        //        return sgi;
        //    }

        //    var gi = ns as GeneratorInfo;
        //    if (gi == null || Equals(ns)) {
        //        return this;
        //    }

        //    bool anyAdded = false;
        //    var newGi = new SimpleGeneratorInfo(ProjectState);

        //    Yields.CopyTo(newGi.Yields);
        //    anyAdded |= gi.Yields.CopyTo(newGi.Yields);

        //    if (!anyAdded) {
        //        return this;
        //    }

        //    newGi.Yields.EnqueueDependents();
        //    return newGi;
        //}
    }
}
