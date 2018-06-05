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

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleModules : IPythonModule, IPythonMultipleMembers, ILocatedMember {
        private IList<IPythonModule> _modules;
        private IModuleContext _importContext;
        private bool _imported;

        public AstPythonMultipleModules() {
            _modules = Array.Empty<IPythonModule>();
        }

        private AstPythonMultipleModules(IPythonModule[] members) {
            _modules = members;
        }

        public AstPythonMultipleModules(IEnumerable<IPythonModule> members) {
            _modules = members.ToArray();
        }

        public static IPythonModule Combine(IPythonModule x, IPythonModule y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            } else if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            } else if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleModules;
            var mmy = y as AstPythonMultipleModules;

            if (mmx != null && mmy == null) {
                mmx.AddModule(y);
                return mmx;
            } else if (mmy != null && mmx == null) {
                mmy.AddModule(x);
                return mmy;
            } else if (mmx != null && mmy != null) {
                mmx.AddModules(mmy._modules);
                return mmx;
            } else {
                return new AstPythonMultipleModules(new[] { x, y });
            }
        }

        public void AddModule(IPythonModule member) {
            if (member == this) {
                return;
            }

            if (member is AstPythonMultipleModules mmod) {
                AddModules(mmod._modules);
                return;
            }

            if (member is IPythonMultipleMembers mm) {
                AddModules(mm.Members.OfType<IPythonModule>());
                return;
            }

            var old = _modules;
            if (!old.Contains(member)) {
                _modules = old.Concat(Enumerable.Repeat(member, 1)).ToArray();
                if (_imported) {
                    member.Imported(_importContext);
                }
            } else if (!old.Any()) {
                _modules = new[] { member };
                if (_imported) {
                    member.Imported(_importContext);
                }
            }
        }

        public void AddModules(IEnumerable<IPythonModule> members) {
            var old = _modules;
            if (old.Any()) {
                _modules = old.Union(members.Where(m => m != this)).ToArray();
            } else {
                _modules = members.Where(m => m != this).ToArray();
            }
            if (_imported) {
                foreach (var m in _modules.Except(old)) {
                    Debug.Assert(m != this);
                    m.Imported(_importContext);
                }
            }
        }

        public IEnumerable<string> GetChildrenModules() {
            return _modules.SelectMany(m => m.GetChildrenModules());
        }

        public void Imported(IModuleContext context) {
            _imported = true;
            _importContext = context;
            foreach (var m in _modules) {
                m.Imported(context);
            }
        }

        public IMember GetMember(IModuleContext context, string name) {
            IMember res = null;
            foreach (var m in _modules) {
                var r = m.GetMember(context, name);
                if (res == null) {
                    res = r;
                } else {
                    res = AstPythonMultipleMembers.Combine(res, r);
                }
            }
            return res;
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _modules.SelectMany(m => m.GetMemberNames(moduleContext)).Distinct();
        }

        public IList<IMember> Members => _modules.OfType<IMember>().ToArray();

        public PythonMemberType MemberType => PythonMemberType.Module;

        public IEnumerable<LocationInfo> Locations => _modules.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());

        public string Name => _modules.Select(m => m.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "<module>";

        public string Documentation => _modules.Select(m => m.Documentation).OrderByDescending(d => d?.Length ?? 0).FirstOrDefault() ?? "";
    }
}
