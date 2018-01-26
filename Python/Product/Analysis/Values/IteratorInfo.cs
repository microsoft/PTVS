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

using System.Diagnostics;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Base class for an iterator.  Used for both built-in types with fixed
    /// iteration types as well as user defined iterators where the types can
    /// change.
    /// </summary>
    internal abstract class BaseIteratorValue : BuiltinInstanceInfo {
        private AnalysisValue _iter;
        private AnalysisValue _next;

        public BaseIteratorValue(BuiltinClassInfo klass) : base(klass) {
        }

        internal static BuiltinClassInfo GetIteratorTypeFromType(BuiltinClassInfo klass, AnalysisUnit unit) {
            switch (klass.PythonType.TypeId) {
                case BuiltinTypeId.List:
                    return unit.State.ClassInfos[BuiltinTypeId.ListIterator];
                case BuiltinTypeId.Tuple:
                    return unit.State.ClassInfos[BuiltinTypeId.TupleIterator];
                case BuiltinTypeId.Set:
                    return unit.State.ClassInfos[BuiltinTypeId.SetIterator];
                case BuiltinTypeId.Str:
                    return unit.State.ClassInfos[BuiltinTypeId.StrIterator];
                case BuiltinTypeId.Unicode:
                    return unit.State.ClassInfos[BuiltinTypeId.UnicodeIterator];
                case BuiltinTypeId.Bytes:
                    return unit.State.ClassInfos[BuiltinTypeId.BytesIterator];
                case BuiltinTypeId.Generator:
                case BuiltinTypeId.DictKeys:
                case BuiltinTypeId.DictValues:
                case BuiltinTypeId.DictItems:
                case BuiltinTypeId.ListIterator:
                case BuiltinTypeId.TupleIterator:
                case BuiltinTypeId.SetIterator:
                case BuiltinTypeId.StrIterator:
                case BuiltinTypeId.UnicodeIterator:
                case BuiltinTypeId.BytesIterator:
                case BuiltinTypeId.CallableIterator:
                    return klass;
                default:
                    Debug.Fail($"No iterator type for {klass.PythonType} ({klass.PythonType.Name}::{klass.PythonType.TypeId}");
                    return null;
            }
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (unit.State.LanguageVersion.Is2x() && name == "next" ||
                unit.State.LanguageVersion.Is3x() && name == "__next__") {
                return _next = _next ?? new SpecializedCallable(
                    null,
                    IteratorNext,
                    false
                );
            } else if (name == "__iter__") {
                return _iter = _iter ?? new SpecializedCallable(
                    null,
                    IteratorIter,
                    false
                );
            }

            return base.GetMember(node, unit, name);
        }

        private IAnalysisSet IteratorIter(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return this;
        }

        protected abstract IAnalysisSet IteratorNext(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames);
    }

    /// <summary>
    /// Iterator which always produces the same type of object.
    /// </summary>
    internal class FixedIteratorValue : BaseIteratorValue {
        private readonly IAnalysisSet _iterable;

        public FixedIteratorValue(IAnalysisSet iterable, BuiltinClassInfo klass) : base(klass) {
            _iterable = iterable;
        }

        protected override IAnalysisSet IteratorNext(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _iterable;
        }
    }

    /// <summary>
    /// Iterator for user defined sequence which can have types added to it but is tracked
    /// by a single VariableDef for all types.  Used for iterators over things like 
    /// dictionary keys.
    /// </summary>
    internal class SingleIteratorValue : BaseIteratorValue {
        internal readonly VariableDef _indexTypes;
        private readonly IPythonProjectEntry _declModule;
        private readonly int _declVersion;

        public SingleIteratorValue(VariableDef indexTypes, BuiltinClassInfo iterType, IPythonProjectEntry declModule)
            : base(iterType) {
            _indexTypes = indexTypes;
            _declModule = declModule;
            _declVersion = _declModule.AnalysisVersion;
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _declModule;
            }
        }

        public override int DeclaringVersion => _declVersion;

        protected override IAnalysisSet IteratorNext(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _indexTypes.GetTypesNoCopy(unit, DeclaringModule);
        }
    }

    /// <summary>
    /// Iterator for a user defined sequence which can have multiple independent indicies
    /// whoses values are all precisely tracked.
    /// </summary>
    internal class IteratorValue : BaseIteratorValue {
        private readonly IterableValue _iterable;

        public IteratorValue(IterableValue iterable, BuiltinClassInfo iterType)
            : base(iterType) {
            _iterable = iterable;
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _iterable.DeclaringModule;
            }
        }

        public override int DeclaringVersion {
            get {
                return _iterable.DeclaringVersion;
            }
        }

        protected override IAnalysisSet IteratorNext(Node node, Analysis.AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _iterable.UnionType;
        }

        public IAnalysisSet UnionType {
            get {
                return _iterable.UnionType;
            }
            set {
                _iterable.UnionType = value;
            }
        }
    }
}
