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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        public override async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params) {
            var members = Enumerable.Empty<MemberResult>();
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;

            foreach (var entry in ProjectFiles.All) {
                members = members.Concat(
                    await GetModuleVariablesAsync(entry as ProjectEntry, opts, @params.query, 50)
                );
            }

            members = members.GroupBy(mr => mr.Name).Select(g => g.First());
            return members.Select(ToSymbolInformation).ToArray();
        }

        public override async Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params) {
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;
            var entry = ProjectFiles.GetEntry(@params.textDocument);

            var members = await GetModuleVariablesAsync(entry as ProjectEntry, opts, string.Empty, 50);
            return members
                .GroupBy(mr => mr.Name)
                .Select(g => g.First())
                .Select(ToSymbolInformation)
                .ToArray();
        }

        public async Task<DocumentSymbol[]> NewDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;
            var entry = ProjectFiles.GetEntry(@params.textDocument);

            var members = await GetModuleVariablesAsync(entry as ProjectEntry, opts, string.Empty, 50);
            return ToDocumentSymbols(members);
        }

        private static async Task<List<MemberResult>> GetModuleVariablesAsync(ProjectEntry entry, GetMemberOptions opts, string prefix, int timeout) {
            var analysis = entry != null ? await entry.GetAnalysisAsync(timeout) : null;
            return analysis == null ? new List<MemberResult>() : GetModuleVariables(entry, opts, prefix, analysis).ToList();
        }

        private static IEnumerable<MemberResult> GetModuleVariables(ProjectEntry entry, GetMemberOptions opts, string prefix, ModuleAnalysis analysis) {
            var all = analysis.GetAllAvailableMembers(SourceLocation.None, opts);
            return all
                .Where(m => {
                    if (m.Values.Any(v => v.DeclaringModule == entry || v.Locations.Any(l => l.DocumentUri == entry.DocumentUri))) {
                        if (string.IsNullOrEmpty(prefix) || m.Name.StartsWithOrdinal(prefix, ignoreCase: true)) {
                            return true;
                        }
                    }
                    return false;
                })
            .Concat(GetChildScopesVariables(analysis, analysis.Scope, opts));
        }

        private static IEnumerable<MemberResult> GetChildScopesVariables(ModuleAnalysis analysis, InterpreterScope scope, GetMemberOptions opts)
            => scope.Children.SelectMany(c => GetScopeVariables(analysis, c, opts));

        private static IEnumerable<MemberResult> GetScopeVariables(ModuleAnalysis analysis, InterpreterScope scope, GetMemberOptions opts)
            => analysis.GetAllAvailableMembersFromScope(scope, opts).Concat(GetChildScopesVariables(analysis, scope, opts));

        private SymbolInformation ToSymbolInformation(MemberResult m) {
            var res = new SymbolInformation {
                name = m.Name,
                kind = ToSymbolKind(m),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            var loc = m.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.FilePath));
            if (loc != null) {
                res.location = new Location {
                    uri = loc.DocumentUri,
                    range = new SourceSpan(
                        new SourceLocation(loc.StartLine, loc.StartColumn),
                        new SourceLocation(loc.EndLine ?? loc.StartLine, loc.EndColumn ?? loc.StartColumn)
                    )
                };
            }

            return res;
        }

        private DocumentSymbol[] ToDocumentSymbols(List<MemberResult> members) {
            var childMap = new Dictionary<MemberResult, List<MemberResult>>();
            var topLevel = new List<MemberResult>();

            foreach (var m in members) {
                var parent = members.FirstOrDefault(x => x.Scope?.Node == m.Scope?.OuterScope?.Node && x.Name == m.Scope?.Name);
                if (parent != default(MemberResult)) {
                    if (!childMap.TryGetValue(parent, out var children)) {
                        childMap[parent] = children = new List<MemberResult>();
                    }
                    children.Add(m);
                } else {
                    topLevel.Add(m);
                }
            }

            var symbols = topLevel
                .GroupBy(mr => mr.Name)
                .Select(g => g.First())
                .Select(m => ToDocumentSymbol(m, childMap))
                .ToArray();

            return symbols;
        }

        private DocumentSymbol ToDocumentSymbol(MemberResult m, Dictionary<MemberResult, List<MemberResult>> childMap) {
            var res = new DocumentSymbol {
                name = m.Name,
                detail = m.Name,
                kind = ToSymbolKind(m),
                deprecated = false
            };

            if (childMap.TryGetValue(m, out var children)) {
                res.children = children.Select(x => ToDocumentSymbol(x, childMap)).ToArray();
            }

            var loc = m.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.FilePath));
            if (loc != null) {
                res.range = new SourceSpan(
                        new SourceLocation(loc.StartLine, loc.StartColumn),
                        new SourceLocation(loc.EndLine ?? loc.StartLine, loc.EndColumn ?? loc.StartColumn)
                    );
                res.selectionRange = res.range;
            }
            return res;
        }

        private static SymbolKind ToSymbolKind(MemberResult m) {
            switch (m.MemberType) {
                case PythonMemberType.Unknown: return SymbolKind.None;
                case PythonMemberType.Class: return m.Scope is FunctionScope ? SymbolKind.Variable : SymbolKind.Class;
                case PythonMemberType.Instance: return SymbolKind.Variable;
                case PythonMemberType.Delegate: return SymbolKind.Function;
                case PythonMemberType.DelegateInstance: return SymbolKind.Function;
                case PythonMemberType.Enum: return SymbolKind.Enum;
                case PythonMemberType.EnumInstance: return SymbolKind.EnumMember;
                case PythonMemberType.Function: return SymbolKind.Function;
                case PythonMemberType.Method: return SymbolKind.Method;
                case PythonMemberType.Module: return SymbolKind.Module;
                case PythonMemberType.Namespace: return SymbolKind.Namespace;
                case PythonMemberType.Constant: return SymbolKind.Constant;
                case PythonMemberType.Event: return SymbolKind.Event;
                case PythonMemberType.Field: return SymbolKind.Field;
                case PythonMemberType.Property: return SymbolKind.Property;
                case PythonMemberType.Multiple: return SymbolKind.Object;
                case PythonMemberType.Keyword: return SymbolKind.None;
                case PythonMemberType.CodeSnippet: return SymbolKind.None;
                case PythonMemberType.NamedArgument: return SymbolKind.None;
                default: return SymbolKind.None;
            }
        }
    }
}
