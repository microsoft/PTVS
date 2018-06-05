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
    class AstPythonMultipleTypes : IPythonMultipleMembers, IPythonType, ILocatedMember {
        private IList<IPythonType> _members;
        private IPythonFunction _constructor;

        public AstPythonMultipleTypes() {
            _members = Array.Empty<IPythonType>();
        }

        private AstPythonMultipleTypes(IPythonType[] members) {
            _members = members;
        }

        public AstPythonMultipleTypes(IEnumerable<IPythonType> members) {
            _members = members.ToArray();
        }

        public IPythonType Trim() => _members.Count == 1 ? _members[0] : this;

        public static IPythonType Combine(IPythonType x, IPythonType y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            } else if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            } else if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleTypes;
            var mmy = y as AstPythonMultipleTypes;

            if (mmx != null && mmy == null) {
                mmx.AddMember(y);
                return mmx;
            } else if (mmy != null && mmx == null) {
                mmy.AddMember(x);
                return mmy;
            } else if (mmx != null && mmy != null) {
                mmx.AddMembers(mmy._members);
                return mmx;
            } else {
                return new AstPythonMultipleTypes(new[] { x, y });
            }
        }

        public void AddMember(IPythonType member) {
            if (member == this) {
                return;
            }

            if (member is AstPythonMultipleTypes mt) {
                AddMembers(mt._members);
            }

            if (member is IPythonMultipleMembers mm) {
                AddMembers(mm.Members.OfType<IPythonType>());
                return;
            }

            var old = _members;
            if (!old.Contains(member)) {
                _members = old.Concat(Enumerable.Repeat(member, 1)).ToArray();
            } else if (!old.Any()) {
                _members = new[] { member };
            }
            _constructor = null;
        }

        public void AddMembers(IEnumerable<IPythonType> members) {
            var old = _members;
            if (old.Any()) {
                _members = old.Union(members.Where(m => m != this)).ToArray();
            } else {
                _members = members.Where(m => m != this).ToArray();
            }
            _constructor = null;
        }

        public IPythonFunction GetConstructors() {
            if (_constructor == null) {
                var fns = _members.Select(m => m.GetConstructors()).Where(c => c != null).ToList();
                if (fns.Count == 0) {
                    return null;
                }
                var fn = new AstPythonFunction(fns[0]);
                foreach (var o in fns.Skip(1).SelectMany(f => f.Overloads)) {
                    fn.AddOverload(o);
                }
                _constructor = fn;
            }
            return _constructor;
        }

        public IMember GetMember(IModuleContext context, string name) {
            return new AstPythonMultipleMembers(_members.Select(m => m.GetMember(context, name))).Trim();
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _members.SelectMany(m => m.GetMemberNames(moduleContext)).Distinct();
        }

        public IList<IMember> Members => _members.Cast<IMember>().ToArray();
        public PythonMemberType MemberType => PythonMemberType.Class;
        public string Name => _members.Select(m => m.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "<type>";
        public string Documentation => _members.Select(m => m.Documentation).OrderByDescending(d => d?.Length ?? 0).FirstOrDefault() ?? "";
        public BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public IPythonModule DeclaringModule => _members.Select(m => m.DeclaringModule).FirstOrDefault(m => m != null);
        public IList<IPythonType> Mro => _members.FirstOrDefault()?.Mro;
        public bool IsBuiltin => _members.All(m => m.IsBuiltin);
        public IEnumerable<LocationInfo> Locations => _members.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
    }
}
