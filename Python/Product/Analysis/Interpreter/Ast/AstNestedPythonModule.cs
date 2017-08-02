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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstNestedPythonModule : IPythonModule, ILocatedMember {
        private string _name, _documentation;
        private IPythonModule _module;
        private readonly IReadOnlyList<string> _importNames;
        private readonly IReadOnlyList<string> _children;

        public AstNestedPythonModule(
            string name,
            string documentation,
            IReadOnlyList<string> children,
            IReadOnlyList<string> importNames
        ) {
            _name = name;
            _documentation = documentation;
            _children = children;
            _importNames = importNames;
        }

        public string Name => MaybeModule?.Name ?? _name;
        public string Documentation => MaybeModule?.Documentation ?? _documentation;
        public PythonMemberType MemberType => PythonMemberType.Module;
        public IEnumerable<LocationInfo> Locations => ((MaybeModule as ILocatedMember)?.Locations).MaybeEnumerate();

        private IPythonModule MaybeModule => Volatile.Read(ref _module);
        private IPythonModule GetModule(IModuleContext context) {
            var mod = Volatile.Read(ref _module);
            if (mod != null) {
                return mod;
            }

            var interp = context as AstPythonInterpreter;
            if (interp != null) {
                foreach (var n in _importNames) {
                    mod = interp.ImportModule(n);
                    if (mod != null) {
                        Debug.Assert(!(mod is AstNestedPythonModule), "ImportModule should not return nested module");
                        break;
                    }
                }
            }
            if (mod == null) {
                mod = new EmptyModule();
            }

            return Interlocked.CompareExchange(ref _module, mod, null) ?? mod;
        }

        public IEnumerable<string> GetChildrenModules() => _children;

        public IMember GetMember(IModuleContext context, string name) {
            return GetModule(context).GetMember(context, name);
        }

        public IEnumerable<string> GetMemberNames(IModuleContext context) {
            return GetModule(context).GetMemberNames(context);
        }

        public void Imported(IModuleContext context) {
            GetModule(context).Imported(context);
        }
    }
}
