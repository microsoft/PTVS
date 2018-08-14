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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleMembers : IPythonMultipleMembers, ILocatedMember {
        private readonly IMember[] _members;
        private IList<IMember> _resolvedMembers;

        public AstPythonMultipleMembers() {
            _members = Array.Empty<IMember>();
        }

        private AstPythonMultipleMembers(IMember[] members) {
            _members = members;
        }

        private void EnsureMembers() {
            if (_resolvedMembers != null) {
                return;
            }
            lock (_members) {
                if (_resolvedMembers != null) {
                    return;
                }

                var unresolved = _members.OfType<ILazyMember>().ToArray();

                if (unresolved.Any()) {
                    // Publish non-lazy members immediately. This will prevent recursion
                    // into EnsureMembers while we are resolving lazy values.
                    var resolved = _members.Where(m => !(m is ILazyMember)).ToList();
                    _resolvedMembers = resolved.ToArray();

                    foreach (var lm in unresolved) {
                        var m = lm.Get();
                        if (m != null) {
                            resolved.Add(m);
                        }
                    }

                    _resolvedMembers = resolved;
                } else {
                    _resolvedMembers = _members;
                }
            }
        }

        public static IMember Create(IEnumerable<IMember> members) => Create(members.Where(m => m != null).Distinct().ToArray(), null);

        private static IMember Create(IMember[] members, IMember single) {
            if (single != null && !members.Contains(single)) {
                members = members.Concat(Enumerable.Repeat(single, 1)).ToArray();
            }

            if (members.Length == 1) {
                return members[0];
            } else if (members.Length == 0) {
                return null;
            }

            if (members.All(m => m is IPythonFunction)) {
                return new MultipleFunctionMembers(members);
            }
            if (members.All(m => m is IPythonType)) {
                return new MultipleTypeMembers(members);
            }
            if (members.All(m => m is IPythonModule)) {
                return new MultipleModuleMembers(members);
            }

            return new AstPythonMultipleMembers(members);
        }

        public static IMember Combine(IMember x, IMember y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            } else if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            } else if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleMembers;
            var mmy = y as AstPythonMultipleMembers;

            if (mmx != null && mmy == null) {
                return Create(mmx._members, y);
            } else if (mmy != null && mmx == null) {
                return Create(mmy._members, x);
            } else if (mmx != null && mmy != null) {
                return Create(mmx._members.Union(mmy._members).ToArray(), null);
            } else {
                return Create(new[] { x }, y);
            }
        }

        public static T CreateAs<T>(IEnumerable<IMember> members) => As<T>(Create(members));
        public static T CombineAs<T>(IMember x, IMember y) => As<T>(Combine(x, y));

        public static T As<T>(IMember member) {
            if (member is T t) {
                return t;
            }
            var members = (member as IPythonMultipleMembers)?.Members;
            if (members != null) {
                member = Create(members.Where(m => m is T));
                if (member is T t2) {
                    return t2;
                }
                return members.OfType<T>().FirstOrDefault();
            }

            return default(T);
        }

        public IList<IMember> Members {
            get {
                EnsureMembers();
                return _resolvedMembers;
            }
        }

        public virtual PythonMemberType MemberType => PythonMemberType.Multiple;
        public IEnumerable<LocationInfo> Locations => Members.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());

        // Equality deliberately uses unresolved members
        public override bool Equals(object obj) => GetType() == obj?.GetType() && obj is AstPythonMultipleMembers mm && new HashSet<IMember>(_members).SetEquals(mm._members);
        public override int GetHashCode() => _members.Aggregate(GetType().GetHashCode(), (hc, m) => hc ^ (m?.GetHashCode() ?? 0));

        protected static string ChooseName(IEnumerable<string> names) => names.FirstOrDefault(n => !string.IsNullOrEmpty(n));
        protected static string ChooseDocumentation(IEnumerable<string> docs) {
            // TODO: Combine distinct documentation
            return docs.FirstOrDefault(d => !string.IsNullOrEmpty(d));
        }

        class MultipleFunctionMembers : AstPythonMultipleMembers, IPythonFunction {
            public MultipleFunctionMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonFunction> Functions => Members.OfType<IPythonFunction>();

            public string Name => ChooseName(Functions.Select(f => f.Name)) ?? "<function>";
            public string Documentation => ChooseDocumentation(Functions.Select(f => f.Documentation));
            public bool IsBuiltin => Functions.Any(f => f.IsBuiltin);
            public bool IsStatic => Functions.Any(f => f.IsStatic);
            public bool IsClassMethod => Functions.Any(f => f.IsClassMethod);
            public IList<IPythonFunctionOverload> Overloads => Functions.SelectMany(f => f.Overloads).ToArray();
            public IPythonType DeclaringType => CreateAs<IPythonType>(Functions.Select(f => f.DeclaringType));
            public IPythonModule DeclaringModule => CreateAs<IPythonModule>(Functions.Select(f => f.DeclaringModule));
            public override PythonMemberType MemberType => PythonMemberType.Function;
        }

        class MultipleMethodMembers : AstPythonMultipleMembers, IPythonMethodDescriptor {
            public MultipleMethodMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonMethodDescriptor> Methods => Members.OfType<IPythonMethodDescriptor>();

            public IPythonFunction Function => CreateAs<IPythonFunction>(Methods.Select(m => m.Function));
            public bool IsBound => Methods.Any(m => m.IsBound);
            public override PythonMemberType MemberType => PythonMemberType.Method;
        }

        class MultipleModuleMembers : AstPythonMultipleMembers, IPythonModule {
            public MultipleModuleMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonModule> Modules => Members.OfType<IPythonModule>();

            public string Name => ChooseName(Modules.Select(m => m.Name)) ?? "<module>";
            public string Documentation => ChooseDocumentation(Modules.Select(m => m.Documentation));
            public IEnumerable<string> GetChildrenModules() => Modules.SelectMany(m => m.GetChildrenModules());
            public IMember GetMember(IModuleContext context, string name) => Create(Modules.Select(m => m.GetMember(context, name)));
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Modules.SelectMany(m => m.GetMemberNames(moduleContext)).Distinct();
            public override PythonMemberType MemberType => PythonMemberType.Module;

            public void Imported(IModuleContext context) {
                List<Exception> exceptions = null;
                foreach (var m in Modules) {
                    try {
                        m.Imported(context);
                    } catch (Exception ex) {
                        exceptions = exceptions ?? new List<Exception>();
                        exceptions.Add(ex);
                    }
                }
                if (exceptions != null) {
                    throw new AggregateException(exceptions);
                }
            }
        }

        class MultipleTypeMembers : AstPythonMultipleMembers, IPythonType {
            public MultipleTypeMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonType> Types => Members.OfType<IPythonType>();

            public string Name => ChooseName(Types.Select(t => t.Name)) ?? "<type>";
            public string Documentation => ChooseDocumentation(Types.Select(t => t.Documentation));
            public BuiltinTypeId TypeId => Types.GroupBy(t => t.TypeId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? BuiltinTypeId.Unknown;
            public IPythonModule DeclaringModule => CreateAs<IPythonModule>(Types.Select(t => t.DeclaringModule));
            public IList<IPythonType> Mro => Types.Select(t => t.Mro).OrderByDescending(m => m.Count).FirstOrDefault() ?? new[] { this };
            public bool IsBuiltin => Types.All(t => t.IsBuiltin);
            public IPythonFunction GetConstructors() => CreateAs<IPythonFunction>(Types.Select(t => t.GetConstructors()));
            public IMember GetMember(IModuleContext context, string name) => Create(Types.Select(t => t.GetMember(context, name)));
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Types.SelectMany(t => t.GetMemberNames(moduleContext)).Distinct();
            public override PythonMemberType MemberType => PythonMemberType.Class;
        }
    }
}
