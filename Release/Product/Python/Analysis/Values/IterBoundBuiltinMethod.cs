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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class IterBoundBuiltinMethodInfo : BoundBuiltinMethodInfo {
        private readonly INamespaceSet _iterator;
        private readonly BuiltinClassInfo _iterClass;
        private readonly VariableDef[] _indexTypes;

        public IterBoundBuiltinMethodInfo(VariableDef[] indexTypes, BuiltinClassInfo iterableClass)
            : base(new IterBuiltinMethodInfo(iterableClass.PythonType, iterableClass.ProjectState)) {
            _indexTypes = indexTypes;
            _iterClass = IteratorInfo.GetIteratorTypeFromType(iterableClass, iterableClass.ProjectState._evalUnit);
        }

        public IterBoundBuiltinMethodInfo(IterableInfo iterable, BuiltinMethodInfo method)
            : base(method) {
            _indexTypes = iterable.IndexTypes;
            _iterClass = IteratorInfo.GetIteratorTypeFromType(iterable.ClassInfo, iterable.ClassInfo.ProjectState._evalUnit);
        }

        public IterBoundBuiltinMethodInfo(BuiltinMethodInfo method, INamespaceSet iterator)
            : base(method) {
            _iterator = iterator;
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 0) {
                return unit.Scope.GetOrMakeNodeValue(node, n => new IteratorInfo(_indexTypes, _iterClass, n));
            }
            return NamespaceSet.Empty;
        }
    }

    class IterBuiltinMethodInfo : BuiltinMethodInfo {
        public IterBuiltinMethodInfo(IPythonType declaringType, PythonAnalyzer projectState)
            : base(new IterFunction(declaringType), PythonMemberType.Method, projectState) { }

        class IterFunction : IPythonFunction {
            public IterFunction(IPythonType declaringType) {
                DeclaringType = declaringType;
            }

            public string Name { get { return "__iter__"; } }
            public string Documentation { get { return "x.__iter__() <==> iter(x)"; } }
            public bool IsBuiltin { get { return true; } }
            public bool IsStatic { get { return true; } }
            public IList<IPythonFunctionOverload> Overloads { get { return new List<IPythonFunctionOverload>(); } }
            public IPythonType DeclaringType { get; private set; }
            public IPythonModule DeclaringModule { get { return DeclaringType.DeclaringModule; } }
            public PythonMemberType MemberType { get { return PythonMemberType.Method; } }
        }

    }

}
