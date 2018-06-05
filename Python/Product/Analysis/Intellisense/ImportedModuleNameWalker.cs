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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    class ImportedModuleNameWalker : PythonWalkerWithLocation {
        private readonly string _importingFromModuleName;
        private readonly string _importingFromFilePath;

        public ImportedModuleNameWalker(IPythonProjectEntry entry, int location) : 
            this(entry.ModuleName, entry.FilePath, location) {
        }
        public ImportedModuleNameWalker(string importingFromModuleName, string importingFromFilePath, int location) : base(location) {
            _importingFromModuleName = importingFromModuleName;
            _importingFromFilePath = importingFromFilePath;
        }

        public IEnumerable<string> ImportedModules { get; private set; } = Enumerable.Empty<string>();

        public override bool Walk(FromImportStatement node) {
            if (node.StartIndex <= Location && Location <= node.EndIndex) {
                // Determine if location is over imported parts such as 
                // over 'a' in 'from . import a, b, c' or over 'x' in 'from a import x'
                // and store module names and imported parts
                ImportedModules = ModuleResolver.ResolveRelativeFromImport(_importingFromModuleName, _importingFromFilePath, node);
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            foreach (var n in node.Names.MaybeEnumerate()) {
                if (n.StartIndex <= Location && Location <= n.EndIndex) {
                    ImportedModules = new[] { n.MakeString() };
                    break;
                }
            }
            return false;
        }
    }
}
