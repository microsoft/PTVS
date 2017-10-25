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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    class ImportedModuleNameWalker : PythonWalkerWithLocation {
        private readonly string[] _module;

        public ImportedModuleNameWalker(string module, int location) : base(location) {
            _module = module?.Split('.') ?? Array.Empty<string>();
        }

        public string ImportedName { get; private set; } = null;

        private bool SetName(RelativeModuleName importName) {
            if (importName == null ||
                (importName.DotCount - 1) > _module.Length ||
                importName.Names?.Any() != true) {
                return false;
            }

            var names = _module
                .Take(_module.Length - (importName.DotCount - 1))
                .Concat(importName.Names.Select(n => n.Name))
                .ToList();

            ImportedName = string.Join(".", names);

            return true;
        }

        private void SetName(DottedName importNames) {
            if (SetName(importNames as RelativeModuleName)) {
                return;
            }

            ImportedName = importNames.MakeString();
        }

        public override bool Walk(FromImportStatement node) {
            if (node.Root.StartIndex <= Location && Location <= node.Root.EndIndex) {
                SetName(node.Root);
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            foreach (var n in node.Names.MaybeEnumerate()) {
                if (n.StartIndex <= Location && Location <= n.EndIndex) {
                    SetName(n);
                    break;
                }
            }

            return false;
        }
    }
}
