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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonType : IPythonType, IMemberContainer, ILocatedMember, IHasQualifiedName {
        private readonly string _name;
        protected readonly Dictionary<string, IMember> _members;
        private IList<IPythonType> _mro;

        private static readonly IPythonModule NoDeclModule = new AstPythonModule();

        [ThreadStatic]
        private static HashSet<AstPythonType> _processing;

        public AstPythonType(string name): this(name, new Dictionary<string, IMember>(), Array.Empty<LocationInfo>()) { }

        public AstPythonType(
            PythonAst ast,
            IPythonModule declModule,
            ClassDefinition def,
            string doc,
            LocationInfo loc
        ) {
            _members = new Dictionary<string, IMember>();

            _name = def?.Name ?? throw new ArgumentNullException(nameof(def));
            Documentation = doc;
            DeclaringModule = declModule ?? throw new ArgumentNullException(nameof(declModule));
            Locations = loc != null ? new[] { loc } : Array.Empty<LocationInfo>();
            StartIndex = def?.StartIndex ?? 0;
        }

        private AstPythonType(string name, Dictionary<string, IMember> members, IEnumerable<LocationInfo> locations) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _members = members;
            _mro = Array.Empty<IPythonType>();
            DeclaringModule = NoDeclModule;
            Locations = locations;
        }

        internal void AddMembers(IEnumerable<KeyValuePair<string, IMember>> members, bool overwrite) {
            lock (_members) {
                foreach (var kv in members) {
                    if (!overwrite) {
                        if (_members.TryGetValue(kv.Key, out var existing)) {
                            continue;
                        }
                    }
                    _members[kv.Key] = kv.Value;
                }
            }
        }

        internal void SetBases(IPythonInterpreter interpreter, IEnumerable<IPythonType> bases) {
            if (Bases != null) {
                throw new InvalidOperationException("cannot set Bases multiple times");
            }
            Bases = bases.MaybeEnumerate().ToArray();
            lock (_members) {
                if (Bases.Count > 0) {
                    _members["__base__"] = Bases[0];
                }
                _members["__bases__"] = new AstPythonSequence(
                    interpreter?.GetBuiltinType(BuiltinTypeId.Tuple),
                    DeclaringModule,
                    Bases,
                    interpreter?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                );
            }
        }

        public IList<IPythonType> Mro {
            get {
                lock (_members) {
                    if (_mro != null) {
                        return _mro;
                    }
                    if (Bases == null) {
                        //Debug.Fail("Accessing Mro before SetBases has been called");
                        return new IPythonType[] { this };
                    }
                    _mro = new IPythonType[] { this };
                    _mro = CalculateMro(this);
                    return _mro;
                }
            }
        }

        internal static IList<IPythonType> CalculateMro(IPythonType cls, HashSet<IPythonType> recursionProtection = null) {
            if (cls == null) {
                return Array.Empty<IPythonType>();
            }
            if (recursionProtection == null) {
                recursionProtection = new HashSet<IPythonType>();
            }
            if (!recursionProtection.Add(cls)) {
                return Array.Empty<IPythonType>();
            }
            try {
                var mergeList = new List<List<IPythonType>> { new List<IPythonType>() };
                var finalMro = new List<IPythonType> { cls };

                var bases = (cls as AstPythonType)?.Bases ??
                    (cls.GetMember(null, "__bases__") as IPythonSequenceType)?.IndexTypes ??
                    Array.Empty<IPythonType>();

                foreach (var b in bases) {
                    var b_mro = new List<IPythonType>();
                    b_mro.AddRange(CalculateMro(b, recursionProtection));
                    mergeList.Add(b_mro);
                }

                while (mergeList.Any()) {
                    // Next candidate is the first head that does not appear in
                    // any other tails.
                    var nextInMro = mergeList.FirstOrDefault(mro => {
                        var m = mro.FirstOrDefault();
                        return m != null && !mergeList.Any(m2 => m2.Skip(1).Contains(m));
                    })?.FirstOrDefault();

                    if (nextInMro == null) {
                        // MRO is invalid, so return just this class
                        return new IPythonType[] { cls };
                    }

                    finalMro.Add(nextInMro);

                    // Remove all instances of that class from potentially being returned again
                    foreach (var mro in mergeList) {
                        mro.RemoveAll(ns => ns == nextInMro);
                    }

                    // Remove all lists that are now empty.
                    mergeList.RemoveAll(mro => !mro.Any());
                }

                return finalMro;
            } finally {
                recursionProtection.Remove(cls);
            }
        }

        public string Name {
            get {
                lock (_members) {
                    IMember nameMember;
                    if (_members.TryGetValue("__name__", out nameMember) && nameMember is AstPythonStringLiteral lit) {
                        return lit.Value;
                    }
                }
                return _name;
            }
        }
        public string Documentation { get; }
        public IPythonModule DeclaringModule { get; }
        public IReadOnlyList<IPythonType> Bases { get; private set; }
        public virtual bool IsBuiltin => false;
        public PythonMemberType MemberType => PythonMemberType.Class;
        public virtual BuiltinTypeId TypeId => BuiltinTypeId.Type;

        /// <summary>
        /// The start index of this class. Used to disambiguate multiple
        /// class definitions with the same name in the same file.
        /// </summary>
        public int StartIndex { get; }

        public IEnumerable<LocationInfo> Locations { get; }

        public string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public KeyValuePair<string, string> FullyQualifiedNamePair => new KeyValuePair<string, string>(DeclaringModule.Name, Name);

        public IMember GetMember(IModuleContext context, string name) {
            IMember member;
            lock (_members) {
                if (_members.TryGetValue(name, out member)) {
                    return member;
                }

                // Special case names that we want to add to our own _members dict
                switch (name) {
                    case "__mro__":
                        member = _members[name] = new AstPythonSequence(
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.Tuple),
                            DeclaringModule,
                            Mro,
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                        );
                        return member;
                }
            }
            if (Push()) {
                try {
                    foreach (var m in Mro.Reverse()) {
                        if (m == this) {
                            return member;
                        }
                        member = member ?? m.GetMember(context, name);
                    }
                } finally {
                    Pop();
                }
            }
            return null;
        }

        private bool Push() {
            if (_processing == null) {
                _processing = new HashSet<AstPythonType> { this };
                return true;
            } else {
                return _processing.Add(this);
            }
        }

        private void Pop() {
            _processing.Remove(this);
            if (_processing.Count == 0) {
                _processing = null;
            }
        }

        public IPythonFunction GetConstructors() => GetMember(null, "__init__") as IPythonFunction;

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            var names = new HashSet<string>();
            lock (_members) {
                names.UnionWith(_members.Keys);
            }

            foreach (var m in Mro.Skip(1)) {
                names.UnionWith(m.GetMemberNames(moduleContext));
            }

            return names;
        }
    }
}
