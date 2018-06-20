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
        public IEnumerable<string> ImportedMembers { get; private set; } = Enumerable.Empty<string>();
        public string ImportedType { get; private set; }

        public override bool Walk(FromImportStatement node) {
            if (node.StartIndex <= Location && Location <= node.EndIndex) {
                // Determine if location is over imported parts such as 
                // over 'a' in 'from . import a, b, c' or over 'x' in 'from a import x'
                // and store module names and imported parts
                var modName = node.Root.MakeString();
                var nameNode = node.Names.MaybeEnumerate().Where(n => n.StartIndex <= Location && Location <= n.EndIndex).FirstOrDefault();
                if (nameNode != null) {
                    // Over name, see if we have 'as' and fetch type from there
                    if (node.AsNames != null) {
                        var index = node.Names.IndexOf(nameNode);
                        if (index < node.AsNames.Count) {
                            ImportedType = node.AsNames[index]?.Name;
                            return false;
                        }
                    }
                    // See if we can resolve relative names
                    var candidates = ModuleResolver.ResolvePotentialModuleNames(_importingFromModuleName, _importingFromFilePath, modName, node.ForceAbsolute);
                    ImportedMembers = candidates.Select(c => "{0}.{1}".FormatInvariant(c, nameNode.Name));
                } else {
                    ImportedModules = new[] { modName };
                }
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            if (node.StartIndex <= Location && Location <= node.EndIndex) {
                var nameNode = node.Names.MaybeEnumerate().Where(n => n.StartIndex <= Location && Location <= n.EndIndex).FirstOrDefault();
                ImportedModules = nameNode != null ? new[] { nameNode.MakeString() } : Enumerable.Empty<string>();
            }
            return false;
        }
    }
}
