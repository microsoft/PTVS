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
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        public override async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params) {
            var members = Enumerable.Empty<MemberResult>();
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;

            foreach (var entry in ProjectFiles.All) {
                members = members.Concat(
                    await GetModuleVariablesAsync(entry as ProjectEntry, opts, @params.query)
                );
            }

            members = members.GroupBy(mr => mr.Name).Select(g => g.First());
            return members.Select(ToSymbolInformation).ToArray();
        }

        public override async Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params) {
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;
            var entry = ProjectFiles.GetEntry(@params.textDocument);

            var members = await GetModuleVariablesAsync(entry as ProjectEntry, opts, string.Empty);
            return members
                .GroupBy(mr => mr.Name)
                .Select(g => g.First())
                .Select(ToSymbolInformation)
                .ToArray();
        }

        private static async Task<List<MemberResult>> GetModuleVariablesAsync(ProjectEntry entry, GetMemberOptions opts, string prefix) {
            var analysis = entry != null ? await entry.GetAnalysisAsync(waitingTimeout: 50) : null;
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

        private static IEnumerable<MemberResult> GetChildScopesVariables(ModuleAnalysis analysis, InterpreterScope scope, GetMemberOptions opts) {
            foreach (var c in scope.Children) {
                var res = GetScopeVariables(analysis, scope, opts);
                foreach (var m in res) {
                    yield return m;
                }
            }
        }

        private static IEnumerable<MemberResult> GetScopeVariables(ModuleAnalysis analysis, InterpreterScope scope, GetMemberOptions opts) {
            var res1 = analysis.GetAllAvailableMembersFromScope(scope, opts);
            foreach (var m in res1) {
                yield return m;
            }
            var res2 = GetChildScopesVariables(analysis, scope, opts);
        }


        private SymbolInformation ToSymbolInformation(MemberResult m) {
            var res = new SymbolInformation {
                name = m.Name,
                kind = ToSymbolKind(m.MemberType),
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

        private static SymbolKind ToSymbolKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return SymbolKind.None;
                case PythonMemberType.Class: return SymbolKind.Class;
                case PythonMemberType.Instance: return SymbolKind.Object;
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
