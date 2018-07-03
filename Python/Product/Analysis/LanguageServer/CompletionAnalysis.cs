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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class CompletionAnalysis {
        private readonly Node _statement;
        private readonly ScopeStatement _scope;
        private readonly ILogger _log;
        private readonly DocumentationBuilder _textBuilder;
        private readonly Func<TextReader> _openDocument;

        public CompletionAnalysis(
            ModuleAnalysis analysis,
            PythonAst tree,
            SourceLocation position,
            GetMemberOptions opts,
            DocumentationBuilder textBuilder,
            ILogger log,
            Func<TextReader> openDocument
        ) {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Position = position;
            Index = Tree.LocationToIndex(Position);
            Options = opts;
            _textBuilder = textBuilder;
            _log = log;
            _openDocument = openDocument;

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions {
                Names = true,
                Members = true,
                NamedArgumentNames = true,
                ImportNames = true,
                ImportAsNames = true,
                Literals = true,
                Errors = true
            });

            Node node;
            finder.Get(Index, Index, out node, out _statement, out _scope);

            int index = Index;
            int col = Position.Column;
            while (CanBackUp(Tree, node, _statement, _scope, col)) {
                col -= 1;
                index -= 1;
                finder.Get(index, index, out node, out _statement, out _scope);
            }

            Node = node ?? (_statement as ExpressionStatement)?.Expression;
        }

        private static bool CanBackUp(PythonAst tree, Node node, Node statement, ScopeStatement scope, int column) {
            if (node != null || (statement != null && !((statement as ExpressionStatement)?.Expression is ErrorExpression))) {
                return false;
            }

            int top = 1;
            if (scope != null) {
                var scopeStart = scope.GetStart(tree);
                if (scope.Body != null) {
                    top = (scope.Body.GetEnd(tree).Line == scopeStart.Line) ?
                        scope.Body.GetStart(tree).Column :
                        scopeStart.Column;
                } else {
                    top = scopeStart.Column;
                }
            }

            if (column <= top) {
                return false;
            }

            return true;
        }

        private static readonly IEnumerable<CompletionItem> Empty = Enumerable.Empty<CompletionItem>();

        public ModuleAnalysis Analysis { get; }
        public PythonAst Tree { get; }
        public SourceLocation Position { get; }
        public int Index { get; }
        public GetMemberOptions Options { get; set; }
        public SourceSpan? ApplicableSpan { get; set; }

        public bool? ShouldCommitByDefault { get; set; }
        public bool? ShouldAllowSnippets { get; set; }

        public Node Node { get; private set; }
        public Node Statement => _statement;
        public ScopeStatement Scope => _scope;
        /// <summary>
        /// The node that members were returned for, if any.
        /// </summary>
        public Expression ParentExpression { get; private set; }


        private IReadOnlyList<KeyValuePair<IndexSpan, Token>> _tokens;
        private NewLineLocation[] _tokenNewlines;
        private IEnumerable<KeyValuePair<IndexSpan, Token>> Tokens {
            get {
                EnsureTokens();
                return _tokens;
            }
        }

        private SourceSpan GetTokenSpan(IndexSpan span) {
            EnsureTokens();
            return new SourceSpan(
                NewLineLocation.IndexToLocation(_tokenNewlines, span.Start),
                NewLineLocation.IndexToLocation(_tokenNewlines, span.End)
            );
        }

        private void EnsureTokens() {
            if (_tokens != null) {
                return;
            }

            var reader = _openDocument?.Invoke();
            if (reader == null) {
                _log.TraceMessage($"Cannot get completions at error node without sources");
                _tokens = Array.Empty<KeyValuePair<IndexSpan, Token>>();
                _tokenNewlines = Array.Empty<NewLineLocation>();
                return;
            }

            var tokens = new List<KeyValuePair<IndexSpan, Token>>();
            Tokenizer tokenizer;
            using (reader) {
                tokenizer = new Tokenizer(Tree.LanguageVersion, options: TokenizerOptions.GroupingRecovery);
                tokenizer.Initialize(reader);
                for (var t = tokenizer.GetNextToken(); t.Kind != TokenKind.EndOfFile && tokenizer.TokenSpan.Start < Index; t = tokenizer.GetNextToken()) {
                    tokens.Add(new KeyValuePair<IndexSpan, Token>(tokenizer.TokenSpan, t));
                }
            }

            _tokens = tokens;
            _tokenNewlines = tokenizer.GetLineLocations();
        }



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

            var res = GetNoCompletionsInComments() ??
                GetCompletionsFromMembers(ref opts) ??
                GetCompletionsInLiterals() ??
                GetCompletionsInImport(ref opts, ref additional) ??
                GetCompletionsForOverride() ??
                GetCompletionsInDefinition(ref allowKeywords, ref additional) ??
                GetCompletionsInForStatement() ??
                GetCompletionsInWithStatement() ??
                GetCompletionsInRaiseStatement(ref allowArguments, ref opts) ??
                GetCompletionsInExceptStatement(ref allowKeywords, ref opts) ??
                GetCompletionsFromError() ??
                GetCompletionsFromTopLevel(allowKeywords, allowArguments, opts);

            if (additional != null) {
                res = res.Concat(additional);
            }

            if (ReferenceEquals(res, Empty)) {
                return null;
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
            if (Node is MemberExpression me && me.Target != null && me.DotIndex > me.StartIndex && Index > me.DotIndex) {
                _log.TraceMessage($"Completing expression {me.Target.ToCodeString(Tree, CodeFormattingOptions.Traditional)}");
                ParentExpression = me.Target;
                if (!string.IsNullOrEmpty(me.Name)) {
                    Node = new NameExpression(me.Name);
                    Node.SetLoc(me.NameHeader, me.NameHeader + me.Name.Length);
                } else {
                    Node = null;
                }
                ShouldCommitByDefault = true;
                return Analysis.GetMembers(me.Target, Position, opts, null).Select(ToCompletionItem);
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

                var names = dn.Names.TakeWhile(n => Index > n.EndIndex).Select(n => n.Name).ToArray();

                return GetModules(names, includeMembers);
            } else if (name == null || name is NameExpression) {
                return Analysis.ProjectState.GetModules().Select(ToCompletionItem);
            }

            return Empty;
        }

        private void SetApplicableSpanToLastToken(Node containingNode) {
            if (containingNode != null && Index >= containingNode.EndIndex) {
                var token = Tokens.LastOrDefault();
                if (token.Key.End >= Index) {
                    ApplicableSpan = GetTokenSpan(token.Key);
                }
            }
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
                            SetApplicableSpanToLastToken(imp);
                            return Once(AsKeywordCompletion);
                        }
                        if (Index >= t.Item1.StartIndex) {
                            Node = t.Item1.Names.MaybeEnumerate().LastOrDefault(n => n.StartIndex <= Index && Index <= n.EndIndex);
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
                            SetApplicableSpanToLastToken(fimp);
                            return Once(AsKeywordCompletion);
                        }
                        if (Index >= t.Item1.StartIndex) {
                            ApplicableSpan = t.Item1.GetSpan(Tree);
                            var mods = GetModulesFromNode(fimp.Root, true);
                            if (mods.Any() && fimp.Names.Count == 1) {
                                return Once(StarCompletion).Concat(mods);
                            }
                            return mods;
                        }
                    }
                }

                if (fimp.ImportIndex > fimp.StartIndex) {
                    if (Index > fimp.ImportIndex + 6) {
                        if (fimp.Root == null) {
                            return Empty;
                        }
                        var mods = GetModulesFromNode(fimp.Root, true);
                        if (mods.Any() && fimp.Names.Count <= 1) {
                            return Once(StarCompletion).Concat(mods);
                        }
                        return mods;
                    }
                    if (Index >= fimp.ImportIndex) {
                        ApplicableSpan = new SourceSpan(
                            Tree.IndexToLocation(fimp.ImportIndex),
                            Tree.IndexToLocation(Math.Min(fimp.ImportIndex + 6, fimp.EndIndex))
                        );
                        return Once(ImportKeywordCompletion);
                    }
                }

                if (fimp.Root != null) {
                    if (Index > fimp.Root.EndIndex && fimp.Root.EndIndex > fimp.Root.StartIndex) {
                        if (Index > fimp.EndIndex) {
                            // Only end up here for "from ... imp", and "imp" is not counted
                            // as part of our span
                            var token = Tokens.LastOrDefault();
                            if (token.Key.End >= Index) {
                                ApplicableSpan = GetTokenSpan(token.Key);
                            }
                        }
                        return Once(ImportKeywordCompletion);
                    } else if (Index >= fimp.Root.StartIndex) {
                        Node = fimp.Root.Names.MaybeEnumerate().LastOrDefault(n => n.StartIndex <= Index && Index <= n.EndIndex);
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
                ShouldCommitByDefault = false;
                return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));
            }
            return null;
        }

        private CompletionItem ToOverrideCompletionItem(IOverloadResult o, ClassDefinition cd, string indent) {
            return new CompletionItem {
                label = o.Name,
                insertText = MakeOverrideCompletionString(indent, o, cd.Name),
                kind = CompletionItemKind.Method
            };
        }

        private IEnumerable<CompletionItem> GetCompletionsInDefinition(ref bool allowKeywords, ref List<CompletionItem> additional) {
            // Here we work backwards through the various parts of the definitions.
            // When we find that Index is within a part, we return either the available
            // completions 

            if (Statement is FunctionDefinition fd) {
                if (fd.HeaderIndex > fd.StartIndex && Index > fd.HeaderIndex) {
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
                    if (fd.NameExpression.StartIndex > fd.KeywordEndIndex && Index >= fd.NameExpression.StartIndex) {
                        return Empty;
                    }
                }

                if (Index > fd.KeywordEndIndex) {
                    return Empty;
                }

                // Disallow keywords, unless we're between the end of decorators and the
                // end of the "[async] def" keyword.
                allowKeywords = false;
                if (Index <= fd.KeywordEndIndex) {
                    if (fd.Decorators == null || Index >= fd.Decorators.EndIndex) {
                        allowKeywords = true;
                    }
                }
                return null;

            } else if (Statement is ClassDefinition cd) {
                if (cd.HeaderIndex > cd.StartIndex && Index > cd.HeaderIndex) {
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
                    if (cd.NameExpression.StartIndex > cd.KeywordEndIndex && Index >= cd.NameExpression.StartIndex) {
                        return Empty;
                    }
                }

                if (Index > cd.KeywordEndIndex) {
                    return Empty;
                }

                // Disallow keywords, unless we're between the end of decorators and the
                // end of the "[async] def" keyword.
                allowKeywords = false;
                if (Index <= cd.KeywordEndIndex) {
                    if (cd.Decorators == null || Index >= cd.Decorators.EndIndex) {
                        allowKeywords = true;
                    }
                }
                return null;
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInForStatement() {
            if (Statement is ForStatement fs) {
                if (fs.Left != null) {
                    if (fs.InIndex > fs.StartIndex) {
                        if (Index > fs.InIndex + 2) {
                            return null;
                        } else if (Index >= fs.InIndex) {
                            ApplicableSpan = new SourceSpan(Tree.IndexToLocation(fs.InIndex), Tree.IndexToLocation(fs.InIndex + 2));
                            return Once(InKeywordCompletion);
                        }
                    }
                    if (fs.Left.StartIndex > fs.StartIndex && fs.Left.EndIndex > fs.Left.StartIndex && Index > fs.Left.EndIndex) {
                        SetApplicableSpanToLastToken(fs);
                        return Once(InKeywordCompletion);
                    } else if (fs.ForIndex >= fs.StartIndex && Index > fs.ForIndex + 3) {
                        return Empty;
                    }
                }
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsInWithStatement() {
            if (Statement is WithStatement ws) {
                if (Index > ws.HeaderIndex && ws.HeaderIndex > ws.StartIndex) {
                    return null;
                }

                foreach (var item in ws.Items.Reverse().MaybeEnumerate()) {
                    if (item.AsIndex > item.StartIndex) {
                        if (Index > item.AsIndex + 2) {
                            return Empty;
                        } else if (Index >= item.AsIndex) {
                            ApplicableSpan = new SourceSpan(Tree.IndexToLocation(item.AsIndex), Tree.IndexToLocation(item.AsIndex + 2));
                            return Once(AsKeywordCompletion);
                        }
                    }
                    if (item.ContextManager != null && !(item.ContextManager is ErrorExpression)) {
                        if (Index > item.ContextManager.EndIndex && item.ContextManager.EndIndex > item.ContextManager.StartIndex) {
                            return Once(AsKeywordCompletion);
                        } else if (Index >= item.ContextManager.StartIndex) {
                            return null;
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
                            SetApplicableSpanToLastToken(rs);
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
                        SetApplicableSpanToLastToken(ts);
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

        private static bool ShouldIncludeStatementKeywords(Node statement, int index, out IndexSpan? span) {
            span = null;
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
                int keywordStart = s.KeywordEndIndex - s.KeywordLength;
                if (index >= keywordStart) {
                    span = new IndexSpan(keywordStart, s.KeywordLength);
                } else if ((s as IMaybeAsyncStatement)?.IsAsync == true) {
                    // Must be in the "async" at the start of the keyword
                    span = new IndexSpan(s.StartIndex, "async".Length);
                }
                return true;
            }
            // TryStatementHandler is 'except', but not a Statement subclass
            if (statement is TryStatementHandler except && index <= except.KeywordEndIndex) {
                int keywordStart = except.KeywordEndIndex - except.KeywordLength;
                if (index >= keywordStart) {
                    span = new IndexSpan(keywordStart, except.KeywordLength);
                }
                return true;
            }
            // Allow keywords in function body (we'd have a different statement if we were deeper)
            if (statement is FunctionDefinition fd && index >= fd.HeaderIndex) {
                return true;
            }
            // Allow keywords within with blocks, but not in their definition
            if (statement is WithStatement ws) {
                return index >= ws.HeaderIndex || index <= ws.KeywordEndIndex;
            }
            return false;
        }

        private IEnumerable<CompletionItem> GetNoCompletionsInComments() {
            if (Node == null) {
                int match = Array.BinarySearch(Tree._commentLocations, Position);
                if (match < 0) {
                    // If our index = -1, it means we're before the first comment
                    if (match == -1) {
                        return null;
                    }
                    // If we couldn't find an exact match for this position, get the nearest
                    // matching comment before this point
                    match = ~match - 1;
                }
                if (match < 0 || match >= Tree._commentLocations.Length) {
                    Debug.Fail("Failed to find nearest preceding comment in AST");
                    return null;
                }

                if (Tree._commentLocations[match].Line == Position.Line &&
                    Tree._commentLocations[match].Column < Position.Column) {
                    // We are inside a comment
                    return Empty;
                }
            }
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromError() {
            if (!(Node is ErrorExpression)) {
                return null;
            }

            if (Statement is AssignmentStatement assign && Node == assign.Right) {
                return null;
            }

            var tokens = Tokens.Reverse().ToArray();

            string exprString;
            var lastToken = tokens.FirstOrDefault();
            var nextLast = tokens.ElementAtOrDefault(1).Value?.Kind ?? TokenKind.EndOfFile;
            switch (lastToken.Value.Kind) {
                case TokenKind.Dot:
                    exprString = ReadExpression(tokens.Skip(1));
                    if (exprString != null) {
                        ApplicableSpan = new SourceSpan(Position, Position);
                        return Analysis.GetMembers(exprString, Position, Options).Select(ToCompletionItem);
                    }
                    break;
                case TokenKind.KeywordDef:
                    if (lastToken.Key.End < Index) {
                        var cd = Scope as ClassDefinition ?? ((Scope as FunctionDefinition)?.Parent as ClassDefinition);
                        if (cd == null) {
                            return null;
                        }

                        ApplicableSpan = new SourceSpan(Position, Position);

                        var loc = GetTokenSpan(lastToken.Key).Start;
                        ShouldCommitByDefault = false;
                        return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));
                    }
                    break;
                case TokenKind.Name:
                        if (nextLast == TokenKind.Dot) {
                        exprString = ReadExpression(tokens.Skip(2));
                            if (exprString != null) {
                            ApplicableSpan = new SourceSpan(GetTokenSpan(lastToken.Key).Start, Position);
                                return Analysis.GetMembers(exprString, Position, Options).Select(ToCompletionItem);
                            }
                        } else if (nextLast == TokenKind.KeywordDef) {
                            var cd = Scope as ClassDefinition ?? ((Scope as FunctionDefinition)?.Parent as ClassDefinition);
                            if (cd == null) {
                                return null;
                            }

                        ApplicableSpan = new SourceSpan(GetTokenSpan(lastToken.Key).Start, Position);

                        var loc = GetTokenSpan(tokens.ElementAt(1).Key).Start;
                            ShouldCommitByDefault = false;
                            return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));
                        }
                    break;
                case TokenKind.KeywordFor:
                case TokenKind.KeywordAs:
                    if (lastToken.Key.Start <= Index && Index <= lastToken.Key.End) {
                        return null;
                    }
                    return Empty;
            }

            Debug.WriteLine($"Unhandled completions from error.\nTokens were: ({lastToken.Value.Image}:{lastToken.Value.Kind}), {string.Join(", ", tokens.AsEnumerable().Take(10).Select(t => $"({t.Value.Image}:{t.Value.Kind})"))}");
            return null;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromTopLevel(bool allowKeywords, bool allowArguments, GetMemberOptions opts) {
            if (Node?.EndIndex < Index) {
                return Empty;
            }

            if (allowKeywords) {
                opts |= GetMemberOptions.IncludeExpressionKeywords;
                if (ShouldIncludeStatementKeywords(Statement, Index, out var span)) {
                    opts |= GetMemberOptions.IncludeStatementKeywords;
                    if (span.HasValue) {
                        ApplicableSpan = new SourceSpan(
                            Tree.IndexToLocation(span.Value.Start),
                            Tree.IndexToLocation(span.Value.End)
                        );
                    }
                }
                ShouldAllowSnippets = true;
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

            return members.Select(ToCompletionItem).Where(c => !string.IsNullOrEmpty(c.insertText));
        }


        private static readonly CompletionItem MetadataArgCompletion = ToCompletionItem("metaclass=", PythonMemberType.NamedArgument);
        private static readonly CompletionItem AsKeywordCompletion = ToCompletionItem("as", PythonMemberType.Keyword);
        private static readonly CompletionItem FromKeywordCompletion = ToCompletionItem("from", PythonMemberType.Keyword);
        private static readonly CompletionItem InKeywordCompletion = ToCompletionItem("in", PythonMemberType.Keyword);
        private static readonly CompletionItem ImportKeywordCompletion = ToCompletionItem("import", PythonMemberType.Keyword);
        private static readonly CompletionItem WithKeywordCompletion = ToCompletionItem("with", PythonMemberType.Keyword);
        private static readonly CompletionItem StarCompletion = ToCompletionItem("*", PythonMemberType.Keyword);

        private static CompletionItem KeywordCompletion(string keyword) => new CompletionItem {
            label = keyword,
            insertText = keyword,
            kind = CompletionItemKind.Keyword,
            _kind = PythonMemberType.Keyword.ToString().ToLowerInvariant()
        };

        private CompletionItem ToCompletionItem(MemberResult m) {
            var completion = m.Completion;
            if (string.IsNullOrEmpty(completion)) {
                completion = m.Name;
            }
            if (string.IsNullOrEmpty(completion)) {
                return default(CompletionItem);
            }
            var doc = _textBuilder.GetDocumentation(m.Values, string.Empty);
            var res = new CompletionItem {
                label = m.Name,
                insertText = completion,
                documentation = string.IsNullOrWhiteSpace(doc) ? null : new MarkupContent {
                    kind = _textBuilder.DisplayOptions.preferredFormat,
                    value = doc
                },
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(completion, 0) ? "1" : "2",
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

        private string MakeOverrideCompletionString(string indentation, IOverloadResult result, string className) {
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

        private string ReadExpression(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            var expr = ReadExpressionTokens(tokens);

            return string.Join("", expr.Select(e => e.VerbatimImage ?? e.Image));
        }

        private IEnumerable<Token> ReadExpressionTokens(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            int nesting = 0;
            var exprTokens = new Stack<Token>();
            int currentLine = -1;

            foreach (var t in tokens) {
                var p = GetTokenSpan(t.Key).Start;
                if (p.Line > currentLine) {
                    currentLine = p.Line;
                } else if (p.Line < currentLine && nesting == 0) {
                    break;
                }

                exprTokens.Push(t.Value);

                switch (t.Value.Kind) {
                    case TokenKind.RightParenthesis:
                    case TokenKind.RightBracket:
                    case TokenKind.RightBrace:
                        nesting += 1;
                        break;
                    case TokenKind.LeftParenthesis:
                    case TokenKind.LeftBracket:
                    case TokenKind.LeftBrace:
                        if (--nesting < 0) {
                            exprTokens.Pop();
                            return exprTokens;
                        }
                        break;

                    case TokenKind.Comment:
                        exprTokens.Pop();
                        break;

                    case TokenKind.Name:
                    case TokenKind.Constant:
                    case TokenKind.Dot:
                    case TokenKind.Ellipsis:
                    case TokenKind.MatMultiply:
                    case TokenKind.KeywordAwait:
                        break;

                    case TokenKind.Assign:
                    case TokenKind.LeftShiftEqual:
                    case TokenKind.RightShiftEqual:
                    case TokenKind.BitwiseAndEqual:
                    case TokenKind.BitwiseOrEqual:
                    case TokenKind.ExclusiveOrEqual:
                        exprTokens.Pop();
                        return exprTokens;

                    default:
                        if (t.Value.Kind >= TokenKind.FirstKeyword || nesting == 0) {
                            exprTokens.Pop();
                            return exprTokens;
                        }
                        break;
                }
            }

            return exprTokens;
        }
    }
}
