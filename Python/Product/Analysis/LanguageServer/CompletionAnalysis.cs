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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class CompletionAnalysis {
        private readonly Node _node;
        private readonly Node _statement;
        private readonly ScopeStatement _scope;
        private readonly ILogger _log;

        public CompletionAnalysis(ModuleAnalysis analysis, PythonAst tree, SourceLocation position, GetMemberOptions opts, ILogger log) {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Position = position;
            Index = Tree.LocationToIndex(Position);
            Options = opts;
            _log = log;

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions {
                Names = true,
                MemberName = true,
                NamedArgumentNames = true,
                ImportNames = true,
                ImportAsNames = true,
                Literals = true,
            });
            finder.Get(Index, Index, out _node, out _statement, out _scope);
        }

        private static readonly IEnumerable<CompletionItem> Empty = Enumerable.Empty<CompletionItem>();

        public ModuleAnalysis Analysis { get; }
        public PythonAst Tree { get; }
        public SourceLocation Position { get; }
        public int Index { get; }
        public GetMemberOptions Options { get; set; }

        public Node Node => _node;
        public Node Statement => _statement;
        public ScopeStatement Scope => _scope;
        /// <summary>
        /// The node that members were returned for, if any.
        /// </summary>
        public Expression ParentExpression { get; private set; }

        public IEnumerable<CompletionItem> GetCompletionsFromString(string expr) {
            if (string.IsNullOrEmpty(expr)) {
                return null;
            }
            _log.TraceMessage($"Completing expression '{expr}'");
            return Analysis.GetMembers(expr, Position, Options).Select(ToCompletionItem);
        }

        public IEnumerable<CompletionItem> GetCompletions() {
            var opts = Options | GetMemberOptions.ForEval;
            bool allowKeywords = true, allowArguments = true;
            List<CompletionItem> additional = null;

            var res = GetCompletionsFromMembers(ref opts) ??
                GetCompletionsInLiterals() ??
                GetCompletionsInImport(ref opts, ref additional) ??
                GetCompletionsForOverride() ??
                GetCompletionsInDefinition(ref allowKeywords, ref additional) ??
                GetCompletionsInForStatement() ??
                GetCompletionsInWithStatement() ??
                GetCompletionsInRaiseStatement(ref allowArguments, ref opts) ??
                GetCompletionsInExceptStatement(ref allowKeywords, ref opts) ??
                GetCompletionsFromTopLevel(allowKeywords, allowArguments, opts);

            if (additional != null) {
                res = res.Concat(additional);
            }

            return res;
        }

        private static IEnumerable<CompletionItem> Once(CompletionItem item) {
            yield return item;
        }

        private static IEnumerable<Tuple<T1, T2>> ZipLongest<T1, T2>(IEnumerable<T1> src1, IEnumerable<T2> src2, T1 default1 = default(T1), T2 default2 = default(T2)) {
            using (var e1 = src1?.GetEnumerator())
            using (var e2 = src2?.GetEnumerator()) {
                bool b1 = e1?.MoveNext() ?? false, b2 = e2?.MoveNext() ?? false;
                while (b1 && b2) {
                    yield return Tuple.Create(e1.Current, e2.Current);
                    b1 = e1.MoveNext();
                    b2 = e2.MoveNext();
                }
                while (b1) {
                    yield return Tuple.Create(e1.Current, default2);
                    b1 = e1.MoveNext();
                }
                while (b2) {
                    yield return Tuple.Create(default1, e2.Current);
                    b2 = e2.MoveNext();
                }
            }
        }

        private IEnumerable<CompletionItem> GetCompletionsFromMembers(ref GetMemberOptions opts) {
            var finder = new ExpressionFinder(Tree, GetExpressionOptions.EvaluateMembers);
            if (finder.GetExpression(Index) is Expression expr) {
                _log.TraceMessage($"Completing expression {expr.ToCodeString(Tree, CodeFormattingOptions.Traditional)}");
                ParentExpression = expr;
                return Analysis.GetMembers(expr, Position, opts, null).Select(ToCompletionItem);
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInLiterals() {
            if (Node is ConstantExpression ce && ce.Value != null) {
                return Empty;
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetModules(IEnumerable<string> names, bool includeMembers) {
            if (names != null && names.Any()) {
                return Analysis.ProjectState.GetModuleMembers(Analysis.InterpreterContext, names.ToArray(), includeMembers)
                    .Select(ToCompletionItem);
            }
            return Analysis.ProjectState.GetModules().Select(ToCompletionItem);
        }

        private IEnumerable<CompletionItem> GetModulesFromNode(Node name, bool includeMembers = false) {
            if (name is DottedName dn) {
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

                
                return GetModules(names, includeMembers);
            } else if (name == null || name is NameExpression) {
                return Analysis.ProjectState.GetModules().Select(ToCompletionItem);
            }

            return Empty;
        }

        private IEnumerable<CompletionItem> GetCompletionsInImport(ref GetMemberOptions opts, ref List<CompletionItem> additional) {
            if (Statement is ImportStatement imp) {
                if (imp.Names == null || imp.Names.Count == 0) {
                    // No names, so if we're at the end return modules
                    if (Index > imp.KeywordEndIndex) {
                        return GetModulesFromNode(null);
                    }
                }
                foreach (var t in ZipLongest(imp.Names, imp.AsNames).Reverse()) {
                    if (t.Item2 != null) {
                        if (Index >= t.Item2.StartIndex) {
                            return Empty;
                        }
                    }
                    if (t.Item1 != null) {
                        if (Index > t.Item1.EndIndex && t.Item1.EndIndex > t.Item1.StartIndex) {
                            return Once(AsKeywordCompletion);
                        }
                        if (Index >= t.Item1.StartIndex) {
                            return GetModulesFromNode(t.Item1);
                        }
                    }
                }

                opts |= GetMemberOptions.IncludeStatementKeywords;
            } else if (Statement is FromImportStatement fimp) {
                // No more completions after '*', ever!
                if (fimp.Names?.Any(n => n?.Name == "*" && Index > n.EndIndex) ?? false) {
                    return Empty;
                }

                foreach (var t in ZipLongest(fimp.Names, fimp.AsNames).Reverse()) {
                    if (t.Item2 != null) {
                        if (Index >= t.Item2.StartIndex) {
                            return Empty;
                        }
                    }
                    if (t.Item1 != null) {
                        if (Index > t.Item1.EndIndex && t.Item1.EndIndex > t.Item1.StartIndex) {
                            return Once(AsKeywordCompletion);
                        }
                        if (Index >= t.Item1.StartIndex) {
                            var mods = GetModulesFromNode(fimp.Root, true);
                            if (mods.Any() && fimp.Names.Count == 1) {
                                return Once(StarCompletion).Concat(mods);
                            }
                            return mods;
                        }
                    }
                }

                if (fimp.Root != null) {
                    if (Index > fimp.Root.EndIndex && fimp.Root.EndIndex > fimp.Root.StartIndex) {
                        return Once(ImportKeywordCompletion);
                    } else if (Index >= fimp.Root.StartIndex) {
                        return GetModulesFromNode(fimp.Root);
                    }
                }

                if (Index > fimp.KeywordEndIndex) {
                    return GetModulesFromNode(null);
                }

                opts |= GetMemberOptions.IncludeStatementKeywords;
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsForOverride() {
            if (Statement is FunctionDefinition fd && fd.Parent is ClassDefinition cd && string.IsNullOrEmpty(fd.Name)) {
                if (fd.NameExpression == null || Index <= fd.NameExpression.StartIndex) {
                    return null;
                }
                var loc = fd.GetStart(Tree);
                return Analysis.GetOverrideable(loc)
                    .Select(o => new CompletionItem {
                        label = o.Name,
                        insertText = MakeCompletionString(new string(' ', loc.Column - 1), o, cd.Name),
                        kind = CompletionItemKind.Method
                    });
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInDefinition(ref bool allowKeywords, ref List<CompletionItem> additional) {
            // Here we work backwards through the various parts of the definitions.
            // When we find that Index is within a part, we return either the available
            // completions 

            if (Statement is FunctionDefinition fd) {
                if (Index > fd.HeaderIndex) {
                    return null;
                } else if (Index == fd.HeaderIndex) {
                    return Empty;
                }

                foreach (var p in fd.ParametersInternal.MaybeEnumerate().Reverse()) {
                    if (Index >= p.StartIndex) {
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
                }

                if (fd.NameExpression != null) {
                    if (Index >= fd.NameExpression.StartIndex) {
                        return Empty;
                    }
                }

                allowKeywords = false;
                return null;

            } else if (Statement is ClassDefinition cd) {
                if (Index > cd.HeaderIndex) {
                    return null;
                } else if (Index == cd.HeaderIndex) {
                    return Empty;
                }

                if (cd.BasesInternal != null && cd.BasesInternal.Length > 0 && Index >= cd.BasesInternal[0].StartIndex) {
                    foreach (var p in cd.BasesInternal.Reverse()) {
                        if (Index >= p.StartIndex) {
                            if (p.Name == null && Tree.LanguageVersion.Is3x() && !cd.BasesInternal.Any(b => b.Name == "metaclass")) {
                                additional = additional ?? new List<CompletionItem>();
                                additional.Add(MetadataArgCompletion);
                            }
                            return null;
                        }
                    }
                }

                if (cd.NameExpression != null) {
                    if (Index >= cd.NameExpression.StartIndex) {
                        return Empty;
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

        private IEnumerable<CompletionItem> GetCompletionsInRaiseStatement(ref bool allowKeywords, ref GetMemberOptions opts) {
            if (Statement is RaiseStatement rs) {
                // raise Type, Value, Traceback with Cause
                if (rs.Cause != null) {
                    if (Index >= rs.CauseFieldStartIndex) {
                        return null;
                    }
                }
                if (rs.Traceback != null) {
                    if (Index >= rs.TracebackFieldStartIndex) {
                        return null;
                    }
                }
                if (rs.Value != null) {
                    if (Index >= rs.ValueFieldStartIndex) {
                        return null;
                    }
                }
                if (rs.ExceptType != null) {
                    if (Index > rs.ExceptType.EndIndex) {
                        if (Tree.LanguageVersion.Is3x()) {
                            return Once(FromKeywordCompletion);
                        }
                        return Empty;
                    } else if (Index >= rs.ExceptType.StartIndex) {
                        opts |= GetMemberOptions.ExceptionsOnly;
                        return null;
                    }
                }
                if (Index > rs.KeywordEndIndex) {
                    opts |= GetMemberOptions.ExceptionsOnly;
                    return null;
                }
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInExceptStatement(ref bool allowKeywords, ref GetMemberOptions opts) {
            if (Statement is TryStatementHandler ts) {
                // except Test as Target
                if (ts.Target != null) {
                    if (Index >= ts.Target.StartIndex) {
                        return Empty;
                    }
                }

                if (ts.Test is TupleExpression te) {
                    opts |= GetMemberOptions.ExceptionsOnly;
                    allowKeywords = false;
                    return null;
                } else if (ts.Test != null) {
                    if (Index > ts.Test.EndIndex) {
                        return Once(AsKeywordCompletion);
                    } else if (Index >= ts.Test.StartIndex) {
                        opts |= GetMemberOptions.ExceptionsOnly;
                        allowKeywords = false;
                        return null;
                    }
                }
            }
            return null;
        }

        private static bool ShouldIncludeStatementKeywords(Node statement, int index) {
            if (statement == null) {
                return true;
            }
            // Always allow keywords in non-keyword statements
            if (statement is ExpressionStatement) {
                return true;
            }
            // Allow keywords at start of assignment, but not in subsequent names
            if (statement is AssignmentStatement ss) {
                var firstAssign = ss.Left?.FirstOrDefault();
                return firstAssign == null || index <= firstAssign.EndIndex;
            }
            // Allow keywords when we are in another keyword
            if (statement is Statement s && index <= s.KeywordEndIndex) {
                return true;
            }
            // TryStatementHandler is 'except', but not a Statement subclass
            if (statement is TryStatementHandler except && index <= except.KeywordEndIndex) {
                return true;
            }
            return false;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromTopLevel(bool allowKeywords, bool allowArguments, GetMemberOptions opts) {
            if (allowKeywords) {
                opts |= GetMemberOptions.IncludeExpressionKeywords;
                if (ShouldIncludeStatementKeywords(Statement, Index)) {
                    opts |= GetMemberOptions.IncludeStatementKeywords;
                }
            }

            _log.TraceMessage($"Completing all names");
            var members = Analysis.GetAllAvailableMembers(Position, opts);

            if (allowArguments) {
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
                    _log.TraceMessage($"Including {argNames.Count()} named arguments");

                    members = members?.Concat(argNames) ?? argNames;
                }
            }

            return members.Select(ToCompletionItem);
        }


        private static readonly CompletionItem MetadataArgCompletion = ToCompletionItem("metaclass=", PythonMemberType.NamedArgument);
        private static readonly CompletionItem AsKeywordCompletion = ToCompletionItem("as", PythonMemberType.Keyword);
        private static readonly CompletionItem FromKeywordCompletion = ToCompletionItem("from", PythonMemberType.Keyword);
        private static readonly CompletionItem ImportKeywordCompletion = ToCompletionItem("import", PythonMemberType.Keyword);
        private static readonly CompletionItem WithKeywordCompletion = ToCompletionItem("with", PythonMemberType.Keyword);
        private static readonly CompletionItem StarCompletion = ToCompletionItem("*", PythonMemberType.Keyword);

        private static CompletionItem KeywordCompletion(string keyword) => new CompletionItem {
            label = keyword,
            insertText = keyword,
            kind = CompletionItemKind.Keyword,
            _kind = PythonMemberType.Keyword.ToString().ToLowerInvariant()
        };

        private static CompletionItem ToCompletionItem(MemberResult m) {
            var res = new CompletionItem {
                label = m.Name,
                insertText = m.Completion,
                documentation = m.Documentation,
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(m.Completion, 0) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            return res;
        }

        private static CompletionItem ToCompletionItem(string text, PythonMemberType type, string label = null) {
            return new CompletionItem {
                label = label ?? text,
                insertText = text,
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(text, 0) ? "1" : "2",
                kind = ToCompletionItemKind(type),
                _kind = type.ToString().ToLowerInvariant()
            };
        }

        private static CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
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


        private static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static string GetSafeParameterName(ParameterResult result, int index) {
            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                return GetSafeArgumentName(result, index) + " = " + result.DefaultValue;
            }
            return GetSafeArgumentName(result, index);
        }

        private static string GetSafeArgumentName(ParameterResult result, int index) {
            var match = ValidParameterName.Match(result.Name);

            if (match.Success) {
                return match.Value;
            } else if (result.Name.StartsWithOrdinal("**")) {
                return "**kwargs";
            } else if (result.Name.StartsWithOrdinal("*")) {
                return "*args";
            } else {
                return "arg" + index.ToString();
            }
        }

        private string MakeCompletionString(string indentation, IOverloadResult result, string className) {
            var sb = new StringBuilder();

            sb.AppendLine(result.Name + "(" + string.Join(", ", result.Parameters.Select((p, i) => GetSafeParameterName(p, i))) + "):");

            sb.Append(indentation);
            sb.Append('\t');

            if (result.Parameters.Length > 0) {
                var parameterString = string.Join(", ", result.Parameters.Skip(1).Select((p, i) => GetSafeArgumentName(p, i + 1)));

                if (Tree.LanguageVersion.Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        result.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        result.Parameters.First().Name,
                        result.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }

    }
}
