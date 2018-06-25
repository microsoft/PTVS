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
    sealed class NamedLocation {
        public string Name { get; set; }
        public SourceSpan SourceSpan { get; set; }
    }

    class ImportedModuleNameWalker : PythonWalkerWithLocation {
        private readonly string _importingFromModuleName;
        private readonly string _importingFromFilePath;
        private readonly PythonAst _ast;

        public ImportedModuleNameWalker(IPythonProjectEntry entry, int location, PythonAst ast) :
            this(entry.ModuleName, entry.FilePath, location, ast) {
        }
        public ImportedModuleNameWalker(string importingFromModuleName, string importingFromFilePath, int location, PythonAst ast) : base(location) {
            _importingFromModuleName = importingFromModuleName;
            _importingFromFilePath = importingFromFilePath;
            _ast = ast;
        }

        public IEnumerable<NamedLocation> ImportedModules { get; private set; } = Enumerable.Empty<NamedLocation>();
        public IEnumerable<NamedLocation> ImportedMembers { get; private set; } = Enumerable.Empty<NamedLocation>();
        public NamedLocation ImportedType { get; private set; }

        public override bool Walk(FromImportStatement node) {
            if (node.StartIndex <= Location && Location <= node.EndIndex) {
                // Determine if location is over imported parts such as 
                // over 'a' in 'from . import a, b, c' or over 'x' in 'from a import x'
                // and store module names and imported parts
                var modName = node.Root.MakeString();
                var nameNode = node.Names.MaybeEnumerate().Where(n => n.StartIndex <= Location && Location <= n.EndIndex).FirstOrDefault();
                if (nameNode != null && node.AsNames != null) {
                    var index = node.Names.IndexOf(nameNode);
                    if (index < node.AsNames.Count && node.AsNames[index] != null) {
                        ImportedType = GetNamedLocation(node.AsNames[index]);
                        return false;
                    }
                }

                // See if we can resolve relative names
                var candidates = ModuleResolver.ResolvePotentialModuleNames(_importingFromModuleName, _importingFromFilePath, modName, node.ForceAbsolute).ToArray();
                if (candidates.Length == 1 && string.IsNullOrEmpty(candidates[0]) && node.Names != null && node.Names.Any()) {
                    ImportedModules = new[] { GetNamedLocation(node.Names.First()) };
                } else if (nameNode != null) {
                    ImportedMembers = candidates.Select(c => GetNamedLocation("{0}.{1}".FormatInvariant(c, nameNode.Name), nameNode));
                } else if (candidates.Length > 0) {
                    ImportedModules = candidates.Select(c => GetNamedLocation(c, node.Root));
                } else {
                    ImportedModules = new[] { GetNamedLocation(modName, node.Root) };
                }
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            if (node.StartIndex <= Location && Location <= node.EndIndex) {
                var nameNode = node.Names.MaybeEnumerate().Where(n => n.StartIndex <= Location && Location <= n.EndIndex).FirstOrDefault();
                if (nameNode != null) {
                    ImportedModules = new[] { GetNamedLocation(nameNode.MakeString(), nameNode) };
                }
            }
            return false;
        }


        private NamedLocation GetNamedLocation(NameExpression node) => GetNamedLocation(node.Name, node);

        private NamedLocation GetNamedLocation(string name, Node node)
            => new NamedLocation { Name = name, SourceSpan = GetSourceSpan(node) };

        private SourceSpan GetSourceSpan(Node n)
            => _ast != null 
            ? new SourceSpan(_ast.IndexToLocation(n.StartIndex), _ast.IndexToLocation(n.EndIndex))
            : default(SourceSpan);
    }
}
