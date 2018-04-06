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
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class CompletionAnalysis {
        private readonly Node _node;
        private readonly Statement _statement;
        private readonly ScopeStatement _scope;
        private readonly Action<FormattableString> _trace;

        public CompletionAnalysis(ModuleAnalysis analysis, PythonAst tree, SourceLocation position, GetMemberOptions opts, Action<FormattableString> trace) {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Position = position;
            Index = Tree.LocationToIndex(Position);
            Options = opts;
            _trace = trace;

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions {
                Names = true,
                MemberName = true,
                NamedArgumentNames = true,
                ImportNameExpression = true,
                ImportAsNames = true,
            });
            finder.Get(Index, Index, out _node, out _statement, out _scope);
        }

        private static readonly IEnumerable<CompletionItem> Empty = Enumerable.Empty<CompletionItem>();

        private void TraceMessage(FormattableString msg) => _trace?.Invoke(msg);

        public ModuleAnalysis Analysis { get; }
        public PythonAst Tree { get; }
        public SourceLocation Position { get; }
        public int Index { get; }
        public GetMemberOptions Options { get; set; }

        public Node Node => _node;
        public Statement Statement => _statement;
        public ScopeStatement Scope => _scope;


        public IEnumerable<CompletionItem> GetCompletionsFromString(string expr) {
            if (string.IsNullOrEmpty(expr)) {
                return null;
            }
            TraceMessage($"Completing expression {expr}");
            return Analysis.GetMembers(expr, Position, Options).Select(ToCompletionItem);
        }

        public IEnumerable<CompletionItem> GetCompletions() {
            var res = GetCompletionsFromMembers();
            if (res != null) {
                return res;
            }

            bool allowKeywords = true;

            res = GetCompletionsInImport() ??
                GetCompletionsInDefinition(ref allowKeywords) ??
                GetCompletionsInForStatement() ??
                GetCompletionsInWithStatement() ??
                GetCompletionsFromTopLevel(allowKeywords);

            return res;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromMembers() {
            var finder = new ExpressionFinder(Tree, GetExpressionOptions.EvaluateMembers);
            if (finder.GetExpression(Index) is Expression expr) {
                TraceMessage($"Completing expression {expr.ToCodeString(Tree, CodeFormattingOptions.Traditional)}");
                return Analysis.GetMembers(expr, Position, Options, null).Select(ToCompletionItem);
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInImport() {
            if (Statement is ImportStatement imp) {
                var first = imp.Names?.FirstOrDefault();
                if (first != null && Index < first.StartIndex) {
                    return Empty;
                }

                if (Node is DottedName dn) {
                    if (dn.Names == null) {
                        return Empty;
                    }

                    var names = new List<string>();
                    foreach (var n in dn.Names) {
                        if (Index > n.EndIndex) {
                            names.Add(n.Name);
                        } else {
                            break;
                        }
                    }

                    if (names.Any()) {
                        return Analysis.ProjectState.GetModuleMembers(Analysis.InterpreterContext, names.ToArray(), false)
                            .Select(ToCompletionItem);
                    }
                    return Analysis.ProjectState.GetModules().Select(ToCompletionItem);
                } else if (Node is NameExpression) {
                    return Analysis.ProjectState.GetModules().Select(ToCompletionItem);
                }

                return Empty;
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInDefinition(ref bool allowKeywords) {
            if (Statement is FunctionDefinition fd) {
                if (Index >= fd.HeaderIndex + 1) {
                    return null;
                }
                if (Index >= fd.HeaderIndex) {
                    return Empty;
                }
                if (Index <= fd.NameExpression.EndIndex) {
                    return Empty;
                }

                foreach (var p in fd.ParametersInternal.MaybeEnumerate()) {
                    if (Index < p.StartIndex) {
                        break;
                    }
                    if (p.Annotation != null) {
                        if (Index < p.Annotation.StartIndex) {
                            return Empty;
                        }
                        return null;
                    }
                    if (p.DefaultValue != null) {
                        if (Index < p.DefaultValue.StartIndex) {
                            return Empty;
                        }
                        return null;
                    }
                }

                allowKeywords = false;
                return null;

            } else if (Statement is ClassDefinition cd) {
                if (Index >= cd.HeaderIndex + 1) {
                    return null;
                }
                if (Index >= cd.HeaderIndex) {
                    return Empty;
                }

                if (cd.BasesInternal != null) {
                    if (cd.BasesInternal.Length > 0 && Index >= cd.BasesInternal[0].StartIndex) {
                        return null;
                    }
                }

                allowKeywords = false;
                return null;

            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInForStatement() {
            if (Statement is ForStatement fs) {
                if (fs.Left != null) {
                    if (Index < fs.Left.StartIndex) {
                        return null;
                    } else if (Index <= fs.Left.EndIndex) {
                        return Empty;
                    }
                }
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInWithStatement() {
            if (Statement is WithStatement ws) {
                foreach (var item in ws.Items.MaybeEnumerate()) {
                    if (item.Variable != null) {
                        if (Index < item.Variable.StartIndex) {
                            return null;
                        } else if (Index <= item.Variable.EndIndex) {
                            return Empty;
                        }
                    }
                }
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromTopLevel(bool allowKeywords) {
            var opts = Options;
            if (allowKeywords) {
                opts |= GetMemberOptions.IncludeExpressionKeywords;
                if (Statement == null) {
                    opts |= GetMemberOptions.IncludeStatementKeywords;
                }
            }

            TraceMessage($"Completing all names");
            var members = Analysis.GetAllAvailableMembers(Position, opts);

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions { Calls = true });
            if (finder.GetExpression(Index) is CallExpression callExpr &&
                callExpr.GetArgumentAtIndex(Tree, Index, out _)) {
                var argNames = Analysis.GetSignatures(callExpr.Target, Position)
                    .SelectMany(o => o.Parameters).Select(p => p?.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                    .Select(n => new MemberResult($"{n}=", PythonMemberType.NamedArgument));

                argNames = argNames.MaybeEnumerate().ToArray();
                TraceMessage($"Including {argNames.Count()} named arguments");

                members = members?.Concat(argNames) ?? argNames;
            }

            return members.Select(ToCompletionItem);
        }


        private CompletionItem ToCompletionItem(MemberResult m) {
            var res = new CompletionItem {
                label = m.Name,
                insertText = m.Completion,
                documentation = m.Documentation,
                // Place regular items first, advanced entries last
                sortText = Char.IsLetter(m.Completion[0]) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            return res;
        }

        private CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return CompletionItemKind.None;
                case PythonMemberType.Class: return CompletionItemKind.Class;
                case PythonMemberType.Instance: return CompletionItemKind.Value;
                case PythonMemberType.Delegate: return CompletionItemKind.Class;
                case PythonMemberType.DelegateInstance: return CompletionItemKind.Function;
                case PythonMemberType.Enum: return CompletionItemKind.Enum;
                case PythonMemberType.EnumInstance: return CompletionItemKind.EnumMember;
                case PythonMemberType.Function: return CompletionItemKind.Function;
                case PythonMemberType.Method: return CompletionItemKind.Method;
                case PythonMemberType.Module: return CompletionItemKind.Module;
                case PythonMemberType.Namespace: return CompletionItemKind.Module;
                case PythonMemberType.Constant: return CompletionItemKind.Constant;
                case PythonMemberType.Event: return CompletionItemKind.Event;
                case PythonMemberType.Field: return CompletionItemKind.Field;
                case PythonMemberType.Property: return CompletionItemKind.Property;
                case PythonMemberType.Multiple: return CompletionItemKind.Value;
                case PythonMemberType.Keyword: return CompletionItemKind.Keyword;
                case PythonMemberType.CodeSnippet: return CompletionItemKind.Snippet;
                case PythonMemberType.NamedArgument: return CompletionItemKind.Variable;
                default:
                    return CompletionItemKind.None;
            }
        }

    }
}
