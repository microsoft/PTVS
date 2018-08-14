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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Parsing {
    public class Parser {
        // immutable properties:
        private readonly Tokenizer _tokenizer;

        // mutable properties:
        private ErrorSink _errors;

        /// <summary>
        /// Language features initialized on parser construction and possibly updated during parsing. 
        /// The code can set the language features (e.g. "from __future__ import division").
        /// </summary>
        private FutureOptions _languageFeatures;
        private readonly PythonLanguageVersion _langVersion;
        // state:
        private TokenWithSpan _token;
        private TokenWithSpan _lookahead, _lookahead2;
        private Stack<FunctionDefinition> _functions;
        private int _classDepth;
        private bool _fromFutureAllowed;
        private string _privatePrefix;
        private bool _parsingStarted, _allowIncomplete;
        private bool _inLoop, _inFinally, _isGenerator, _inGeneratorExpression;
        private List<IndexSpan> _returnsWithValue;
        private int _errorCode;
        private readonly bool _verbatim;                            // true if we're in verbatim mode and the ASTs can be turned back into source code, preserving white space / comments
        private readonly bool _bindReferences;                      // true if we should bind the references in the ASTs
        private string _tokenWhiteSpace, _lookaheadWhiteSpace;      // the whitespace for the current and lookahead tokens as provided from the parser
        private string _lookahead2WhiteSpace;
        private Dictionary<Node, Dictionary<object, object>> _attributes = new Dictionary<Node, Dictionary<object, object>>();  // attributes for each node, currently just round tripping information

        private bool _alwaysAllowContextDependentSyntax;
        private bool _stubFile;

        private static Encoding _utf8throwing;
        private static Regex _codingRegex;

        #region Construction

        private Parser(Tokenizer tokenizer, ErrorSink errorSink, PythonLanguageVersion langVersion, bool verbatim, bool bindRefs, string privatePrefix) {
            Contract.Assert(tokenizer != null);
            Contract.Assert(errorSink != null);

            tokenizer.ErrorSink = new TokenizerErrorSink(this);

            _tokenizer = tokenizer;
            _errors = errorSink;
            _langVersion = langVersion;
            _verbatim = verbatim;
            _bindReferences = bindRefs;

            Reset(FutureOptions.None);

            if (langVersion.Is3x()) {
                // 3.x always does true division and absolute import
                _languageFeatures |= FutureOptions.TrueDivision | FutureOptions.AbsoluteImports;
            }

            _privatePrefix = privatePrefix;
        }

        public static Parser CreateParser(TextReader reader, PythonLanguageVersion version) {
            return CreateParser(reader, version, null);
        }

        public static Parser CreateParser(TextReader reader, PythonLanguageVersion version, ParserOptions parserOptions) {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }

            var options = parserOptions ?? ParserOptions.Default;

            Parser parser = null;
            var tokenizer = new Tokenizer(
                version, options.ErrorSink,
                (options.Verbatim ? TokenizerOptions.Verbatim : TokenizerOptions.None) |
                    TokenizerOptions.GroupingRecovery |
                    (options.StubFile ? TokenizerOptions.StubFile : 0),
                (span, text) => options.RaiseProcessComment(parser, new CommentEventArgs(span, text)));
            tokenizer.Initialize(null, reader, SourceLocation.MinValue);
            tokenizer.IndentationInconsistencySeverity = options.IndentationInconsistencySeverity;

            parser = new Parser(
                tokenizer,
                options.ErrorSink ?? ErrorSink.Null,
                version,
                options.Verbatim,
                options.BindReferences,
                options.PrivatePrefix
            ) { _stubFile = options.StubFile };

            return parser;
        }

        public static Parser CreateParser(Stream stream, PythonLanguageVersion version) {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }

            return CreateParser(stream, version, null);
        }

        /// <summary>
        /// Creates a new parser from a seekable stream including scanning the BOM or looking for a # coding: comment to detect the appropriate coding.
        /// </summary>
        public static Parser CreateParser(Stream stream, PythonLanguageVersion version, ParserOptions parserOptions = null) {
            var options = parserOptions ?? ParserOptions.Default;

            var defaultEncoding = version.Is2x() ? PythonAsciiEncoding.Instance : Encoding.UTF8;

            var reader = GetStreamReaderWithEncoding(stream, defaultEncoding, options.ErrorSink);

            return CreateParser(reader, version, options);
        }

        #endregion

        #region Public parser interface

        //single_input: Newline | simple_stmt | compound_stmt Newline
        //eval_input: testlist Newline* ENDMARKER
        //file_input: (Newline | stmt)* ENDMARKER
        public PythonAst ParseFile() {
            return ParseFileWorker();
        }

        //[stmt_list] Newline | compound_stmt Newline
        //stmt_list ::= simple_stmt (";" simple_stmt)* [";"]
        //compound_stmt: if_stmt | while_stmt | for_stmt | try_stmt | funcdef | classdef
        //Returns a simple or coumpound_stmt or null if input is incomplete
        /// <summary>
        /// Parse one or more lines of interactive input
        /// </summary>
        /// <returns>null if input is not yet valid but could be with more lines</returns>
        public PythonAst ParseInteractiveCode(out ParseResult properties) {
            bool parsingMultiLineCmpdStmt;
            bool isEmptyStmt = false;

            properties = ParseResult.Complete;

            StartParsing();
            Statement ret = InternalParseInteractiveInput(out parsingMultiLineCmpdStmt, out isEmptyStmt);

            if (_errorCode == 0) {
                if (isEmptyStmt) {
                    properties = ParseResult.Empty;
                } else if (parsingMultiLineCmpdStmt) {
                    properties = ParseResult.IncompleteStatement;
                }

                if (isEmptyStmt) {
                    return null;
                }

                return CreateAst(ret);
            } else {
                if ((_errorCode & ErrorCodes.IncompleteMask) != 0) {
                    if ((_errorCode & ErrorCodes.IncompleteToken) != 0) {
                        properties = ParseResult.IncompleteToken;
                        return null;
                    }

                    if ((_errorCode & ErrorCodes.IncompleteStatement) != 0) {
                        if (parsingMultiLineCmpdStmt) {
                            properties = ParseResult.IncompleteStatement;
                        } else {
                            properties = ParseResult.IncompleteToken;
                        }
                        return null;
                    }
                }

                properties = ParseResult.Invalid;
                return null;
            }
        }

        private PythonAst CreateAst(Statement ret) {
            var ast = new PythonAst(ret, _tokenizer.GetLineLocations(), _tokenizer.LanguageVersion, _tokenizer.GetCommentLocations());
            ast.HasVerbatim = _verbatim;
            ast.PrivatePrefix = _privatePrefix;
            if (_token.Token != null) {
                ast.SetLoc(0, GetEndForStatement());
            }
            if (_verbatim) {
                AddExtraVerbatimText(ast, _lookaheadWhiteSpace + _lookahead.Token.VerbatimImage);
            }

            ast.SetAttributes(_attributes);
            PythonNameBinder.BindAst(_langVersion, ast, _errors, _bindReferences);

            return ast;
        }

        public PythonAst ParseSingleStatement() {
            StartParsing();

            MaybeEatNewLine();
            Statement statement = ParseStmt();
            EatEndOfInput();
            return CreateAst(statement);
        }

        public PythonAst ParseTopExpression() {
            // TODO: move from source unit  .TrimStart(' ', '\t')
            _alwaysAllowContextDependentSyntax = true;
            ReturnStatement ret = new ReturnStatement(ParseTestListAsExpression());
            _alwaysAllowContextDependentSyntax = false;
            ret.SetLoc(0, 0);
            return CreateAst(ret);
        }

        internal ErrorSink ErrorSink {
            get {
                return _errors;
            }
            set {
                Contract.Assert(value != null);
                _errors = value;
            }
        }

        public int ErrorCode {
            get { return _errorCode; }
        }

        public void Reset(FutureOptions languageFeatures) {
            _languageFeatures = languageFeatures;
            _token = new TokenWithSpan();
            _lookahead = new TokenWithSpan();
            _fromFutureAllowed = true;
            _classDepth = 0;
            _functions = null;
            _privatePrefix = null;

            _parsingStarted = false;
            _errorCode = 0;
        }

        public void Reset() {
            Reset(_languageFeatures);
        }

        #endregion

        #region Error Reporting

        private void ReportSyntaxError(TokenWithSpan t) {
            ReportSyntaxError(t, ErrorCodes.SyntaxError);
        }

        private void ReportSyntaxError(TokenWithSpan t, int errorCode) {
            ReportSyntaxError(t.Token, t.Span, errorCode, true);
        }

        private void ReportSyntaxError(Token t, IndexSpan span, int errorCode, bool allowIncomplete) {
            var start = span.Start;
            var end = span.End;

            if (allowIncomplete && (t.Kind == TokenKind.EndOfFile || (_tokenizer.IsEndOfFile && (t.Kind == TokenKind.Dedent || t.Kind == TokenKind.NLToken)))) {
                errorCode |= ErrorCodes.IncompleteStatement;
            }

            string msg = GetErrorMessage(t, errorCode);

            ReportSyntaxError(start, end, msg, errorCode);
        }

        private static string GetErrorMessage(Token t, int errorCode) {
            string msg;
            if ((errorCode & ~ErrorCodes.IncompleteMask) == ErrorCodes.IndentationError) {
                msg = "expected an indented block";
            } else if (t.Kind != TokenKind.EndOfFile) {
                msg = "unexpected token '{0}'".FormatUI(t.Image);
            } else {
                msg = "unexpected EOF while parsing";
            }

            return msg;
        }

        private void ReportSyntaxError(string message) {
            ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, message);
        }

        internal void ReportSyntaxError(int start, int end, string message) {
            ReportSyntaxError(start, end, message, ErrorCodes.SyntaxError);
        }

        internal void ReportSyntaxError(int start, int end, string message, int errorCode) {
            // save the first one, the next error codes may be induced errors:
            if (_errorCode == 0) {
                _errorCode = errorCode;
            }
            _errors.Add(
                message,
                _tokenizer.GetLineLocations(),
                start, end,
                errorCode,
                Severity.FatalError);
        }

        #endregion

        #region LL(1) Parsing

        private static bool IsPrivateName(string name) {
            return name.StartsWithOrdinal("__") && !name.EndsWithOrdinal("__");
        }

        private string FixName(string name) {
            if (_privatePrefix != null && IsPrivateName(name)) {
                name = "_" + _privatePrefix + name;
            }

            return name;
        }

        private Name ReadNameMaybeNone() {
            // peek for better error recovery
            Token t = PeekToken();
            if (t == Tokens.NoneToken) {
                NextToken();
                return Name.None;
            }

            var n = TokenToName(t);
            if (n.HasName) {
                NextToken();
                return n;
            }

            ReportSyntaxError("syntax error");
            return Name.Empty;
        }

        struct Name {
            public readonly string RealName;
            public readonly string VerbatimName;

            public static readonly Name Empty = new Name();
            public static readonly Name Async = new Name("async", "async");
            public static readonly Name Await = new Name("await", "await");
            public static readonly Name None = new Name("None", "None");

            public Name(string name, string verbatimName) {
                RealName = name;
                VerbatimName = verbatimName;
            }

            public bool HasName {
                get {
                    return RealName != null;
                }
            }
        }

        private Name ReadName() {
            var n = TokenToName(PeekToken());
            if (n.HasName) {
                NextToken();
            } else {
                ReportSyntaxError(_lookahead);
            }
            return n;
        }

        private Name TokenToName(Token t) {
            if (!AllowAsyncAwaitSyntax) {
                if (t.Kind == TokenKind.KeywordAwait) {
                    return Name.Await;
                } else if (t.Kind == TokenKind.KeywordAsync) {
                    return Name.Async;
                }
            }
            var n = t as NameToken;
            if (n != null) {
                return new Name(FixName(n.Name), n.Name);
            }
            return Name.Empty;
        }

        private bool AllowReturnSyntax {
            get {
                return _alwaysAllowContextDependentSyntax ||
                    CurrentFunction != null;
            }
        }

        private bool AllowYieldSyntax {
            get {
                FunctionDefinition cf;
                if (_alwaysAllowContextDependentSyntax) {
                    return true;
                }
                if ((cf = CurrentFunction) == null) {
                    return false;
                }
                if (_langVersion >= PythonLanguageVersion.V36) {
                    return true;
                }
                if (!cf.IsCoroutine) {
                    return true;
                }
                return false;
            }
        }

        private bool AllowAsyncAwaitSyntax {
            get {
                FunctionDefinition cf;
                return _alwaysAllowContextDependentSyntax ||
                    ((cf = CurrentFunction) != null && cf.IsCoroutine);
            }
        }

        //stmt: simple_stmt | compound_stmt
        //compound_stmt: if_stmt | while_stmt | for_stmt | try_stmt | funcdef | classdef
        private Statement ParseStmt() {
            switch (PeekToken().Kind) {
                case TokenKind.KeywordIf:
                    return ParseIfStmt();
                case TokenKind.KeywordWhile:
                    return ParseWhileStmt();
                case TokenKind.KeywordFor:
                    return ParseForStmt(isAsync: false);
                case TokenKind.KeywordTry:
                    return ParseTryStatement();
                case TokenKind.At:
                    return ParseDecorated();
                case TokenKind.KeywordDef:
                    return ParseFuncDef(isCoroutine: false);
                case TokenKind.KeywordClass:
                    return ParseClassDef();
                case TokenKind.KeywordWith:
                    return ParseWithStmt(isAsync: false);
                case TokenKind.KeywordAsync:
                    return ParseAsyncStmt();
                default:
                    return ParseSimpleStmt();
            }
        }

        private Statement ParseAsyncStmt() {
            var token2 = PeekToken2();
            if (token2.Kind == TokenKind.KeywordDef) {
                Eat(TokenKind.KeywordAsync);
                return ParseFuncDef(isCoroutine: true);
            }

            if (!AllowAsyncAwaitSyntax) {
                // 'async', outside coroutine, and not followed by def, is a
                // regular name
                return ParseSimpleStmt();
            }

            NextToken();

            switch (PeekToken().Kind) {
                case TokenKind.KeywordFor:
                    return ParseForStmt(isAsync: true);
                case TokenKind.KeywordWith:
                    return ParseWithStmt(isAsync: true);
            }

            ReportSyntaxError("syntax error");
            return ParseStmt();
        }

        //simple_stmt: small_stmt (';' small_stmt)* [';'] Newline
        private Statement ParseSimpleStmt() {
            Statement s = ParseSmallStmt();

            string newline = null;
            if (MaybeEat(TokenKind.Semicolon)) {
                var itemWhiteSpace = MakeWhiteSpaceList();
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }

                var start = s.StartIndex;
                List<Statement> l = new List<Statement>();
                l.Add(s);
                while (true) {
                    if (MaybeEatNewLine(out newline) || MaybeEatEof()) {
                        break;
                    }

                    l.Add(ParseSmallStmt());

                    if (MaybeEatEof()) {
                        // implies a new line
                        break;
                    } else if (!MaybeEat(TokenKind.Semicolon)) {
                        EatNewLine(out newline);
                        break;
                    }
                    if (itemWhiteSpace != null) {
                        itemWhiteSpace.Add(_tokenWhiteSpace);
                    }
                }
                Statement[] stmts = l.ToArray();

                SuiteStatement ret = new SuiteStatement(stmts);
                ret.SetLoc(start, stmts[stmts.Length - 1].EndIndex);
                if (itemWhiteSpace != null) {
                    AddListWhiteSpace(ret, itemWhiteSpace.ToArray());
                }
                if (newline != null) {
                    _lookaheadWhiteSpace = newline + _lookaheadWhiteSpace;
                }
                return ret;
            } else if (MaybeEatEof()) {
            } else if (EatNewLine(out newline)) {
                if (_verbatim) {
                    _lookaheadWhiteSpace = newline + _lookaheadWhiteSpace;
                }
            } else {
                // error handling, make sure we're making forward progress
                NextToken();
                if (_verbatim) {
                    _lookaheadWhiteSpace = _tokenWhiteSpace + _token.Token.VerbatimImage + _lookaheadWhiteSpace;
                }
            }
            return s;
        }

        private bool MaybeEatEof() {
            if (PeekToken().Kind == TokenKind.EndOfFile) {
                return true;
            }

            return false;
        }
        /*
        small_stmt: expr_stmt | print_stmt  | del_stmt | pass_stmt | flow_stmt | import_stmt | global_stmt | exec_stmt | assert_stmt

        del_stmt: 'del' exprlist
        pass_stmt: 'pass'
        flow_stmt: break_stmt | continue_stmt | return_stmt | raise_stmt | yield_stmt
        break_stmt: 'break'
        continue_stmt: 'continue'
        return_stmt: 'return' [testlist]
        yield_stmt: 'yield' testlist
        */
        private Statement ParseSmallStmt() {
            switch (PeekToken().Kind) {
                case TokenKind.KeywordPrint:
                    return ParsePrintStmt();
                case TokenKind.KeywordPass:
                    return FinishSmallStmt(new EmptyStatement());
                case TokenKind.KeywordBreak:
                    if (!_inLoop) {
                        ReportSyntaxError("'break' outside loop");
                    }
                    return FinishSmallStmt(new BreakStatement());
                case TokenKind.KeywordContinue:
                    if (!_inLoop) {
                        ReportSyntaxError("'continue' not properly in loop");
                    } else if (_inFinally) {
                        ReportSyntaxError("'continue' not supported inside 'finally' clause");
                    }
                    return FinishSmallStmt(new ContinueStatement());
                case TokenKind.KeywordReturn:
                    return ParseReturnStmt();
                case TokenKind.KeywordFrom:
                    return ParseFromImportStmt();
                case TokenKind.KeywordImport:
                    return ParseImportStmt();
                case TokenKind.KeywordGlobal:
                    return ParseGlobalStmt();
                case TokenKind.KeywordNonlocal:
                    return ParseNonlocalStmt();
                case TokenKind.KeywordRaise:
                    return ParseRaiseStmt();
                case TokenKind.KeywordAssert:
                    return ParseAssertStmt();
                case TokenKind.KeywordExec:
                    return ParseExecStmt();
                case TokenKind.KeywordDel:
                    return ParseDelStmt();
                case TokenKind.KeywordYield:
                    return ParseYieldStmt();
                default:
                    return ParseExprStmt();
            }
        }

        // del_stmt: "del" target_list
        //  for error reporting reasons we allow any expression and then report the bad
        //  delete node when it fails.  This is the reason we don't call ParseTargetList.
        private Statement ParseDelStmt() {
            var curLookahead = _lookahead;
            NextToken();
            string delWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            List<string> itemWhiteSpace;

            DelStatement ret;
            if (PeekToken(TokenKind.NewLine) || PeekToken(TokenKind.EndOfFile)) {
                ReportSyntaxError(curLookahead.Span.Start, curLookahead.Span.End, "expected expression after del");
                ret = new DelStatement(new Expression[0]);
            } else {
                List<Expression> l = ParseExprList(out itemWhiteSpace);
                foreach (Expression e in l) {
                    if (e is ErrorExpression) {
                        continue;
                    }
                    string delError = e.CheckDelete();
                    if (delError != null) {
                        ReportSyntaxError(e.StartIndex, e.EndIndex, delError, ErrorCodes.SyntaxError);
                    }
                }

                ret = new DelStatement(l.ToArray());
                if (itemWhiteSpace != null) {
                    AddListWhiteSpace(ret, itemWhiteSpace.ToArray());
                }
            }

            ret.SetLoc(start, GetEndForStatement());
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, delWhiteSpace);
            }
            return ret;
        }

        private Statement ParseReturnStmt() {
            if (!AllowReturnSyntax) {
                ReportSyntaxError("'return' outside function");
            }
            var returnToken = _lookahead;
            NextToken();
            string returnWhitespace = _tokenWhiteSpace;
            Expression expr = null;
            var start = GetStart();
            if (!NeverTestToken(PeekToken())) {
                expr = ParseTestListAsExpr();
            }

            if (expr != null && _langVersion < PythonLanguageVersion.V33) {
                if (_isGenerator) {
                    ReportSyntaxError(returnToken.Span.Start, expr.EndIndex, "'return' with argument inside generator");
                } else {
                    if (_returnsWithValue == null) {
                        _returnsWithValue = new List<IndexSpan>();
                    }
                    _returnsWithValue.Add(new IndexSpan(returnToken.Span.Start, expr.EndIndex - returnToken.Span.Start));
                }
            }

            ReturnStatement ret = new ReturnStatement(expr);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, returnWhitespace);
            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        private Statement FinishSmallStmt(Statement stmt) {
            NextToken();
            stmt.SetLoc(GetStart(), GetEndForStatement());
            if (_verbatim) {
                AddPreceedingWhiteSpace(stmt, _tokenWhiteSpace);
            }
            return stmt;
        }


        private Statement ParseYieldStmt() {
            // For yield statements, continue to enforce that it's currently in a function. 
            // This gives us better syntax error reporting for yield-statements than for yield-expressions.
            if (!AllowYieldSyntax) {
                if (AllowAsyncAwaitSyntax) {
                    ReportSyntaxError("'yield' inside async function");
                } else {
                    ReportSyntaxError("misplaced yield");
                }
            }

            _isGenerator = true;
            if (_returnsWithValue != null && _langVersion < PythonLanguageVersion.V33) {
                foreach (var span in _returnsWithValue) {
                    ReportSyntaxError(span.Start, span.End, "'return' with argument inside generator");
                }
            }

            Eat(TokenKind.KeywordYield);

            // See Pep 342: a yield statement is now just an expression statement around a yield expression.
            Expression e = ParseYieldExpression();
            Debug.Assert(e != null); // caller already verified we have a yield.

            Statement s = new ExpressionStatement(e);
            s.SetLoc(e.StartIndex, GetEndForStatement());
            return s;
        }

        /// <summary>
        /// Peek if the next token is a 'yield' and parse a yield or yield from expression. Else return null.
        /// 
        /// Called w/ yield already eaten.
        /// </summary>
        /// <returns>A yield or yield from expression if present, else null.</returns>
        // yield_expression: "yield" [expression_list] 
        private Expression ParseYieldExpression() {
            // Mark that this function is actually a generator.
            // If we're in a generator expression, then we don't have a function yet.
            //    g=((yield i) for i in range(5))
            // In that case, the genexp will mark IsGenerator. 
            FunctionDefinition current = CurrentFunction;
            if (current != null && !current.IsCoroutine) {
                current.IsGenerator = true;
            }
            string whitespace = _tokenWhiteSpace;

            var start = GetStart();

            // Parse expression list after yield. This can be:
            // 1) empty, in which case it becomes 'yield None'
            // 2) a single expression
            // 3) multiple expression, in which case it's wrapped in a tuple.
            // 4) 'from', in which case we expect a single expression and return YieldFromExpression
            Expression yieldResult;

            bool isYieldFrom = PeekToken(TokenKind.KeywordFrom);
            bool suppressSyntaxError = false;
            string fromWhitespace = string.Empty;

            if (isYieldFrom) {
                if (_langVersion < PythonLanguageVersion.V33) {
                    // yield from added to 3.3
                    ReportSyntaxError("invalid syntax");
                    suppressSyntaxError = true;
                }
                NextToken();
                fromWhitespace = _tokenWhiteSpace;
            }

            bool trailingComma;
            List<string> itemWhiteSpace;
            List<Expression> l = ParseTestListAsExpr(null, out itemWhiteSpace, out trailingComma);
            if (l.Count == 0) {
                if (_langVersion < PythonLanguageVersion.V25 && !suppressSyntaxError) {
                    // 2.4 doesn't allow plain yield
                    ReportSyntaxError("invalid syntax");
                } else if (isYieldFrom && !suppressSyntaxError) {
                    // yield from requires one expression
                    ReportSyntaxError("invalid syntax");
                }
                // Check empty expression and convert to 'none'
                yieldResult = new ConstantExpression(null);
            } else if (l.Count != 1) {
                if (isYieldFrom && !suppressSyntaxError) {
                    // yield from requires one expression
                    ReportSyntaxError(l[0].StartIndex, l[l.Count - 1].EndIndex, "invalid syntax");
                }
                // make a tuple
                yieldResult = MakeTupleOrExpr(l, itemWhiteSpace, trailingComma, true);
            } else {
                // just take the single expression
                yieldResult = l[0];
            }

            Expression yieldExpression;
            if (isYieldFrom) {
                yieldExpression = new YieldFromExpression(yieldResult);
            } else {
                yieldExpression = new YieldExpression(yieldResult);
            }
            if (_verbatim) {
                AddPreceedingWhiteSpace(yieldExpression, whitespace);
                if (!string.IsNullOrEmpty(fromWhitespace)) {
                    AddSecondPreceedingWhiteSpace(yieldExpression, fromWhitespace);
                }

                if (l.Count == 0) {
                    AddIsAltForm(yieldExpression);
                } else if (l.Count == 1 && trailingComma) {
                    AddListWhiteSpace(yieldExpression, itemWhiteSpace.ToArray());
                }
            }
            yieldExpression.SetLoc(start, GetEnd());
            return yieldExpression;

        }

        private Statement FinishAssignments(Expression right, bool thereCanBeOnlyOne = false) {
            List<Expression> left = null;
            List<string> assignWhiteSpace = MakeWhiteSpaceList();
            Expression singleLeft = null;

            while (MaybeEat(TokenKind.Assign)) {
                if (assignWhiteSpace != null) {
                    assignWhiteSpace.Add(_tokenWhiteSpace);
                }
                string assignError = right.CheckAssign();
                if (assignError != null) {
                    ReportSyntaxError(right.StartIndex, right.EndIndex, assignError, ErrorCodes.SyntaxError | ErrorCodes.NoCaret);
                }

                if (singleLeft == null) {
                    singleLeft = right;
                } else {
                    if (thereCanBeOnlyOne) {
                        ReportSyntaxError(GetStart(), GetEnd(), "invalid syntax");
                    }
                    if (left == null) {
                        left = new List<Expression>();
                        left.Add(singleLeft);
                    }
                    left.Add(right);
                }

                if (_langVersion >= PythonLanguageVersion.V25 && PeekToken(TokenKind.KeywordYield)) {
                    if (!AllowYieldSyntax && AllowAsyncAwaitSyntax) {
                        ReportSyntaxError("'yield' inside async function");
                    }
                    Eat(TokenKind.KeywordYield);
                    right = ParseYieldExpression();
                } else {
                    right = ParseTestListAsExpr();
                }
            }

            AssignmentStatement assign;
            if (left != null) {
                Debug.Assert(left.Count > 0);

                assign = new AssignmentStatement(left.ToArray(), right);
                assign.SetLoc(left[0].StartIndex, right.EndIndex);
            } else {
                Debug.Assert(singleLeft != null);

                assign = new AssignmentStatement(new[] { singleLeft }, right);
                assign.SetLoc(singleLeft.StartIndex, right.EndIndex);
            }
            if (assignWhiteSpace != null) {
                AddListWhiteSpace(assign, assignWhiteSpace.ToArray());
            }
            return assign;
        }

        private static bool IsEndOfLineToken(Token t) {
            switch (t.Kind) {
                case TokenKind.Comment:
                case TokenKind.NewLine:
                case TokenKind.NLToken:
                case TokenKind.EndOfFile:
                    return true;
            }
            return false;
        }

        private ErrorExpression ReadLineAsError(Expression preceeding, string message) {
            var t = NextToken();

            Debug.Assert(t.Kind == TokenKind.Colon);
            var image = new StringBuilder();
            if (_verbatim) {
                image.Append(_tokenWhiteSpace);
            }
            image.Append(':');

            while (!IsEndOfLineToken(PeekToken())) {
                t = NextToken();
                if (_verbatim) {
                    image.Append(_tokenWhiteSpace);
                    image.Append(t.VerbatimImage);
                } else {
                    image.Append(t.Image);
                }
            }
            var err = new ErrorExpression(image.ToString(), preceeding);
            err.SetLoc(preceeding.StartIndex, GetEnd());
            ReportSyntaxError(err.StartIndex, err.EndIndex, message);
            return err;
        }

        private Expression ParseNameAnnotation(Expression expr) {
            var inex = (expr as ParenthesisExpression)?.Expression;
            if (expr is NameExpression || expr is MemberExpression || expr is IndexExpression ||
                inex is NameExpression || inex is MemberExpression || inex is IndexExpression) {
                // pass
            } else if (expr is TupleExpression) {
                return ReadLineAsError(expr, "only single target (not tuple) can be annotated");
            } else {
                return ReadLineAsError(expr, "illegal target for annotation");
            }

            Eat(TokenKind.Colon);
            var ws2 = _tokenWhiteSpace;
            var startColon = GetStart();

            var ann = ParseExpression();

            var err = ann as ErrorExpression;
            if (err != null) {
                var image = ws2 + ":";
                Dictionary<object, object> attr = null;
                if (_verbatim && _attributes.TryGetValue(err, out attr)) {
                    object o;
                    if (attr.TryGetValue(NodeAttributes.PreceedingWhiteSpace, out o)) {
                        image += o.ToString();
                    }
                }

                var err2 = err.AddPrefix(image, expr);
                err2.SetLoc(startColon, err.EndIndex);
                if (attr != null) {
                    _attributes[err2] = attr;
                    _attributes.Remove(err);
                }
                return err2;
            }
            var ret = new ExpressionWithAnnotation(expr, ann);
            ret.SetLoc(expr.StartIndex, ann.EndIndex);
            if (_verbatim) {
                AddSecondPreceedingWhiteSpace(ret, ws2);
            }
            return ret;
        }

        // expr_stmt: expression_list
        // expression_list: expression ( "," expression )* [","] 
        // assignment_stmt: (target_list "=")+ (expression_list | yield_expression) 
        // augmented_assignment_stmt ::= target augop (expression_list | yield_expression) 
        // augop: '+=' | '-=' | '*=' | '/=' | '%=' | '**=' | '>>=' | '<<=' | '&=' | '^=' | '|=' | '//='
        private Statement ParseExprStmt() {
            Expression ret = ParseTestListAsExpr();
            bool hasAnnotation = false;

            if (PeekToken(TokenKind.Colon) && (_stubFile || _langVersion >= PythonLanguageVersion.V36)) {
                ret = ParseNameAnnotation(ret);
                hasAnnotation = true;
                if (!PeekToken(TokenKind.Assign)) {
                    Statement stmt = new ExpressionStatement(ret);
                    stmt.SetLoc(ret.StartIndex, GetEndForStatement());
                    return stmt;
                }
            }

            if (PeekToken(TokenKind.Assign)) {
                if (_stubFile || _langVersion.Is3x()) {
                    SequenceExpression seq = ret as SequenceExpression;
                    bool hasStar = false;
                    if (seq != null) {
                        for (int i = 0; i < seq.Items.Count; i++) {
                            if (seq.Items[i] is StarredExpression) {
                                if (hasStar) {
                                    ReportSyntaxError(seq.Items[i].StartIndex, seq.Items[i].EndIndex, "two starred expressions in assignment");
                                }
                                hasStar = true;
                            }
                        }
                    }
                }

                return FinishAssignments(ret, hasAnnotation);
            } else {
                PythonOperator op = GetAssignOperator(PeekToken());
                if (op != PythonOperator.None) {
                    NextToken();
                    string whiteSpace = _tokenWhiteSpace;
                    Expression rhs;

                    if (_langVersion >= PythonLanguageVersion.V25 && PeekToken(TokenKind.KeywordYield)) {
                        if (!AllowYieldSyntax && AllowAsyncAwaitSyntax) {
                            ReportSyntaxError("'yield' inside async function");
                        }
                        Eat(TokenKind.KeywordYield);
                        rhs = ParseYieldExpression();
                    } else {
                        rhs = ParseTestListAsExpr();
                    }

                    string assignError = ret.CheckAugmentedAssign();
                    if (assignError != null) {
                        ReportSyntaxError(ret.StartIndex, ret.EndIndex, assignError);
                    }

                    AugmentedAssignStatement aug = new AugmentedAssignStatement(op, ret, rhs);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(aug, whiteSpace);
                    }
                    aug.SetLoc(ret.StartIndex, GetEnd());
                    return aug;
                } else {
                    Statement stmt = new ExpressionStatement(ret);
                    stmt.SetLoc(ret.StartIndex, GetEndForStatement());
                    return stmt;
                }
            }
        }

        private PythonOperator GetAssignOperator(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual: return PythonOperator.Add;
                case TokenKind.SubtractEqual: return PythonOperator.Subtract;
                case TokenKind.MultiplyEqual: return PythonOperator.Multiply;
                case TokenKind.MatMultiplyEqual: return PythonOperator.MatMultiply;
                case TokenKind.DivideEqual: return TrueDivision ? PythonOperator.TrueDivide : PythonOperator.Divide;
                case TokenKind.ModEqual: return PythonOperator.Mod;
                case TokenKind.BitwiseAndEqual: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOrEqual: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOrEqual: return PythonOperator.Xor;
                case TokenKind.LeftShiftEqual: return PythonOperator.LeftShift;
                case TokenKind.RightShiftEqual: return PythonOperator.RightShift;
                case TokenKind.PowerEqual: return PythonOperator.Power;
                case TokenKind.FloorDivideEqual: return PythonOperator.FloorDivide;
                default: return PythonOperator.None;
            }
        }


        private PythonOperator GetBinaryOperator(OperatorToken token) {
            switch (token.Kind) {
                case TokenKind.Add: return PythonOperator.Add;
                case TokenKind.Subtract: return PythonOperator.Subtract;
                case TokenKind.Multiply: return PythonOperator.Multiply;
                case TokenKind.MatMultiply: return PythonOperator.MatMultiply;
                case TokenKind.Divide: return TrueDivision ? PythonOperator.TrueDivide : PythonOperator.Divide;
                case TokenKind.Mod: return PythonOperator.Mod;
                case TokenKind.BitwiseAnd: return PythonOperator.BitwiseAnd;
                case TokenKind.BitwiseOr: return PythonOperator.BitwiseOr;
                case TokenKind.ExclusiveOr: return PythonOperator.Xor;
                case TokenKind.LeftShift: return PythonOperator.LeftShift;
                case TokenKind.RightShift: return PythonOperator.RightShift;
                case TokenKind.Power: return PythonOperator.Power;
                case TokenKind.FloorDivide: return PythonOperator.FloorDivide;
                default:
                    Debug.Assert(false, "Unreachable");
                    return PythonOperator.None;
            }
        }


        // import_stmt: 'import' module ['as' name"] (',' module ['as' name])*        
        // name: identifier
        private ImportStatement ParseImportStmt() {
            Eat(TokenKind.KeywordImport);
            string whitespace = _tokenWhiteSpace;
            var start = GetStart();

            List<string> asNameWhiteSpace = MakeWhiteSpaceList();
            List<ModuleName> l = new List<ModuleName>();
            List<NameExpression> las = new List<NameExpression>();
            var modName = ParseModuleName();
            var commaWhiteSpace = MakeWhiteSpaceList();
            if (modName.Names.Count > 0) {
                l.Add(modName);
                las.Add(MaybeParseAsName(asNameWhiteSpace));
                while (MaybeEat(TokenKind.Comma)) {
                    if (commaWhiteSpace != null) {
                        commaWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    l.Add(ParseModuleName());
                    las.Add(MaybeParseAsName(asNameWhiteSpace));
                }
            }
            ModuleName[] names = l.ToArray();
            var asNames = las.ToArray();

            ImportStatement ret = new ImportStatement(names, asNames, AbsoluteImports);
            if (_verbatim) {
                AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                AddNamesWhiteSpace(ret, asNameWhiteSpace.ToArray());
                AddPreceedingWhiteSpace(ret, whitespace);
            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        // module: (identifier '.')* identifier
        private ModuleName ParseModuleName() {
            List<string> dotWhiteSpace;
            ModuleName ret = new ModuleName(ReadDottedName(out dotWhiteSpace));
            if (_verbatim) {
                AddNamesWhiteSpace(ret, dotWhiteSpace.ToArray());
            }

            int start = ret.Names.FirstOrDefault()?.StartIndex ?? GetEnd();
            ret.SetLoc(start, GetEnd());
            return ret;
        }

        private static NameExpression[] EmptyNames = new NameExpression[0];

        // relative_module: "."* module | "."+
        private ModuleName ParseRelativeModuleName() {
            int start = -1;
            bool isStartSetCorrectly = false;

            int dotCount = 0;
            List<string> dotWhiteSpace = MakeWhiteSpaceList();
            for (; ; ) {
                if (MaybeEat(TokenKind.Dot)) {
                    if (dotWhiteSpace != null) {
                        dotWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    dotCount++;
                } else if (MaybeEat(TokenKind.Ellipsis)) {
                    if (dotWhiteSpace != null) {
                        dotWhiteSpace.Add(_tokenWhiteSpace);
                        dotWhiteSpace.Add("");
                        dotWhiteSpace.Add("");
                    }
                    dotCount += 3;
                } else {
                    break;
                }
                if (!isStartSetCorrectly) {
                    start = GetStart();
                    isStartSetCorrectly = true;
                }
            }

            List<string> nameWhiteSpace = null;
            NameExpression[] names = EmptyNames;
            if (PeekToken() is NameToken) {
                names = ReadDottedName(out nameWhiteSpace);
                if (!isStartSetCorrectly && names.Length > 0) {
                    start = names[0].StartIndex;
                    isStartSetCorrectly = true;
                }
            }

            if (!isStartSetCorrectly) {
                start = GetEnd();
                isStartSetCorrectly = true;
            }

            ModuleName ret;
            if (dotCount > 0) {
                ret = new RelativeModuleName(names, dotCount);
                if (_verbatim) {
                    if (nameWhiteSpace != null) {
                        AddNamesWhiteSpace(ret, nameWhiteSpace.ToArray());
                    }
                    AddListWhiteSpace(ret, dotWhiteSpace.ToArray());
                }
            } else {
                if (names.Length == 0) {
                    ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, "missing module name");
                }
                ret = new ModuleName(names);
                if (nameWhiteSpace != null) {
                    AddNamesWhiteSpace(ret, nameWhiteSpace.ToArray());
                }
            }

            Debug.Assert(isStartSetCorrectly, "Start location was not set correctly");
            ret.SetLoc(start, GetEnd());
            return ret;
        }

        private NameExpression[] ReadDottedName(out List<string> dotWhiteSpace) {
            List<NameExpression> l = new List<NameExpression>();
            dotWhiteSpace = MakeWhiteSpaceList();

            var name = ReadName();
            if (name.HasName) {
                var nameExpr = MakeName(name);
                l.Add(nameExpr);

                if (_verbatim) {
                    dotWhiteSpace.Add(_tokenWhiteSpace);
                }
                while (MaybeEat(TokenKind.Dot)) {
                    if (dotWhiteSpace != null) {
                        dotWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    name = ReadName();
                    nameExpr = MakeName(name);
                    l.Add(nameExpr);
                    if (_verbatim) {
                        dotWhiteSpace.Add(_tokenWhiteSpace);
                    }
                }
            }
            return l.ToArray();
        }


        // 'from' relative_module 'import' identifier ['as' name] (',' identifier ['as' name]) *
        // 'from' relative_module 'import' '(' identifier ['as' name] (',' identifier ['as' name])* [','] ')'        
        // 'from' module 'import' "*"                                        
        private FromImportStatement ParseFromImportStmt() {
            Eat(TokenKind.KeywordFrom);
            string fromWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            ModuleName dname = ParseRelativeModuleName();

            bool ateImport = Eat(TokenKind.KeywordImport);
            int importIndex = ateImport ? GetStart() : 0;
            string importWhiteSpace = _tokenWhiteSpace;

            bool ateParen = ateImport && MaybeEat(TokenKind.LeftParenthesis);
            string parenWhiteSpace = ateParen ? _tokenWhiteSpace : null;

            NameExpression/*!*/[] names;
            NameExpression[] asNames;
            bool fromFuture = false;

            List<string> namesWhiteSpace = null;
            if (ateImport) {
                List<NameExpression/*!*/> l = new List<NameExpression>();
                List<NameExpression> las = new List<NameExpression>();
                ParseAsNameList(l, las, out namesWhiteSpace);

                names = l.ToArray();
                asNames = las.ToArray();

                if (_langVersion.Is3x() && ((_functions != null && _functions.Count > 0) || _classDepth > 0)) {
                    foreach (var n in names.Where(n => n.Name == "*")) {
                        ReportSyntaxError(n.StartIndex, n.EndIndex, "import * only allowed at module level");
                    }
                }
            } else {
                names = EmptyNames;
                asNames = EmptyNames;
            }

            // Process from __future__ statement
            if (dname.Names.Count == 1 && dname.Names[0].Name == "__future__") {
                fromFuture = ProcessFutureStatements(start, names, fromFuture);
            }

            bool ateRightParen = ateParen && Eat(TokenKind.RightParenthesis);

            FromImportStatement ret = new FromImportStatement(dname, names, asNames, fromFuture, AbsoluteImports, importIndex);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, fromWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, importWhiteSpace);
                if (namesWhiteSpace != null) {
                    AddNamesWhiteSpace(ret, namesWhiteSpace.ToArray());
                }
                if (ateParen) {
                    AddThirdPreceedingWhiteSpace(ret, parenWhiteSpace);
                    AddFourthPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                    if (!ateRightParen) {
                        AddErrorMissingCloseGrouping(ret);
                    }
                } else {
                    AddIsAltForm(ret);
                }
                if (!ateImport) {
                    AddErrorIsIncompleteNode(ret);
                }

            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        private bool ProcessFutureStatements(int start, NameExpression/*!*/[] names, bool fromFuture) {
            if (!_fromFutureAllowed) {
                ReportSyntaxError(start, GetEnd(), "from __future__ imports must occur at the beginning of the file");
            }
            if (names.Length == 1 && names[0].Name == "*") {
                ReportSyntaxError(start, GetEnd(), "future statement does not support import *");
            }
            fromFuture = true;
            foreach (var name in names) {
                if (name.Name == "nested_scopes") {

                    // v2.4
                } else if (name.Name == "division") {
                    _languageFeatures |= FutureOptions.TrueDivision;
                } else if (name.Name == "generators") {

                    // v2.5:
                } else if (_langVersion >= PythonLanguageVersion.V25 && name.Name == "with_statement") {
                    _languageFeatures |= FutureOptions.WithStatement;
                    _tokenizer.WithStatement = true;
                } else if (_langVersion >= PythonLanguageVersion.V25 && name.Name == "absolute_import") {
                    _languageFeatures |= FutureOptions.AbsoluteImports;

                    // v2.6:
                } else if (_langVersion >= PythonLanguageVersion.V26 && name.Name == "print_function") {
                    _languageFeatures |= FutureOptions.PrintFunction;
                    _tokenizer.PrintFunction = true;
                } else if (_langVersion >= PythonLanguageVersion.V26 && name.Name == "unicode_literals") {
                    _tokenizer.UnicodeLiterals = true;
                    _languageFeatures |= FutureOptions.UnicodeLiterals;

                    // v3.5:
                } else if (_langVersion >= PythonLanguageVersion.V35 && name.Name == "generator_stop") {
                    // No behavior change, but we don't want to display an error

                    // v3.7
                } else if (_langVersion >= PythonLanguageVersion.V37 && name.Name == "annotations") {
                    _languageFeatures |= FutureOptions.Annotations;
                } else {
                    string strName = name.Name;

                    if (strName != "braces") {
                        ReportSyntaxError(start, GetEnd(), "future feature is not defined: " + strName);
                    } else {
                        // match CPython error message
                        ReportSyntaxError(start, GetEnd(), "not a chance");
                    }
                }
            }
            return fromFuture;
        }

        // import_as_name (',' import_as_name)*
        private void ParseAsNameList(List<NameExpression/*!*/> l, List<NameExpression> las, out List<string> asNamesWhiteSpace) {
            asNamesWhiteSpace = MakeWhiteSpaceList();

            Name name;
            NameExpression nameExpr;
            string ws;

            if (MaybeEat(TokenKind.Multiply)) {
                nameExpr = new NameExpression("*");
                nameExpr.SetLoc(GetStart(), GetEnd());
                ws = _tokenWhiteSpace;
            } else {
                name = ReadName();
                if (!name.HasName) {
                    return;
                }
                nameExpr = MakeName(name);
                ws = name.HasName ? _tokenWhiteSpace : "";
            }
            l.Add(nameExpr);
            if (_verbatim) {
                asNamesWhiteSpace.Add(ws);
            }

            las.Add(MaybeParseAsName(asNamesWhiteSpace));
            while (MaybeEat(TokenKind.Comma)) {
                if (asNamesWhiteSpace != null) {
                    asNamesWhiteSpace.Add(_tokenWhiteSpace);
                }

                if (PeekToken(TokenKind.RightParenthesis)) return;  // the list is allowed to end with a ,

                if (MaybeEat(TokenKind.Multiply)) {
                    nameExpr = new NameExpression("*");
                    ws = _tokenWhiteSpace;
                } else {
                    name = ReadName();
                    nameExpr = MakeName(name);
                    ws = name.HasName ? _tokenWhiteSpace : "";
                }
                l.Add(nameExpr);
                if (_verbatim) {
                    asNamesWhiteSpace.Add(ws);
                }
                las.Add(MaybeParseAsName(asNamesWhiteSpace));
            }
        }

        //import_as_name: NAME [NAME NAME]
        //dotted_as_name: dotted_name [NAME NAME]
        private NameExpression MaybeParseAsName(List<string> asNameWhiteSpace) {
            if (MaybeEat(TokenKind.KeywordAs) || MaybeEatName("as")) {
                if (_verbatim) {
                    asNameWhiteSpace.Add(_tokenWhiteSpace);
                }
                var res = ReadName();
                if (_verbatim) {
                    asNameWhiteSpace.Add(_tokenWhiteSpace);
                }
                return MakeName(res);
            }

            return null;
        }

        //exec_stmt: 'exec' expr ['in' expression [',' expression]]
        private ExecStatement ParseExecStmt() {
            Eat(TokenKind.KeywordExec);
            string execWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            Expression code, locals = null, globals = null;
            code = ParseExpr();
            string inWhiteSpace = null, commaWhiteSpace = null;
            if (MaybeEat(TokenKind.KeywordIn)) {
                inWhiteSpace = _tokenWhiteSpace;
                globals = ParseExpression();
                if (MaybeEat(TokenKind.Comma)) {
                    commaWhiteSpace = _tokenWhiteSpace;
                    locals = ParseExpression();
                }
            }

            ExecStatement ret = new ExecStatement(code, locals, globals, code as TupleExpression);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, execWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, inWhiteSpace);
                AddThirdPreceedingWhiteSpace(ret, commaWhiteSpace);
            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        //global_stmt: 'global' NAME (',' NAME)*
        private GlobalStatement ParseGlobalStmt() {
            Eat(TokenKind.KeywordGlobal);
            var start = GetStart();
            string globalWhiteSpace = _tokenWhiteSpace;
            List<string> commaWhiteSpace;
            List<string> namesWhiteSpace;

            var l = ReadNameList(out commaWhiteSpace, out namesWhiteSpace);
            var names = l.ToArray();
            GlobalStatement ret = new GlobalStatement(names);
            ret.SetLoc(start, GetEndForStatement());
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, globalWhiteSpace);
                AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                AddNamesWhiteSpace(ret, namesWhiteSpace.ToArray());
            }
            return ret;
        }

        private NonlocalStatement ParseNonlocalStmt() {
            if (_functions != null && _functions.Count == 0 && _classDepth == 0) {
                ReportSyntaxError("nonlocal declaration not allowed at module level");
            }

            Eat(TokenKind.KeywordNonlocal);
            string localWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            List<string> commaWhiteSpace;
            List<string> namesWhiteSpace;

            var l = ReadNameList(out commaWhiteSpace, out namesWhiteSpace);
            var names = l.ToArray();
            NonlocalStatement ret = new NonlocalStatement(names);
            ret.SetLoc(start, GetEndForStatement());
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, localWhiteSpace);
                AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                AddNamesWhiteSpace(ret, namesWhiteSpace.ToArray());
            }
            return ret;
        }

        private List<NameExpression> ReadNameList(out List<string> commaWhiteSpace, out List<string> namesWhiteSpace) {
            List<NameExpression> l = new List<NameExpression>();
            commaWhiteSpace = MakeWhiteSpaceList();
            namesWhiteSpace = MakeWhiteSpaceList();

            var name = ReadName();
            if (name.HasName) {
                var nameExpr = MakeName(name);
                l.Add(nameExpr);
                if (_verbatim) {
                    namesWhiteSpace.Add(_tokenWhiteSpace);
                }
                while (MaybeEat(TokenKind.Comma)) {
                    if (commaWhiteSpace != null) {
                        commaWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    name = ReadName();
                    nameExpr = MakeName(name);
                    l.Add(nameExpr);
                    if (_verbatim) {
                        namesWhiteSpace.Add(_tokenWhiteSpace);
                    }
                }
            }
            return l;
        }

        //raise_stmt: 'raise' [expression [',' expression [',' expression]]]
        private RaiseStatement ParseRaiseStmt() {
            Eat(TokenKind.KeywordRaise);
            string raiseWhiteSpace = _tokenWhiteSpace;
            string commaWhiteSpace = null, secondCommaWhiteSpace = null;
            var start = GetStart();
            Expression type = null, value = null, traceback = null, cause = null;
            bool isFromForm = false;

            int? valueFieldStart = null, tracebackFieldStart = null, causeFieldStart = null;

            if (!NeverTestToken(PeekToken())) {
                type = ParseExpression();

                if (MaybeEat(TokenKind.Comma)) {
                    var commaStart = GetStart();
                    commaWhiteSpace = _tokenWhiteSpace;
                    valueFieldStart = GetEnd();
                    value = ParseExpression();
                    if (_stubFile || _langVersion.Is3x()) {
                        ReportSyntaxError(commaStart, GetEnd(), "invalid syntax, only exception value is allowed in 3.x.");
                    }
                    if (MaybeEat(TokenKind.Comma)) {
                        secondCommaWhiteSpace = _tokenWhiteSpace;
                        tracebackFieldStart = GetEnd();
                        traceback = ParseExpression();
                    }
                } else if (MaybeEat(TokenKind.KeywordFrom)) {
                    commaWhiteSpace = _tokenWhiteSpace;
                    var fromStart = GetStart();
                    causeFieldStart = GetEnd();
                    cause = ParseExpression();
                    isFromForm = true;

                    if (!_stubFile && _langVersion.Is2x()) {
                        ReportSyntaxError(fromStart, cause.EndIndex, "invalid syntax, from cause not allowed in 2.x.");
                    }
                }

            }

            RaiseStatement ret = new RaiseStatement(type, value, traceback, cause);
            if (valueFieldStart.HasValue) {
                ret.ValueFieldStartIndex = valueFieldStart.Value;
            }
            if (tracebackFieldStart.HasValue) {
                ret.TracebackFieldStartIndex = tracebackFieldStart.Value;
            }
            if (causeFieldStart.HasValue) {
                ret.CauseFieldStartIndex = causeFieldStart.Value;
            }
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, raiseWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, commaWhiteSpace);
                AddThirdPreceedingWhiteSpace(ret, secondCommaWhiteSpace);
                if (isFromForm) {
                    AddIsAltForm(ret);
                }
            }

            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        //assert_stmt: 'assert' expression [',' expression]
        private AssertStatement ParseAssertStmt() {
            Eat(TokenKind.KeywordAssert);
            string whiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            Expression expr = ParseExpression();
            Expression message = null;
            string commaWhiteSpace = null;
            if (MaybeEat(TokenKind.Comma)) {
                commaWhiteSpace = _tokenWhiteSpace;
                message = ParseExpression();
            }
            AssertStatement ret = new AssertStatement(expr, message);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, whiteSpace);
                AddSecondPreceedingWhiteSpace(ret, commaWhiteSpace);
            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        //print_stmt: 'print' ( [ expression (',' expression)* [','] ] | '>>' expression [ (',' expression)+ [','] ] )
        private PrintStatement ParsePrintStmt() {
            Eat(TokenKind.KeywordPrint);
            string printWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            Expression dest = null;
            PrintStatement ret;

            string rightShiftWhiteSpace = null;
            string theCommaWhiteSpace = null;
            bool needNonEmptyTestList = false;
            int end = 0;
            if (MaybeEat(TokenKind.RightShift)) {
                rightShiftWhiteSpace = _tokenWhiteSpace;
                dest = ParseExpression();
                if (MaybeEat(TokenKind.Comma)) {
                    theCommaWhiteSpace = _tokenWhiteSpace;
                    needNonEmptyTestList = true;
                    end = GetEnd();
                } else {
                    ret = new PrintStatement(dest, new Expression[0], false);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret, printWhiteSpace);
                        AddSecondPreceedingWhiteSpace(ret, rightShiftWhiteSpace);
                    }
                    ret.SetLoc(start, GetEndForStatement());
                    return ret;
                }
            }

            bool trailingComma = false;
            List<string> commaWhiteSpace = null;

            Expression[] exprs;
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseExpression();
                if (!MaybeEat(TokenKind.Comma)) {
                    exprs = new[] { expr };
                } else {
                    List<Expression> exprList = ParseTestListAsExpr(expr, out commaWhiteSpace, out trailingComma);
                    exprs = exprList.ToArray();
                }
            } else {
                if (needNonEmptyTestList) {
                    ReportSyntaxError(start, end, "print statement expected expression to be printed");
                    exprs = new[] { Error("") };
                } else {
                    exprs = new Expression[0];
                }
            }

            ret = new PrintStatement(dest, exprs, trailingComma);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, printWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, rightShiftWhiteSpace);
                AddThirdPreceedingWhiteSpace(ret, theCommaWhiteSpace);
                if (commaWhiteSpace != null) {
                    AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                }
            }
            ret.SetLoc(start, GetEndForStatement());
            return ret;
        }

        private string SetPrivatePrefix(string name) {
            string oldPrefix = _privatePrefix;

            // Remove any leading underscores before saving the prefix
            _privatePrefix = name?.TrimStart('_');

            return oldPrefix;
        }

        private ErrorExpression Error(string verbatimImage = null, Expression preceeding = null) {
            var res = new ErrorExpression(verbatimImage, preceeding);
            res.SetLoc(GetStart(), GetEnd());
            return res;
        }

        private ErrorStatement ErrorStmt(string verbatimImage = null, params Statement[] preceeding) {
            var res = new ErrorStatement(preceeding);
            if (verbatimImage != null) {
                AddVerbatimImage(res, verbatimImage);
            }

            res.SetLoc(GetStart(), GetEndForStatement());
            return res;
        }

        //classdef: 'class' NAME ['(' testlist ')'] ':' suite
        private Statement ParseClassDef() {
            Eat(TokenKind.KeywordClass);
            string classWhiteSpace = _tokenWhiteSpace;

            var start = GetStart();
            var name = ReadName();
            var nameExpr = MakeName(name);
            string nameWhiteSpace = name.HasName ? _tokenWhiteSpace : null;

            bool isParenFree = false;
            string leftParenWhiteSpace = null, rightParenWhiteSpace = null;
            List<string> commaWhiteSpace = null;
            Arg[] args;
            bool ateTerminator = true;
            if (MaybeEat(TokenKind.LeftParenthesis)) {
                leftParenWhiteSpace = _tokenWhiteSpace;
                commaWhiteSpace = MakeWhiteSpaceList();
                args = FinishArgumentList(null, commaWhiteSpace, out ateTerminator);
                rightParenWhiteSpace = _tokenWhiteSpace;
                if (!_stubFile && _langVersion.Is2x()) {
                    foreach (var a in args) {
                        if (a.Name != null) {
                            ReportSyntaxError(a.StartIndex, a.EndIndex, "invalid syntax");
                        }
                    }
                }
            } else {
                isParenFree = true;
                args = new Arg[0];
            }
            var mid = _lookahead.Span.Start;

            // Save private prefix
            string savedPrefix = SetPrivatePrefix(name.VerbatimName);

            _classDepth++;
            // Parse the class body
            Statement body = ParseClassOrFuncBody();
            _classDepth--;

            // Restore the private prefix
            _privatePrefix = savedPrefix;

            ClassDefinition ret = new ClassDefinition(nameExpr, args, body);
            AddVerbatimName(name, ret);
            if (_verbatim) {
                if (isParenFree) {
                    AddIsAltForm(ret);
                }
                AddPreceedingWhiteSpace(ret, classWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, nameWhiteSpace);
                if (leftParenWhiteSpace != null) {
                    AddThirdPreceedingWhiteSpace(ret, leftParenWhiteSpace);
                }
                if (rightParenWhiteSpace != null) {
                    AddFourthPreceedingWhiteSpace(ret, rightParenWhiteSpace);
                }
                if (commaWhiteSpace != null) {
                    AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                }
                if (!ateTerminator) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }
            ret.HeaderIndex = mid;
            ret.SetLoc(start, body.EndIndex);
            return ret;
        }

        private NameExpression/*!*/ MakeName(Name name) {
            var res = new NameExpression(name.RealName);
            AddVerbatimName(name, res);
            if (name.HasName) {
                res.SetLoc(GetStart(), GetEnd());
            } else {
                res.SetLoc(GetEnd(), GetEnd());
            }
            return res;
        }

        private MemberExpression MakeMember(Expression target, Name name) {
            var res = new MemberExpression(target, name.RealName);
            AddVerbatimName(name, res);
            return res;
        }

        //  decorators ::=
        //      decorator+
        //  decorator ::=
        //      "@" dotted_name ["(" [argument_list [","]] ")"] NEWLINE
        private DecoratorStatement ParseDecorators(out List<string> newlineWhiteSpace) {
            List<Expression> decorators = new List<Expression>();
            newlineWhiteSpace = MakeWhiteSpaceList();
            Eat(TokenKind.At);
            var decStart = GetStart();
            do {
                if (newlineWhiteSpace != null) {
                    newlineWhiteSpace.Add(_tokenWhiteSpace);
                }
                var start = GetStart();
                var name = ReadName();
                Expression decorator = MakeName(name);
                if (!name.HasName) {
                    decorators.Add(null);
                    continue;
                }

                if (_verbatim) {
                    AddPreceedingWhiteSpace(decorator, _tokenWhiteSpace);
                }
                decorator.SetLoc(GetStart(), GetEnd());
                while (MaybeEat(TokenKind.Dot)) {
                    int dotStart = GetStart();
                    string whitespace = _tokenWhiteSpace;
                    name = ReadNameMaybeNone();
                    if (!name.HasName) {
                        decorator = Error(_verbatim ? (_tokenWhiteSpace + _token.Token.VerbatimImage + _lookaheadWhiteSpace + _lookahead.Token.VerbatimImage) : null, decorator);
                        NextToken();
                    } else {
                        string nameWhitespace = _tokenWhiteSpace;
                        var memberDecorator = MakeMember(decorator, name);
                        memberDecorator.SetLoc(start, GetStart(), GetEnd());
                        memberDecorator.DotIndex = dotStart;
                        if (_verbatim) {
                            AddPreceedingWhiteSpace(memberDecorator, whitespace);
                            AddSecondPreceedingWhiteSpace(memberDecorator, nameWhitespace);
                        }

                        decorator = memberDecorator;
                    }
                }

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    string parenWhiteSpace = _tokenWhiteSpace;
                    var commaWhiteSpace = MakeWhiteSpaceList();
                    bool ateTerminator;
                    Arg[] args = FinishArgumentList(null, commaWhiteSpace, out ateTerminator);
                    decorator = FinishCallExpr(decorator, args);

                    if (_verbatim) {
                        AddPreceedingWhiteSpace(decorator, parenWhiteSpace);
                        AddSecondPreceedingWhiteSpace(decorator, _tokenWhiteSpace);
                        if (commaWhiteSpace != null) {
                            AddListWhiteSpace(decorator, commaWhiteSpace.ToArray());
                        }
                        if (!ateTerminator) {
                            AddErrorMissingCloseGrouping(decorator);
                        }
                    }
                    decorator.SetLoc(start, GetEnd());
                }

                string newline;
                EatNewLine(out newline);
                if (newlineWhiteSpace != null) {
                    newlineWhiteSpace.Add(newline);
                }

                decorators.Add(decorator);
            } while (MaybeEat(TokenKind.At));

            var res = new DecoratorStatement(decorators.ToArray());
            res.SetLoc(decStart, GetEndForStatement());
            return res;
        }

        // funcdef: [decorators] 'def' NAME parameters ':' suite
        // 2.6: 
        //  decorated: decorators (classdef | funcdef)
        // this gets called with "@" look-ahead
        private Statement ParseDecorated() {
            List<string> newlineWhiteSpace;
            var decorators = ParseDecorators(out newlineWhiteSpace);

            Statement res;

            var next = PeekToken();
            if (next == Tokens.KeywordDefToken || next == Tokens.KeywordAsyncToken) {
                bool isCoroutine = (next == Tokens.KeywordAsyncToken);
                if (isCoroutine) {
                    Eat(TokenKind.KeywordAsync);
                }
                FunctionDefinition fnc = ParseFuncDef(isCoroutine: isCoroutine);
                fnc.Decorators = decorators;
                fnc.SetLoc(decorators.StartIndex, fnc.EndIndex);
                res = fnc;
            } else if (next == Tokens.KeywordClassToken) {
                if (_langVersion < PythonLanguageVersion.V26) {
                    ReportSyntaxError("invalid syntax, class decorators require 2.6 or later.");
                }
                var cls = ParseClassDef();
                if (cls is ClassDefinition) {
                    ((ClassDefinition)cls).Decorators = decorators;
                    cls.SetLoc(decorators.StartIndex, cls.EndIndex);
                    res = cls;
                } else {
                    // Class was an error...
                    res = ErrorStmt("", decorators, cls);
                }
            } else {
                ReportSyntaxError(_lookahead);
                res = ErrorStmt("", decorators);
            }
            if (newlineWhiteSpace != null) {
                AddNamesWhiteSpace(decorators, newlineWhiteSpace.ToArray());
            }
            return res;
        }

        // funcdef: [decorators] 'def' NAME parameters ':' suite
        // parameters: '(' [varargslist] ')'
        // this gets called with "def" as the look-ahead
        private FunctionDefinition ParseFuncDef(bool isCoroutine) {
            string preWhitespace = null, afterAsyncWhitespace = null;

            var start = isCoroutine ? GetStart() : 0;
            if (isCoroutine) {
                preWhitespace = _tokenWhiteSpace;
            }

            Eat(TokenKind.KeywordDef);
            int keywordEnd = GetEnd();

            if (isCoroutine) {
                afterAsyncWhitespace = _tokenWhiteSpace;
            } else {
                preWhitespace = _tokenWhiteSpace;
                start = GetStart();
            }

            var name = ReadName();
            var nameExpr = MakeName(name);
            string nameWhiteSpace = _tokenWhiteSpace;

            bool ateLeftParen = name.HasName && Eat(TokenKind.LeftParenthesis);
            string parenWhiteSpace = _tokenWhiteSpace;

            var lStart = GetStart();
            var lEnd = GetEndForStatement();
            int grouping = _tokenizer.GroupingLevel;

            List<string> commaWhiteSpace = null;
            bool ateTerminator = false;
            var parameters = ateLeftParen ? ParseVarArgsList(TokenKind.RightParenthesis, true, out commaWhiteSpace, out ateTerminator) : null;
            string closeParenWhiteSpace = ateTerminator || PeekToken(TokenKind.EndOfFile) ? _tokenWhiteSpace : null;
            FunctionDefinition ret;
            if (parameters == null) {
                // error in parameters
                ret = new FunctionDefinition(nameExpr, new Parameter[0]);
                ret.IsCoroutine = isCoroutine;
                if (_verbatim) {
                    AddVerbatimName(name, ret);
                    AddPreceedingWhiteSpace(ret, preWhitespace);
                    if (afterAsyncWhitespace != null) {
                        GetNodeAttributes(ret)[FunctionDefinition.WhitespaceAfterAsync] = afterAsyncWhitespace;
                    }
                    AddSecondPreceedingWhiteSpace(ret, nameWhiteSpace);
                    AddThirdPreceedingWhiteSpace(ret, parenWhiteSpace);
                    AddFourthPreceedingWhiteSpace(ret, closeParenWhiteSpace);
                    if (!ateTerminator) {
                        AddErrorMissingCloseGrouping(ret);
                    }
                    if (!ateLeftParen) {
                        AddErrorIsIncompleteNode(ret);
                    }
                }
                ret.SetLoc(start, lEnd);
                ret.DefIndex = start;
                ret.SetKeywordEndIndex(keywordEnd);
                return ret;
            }

            string arrowWhiteSpace = null;
            Expression returnAnnotation = null;
            if (ateTerminator && MaybeEat(TokenKind.Arrow)) {
                arrowWhiteSpace = _tokenWhiteSpace;
                var arrStart = GetStart();
                returnAnnotation = ParseExpression();
                if (!_stubFile && _langVersion.Is2x()) {
                    ReportSyntaxError(arrStart, returnAnnotation.EndIndex, "invalid syntax, return annotations require 3.x");
                }
            }

            var rStart = GetStart();
            var mid = _lookahead.Span.Start;

            ret = new FunctionDefinition(nameExpr, parameters);
            AddVerbatimName(name, ret);

            PushFunction(ret);

            // set IsCoroutine before parsing the body to enable use of 'await'
            ret.IsCoroutine = isCoroutine;

            Statement body = ParseClassOrFuncBody();
            FunctionDefinition ret2 = PopFunction();
            System.Diagnostics.Debug.Assert(ret == ret2);

            ret.SetBody(body);
            ret.ReturnAnnotation = returnAnnotation;
            // StartIndex may be adjusted later, but we want to keep the def (or
            // async) index.
            ret.DefIndex = start;
            ret.HeaderIndex = mid;
            ret.SetKeywordEndIndex(keywordEnd);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, preWhitespace);
                if (afterAsyncWhitespace != null) {
                    GetNodeAttributes(ret)[FunctionDefinition.WhitespaceAfterAsync] = afterAsyncWhitespace;
                }
                AddSecondPreceedingWhiteSpace(ret, nameWhiteSpace);
                AddThirdPreceedingWhiteSpace(ret, parenWhiteSpace);
                AddFourthPreceedingWhiteSpace(ret, closeParenWhiteSpace);
                AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                if (arrowWhiteSpace != null) {
                    AddFifthPreceedingWhiteSpace(ret, arrowWhiteSpace);
                }
                if (!ateTerminator) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }
            ret.SetLoc(start, body.EndIndex);

            return ret;
        }

        //varargslist: (fpdef ['=' expression ] ',')* ('*' NAME [',' '**' NAME] | '**' NAME) | fpdef ['=' expression] (',' fpdef ['=' expression])* [',']
        //fpdef: NAME | '(' fplist ')'
        //fplist: fpdef (',' fpdef)* [',']
        private Parameter[] ParseVarArgsList(TokenKind terminator, bool allowAnnotations, out List<string> commaWhiteSpace, out bool ateTerminator) {
            var parameters = new List<Parameter>();
            commaWhiteSpace = MakeWhiteSpaceList();

            bool namedOnly = false;
            bool lastComma = true;

            int lastStart = -1;

            // First we parse all parameter names, as leniently as possible.
            for (int pos = 0; !(ateTerminator = MaybeEat(terminator)) && !MaybeEat(TokenKind.EndOfFile); ++pos) {
                Parameter p;

                if (!lastComma || lastStart == GetStart()) {
                    // No progress is being made, so we're probably in invalid code.
                    break;
                }
                lastStart = GetStart();

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    p = ParseSublistParameter(pos);
                } else {
                    var kind = namedOnly ? ParameterKind.KeywordOnly : ParameterKind.Normal;
                    string preStarWhitespace = null;

                    int start = -1;

                    if (MaybeEat(TokenKind.Multiply)) {
                        start = GetStart();
                        kind = ParameterKind.List;
                        namedOnly = _langVersion.Is3x() || _stubFile;
                        preStarWhitespace = _tokenWhiteSpace;
                    } else if (MaybeEat(TokenKind.Power)) {
                        start = GetStart();
                        kind = ParameterKind.Dictionary;
                        preStarWhitespace = _tokenWhiteSpace;
                    }

                    var name = TokenToName(PeekToken());
                    if (name.HasName) {
                        NextToken();
                        var ne = new NameExpression(name.RealName);
                        ne.SetLoc(GetStart(), GetEnd());
                        if (_verbatim) {
                            AddVerbatimName(name, ne);
                            AddPreceedingWhiteSpace(ne);
                        }
                        p = new Parameter(ne, kind);
                    } else if (kind == ParameterKind.List) {
                        // bare lists are allowed
                        p = new Parameter(null, kind);
                    } else {
                        var expr = ParseExpression();
                        p = new ErrorParameter(expr, kind);
                    }
                    p.SetLoc(start < 0 ? GetStart() : start, GetEnd());

                    if (_verbatim && preStarWhitespace != null) {
                        AddPreceedingWhiteSpace(p, preStarWhitespace);
                    }

                    if (allowAnnotations && MaybeEat(TokenKind.Colon)) {
                        p.Annotation = ParseExpression();
                        p.SetLoc(p.StartIndex, GetEnd());
                    }
                }

                if (MaybeEat(TokenKind.Assign)) {
                    if (_verbatim) {
                        GetNodeAttributes(p)[Parameter.WhitespacePrecedingAssign] = _tokenWhiteSpace;
                    }
                    p.DefaultValue = ParseExpression();
                    p.SetLoc(p.StartIndex, GetEnd());
                }

                parameters.Add(p);

                lastComma = MaybeEat(TokenKind.Comma);
                if (lastComma) {
                    commaWhiteSpace?.Add(_tokenWhiteSpace);
                }
            }

            // Now we validate the parameters
            bool seenListArg = false, seenDictArg = false, seenDefault = false;
            var seenNames = new HashSet<string>();
            foreach (var p in parameters) {
                if (p.Annotation != null) {
                    if (!_stubFile && _langVersion.Is2x()) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "invalid syntax, parameter annotations require 3.x");
                        continue;
                    } else if (!allowAnnotations) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "invalid syntax");
                        continue;
                    }
                }

                if (p.DefaultValue == null) {
                    if (seenDefault && p.Kind == ParameterKind.Normal) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "default value must be specified here");
                    }
                } else {
                    seenDefault = true;
                }

                if (p is SublistParameter sp) {
                    if (_stubFile || _langVersion.Is3x()) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "sublist parameters are not supported in 3.x");
                    } else {
                        ValidateSublistParameter(sp.Tuple?.Items, seenNames);
                    }
                    continue;
                }

                if (p is ErrorParameter) {
                    ReportSyntaxError(p.StartIndex, p.EndIndex, "invalid parameter");
                    continue;
                }

                if (string.IsNullOrEmpty(p.Name)) {
                    if (p.Kind != ParameterKind.List || !(_stubFile || _langVersion.Is3x())) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "invalid syntax");
                        continue;
                    }
                } else if (!seenNames.Add(p.Name)) {
                    ReportSyntaxError(p.StartIndex, p.EndIndex, "duplicate argument '{0}' in function definition".FormatUI(p.Name));
                }

                if (p.Kind == ParameterKind.List) {
                    if (seenListArg) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "duplicate * args arguments");
                    }
                    seenListArg = true;
                } else if (p.Kind == ParameterKind.Dictionary) {
                    if (seenDictArg) {
                        ReportSyntaxError(p.StartIndex, p.EndIndex, "duplicate ** args arguments");
                    }
                    seenDictArg = true;
                } else if (seenListArg && p.Kind != ParameterKind.KeywordOnly) {
                    ReportSyntaxError(p.StartIndex, p.EndIndex, "positional parameter after * args not allowed");
                } else if (seenDictArg) {
                    ReportSyntaxError(p.StartIndex, p.EndIndex, "invalid syntax");
                }
            }

            if (parameters.Count > 0 && seenListArg) {
                var p = parameters.Last();
                if (p.Kind == ParameterKind.List && string.IsNullOrEmpty(p.Name)) {
                    ReportSyntaxError(p.StartIndex, p.EndIndex, "named arguments must follow bare *");
                }
            }

            return parameters.ToArray();
        }


        //  parameter ::=
        //      identifier | "(" sublist ")"
        private Parameter ParseSublistParameter(int position) {
            Parameter p = null;
            var sublist = FinishTupleOrGenExp();

            if (sublist is TupleExpression te) {
                p = new SublistParameter(position, te);
                p.SetLoc(te.StartIndex, te.EndIndex);
            } else if ((sublist as ParenthesisExpression)?.Expression is NameExpression ne && _langVersion.Is2x() && !_stubFile) {
                p = new Parameter(ne, ParameterKind.Normal);
                p.SetLoc(ne.StartIndex, ne.EndIndex);
                MoveNodeAttributes(p, sublist, NodeAttributes.PreceedingWhiteSpace);
                MoveNodeAttributes(p, sublist, NodeAttributes.SecondPreceedingWhiteSpace);
                MoveNodeAttributes(p, sublist, NodeAttributes.ErrorMissingCloseGrouping);
                AddIsAltForm(p);
            } else {
                ReportSyntaxError(sublist.StartIndex, sublist.EndIndex, "invalid sublist parameter");
                p = new ErrorParameter(sublist, ParameterKind.Normal);
                p.SetLoc(sublist.StartIndex, sublist.EndIndex);
                if (!(sublist is ParenthesisExpression) && !(sublist is TupleExpression)) {
                    AddIsAltForm(p);
                }
            }
            return p;
        }

        private void ValidateSublistParameter(IEnumerable<Expression> parameters, HashSet<string> seenNames) {
            if (parameters == null) {
                return;
            }

            foreach (var e in parameters) {
                if (e is TupleExpression te) {
                    ValidateSublistParameter(te.Items, seenNames);
                } else if (e is NameExpression ne) {
                    if (string.IsNullOrEmpty(ne.Name)) {
                        ReportSyntaxError(e.StartIndex, e.EndIndex, "invalid sublist parameter");
                    } else if (!seenNames.Add(ne.Name)) {
                        ReportSyntaxError(e.StartIndex, e.EndIndex, "duplicate argument '{0}' in function definition".FormatUI(ne.Name));
                    }
                } else {
                    ReportSyntaxError(e.StartIndex, e.EndIndex, "invalid sublist parameter");
                }
            }
        }

        //Python2.5 -> old_lambdef: 'lambda' [varargslist] ':' old_expression
        private Expression FinishOldLambdef() {
            string whitespace = _tokenWhiteSpace;
            List<string> commaWhiteSpace;
            bool ateTerminator;
            FunctionDefinition func = ParseLambdaHelperStart(out commaWhiteSpace, out ateTerminator);
            string colonWhiteSpace = ateTerminator || PeekToken(TokenKind.EndOfFile) ? _tokenWhiteSpace : null;

            Expression expr = ateTerminator ? ParseOldExpression() : Error("");
            return ParseLambdaHelperEnd(func, expr, whitespace, colonWhiteSpace, commaWhiteSpace, ateTerminator);
        }

        //lambdef: 'lambda' [varargslist] ':' expression
        private Expression FinishLambdef() {
            string whitespace = _tokenWhiteSpace;
            List<string> commaWhiteSpace;
            bool ateTerminator;
            FunctionDefinition func = ParseLambdaHelperStart(out commaWhiteSpace, out ateTerminator);
            string colonWhiteSpace = ateTerminator || PeekToken(TokenKind.EndOfFile) ? _tokenWhiteSpace : null;

            Expression expr = ateTerminator ? ParseExpression() : Error("");
            return ParseLambdaHelperEnd(func, expr, whitespace, colonWhiteSpace, commaWhiteSpace, ateTerminator);
        }


        // Helpers for parsing lambda expressions. 
        // Usage
        //   FunctionDefinition f = ParseLambdaHelperStart(string);
        //   Expression expr = ParseXYZ();
        //   return ParseLambdaHelperEnd(f, expr);
        private FunctionDefinition ParseLambdaHelperStart(out List<string> commaWhiteSpace, out bool ateTerminator) {
            var start = GetStart();

            var parameters = ParseVarArgsList(TokenKind.Colon, false, out commaWhiteSpace, out ateTerminator);
            var mid = GetEnd();

            FunctionDefinition func = new FunctionDefinition(null, parameters.MaybeEnumerate().ToArray());
            func.HeaderIndex = mid;
            func.DefIndex = func.StartIndex = start;

            // Push the lambda function on the stack so that it's available for any yield expressions to mark it as a generator.
            PushFunction(func);

            return func;
        }

        private Expression ParseLambdaHelperEnd(FunctionDefinition func, Expression expr, string whitespace, string colonWhiteSpace, List<string> commaWhiteSpace, bool ateTerminator) {
            // Pep 342 in Python 2.5 allows Yield Expressions, which can occur inside a Lambda body. 
            // In this case, the lambda is a generator and will yield it's final result instead of just return it.
            Statement body;
            if (func.IsGenerator) {
                body = new ExpressionStatement(expr);
            } else {
                body = new ReturnStatement(expr);
            }
            body.SetLoc(expr.StartIndex, GetEndForStatement());

            FunctionDefinition func2 = PopFunction();
            System.Diagnostics.Debug.Assert(func == func2);

            func.SetBody(body);
            func.EndIndex = GetEndForStatement();

            LambdaExpression ret = new LambdaExpression(func);
            func.LambdaExpression = ret;
            func.SetLoc(func.IndexSpan);
            ret.SetLoc(func.IndexSpan);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, whitespace);
                AddSecondPreceedingWhiteSpace(ret, colonWhiteSpace);
                AddListWhiteSpace(ret, commaWhiteSpace.ToArray());
                if (!ateTerminator) {
                    AddErrorIsIncompleteNode(ret);
                }
            }
            return ret;
        }

        //while_stmt: 'while' expression ':' suite ['else' ':' suite]
        private WhileStatement ParseWhileStmt() {
            Eat(TokenKind.KeywordWhile);
            string whileWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            Expression expr = ParseExpression();
            var mid = GetEnd();
            Statement body = ParseLoopSuite();
            Statement else_ = null;
            string elseWhiteSpace = null;
            int end = body.EndIndex;
            int elseIndex = -1;
            if (MaybeEat(TokenKind.KeywordElse)) {
                elseIndex = _lookahead.Span.End;
                elseWhiteSpace = _tokenWhiteSpace;
                else_ = ParseSuite();
                end = else_.EndIndex;
            }
            WhileStatement ret = new WhileStatement(expr, body, else_);
            ret.SetLoc(start, mid, end, elseIndex);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, whileWhiteSpace);
                if (elseWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, elseWhiteSpace);
                }
            }
            return ret;
        }

        //with_stmt: 'with' with_item (',' with_item)* ':' suite
        //with_item: test ['as' expr]
        private WithStatement ParseWithStmt(bool isAsync) {
            var start = isAsync ? GetStart() : 0;
            var asyncWhiteSpace = isAsync ? _tokenWhiteSpace : null;
            Eat(TokenKind.KeywordWith);
            if (!isAsync) {
                start = GetStart();
            }
            int keywordEnd = GetEnd();

            string withWhiteSpace = _tokenWhiteSpace;
            var itemWhiteSpace = MakeWhiteSpaceList();

            List<WithItem> items = new List<WithItem>();
            items.Add(ParseWithItem(itemWhiteSpace));
            while (MaybeEat(TokenKind.Comma)) {
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                items.Add(ParseWithItem(itemWhiteSpace));
            }


            var header = PeekToken(TokenKind.Colon) ? GetEnd() : -1;
            Statement body = ParseSuite();

            WithStatement ret = new WithStatement(items.ToArray(), body, isAsync);
            ret.HeaderIndex = header;
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, isAsync ? asyncWhiteSpace : withWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, isAsync ? withWhiteSpace : null);
                AddListWhiteSpace(ret, itemWhiteSpace.ToArray());
            }
            ret.SetLoc(start, GetEndForStatement());
            ret.SetKeywordEndIndex(keywordEnd);
            return ret;
        }

        private WithItem ParseWithItem(List<string> itemWhiteSpace) {
            var start = GetStart();
            Expression contextManager = ParseExpression();
            Expression var = null;
            int asIndex = -1;
            if (MaybeEat(TokenKind.KeywordAs)) {
                asIndex = GetStart();
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                var = ParseExpression();
            }

            var res = new WithItem(contextManager, var, asIndex);
            res.SetLoc(start, GetEnd());
            return res;
        }

        //for_stmt: 'for' target_list 'in' expression_list ':' suite ['else' ':' suite]
        private Statement ParseForStmt(bool isAsync) {
            var start = isAsync ? GetStart() : 0;
            var asyncWhiteSpace = isAsync ? _tokenWhiteSpace : null;
            Eat(TokenKind.KeywordFor);
            int forIndex = GetStart();
            if (!isAsync) {
                start = GetStart();
            }
            int keywordEnd = GetEnd();
            string forWhiteSpace = _tokenWhiteSpace;

            bool trailingComma;
            List<string> listWhiteSpace;

            List<Expression> l = ParseExpressionList(out trailingComma, out listWhiteSpace);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            string inWhiteSpace = null, elseWhiteSpace = null;
            Expression lhs = MakeTupleOrExpr(l, listWhiteSpace, trailingComma, true);
            Expression list;
            Statement body, else_;
            bool incomplete = false;
            int header, inIndex = -1, elseIndex = -1;
            string newlineWhiteSpace = "";
            int end;
            if ((lhs is ErrorExpression && MaybeEatNewLine(out newlineWhiteSpace)) || !Eat(TokenKind.KeywordIn)) {
                // error handling
                else_ = null;
                end = header = GetEndForStatement();
                list = null;
                body = null;
                lhs = Error(newlineWhiteSpace, lhs);
                incomplete = true;
            } else {
                inWhiteSpace = _tokenWhiteSpace;
                inIndex = GetStart();
                list = ParseTestListAsExpr();
                header = GetEndForStatement();
                body = ParseLoopSuite();
                else_ = null;
                end = body.EndIndex;
                if (MaybeEat(TokenKind.KeywordElse)) {
                    elseIndex = _lookahead.Span.End;
                    elseWhiteSpace = _tokenWhiteSpace;
                    else_ = ParseSuite();
                    end = else_.EndIndex;
                }
            }

            ForStatement ret = new ForStatement(lhs, list, body, else_, isAsync);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, isAsync ? asyncWhiteSpace : forWhiteSpace);
                AddFourthPreceedingWhiteSpace(ret, isAsync ? forWhiteSpace : null);
                if (inWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, inWhiteSpace);
                }
                if (elseWhiteSpace != null) {
                    AddThirdPreceedingWhiteSpace(ret, elseWhiteSpace);
                }
                if (incomplete) {
                    AddErrorIsIncompleteNode(ret);
                }
            }
            ret.ForIndex = forIndex;
            ret.InIndex = inIndex;
            ret.HeaderIndex = header;
            ret.ElseIndex = elseIndex;
            ret.SetKeywordEndIndex(keywordEnd);
            ret.SetLoc(start, end);
            return ret;
        }

        private Statement ParseLoopSuite() {
            Statement body;
            bool inLoop = _inLoop;
            bool inFinally = _inFinally;
            try {
                _inLoop = true;
                _inFinally = false;
                body = ParseSuite();
            } finally {
                _inLoop = inLoop;
                _inFinally = inFinally;
            }
            return body;
        }

        private Statement ParseClassOrFuncBody() {
            Statement body;
            bool inLoop = _inLoop, inFinally = _inFinally, isGenerator = _isGenerator;
            var returnsWithValue = _returnsWithValue;
            try {
                _inLoop = false;
                _inFinally = false;
                _isGenerator = false;
                _returnsWithValue = null;
                body = ParseSuite();
            } finally {
                _inLoop = inLoop;
                _inFinally = inFinally;
                _isGenerator = isGenerator;
                _returnsWithValue = returnsWithValue;
            }
            return body;
        }

        // if_stmt: 'if' expression ':' suite ('elif' expression ':' suite)* ['else' ':' suite]
        private IfStatement ParseIfStmt() {
            Eat(TokenKind.KeywordIf);
            var itemWhiteSpace = MakeWhiteSpaceList();
            if (itemWhiteSpace != null) {
                itemWhiteSpace.Add(_tokenWhiteSpace);
            }

            var start = GetStart();
            List<IfStatementTest> l = new List<IfStatementTest>();
            l.Add(ParseIfStmtTest());

            while (MaybeEat(TokenKind.KeywordElseIf)) {
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                l.Add(ParseIfStmtTest());
            }

            Statement else_ = null;
            string elseWhiteSpace = null;
            int elseIndex = -1;
            if (MaybeEat(TokenKind.KeywordElse)) {
                elseWhiteSpace = _tokenWhiteSpace;
                elseIndex = _lookahead.Span.End;
                else_ = ParseSuite();
            }

            IfStatementTest[] tests = l.ToArray();
            IfStatement ret = new IfStatement(tests, else_);
            if (_verbatim) {
                if (elseWhiteSpace != null) {
                    AddPreceedingWhiteSpace(ret, elseWhiteSpace);
                }
                if (itemWhiteSpace != null) {
                    AddListWhiteSpace(ret, itemWhiteSpace.ToArray());
                }
            }
            ret.ElseIndex = elseIndex;
            ret.SetLoc(start, else_ != null ? else_.EndIndex : tests[tests.Length - 1].EndIndex);
            return ret;
        }

        private IfStatementTest ParseIfStmtTest() {
            var start = GetStart();
            Expression expr = ParseExpression();
            var header = GetEnd();
            Statement suite = ParseSuite();
            IfStatementTest ret = new IfStatementTest(expr, suite);
            ret.SetLoc(start, GetEndForStatement());
            ret.HeaderIndex = header;
            return ret;
        }

        //try_stmt: ('try' ':' suite (except_clause ':' suite)+
        //    ['else' ':' suite] | 'try' ':' suite 'finally' ':' suite)
        //# NB compile.c makes sure that the default except clause is last

        // Python 2.5 grammar
        //try_stmt: 'try' ':' suite
        //          (
        //            (except_clause ':' suite)+
        //            ['else' ':' suite]
        //            ['finally' ':' suite]
        //          |
        //            'finally' : suite
        //          )


        private Statement ParseTryStatement() {
            Eat(TokenKind.KeywordTry);
            string tryWhiteSpace = _tokenWhiteSpace;
            var start = GetStart();
            var mid = _lookahead.Span.End;
            int elseIndex = -1, finallyIndex = -1;
            Statement body = ParseSuite();
            Statement finallySuite = null;
            Statement elseSuite = null;
            TryStatement ret;

            string finallyWhiteSpace = null, elseWhiteSpace = null;
            if (MaybeEat(TokenKind.KeywordFinally)) {
                finallyWhiteSpace = _tokenWhiteSpace;
                finallyIndex = _lookahead.Span.End;
                finallySuite = ParseFinallySuite(finallySuite);
                ret = new TryStatement(body, null, elseSuite, finallySuite);
                ret.HeaderIndex = mid;
            } else {
                List<TryStatementHandler> handlers = new List<TryStatementHandler>();
                TryStatementHandler dh = null;
                while (true) {
                    if (!MaybeEat(TokenKind.KeywordExcept)) {
                        break;
                    }
                    TryStatementHandler handler = ParseTryStmtHandler();

                    handlers.Add(handler);

                    if (dh != null) {
                        ReportSyntaxError(dh.StartIndex, dh.HeaderIndex, "default 'except' must be last");
                    }
                    if (handler.Test == null) {
                        dh = handler;
                    }
                }

                if (MaybeEat(TokenKind.KeywordElse)) {
                    elseWhiteSpace = _tokenWhiteSpace;
                    elseIndex = _lookahead.Span.End;
                    elseSuite = ParseSuite();
                }

                if (MaybeEat(TokenKind.KeywordFinally)) {
                    // If this function has an except block, then it can set the current exception.
                    finallyWhiteSpace = _tokenWhiteSpace;
                    finallyIndex = _lookahead.Span.End;
                    finallySuite = ParseFinallySuite(finallySuite);
                }

                ret = new TryStatement(body, handlers.ToArray(), elseSuite, finallySuite);
                ret.HeaderIndex = mid;
            }
            ret.ElseIndex = elseIndex;
            ret.FinallyIndex = finallyIndex;
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, tryWhiteSpace);
                if (elseWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, elseWhiteSpace);
                }
                if (finallyWhiteSpace != null) {
                    AddThirdPreceedingWhiteSpace(ret, finallyWhiteSpace);
                }
            }
            ret.SetLoc(start, GetEndForStatement());

            return ret;
        }

        private Statement ParseFinallySuite(Statement finallySuite) {
            bool inFinally = _inFinally;
            try {
                _inFinally = true;
                finallySuite = ParseSuite();
            } finally {
                _inFinally = inFinally;
            }
            return finallySuite;
        }

        //except_clause: 'except' [expression [',' expression]]
        //2.6: except_clause: 'except' [expression [(',' or 'as') expression]]
        private TryStatementHandler ParseTryStmtHandler() {
            string exceptWhiteSpace = _tokenWhiteSpace;
            string commaWhiteSpace = null;
            var start = GetStart();
            var keywordEnd = GetEnd();
            Expression test1 = null, test2 = null;
            bool altForm = false;
            if (PeekToken().Kind != TokenKind.Colon) {
                test1 = ParseExpression();

                // parse the expression even if the syntax isn't allowed so we
                // report better error messages when opening against the wrong Python version
                var lookahead = _lookahead;
                if (MaybeEat(TokenKind.KeywordAs) || MaybeEatName("as")) {
                    commaWhiteSpace = _tokenWhiteSpace;
                    if (_langVersion < PythonLanguageVersion.V26 && !_stubFile) {
                        ReportSyntaxError(lookahead.Span.Start, lookahead.Span.End, "'as' requires Python 2.6 or later");
                    }
                    test2 = ParseExpression();
                    altForm = true;
                } else if (MaybeEat(TokenKind.Comma)) {
                    commaWhiteSpace = _tokenWhiteSpace;
                    test2 = ParseExpression();
                    if (_langVersion.Is3x() || _stubFile) {
                        ReportSyntaxError(lookahead.Span.Start, GetEnd(), "\", variable\" not allowed in 3.x - use \"as variable\" instead.");
                    }
                }
            }
            var mid = _lookahead.Span.End;
            Statement body = ParseSuite();
            TryStatementHandler ret = new TryStatementHandler(test1, test2, body);
            ret.HeaderIndex = mid;
            ret.KeywordEndIndex = keywordEnd;
            ret.SetLoc(start, body.EndIndex);

            if (_verbatim) {
                if (altForm) {
                    AddIsAltForm(ret);
                }
                if (commaWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, commaWhiteSpace);
                }
                AddPreceedingWhiteSpace(ret, exceptWhiteSpace);
            }
            return ret;
        }

        //suite: simple_stmt NEWLINE | Newline INDENT stmt+ DEDENT
        private Statement ParseSuite() {

            if (!EatNoEof(TokenKind.Colon)) {
                // improve error handling...
                var error = ErrorStmt(_verbatim ? (_lookaheadWhiteSpace + _lookahead.Token.VerbatimImage) : null);
                error.SetLoc(_lookahead.Span);
                NextToken();
                return error;
            }

            string colonWhiteSpace = _tokenWhiteSpace;

            TokenWithSpan cur = _lookahead;
            List<Statement> l = new List<Statement>();

            // we only read a real NewLine here because we need to adjust error reporting
            // for the interpreter.
            SuiteStatement ret;
            if (MaybeEat(TokenKind.NewLine)) {
                string suiteStartWhiteSpace = null;
                if (_verbatim) {
                    suiteStartWhiteSpace = _tokenWhiteSpace + _token.Token.VerbatimImage;
                }

                CheckSuiteEofError(cur);

                // for error reporting we track the NL tokens and report the error on
                // the last one.  This matches CPython.
                cur = _lookahead;
                while (PeekToken(TokenKind.NLToken)) {
                    cur = _lookahead;
                    NextToken();

                    if (_verbatim) {
                        suiteStartWhiteSpace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                    }
                }

                if (!MaybeEat(TokenKind.Indent)) {
                    // no indent?  report the indentation error.
                    if (cur.Token.Kind == TokenKind.Dedent) {
                        ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, "expected an indented block", ErrorCodes.SyntaxError | ErrorCodes.IncompleteStatement);
                    } else {
                        ReportSyntaxError(cur, ErrorCodes.IndentationError);
                    }
                    return ErrorStmt(_verbatim ? (colonWhiteSpace + ':' + suiteStartWhiteSpace) : null);
                } else if (_verbatim) {
                    // indent white space belongs to the statement we're about to parse
                    _lookaheadWhiteSpace = suiteStartWhiteSpace + _tokenWhiteSpace + _token.Token.VerbatimImage + _lookaheadWhiteSpace;
                }

                while (true) {
                    Statement s = ParseStmt();

                    l.Add(s);
                    if (MaybeEat(TokenKind.Dedent)) {
                        // dedent white space belongs to the statement which follows the suite
                        if (_verbatim) {
                            _lookaheadWhiteSpace = _tokenWhiteSpace + _lookaheadWhiteSpace;
                        }
                        break;
                    }
                    if (PeekToken().Kind == TokenKind.EndOfFile) {
                        ReportSyntaxError("unexpected end of file");
                        break; // error handling
                    }
                }
                ret = new SuiteStatement(l.ToArray());
            } else {
                //  simple_stmt NEWLINE
                //  ParseSimpleStmt takes care of the NEWLINE
                ret = new SuiteStatement(new[] { ParseSimpleStmt() });
                if (_verbatim) {
                    AddSecondPreceedingWhiteSpace(ret, "");
                }
            }

            ret.SetLoc(ret.Statements[0].StartIndex, ret.Statements[ret.Statements.Count - 1].EndIndex);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, colonWhiteSpace);
            }
            return ret;
        }

        private void CheckSuiteEofError(TokenWithSpan cur) {
            if (MaybeEatEof()) {
                // for interactive parsing we allow the user to continue in this case
                ReportSyntaxError(_lookahead.Token, cur.Span, ErrorCodes.SyntaxError, true);
            }
        }

        // Python 2.5 -> old_test: or_test | old_lambdef
        private Expression ParseOldExpression() {
            if (MaybeEat(TokenKind.KeywordLambda)) {
                return FinishOldLambdef();
            }
            return ParseOrTest();
        }

        // expression: conditional_expression | lambda_form
        // conditional_expression: or_test ['if' or_test 'else' expression]
        // lambda_form: "lambda" [parameter_list] : expression
        private Expression ParseExpression() {
            if (MaybeEat(TokenKind.KeywordLambda)) {
                return FinishLambdef();
            }

            Expression ret = ParseOrTest();
            if (ret is ErrorExpression) {
                return ret;
            } else if (MaybeEat(TokenKind.KeywordIf)) {
                var start = ret.StartIndex;
                ret = ParseConditionalTest(ret);
                ret.SetLoc(start, GetEnd());
            }

            return ret;
        }

        // or_test: and_test ('or' and_test)*
        private Expression ParseOrTest() {
            Expression ret = ParseAndTest();
            while (MaybeEat(TokenKind.KeywordOr)) {
                string proceeding = _tokenWhiteSpace;
                var start = ret.StartIndex;
                var orIndex = GetStart();
                ret = new OrExpression(ret, ParseAndTest(), orIndex);
                if (_verbatim) {
                    AddPreceedingWhiteSpace(ret, proceeding);
                }
                ret.SetLoc(start, GetEnd());
            }
            return ret;
        }

        private Expression ParseConditionalTest(Expression trueExpr) {
            int ifIndex = GetStart();
            string ifWhiteSpace = _tokenWhiteSpace;
            Expression expr = ParseOrTest();
            bool ateElse = Eat(TokenKind.KeywordElse);
            int elseIndex = GetStart();
            string elseWhiteSpace;
            Expression falseExpr;
            if (ateElse) {
                elseWhiteSpace = _tokenWhiteSpace;
                falseExpr = ParseExpression();
            } else {
                elseWhiteSpace = null;
                falseExpr = Error("");
            }
            var res = new ConditionalExpression(expr, trueExpr, falseExpr, ifIndex, elseIndex);
            if (_verbatim) {
                AddPreceedingWhiteSpace(res, ifWhiteSpace);
                AddSecondPreceedingWhiteSpace(res, elseWhiteSpace);
                if (!ateElse) {
                    AddErrorIsIncompleteNode(res);
                }
            }
            return res;
        }

        // and_test: not_test ('and' not_test)*
        private Expression ParseAndTest() {
            Expression ret = ParseNotTest();
            while (MaybeEat(TokenKind.KeywordAnd)) {
                string proceeding = _tokenWhiteSpace;

                var start = ret.StartIndex;
                var andIndex = GetStart();
                ret = new AndExpression(ret, ParseAndTest(), andIndex);
                if (_verbatim) {
                    AddPreceedingWhiteSpace(ret, proceeding);
                }
                ret.SetLoc(start, GetEnd());
            }
            return ret;
        }

        //not_test: 'not' not_test | comparison
        private Expression ParseNotTest() {
            if (MaybeEat(TokenKind.KeywordNot)) {
                string proceeding = _tokenWhiteSpace;
                var start = GetStart();
                Expression ret = new UnaryExpression(PythonOperator.Not, ParseNotTest());
                if (_verbatim) {
                    AddPreceedingWhiteSpace(ret, proceeding);
                }
                ret.SetLoc(start, GetEnd());
                return ret;
            } else {
                return ParseComparison();
            }
        }
        //comparison: expr (comp_op expr)*
        //comp_op: '<'|'>'|'=='|'>='|'<='|'<>'|'!='|'in'|'not' 'in'|'is'|'is' 'not'
        private Expression ParseComparison() {
            Expression ret = ParseStarExpression();
            while (true) {
                PythonOperator op;
                string whitespaceBeforeOperator = _lookaheadWhiteSpace;
                string secondWhiteSpace = null;
                bool isLessThanGreaterThan = false, isIncomplete = false;
                int opIndex = -1;
                switch (PeekToken().Kind) {
                    case TokenKind.LessThan: NextToken(); op = PythonOperator.LessThan; break;
                    case TokenKind.LessThanOrEqual: NextToken(); op = PythonOperator.LessThanOrEqual; break;
                    case TokenKind.GreaterThan: NextToken(); op = PythonOperator.GreaterThan; break;
                    case TokenKind.GreaterThanOrEqual: NextToken(); op = PythonOperator.GreaterThanOrEqual; break;
                    case TokenKind.Equals: NextToken(); op = PythonOperator.Equal; break;
                    case TokenKind.NotEquals: NextToken(); op = PythonOperator.NotEqual; break;
                    case TokenKind.LessThanGreaterThan: NextToken(); op = PythonOperator.NotEqual; isLessThanGreaterThan = true; break;
                    case TokenKind.KeywordIn: NextToken(); op = PythonOperator.In; break;

                    case TokenKind.KeywordNot:
                        NextToken();
                        opIndex = GetStart();
                        isIncomplete = !Eat(TokenKind.KeywordIn);
                        secondWhiteSpace = _tokenWhiteSpace;
                        op = PythonOperator.NotIn;
                        break;

                    case TokenKind.KeywordIs:
                        NextToken();
                        opIndex = GetStart();
                        if (MaybeEat(TokenKind.KeywordNot)) {
                            op = PythonOperator.IsNot;
                            secondWhiteSpace = _tokenWhiteSpace;
                        } else {
                            op = PythonOperator.Is;
                        }
                        break;
                    default:
                        return ret;
                }
                if (opIndex < 0) {
                    opIndex = GetStart();
                }

                Expression rhs = ParseComparison();
                BinaryExpression be = new BinaryExpression(op, ret, rhs, opIndex);
                if (_verbatim) {
                    AddPreceedingWhiteSpace(be, whitespaceBeforeOperator);
                    GetNodeAttributes(be)[NodeAttributes.SecondPreceedingWhiteSpace] = secondWhiteSpace;
                    if (isLessThanGreaterThan) {
                        AddVerbatimImage(be, "<>");
                    }
                    if (isIncomplete) {
                        AddErrorIsIncompleteNode(be);
                    }
                }
                be.SetLoc(ret.StartIndex, GetEnd());
                ret = be;
            }
        }

        /*
        expr: xor_expr ('|' xor_expr)*
        xor_expr: and_expr ('^' and_expr)*
        and_expr: shift_expr ('&' shift_expr)*
        shift_expr: arith_expr (('<<'|'>>') arith_expr)*
        arith_expr: term (('+'|'-') term)*
        term: factor (('*'|'@'|'/'|'%'|'//') factor)*
        */
        private Expression ParseExpr() {
            return ParseExpr(0);
        }

        private Expression ParseExpr(int precedence) {
            Expression ret = ParseFactor();
            while (true) {
                Token t = PeekToken();
                if (_langVersion >= PythonLanguageVersion.V35 && t.Kind == TokenKind.At) {
                    t = Tokens.MatMultiplyToken;
                }
                OperatorToken ot = t as OperatorToken;
                if (ot == null) return ret;

                int prec = ot.Precedence;
                if (prec >= precedence) {
                    NextToken();
                    int opIndex = GetStart();
                    string whiteSpace = _tokenWhiteSpace;
                    Expression right = ParseExpr(prec + 1);
                    var start = ret.StartIndex;
                    ret = new BinaryExpression(GetBinaryOperator(ot), ret, right, opIndex);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret, whiteSpace);
                    }
                    ret.SetLoc(start, GetEnd());
                } else {
                    return ret;
                }
            }
        }

        // factor: ('+'|'-'|'~') factor | power
        private Expression ParseFactor() {
            var start = _lookahead.Span.Start;
            Expression ret;
            switch (PeekToken().Kind) {
                case TokenKind.Add:
                    NextToken();
                    string posWhiteSpace = _tokenWhiteSpace;
                    ret = new UnaryExpression(PythonOperator.Pos, ParseFactor());
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret, posWhiteSpace);
                    }
                    break;
                case TokenKind.Subtract:
                    NextToken();
                    ret = FinishUnaryNegate();
                    break;
                case TokenKind.Twiddle:
                    NextToken();
                    string twiddleWhiteSpace = _tokenWhiteSpace;
                    ret = new UnaryExpression(PythonOperator.Invert, ParseFactor());
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret, twiddleWhiteSpace);
                    }
                    break;
                default:
                    return ParseAwaitExpr();
            }
            ret.SetLoc(start, GetEnd());
            return ret;
        }

        private Expression FinishUnaryNegate() {
            // Special case to ensure that System.Int32.MinValue is an int and not a BigInteger
            if (PeekToken().Kind == TokenKind.Constant) {
                Token t = PeekToken();

                if (t.Value is BigInteger) {
                    BigInteger bi = (BigInteger)t.Value;
                    if (bi == 0x80000000) {
                        string tokenString = _tokenizer.GetTokenString(); ;
                        Debug.Assert(tokenString.Length > 0);

                        if (tokenString[tokenString.Length - 1] != 'L' &&
                            tokenString[tokenString.Length - 1] != 'l') {
                            string minusWhiteSpace = _tokenWhiteSpace;

                            NextToken();
                            // TODO Fix the white space here
                            var ret = new ConstantExpression(-2147483648);

                            if (_verbatim) {
                                AddExtraVerbatimText(ret, minusWhiteSpace + "-" + _tokenWhiteSpace + t.VerbatimImage);
                            }
                            return ret;
                        }
                    }
                }
            }

            string whitespace = _tokenWhiteSpace;
            var res = new UnaryExpression(PythonOperator.Negate, ParseFactor());
            if (_verbatim) {
                AddPreceedingWhiteSpace(res, whitespace);
            }
            return res;
        }

        private Expression ParseAwaitExpr() {
            if (_langVersion >= PythonLanguageVersion.V35) {
                var allowed = AllowAsyncAwaitSyntax ||
                    (_langVersion >= PythonLanguageVersion.V37 && _inGeneratorExpression);

                if (allowed && MaybeEat(TokenKind.KeywordAwait)) {
                    var start = GetStart();
                    string whitespace = _tokenWhiteSpace;
                    var res = new AwaitExpression(ParseAwaitExpr());
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(res, whitespace);
                    }
                    res.SetLoc(start, GetEnd());
                    return res;
                }
            }
            return ParsePower();
        }

        // power: atom trailer* ['**' factor]
        private Expression ParsePower() {
            Expression ret = ParsePrimary();
            ret = AddTrailers(ret);
            if (MaybeEat(TokenKind.Power)) {
                var start = ret.StartIndex;
                int opIndex = GetStart();
                string whitespace = _tokenWhiteSpace;
                ret = new BinaryExpression(PythonOperator.Power, ret, ParseFactor(), opIndex);
                if (_verbatim) {
                    AddPreceedingWhiteSpace(ret, whitespace);
                }
                ret.SetLoc(start, GetEnd());
            }
            return ret;
        }


        // primary: atom | attributeref | subscription | slicing | call 
        // atom:    identifier | literal | enclosure 
        // enclosure: 
        //      parenth_form | 
        //      list_display | 
        //      generator_expression | 
        //      dict_display | 
        //      string_conversion | 
        //      yield_atom 
        private Expression ParsePrimary() {
            Token t = PeekToken();
            Expression ret;
            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // parenth_form, generator_expression, yield_atom
                    NextToken();
                    return FinishTupleOrGenExp();
                case TokenKind.LeftBracket:     // list_display
                    NextToken();
                    return FinishListValue();
                case TokenKind.LeftBrace:       // dict_display
                    NextToken();
                    return FinishDictOrSetValue();
                case TokenKind.BackQuote:       // string_conversion
                    NextToken();
                    return FinishStringConversion();
                case TokenKind.KeywordAsync:
                case TokenKind.KeywordAwait:
                // if we made it this far, treat await and async as names
                // See ParseAwaitExpr() for treating 'await' as a keyword
                case TokenKind.Name:            // identifier
                    NextToken();
                    ret = MakeName(TokenToName(t));
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret);
                    }
                    ret.SetLoc(GetStart(), GetEnd());
                    return ret;
                case TokenKind.Ellipsis:
                    NextToken();
                    ret = new ConstantExpression(Ellipsis.Value);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                    }
                    ret.SetLoc(GetStart(), GetEnd());
                    return ret;
                case TokenKind.KeywordTrue:
                    NextToken();
                    ret = new ConstantExpression(true);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret);
                    }
                    ret.SetLoc(GetStart(), GetEnd());
                    return ret;
                case TokenKind.KeywordFalse:
                    NextToken();
                    ret = new ConstantExpression(false);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(ret);
                    }
                    ret.SetLoc(GetStart(), GetEnd());
                    return ret;
                case TokenKind.Constant:        // literal
                    NextToken();
                    var start = GetStart();
                    object cv = t.Value;
                    string cvs = cv as string;
                    AsciiString bytes;
                    if (PeekToken() is ConstantValueToken && (cv is string || cv is AsciiString)) {
                        // string plus
                        string[] verbatimImages = null, verbatimWhiteSpace = null;
                        if (cvs != null) {
                            cv = FinishStringPlus(cvs, t, out verbatimImages, out verbatimWhiteSpace);
                        } else if ((bytes = cv as AsciiString) != null) {
                            cv = FinishBytesPlus(bytes, t, out verbatimImages, out verbatimWhiteSpace);
                        }
                        ret = new ConstantExpression(cv);
                        if (_verbatim) {
                            AddListWhiteSpace(ret, verbatimWhiteSpace);
                            AddVerbatimNames(ret, verbatimImages);
                        }
                    } else {
                        ret = new ConstantExpression(cv);
                        if (_verbatim) {
                            AddExtraVerbatimText(ret, t.VerbatimImage);
                            AddPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                        }
                    }

                    ret.SetLoc(start, GetEnd());
                    return ret;
                case TokenKind.EndOfFile:
                    // don't eat the end of file token
                    ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, _allowIncomplete || _tokenizer.EndContinues);
                    // error node
                    var error = Error(_verbatim ? "" : null);
                    error.SetLoc(_lookahead.Span);
                    return error;
                default:
                    ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, _allowIncomplete || _tokenizer.EndContinues);
                    if (_lookahead.Token.Kind != TokenKind.NewLine) {
                        NextToken();
                        return Error(_verbatim ? (_tokenWhiteSpace + _token.Token.VerbatimImage) : null);
                    }

                    // error node
                    return Error("");
            }
        }

        private string FinishStringPlus(string s, Token initialToken, out string[] verbatimImages, out string[] verbatimWhiteSpace) {
            List<string> verbatimImagesList = null;
            List<string> verbatimWhiteSpaceList = null;
            if (_verbatim) {
                verbatimWhiteSpaceList = new List<string>();
                verbatimImagesList = new List<string>();
                verbatimWhiteSpaceList.Add(_tokenWhiteSpace);
                verbatimImagesList.Add(initialToken.VerbatimImage);
            }

            var res = FinishStringPlus(s, verbatimImagesList, verbatimWhiteSpaceList);
            if (_verbatim) {
                verbatimWhiteSpace = verbatimWhiteSpaceList.ToArray();
                verbatimImages = verbatimImagesList.ToArray();
            } else {
                verbatimWhiteSpace = verbatimImages = null;
            }
            return res;
        }

        private string FinishStringPlus(string s, List<string> verbatimImages, List<string> verbatimWhiteSpace) {
            Token t = PeekToken();
            while (true) {
                if (t is ConstantValueToken) {
                    string cvs;
                    AsciiString bytes;
                    if ((cvs = t.Value as String) != null) {
                        s += cvs;
                        NextToken();
                        if (_verbatim) {
                            verbatimWhiteSpace.Add(_tokenWhiteSpace);
                            verbatimImages.Add(t.VerbatimImage);
                        }
                        t = PeekToken();
                        continue;
                    } else if ((bytes = t.Value as AsciiString) != null) {
                        if (_langVersion.Is3x()) {
                            ReportSyntaxError("cannot mix bytes and nonbytes literals");
                        }

                        s += bytes.String;
                        NextToken();
                        if (_verbatim) {
                            verbatimWhiteSpace.Add(_tokenWhiteSpace);
                            verbatimImages.Add(t.VerbatimImage);
                        }
                        t = PeekToken();
                        continue;
                    } else {
                        ReportSyntaxError("invalid syntax");
                    }
                }
                break;
            }
            return s;
        }

        internal static string MakeString(IList<byte> bytes) {
            StringBuilder res = new StringBuilder(bytes.Count);
            for (int i = 0; i < bytes.Count; i++) {
                res.Append((char)bytes[i]);
            }
            return res.ToString();
        }

        private object FinishBytesPlus(AsciiString s, Token initialToken, out string[] verbatimImages, out string[] verbatimWhiteSpace) {
            List<string> verbatimImagesList = null;
            List<string> verbatimWhiteSpaceList = null;
            if (_verbatim) {
                verbatimWhiteSpaceList = new List<string>();
                verbatimImagesList = new List<string>();
                verbatimWhiteSpaceList.Add(_tokenWhiteSpace);
                verbatimImagesList.Add(initialToken.VerbatimImage);
            }

            var res = FinishBytesPlus(s, verbatimImagesList, verbatimWhiteSpaceList);

            if (_verbatim) {
                verbatimWhiteSpace = verbatimWhiteSpaceList.ToArray();
                verbatimImages = verbatimImagesList.ToArray();
            } else {
                verbatimWhiteSpace = verbatimImages = null;
            }
            return res;
        }

        private object FinishBytesPlus(AsciiString s, List<string> verbatimImages, List<string> verbatimWhiteSpace) {
            Token t = PeekToken();
            while (true) {
                if (t is ConstantValueToken) {
                    AsciiString cvs;
                    string str;
                    if ((cvs = t.Value as AsciiString) != null) {
                        List<byte> res = new List<byte>(s.Bytes);
                        res.AddRange(cvs.Bytes);
                        s = new AsciiString(res.ToArray(), s.String + cvs.String);
                        NextToken();
                        if (_verbatim) {
                            verbatimWhiteSpace.Add(_tokenWhiteSpace);
                            verbatimImages.Add(t.VerbatimImage);
                        }
                        t = PeekToken();
                        continue;
                    } else if ((str = t.Value as string) != null) {
                        if (_langVersion.Is3x()) {
                            ReportSyntaxError("cannot mix bytes and nonbytes literals");
                        }

                        string final = s.String + str;
                        NextToken();
                        if (_verbatim) {
                            verbatimWhiteSpace.Add(_tokenWhiteSpace);
                            verbatimImages.Add(t.VerbatimImage);
                        }

                        return FinishStringPlus(final, verbatimImages, verbatimWhiteSpace);
                    } else {
                        ReportSyntaxError("invalid syntax");
                    }
                }
                break;
            }
            return s;
        }

        private Expression AddTrailers(Expression ret) {
            return AddTrailers(ret, true);
        }

        // trailer: '(' [ arglist_genexpr ] ')' | '[' subscriptlist ']' | '.' NAME
        private Expression AddTrailers(Expression ret, bool allowGeneratorExpression) {
            bool prevAllow = _allowIncomplete;
            try {
                _allowIncomplete = true;
                while (true) {
                    switch (PeekToken().Kind) {
                        case TokenKind.LeftParenthesis:
                            if (!allowGeneratorExpression) return ret;

                            NextToken();
                            string whitespace = _tokenWhiteSpace;
                            List<string> commaWhiteSpace;
                            bool ateTerminator;
                            Arg[] args = FinishArgListOrGenExpr(out commaWhiteSpace, out ateTerminator);
                            string closeParenWhiteSpace = _tokenWhiteSpace;
                            CallExpression call;
                            if (args != null) {
                                call = FinishCallExpr(ret, args);
                            } else {
                                call = new CallExpression(ret, new Arg[0]);
                            }

                            if (_verbatim) {
                                AddPreceedingWhiteSpace(call, whitespace);
                                AddSecondPreceedingWhiteSpace(call, closeParenWhiteSpace);
                                if (commaWhiteSpace != null) {
                                    AddListWhiteSpace(call, commaWhiteSpace.ToArray());
                                }
                                if (!ateTerminator) {
                                    AddErrorMissingCloseGrouping(call);
                                }
                            }

                            call.SetLoc(ret.StartIndex, GetEnd());
                            ret = call;
                            break;
                        case TokenKind.LeftBracket:
                            NextToken();
                            whitespace = _tokenWhiteSpace;

                            Expression index = ParseSubscriptList(out ateTerminator);
                            IndexExpression ie = new IndexExpression(ret, index);
                            string finishWhiteSpace = _tokenWhiteSpace;
                            ie.SetLoc(ret.StartIndex, GetEnd());
                            if (_verbatim) {
                                AddPreceedingWhiteSpace(ie, whitespace);
                                AddSecondPreceedingWhiteSpace(ie, finishWhiteSpace);
                                if (!ateTerminator) {
                                    AddErrorMissingCloseGrouping(ie);
                                }
                            }
                            ret = ie;
                            break;
                        case TokenKind.Dot:
                            NextToken();
                            int dotStart = GetStart();
                            whitespace = _tokenWhiteSpace;
                            var name = ReadNameMaybeNone();
                            string nameWhitespace = _tokenWhiteSpace;
                            MemberExpression fe = MakeMember(ret, name);
                            fe.SetLoc(ret.StartIndex, name.HasName ? GetStart() : GetEnd(), GetEnd());
                            fe.DotIndex = dotStart;
                            if (_verbatim) {
                                AddPreceedingWhiteSpace(fe, whitespace);
                                AddSecondPreceedingWhiteSpace(fe, nameWhitespace);
                                if (!name.HasName) {
                                    AddErrorIsIncompleteNode(fe);
                                }
                            }
                            ret = fe;
                            break;
                        case TokenKind.Constant:
                            // abc.1, abc"", abc 1L, abc 0j
                            ReportSyntaxError("invalid syntax");
                            ret = Error(_verbatim ? _lookaheadWhiteSpace + _lookahead.Token.VerbatimImage : null, ret);
                            NextToken();
                            break;
                        default:
                            return ret;
                    }
                }
            } finally {
                _allowIncomplete = prevAllow;
            }
        }

        //subscriptlist: subscript (',' subscript)* [',']
        //subscript: '.' '.' '.' | expression | [expression] ':' [expression] [sliceop]
        //sliceop: ':' [expression]
        private Expression ParseSubscriptList(out bool ateTerminator) {
            const TokenKind terminator = TokenKind.RightBracket;
            var start0 = GetStart();
            bool trailingComma = false;

            List<Expression> l = new List<Expression>();
            List<string> listWhiteSpace = MakeWhiteSpaceList();
            while (true) {
                Expression e;
                if (MaybeEat(TokenKind.Dot)) {
                    string whitespace = _tokenWhiteSpace;
                    var start = GetStart();
                    if (Eat(TokenKind.Dot)) {
                        if (Eat(TokenKind.Dot)) {
                            e = new ConstantExpression(Ellipsis.Value);
                            if (_verbatim) {
                                AddPreceedingWhiteSpace(e, whitespace);
                            }
                        } else {
                            e = Error(_verbatim ? whitespace + ".." : null);
                        }
                    } else {
                        e = Error(_verbatim ? whitespace + "." : null);
                    }
                    e.SetLoc(start, GetEnd());
                } else if (MaybeEat(TokenKind.Colon)) {
                    e = FinishSlice(null, GetStart());
                } else {
                    e = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) {
                        e = FinishSlice(e, e.StartIndex);
                    }
                }

                l.Add(e);
                if (!MaybeEat(TokenKind.Comma)) {
                    ateTerminator = Eat(terminator);
                    trailingComma = false;
                    break;
                }
                if (listWhiteSpace != null) {
                    listWhiteSpace.Add(_tokenWhiteSpace);
                }

                trailingComma = true;
                if (MaybeEat(terminator)) {
                    ateTerminator = true;
                    break;
                }
            }
            Expression ret = MakeTupleOrExpr(l, listWhiteSpace, trailingComma, true);
            if (l.Count != 1 || ret != l[0]) {
                ret.SetLoc(start0, GetEnd());
            }
            return ret;
        }

        private Expression ParseSliceEnd() {
            Expression e2 = null;
            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                default:
                    e2 = ParseExpression();
                    break;
            }
            return e2;
        }

        private Expression FinishSlice(Expression e0, int start) {
            Expression e1 = null;
            Expression e2 = null;
            bool stepProvided = false;
            Debug.Assert(_token.Token.Kind == TokenKind.Colon);
            string colonWhiteSpace = _tokenWhiteSpace;
            string secondColonWhiteSpace = null;

            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                case TokenKind.Colon:
                    // x[?::?]
                    stepProvided = true;
                    NextToken();
                    secondColonWhiteSpace = _tokenWhiteSpace;
                    e2 = ParseSliceEnd();
                    break;
                default:
                    // x[?:val:?]
                    e1 = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) {
                        secondColonWhiteSpace = _tokenWhiteSpace;
                        stepProvided = true;
                        e2 = ParseSliceEnd();
                    }
                    break;
            }
            SliceExpression ret = new SliceExpression(e0, e1, e2, stepProvided);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, colonWhiteSpace);
                if (secondColonWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, secondColonWhiteSpace);
                }
            }
            ret.SetLoc(start, GetEnd());
            return ret;
        }


        //exprlist: expr (',' expr)* [',']
        private List<Expression> ParseExprList(out List<string> commaWhiteSpace) {
            List<Expression> l = new List<Expression>();
            commaWhiteSpace = MakeWhiteSpaceList();
            while (true) {
                Expression e = ParseExpr();
                l.Add(e);
                if (!MaybeEat(TokenKind.Comma)) {
                    break;
                }
                if (commaWhiteSpace != null) {
                    commaWhiteSpace.Add(_tokenWhiteSpace);
                }
                if (NeverTestToken(PeekToken())) {
                    break;
                }
            }
            return l;
        }

        // arglist:
        //             expression                     rest_of_arguments
        //             expression "=" expression      rest_of_arguments
        //             expression "for" gen_expr_rest
        //
        private Arg[] FinishArgListOrGenExpr(out List<string> commaWhiteSpace, out bool ateTerminator) {
            Arg a = null;
            commaWhiteSpace = MakeWhiteSpaceList();

            Token t = PeekToken();
            if (t.Kind != TokenKind.RightParenthesis && t.Kind != TokenKind.Multiply && t.Kind != TokenKind.Power) {
                Expression e = ParseExpression();
                if (e is ErrorExpression) {
                    ateTerminator = false;
                    a = new Arg(e);
                    a.SetLoc(e.StartIndex, e.EndIndex);
                    a.EndIndexIncludingWhitespace = e.EndIndex;
                    return new[] { a };
                }

                if (MaybeEat(TokenKind.Assign)) {               //  Keyword argument
                    a = FinishKeywordArgument(e);
                } else if (PeekTokenForOrAsyncForToStartGenerator) {    //  Generator expression
                    var genExpr = ParseGeneratorExpression(e);
                    AddIsAltForm(genExpr);
                    a = new Arg(genExpr);
                    a.SetLoc(e.StartIndex, GetEnd());
                    ateTerminator = Eat(TokenKind.RightParenthesis);
                    a.EndIndexIncludingWhitespace = GetStart();
                    return new Arg[1] { a };       //  Generator expression is the argument
                } else {
                    a = new Arg(e);
                    a.SetLoc(e.StartIndex, e.EndIndex);
                }

                //  Was this all?
                //
                if (MaybeEat(TokenKind.Comma)) {
                    if (commaWhiteSpace != null) {
                        commaWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    a.EndIndexIncludingWhitespace = GetStart();
                } else {
                    ateTerminator = Eat(TokenKind.RightParenthesis);
                    a.EndIndexIncludingWhitespace = GetStart();
                    return new Arg[1] { a };
                }
            }

            return FinishArgumentList(a, commaWhiteSpace, out ateTerminator);   // TODO: Use ateTerminator
        }

        private Arg FinishKeywordArgument(Expression t) {
            Debug.Assert(_token.Token.Kind == TokenKind.Assign);
            string equalWhiteSpace = _tokenWhiteSpace;
            NameExpression n = t as NameExpression;

            string name;
            if (n == null) {
                ReportSyntaxError(t.StartIndex, t.EndIndex, "expected name");
                name = null;
            } else {
                name = n.Name;
            }

            Expression val = ParseExpression();
            Arg arg = new Arg(t, val);
            arg.SetLoc(t.StartIndex, val.EndIndex);
            if (_verbatim) {
                AddPreceedingWhiteSpace(arg, equalWhiteSpace);
            }

            // we're losing the name expression...
            return arg;
        }

        private void CheckUniqueArgument(List<Arg> names, Arg arg) {
            if (arg != null && arg.Name != null) {
                string name = arg.Name;
                for (int i = 0; i < names.Count; i++) {
                    if (names[i].Name == arg.Name) {
                        ReportSyntaxError("duplicate keyword argument");
                    }
                }
            }
        }

        //arglist: (argument ',')* (argument [',']| '*' expression [',' '**' expression] | '**' expression)
        //argument: [expression '='] expression    # Really [keyword '='] expression
        private Arg[] FinishArgumentList(Arg first, List<string> commaWhiteSpace, out bool ateTerminator) {
            const TokenKind terminator = TokenKind.RightParenthesis;
            List<Arg> l = new List<Arg>();

            if (first != null) {
                l.Add(first);
            }

            // Parse remaining arguments
            while (true) {
                if (MaybeEat(terminator)) {
                    ateTerminator = true;
                    break;
                }
                int start;
                Arg a;
                if (MaybeEat(TokenKind.Multiply)) {
                    string starWhiteSpace = _tokenWhiteSpace;
                    start = GetStart();
                    Expression t = ParseExpression();
                    var name = new NameExpression("*");
                    a = new Arg(name, t);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(name, starWhiteSpace);
                    }
                } else if (MaybeEat(TokenKind.Power)) {
                    string starStarWhiteSpace = _tokenWhiteSpace;
                    start = GetStart();
                    Expression t = ParseExpression();
                    var name = new NameExpression("**");
                    a = new Arg(name, t);
                    if (_verbatim) {
                        AddPreceedingWhiteSpace(name, starStarWhiteSpace);
                    }
                } else {
                    Expression e = ParseExpression();
                    start = e.StartIndex;
                    if (MaybeEat(TokenKind.Assign)) {
                        a = FinishKeywordArgument(e);
                        CheckUniqueArgument(l, a);
                    } else {
                        a = new Arg(e);
                    }
                }
                a.SetLoc(start, GetEnd());
                l.Add(a);
                if (MaybeEat(TokenKind.Comma)) {
                    if (commaWhiteSpace != null) {
                        commaWhiteSpace.Add(_tokenWhiteSpace);
                    }
                    a.EndIndexIncludingWhitespace = GetStart();
                } else {
                    ateTerminator = Eat(terminator);
                    a.EndIndexIncludingWhitespace = ateTerminator ? GetStart() : a.EndIndex;
                    break;
                }
            }

            return l.ToArray();
        }

        private Expression ParseOldExpressionListAsExpr() {
            bool trailingComma;
            List<string> itemWhiteSpace;
            List<Expression> l = ParseOldExpressionList(out trailingComma, out itemWhiteSpace);
            //  the case when no expression was parsed e.g. when we have an empty expression list
            if (l.Count == 0 && !trailingComma) {
                ReportSyntaxError("invalid syntax");
            }
            return MakeTupleOrExpr(l, itemWhiteSpace, trailingComma, true);
        }

        // old_expression_list: old_expression [(',' old_expression)+ [',']]
        private List<Expression> ParseOldExpressionList(out bool trailingComma, out List<string> itemWhiteSpace) {
            List<Expression> l = new List<Expression>();
            itemWhiteSpace = MakeWhiteSpaceList();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseOldExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                trailingComma = true;
            }
            return l;
        }

        // expression_list: expression (',' expression)* [',']
        private List<Expression> ParseExpressionList(out bool trailingComma, out List<string> whitespace) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            whitespace = MakeWhiteSpaceList();

            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseStarExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                if (whitespace != null) {
                    whitespace.Add(_tokenWhiteSpace);
                }
                trailingComma = true;
            }

            return l;
        }

        // 3.x: star_expr: ['*' | '**'] expr
        private Expression ParseStarExpression() {
            var token = PeekToken().Kind;
            if ((token == TokenKind.Multiply || token == TokenKind.Power) && Eat(token)) {
                string whitespace = _tokenWhiteSpace;
                if (_langVersion.Is2x() && !_stubFile) {
                    ReportSyntaxError("invalid syntax");
                }
                var start = GetStart();
                var expr = ParseExpr();
                var res = new StarredExpression(expr, token == TokenKind.Power ? 2 : 1);
                if (_verbatim) {
                    AddPreceedingWhiteSpace(res, whitespace);
                }
                res.SetLoc(start, expr.EndIndex);
                return res;
            }

            return ParseExpr();
        }

        private Expression ParseTestListAsExpr() {
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseExpression();
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestListAsExpr(expr);
            } else {
                return ParseTestListAsExprError();
            }
        }

        private Expression ParseTestListAsExpr(Expression expr) {
            List<string> itemWhiteSpace;
            bool trailingComma;
            List<Expression> l = ParseTestListAsExpr(expr, out itemWhiteSpace, out trailingComma);
            return MakeTupleOrExpr(l, itemWhiteSpace, trailingComma, parenFreeTuple: true);
        }

        private List<Expression> ParseTestListAsExpr(Expression expr, out List<string> itemWhiteSpace, out bool trailingComma, List<int> whitespaceAfterExpression = null) {
            var l = new List<Expression>();
            itemWhiteSpace = MakeWhiteSpaceList();
            if (expr != null) {
                l.Add(expr);
                if (itemWhiteSpace != null) {
                    Debug.Assert(_token.Token.Kind == TokenKind.Comma);
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
            }

            trailingComma = true;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                var e = ParseExpression();
                l.Add(e);

                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                if (whitespaceAfterExpression != null) {
                    whitespaceAfterExpression.Add(GetStart() - e.EndIndex);
                }
            }
            return l;
        }

        private Expression ParseTestListAsExprError() {
            if (MaybeEat(TokenKind.Indent)) {
                // the error is on the next token which has a useful location, unlike the indent - note we don't have an
                // indent if we're at an EOF.  It'a also an indentation error instead of a syntax error.
                string indentVerbatim = _verbatim ? _tokenWhiteSpace + _token.Token.VerbatimImage : null;
                NextToken();
                ReportSyntaxError(GetStart(), GetEnd(), "unexpected indent", ErrorCodes.IndentationError);
                return Error(_verbatim ? (indentVerbatim + _tokenWhiteSpace + _token.Token.VerbatimImage) : null);
            } else {
                ReportSyntaxError(_lookahead);
            }
            NextToken();
            return Error(_verbatim ? (_tokenWhiteSpace + _token.Token.VerbatimImage) : null);
        }

        private Expression FinishExpressionListAsExpr(Expression expr) {
            var start = GetStart();
            bool trailingComma = true;
            List<Expression> l = new List<Expression>();
            List<string> itemWhiteSpace = MakeWhiteSpaceList();
            if (itemWhiteSpace != null) {
                itemWhiteSpace.Add(_tokenWhiteSpace);
            }
            l.Add(expr);

            while (true) {
                if (NeverTestToken(PeekToken())) break;

                expr = ParseExpression();
                l.Add(expr);
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                if (itemWhiteSpace != null) {
                    itemWhiteSpace.Add(_tokenWhiteSpace);
                }
                trailingComma = true;
            }

            Expression ret = MakeTupleOrExpr(l, itemWhiteSpace, trailingComma);
            ret.SetLoc(start, GetEnd());
            return ret;
        }

        //
        //  testlist_gexp: expression ( genexpr_for | (',' expression)* [','] )
        //
        private Expression FinishTupleOrGenExp() {
            string startingWhiteSpace = _tokenWhiteSpace;
            var lStart = GetStart();
            int grouping = _tokenizer.GroupingLevel;
            bool hasRightParenthesis;

            Expression ret;
            //  Empty tuple
            if (MaybeEat(TokenKind.RightParenthesis)) {
                ret = MakeTupleOrExpr(new List<Expression>(), MakeWhiteSpaceList(), false);
                hasRightParenthesis = true;
            } else if (PeekToken(TokenKind.KeywordYield)) {
                if (!AllowYieldSyntax && AllowAsyncAwaitSyntax) {
                    ReportSyntaxError("'yield' inside async function");
                }
                Eat(TokenKind.KeywordYield);
                ret = new ParenthesisExpression(ParseYieldExpression());
                hasRightParenthesis = Eat(TokenKind.RightParenthesis);
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;

                    Expression expr = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        // "(" expression "," ...
                        ret = FinishExpressionListAsExpr(expr);
                    } else if (PeekTokenForOrAsyncForToStartGenerator) {
                        // "(" expression "for" ...
                        if (expr is StarredExpression) {
                            ReportSyntaxError(expr.StartIndex, expr.EndIndex, "iterable unpacking cannot be used in comprehension");
                        }
                        ret = ParseGeneratorExpression(expr, startingWhiteSpace);
                    } else {
                        // "(" expression ")"
                        ret = new ParenthesisExpression(expr);
                        if (expr is StarredExpression) {
                            ReportSyntaxError(expr.StartIndex, expr.EndIndex, "can't use starred expression here");
                        }
                    }
                    hasRightParenthesis = Eat(TokenKind.RightParenthesis);
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, startingWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                if (!hasRightParenthesis) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }

            ret.SetLoc(lStart, GetEnd());
            return ret;
        }

        //  genexpr_for  ::= [async] "for" target_list "in" or_test [comp_iter]
        //
        //  "[async] for" has NOT been eaten before entering this method
        private Expression ParseGeneratorExpression(Expression expr, string rightParenWhiteSpace = null) {
            GeneratorExpression ret;
            var prevIn = _inGeneratorExpression;
            _inGeneratorExpression = true;
            try {
                ComprehensionIterator[] iters = ParseCompIter();

                ret = new GeneratorExpression(expr, iters);
            } finally {
                _inGeneratorExpression = prevIn;
            }

            ret.SetLoc(expr.StartIndex, GetEnd());
            return ret;
        }

        private static Statement NestGenExpr(Statement current, Statement nested) {
            ForStatement fes = current as ForStatement;
            IfStatement ifs;
            if (fes != null) {
                fes.Body = nested;
            } else if ((ifs = current as IfStatement) != null) {
                ifs.TestsInternal[0].Body = nested;
            }
            return nested;
        }

        /*
        // "for" target_list "in" or_test
        private ForStatement ParseGenExprFor() {
            var start = GetStart();
            Eat(TokenKind.KeywordFor);
            bool trailingComma;
            List<string> listWhiteSpace;
            List<Expression> l = ParseTargetList(out trailingComma, out listWhiteSpace);
            Expression lhs = MakeTupleOrExpr(l, listWhiteSpace, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression expr = null;
            expr = ParseOrTest();

            ForStatement gef = new ForStatement(lhs, expr, null, null);
            var end = GetEnd();
            gef.SetLoc(start, end);
            gef.HeaderIndex = end;
            return gef;
        }

        //  genexpr_if: "if" old_test
        private IfStatement ParseGenExprIf() {
            var start = GetStart();
            Eat(TokenKind.KeywordIf);
            Expression expr = ParseOldExpression();
            IfStatementTest ist = new IfStatementTest(expr, null);
            var end = GetEnd();
            ist.HeaderIndex = end;
            ist.SetLoc(start, end);
            IfStatement gei = new IfStatement(new IfStatementTest[] { ist }, null);
            gei.SetLoc(start, end);
            return gei;
        }
        */

        // dict_display: '{' [dictorsetmaker] '}'
        // dictorsetmaker: ( (test ':' test (comp_for | (',' test ':' test)* [','])) |
        //                   (test (comp_for | (',' test)* [','])) )


        private Expression FinishDictOrSetValue() {
            string startWhiteSpace = _tokenWhiteSpace, finishWhiteSpace;
            var oStart = GetStart();
            var oEnd = GetEnd();

            List<SliceExpression> dictMembers = null;
            List<Expression> setMembers = null;
            List<string> itemWhiteSpace = MakeWhiteSpaceList();
            bool prevAllow = _allowIncomplete;
            bool reportedError = false;
            bool ateTerminator = false;
            bool hasSequenceUnpack = false, hasDictUnpack = false;
            try {
                _allowIncomplete = true;
                while (true) {
                    if (MaybeEat(TokenKind.RightBrace)) { // empty dict literal
                        finishWhiteSpace = _tokenWhiteSpace;
                        ateTerminator = true;
                        break;
                    }

                    bool isSequenceUnpack = false, isDictUnpack = false;
                    bool first = false;
                    Expression e1 = ParseExpression();
                    if (e1 is StarredExpression s) {
                        if (s.StarCount == 1) {
                            isSequenceUnpack = true;
                            hasSequenceUnpack = true;
                        } else if (s.StarCount == 2) {
                            isDictUnpack = true;
                            hasDictUnpack = true;
                        }
                    }

                    if (MaybeEat(TokenKind.Colon)) { // dict literal
                        string colonWhiteSpace = _tokenWhiteSpace;
                        if (setMembers == null && dictMembers == null) {
                            dictMembers = new List<SliceExpression>();
                            first = true;
                        }
                        Expression e2 = ParseExpression();

                        if (!reportedError) {
                            if (setMembers != null || hasSequenceUnpack || isSequenceUnpack || isDictUnpack) {
                                ReportSyntaxError(e1.StartIndex, e2.EndIndex, "invalid syntax");
                                reportedError = true;
                            }
                        }


                        SliceExpression se = new SliceExpression(e1, e2, null, false);
                        if (_verbatim) {
                            AddPreceedingWhiteSpace(se, colonWhiteSpace);
                        }
                        se.SetLoc(e1.StartIndex, e2.EndIndex);

                        if (PeekTokenForOrAsyncFor) {
                            if (!first || (!_stubFile && _langVersion < PythonLanguageVersion.V27)) {
                                ReportSyntaxError("invalid syntax");
                            }

                            var dictComp = FinishDictComp(se, out ateTerminator);
                            if (_verbatim) {
                                AddPreceedingWhiteSpace(dictComp, startWhiteSpace);
                                AddSecondPreceedingWhiteSpace(dictComp, _tokenWhiteSpace);
                                if (!ateTerminator) {
                                    AddErrorMissingCloseGrouping(dictComp);
                                }
                            }
                            dictComp.SetLoc(oStart, GetEnd());
                            return dictComp;
                        }

                        if (dictMembers != null) {
                            dictMembers.Add(se);
                        } else {
                            setMembers.Add(se);
                        }
                    } else { // set literal or dict unpack
                        if (!_stubFile && _langVersion < PythonLanguageVersion.V27 && !reportedError) {
                            ReportSyntaxError(e1.StartIndex, e1.EndIndex, "invalid syntax, set literals require Python 2.7 or later.");
                            reportedError = true;
                        }

                        if (isDictUnpack && hasDictUnpack) {
                            // **{}, we don't have a colon and a value...
                            if (setMembers != null && !reportedError) {
                                ReportSyntaxError(e1.StartIndex, e1.EndIndex, "invalid syntax");
                                reportedError = true;
                            }

                            if (dictMembers == null) {
                                dictMembers = new List<SliceExpression>();
                            }
                            dictMembers.Add(new DictValueOnlyExpression(e1));
                        } else {
                            if (dictMembers != null) {
                                if (!reportedError) {
                                    ReportSyntaxError(e1.StartIndex, e1.EndIndex, "invalid syntax");
                                    reportedError = true;
                                }
                            } else if (setMembers == null) {
                                setMembers = new List<Expression>();
                                first = true;
                            }

                            if (PeekTokenForOrAsyncFor) {
                                if (!first) {
                                    ReportSyntaxError("invalid syntax");
                                }
                                var setComp = FinishSetComp(e1, out ateTerminator);
                                if (_verbatim) {
                                    AddPreceedingWhiteSpace(setComp, startWhiteSpace);
                                    AddSecondPreceedingWhiteSpace(setComp, _tokenWhiteSpace);
                                    if (!ateTerminator) {
                                        AddErrorMissingCloseGrouping(setComp);
                                    }
                                }
                                setComp.SetLoc(oStart, GetEnd());
                                return setComp;
                            }

                            // error recovery
                            if (setMembers != null) {
                                setMembers.Add(e1);
                            } else {
                                var slice = new DictKeyOnlyExpression(e1);
                                slice.SetLoc(e1.IndexSpan);
                                if (_verbatim) {
                                    AddErrorIsIncompleteNode(slice);
                                }
                                dictMembers.Add(slice);
                            }
                        }
                    }

                    if (!MaybeEat(TokenKind.Comma)) {
                        ateTerminator = Eat(TokenKind.RightBrace);
                        finishWhiteSpace = _tokenWhiteSpace;
                        break;
                    }
                    if (itemWhiteSpace != null) {
                        itemWhiteSpace.Add(_tokenWhiteSpace);
                    }
                }
            } finally {
                _allowIncomplete = prevAllow;
            }


            Expression ret;
            if (dictMembers != null || setMembers == null) {
                SliceExpression[] exprs;
                if (dictMembers != null) {
                    exprs = dictMembers.ToArray();
                } else {
                    exprs = new SliceExpression[0];
                }
                ret = new DictionaryExpression(exprs);
            } else {
                ret = new SetExpression(setMembers.ToArray());
            }
            ret.SetLoc(oStart, GetEnd());
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, startWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, finishWhiteSpace);
                AddListWhiteSpace(ret, itemWhiteSpace.ToArray());
                if (!ateTerminator) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }
            return ret;
        }

        // comp_iter '}'
        private SetComprehension FinishSetComp(Expression item, out bool ateTerminator) {
            ComprehensionIterator[] iters = ParseCompIter();
            ateTerminator = Eat(TokenKind.RightBrace);
            return new SetComprehension(item, iters);
        }

        // comp_iter '}'
        private DictionaryComprehension FinishDictComp(SliceExpression value, out bool ateTerminator) {
            ComprehensionIterator[] iters = ParseCompIter();
            ateTerminator = Eat(TokenKind.RightBrace);
            return new DictionaryComprehension(value, iters);
        }

        // comp_iter: comp_for | comp_if
        private ComprehensionIterator[] ParseCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseCompFor();
            iters.Add(firstFor);

            while (true) {
                if (PeekTokenForOrAsyncFor) {
                    iters.Add(ParseCompFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iters.Add(ParseCompIf());
                } else {
                    break;
                }
            }

            return iters.ToArray();
        }

        private bool PeekTokenForOrAsyncFor {
            get {
                if (PeekToken(Tokens.KeywordForToken)) {
                    return true;
                }
                if (AllowAsyncAwaitSyntax || (_langVersion >= PythonLanguageVersion.V37 && _inGeneratorExpression)) {
                    return PeekToken(Tokens.KeywordAsyncToken) && PeekToken2()?.Kind == TokenKind.KeywordFor;
                }
                return false;
            }
        }

        private bool PeekTokenForOrAsyncForToStartGenerator {
            get {
                var prevIn = _inGeneratorExpression;
                _inGeneratorExpression = true;
                try {
                    return PeekTokenForOrAsyncFor;
                } finally {
                    _inGeneratorExpression = prevIn;
                }
            }
        }

        // comp_for: '[async] for target_list 'in' or_test [comp_iter]
        private ComprehensionFor ParseCompFor() {
            bool isAsync = false;
            string firstWhitespace = null, asyncWhitespace = null;
            int start = -1;

            if (MaybeEat(TokenKind.KeywordAsync)) {
                isAsync = true;
                asyncWhitespace = _tokenWhiteSpace;
                start = GetStart();
            }
            Eat(TokenKind.KeywordFor);
            firstWhitespace = _tokenWhiteSpace;
            if (start < 0) {
                start = GetStart();
            }

            bool trailingComma;
            List<string> listWhiteSpace;
            List<Expression> l = ParseExpressionList(out trailingComma, out listWhiteSpace);

            // expr list is something like:
            //  ()
            // a
            // a,b
            // a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, listWhiteSpace, trailingComma, true);
            bool ateIn = Eat(TokenKind.KeywordIn);

            string inWhiteSpace;
            Expression list;
            if (ateIn) {
                inWhiteSpace = _tokenWhiteSpace;
                list = ParseOrTest();
            } else {
                inWhiteSpace = null;
                list = Error("");
            }

            ComprehensionFor ret = new ComprehensionFor(lhs, list, isAsync);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, firstWhitespace);
                AddSecondPreceedingWhiteSpace(ret, inWhiteSpace);
                if (isAsync) {
                    AddThirdPreceedingWhiteSpace(ret, asyncWhitespace);
                }
                if (!ateIn) {
                    AddErrorIsIncompleteNode(ret);
                }
            }

            ret.SetLoc(start, GetEnd());
            return ret;
        }

        // listmaker: expression ( list_for | (',' expression)* [','] )
        private Expression FinishListValue() {
            string proceedingWhiteSpace = _tokenWhiteSpace;

            var oStart = GetStart();
            int grouping = _tokenizer.GroupingLevel;

            Expression ret;
            bool ateRightBracket;
            if (MaybeEat(TokenKind.RightBracket)) {
                ret = new ListExpression();
                ateRightBracket = true;
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;
                    /*if (MaybeEat(TokenKind.Multiply)) {
                        // Need to produce an AST with the splatting here...
                    }*/

                    Expression t0 = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        string commaWhiteSpace = _tokenWhiteSpace;
                        bool trailingComma;
                        List<string> listWhiteSpace;
                        var l = ParseTestListAsExpr(t0, out listWhiteSpace, out trailingComma);
                        ateRightBracket = Eat(TokenKind.RightBracket);

                        ret = new ListExpression(l.ToArray());

                        if (listWhiteSpace != null) {
                            AddListWhiteSpace(ret, listWhiteSpace.ToArray());
                        }
                    } else if (PeekTokenForOrAsyncFor) {
                        ret = FinishListComp(t0, out ateRightBracket);
                    } else {
                        ateRightBracket = Eat(TokenKind.RightBracket);
                        ret = new ListExpression(t0);
                    }
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, proceedingWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                if (!ateRightBracket) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }

            ret.SetLoc(oStart, GetEnd());
            return ret;
        }

        // list_iter ']'
        private ListComprehension FinishListComp(Expression item, out bool ateRightBracket) {
            ComprehensionIterator[] iters = ParseListCompIter();
            ateRightBracket = Eat(TokenKind.RightBracket);
            return new ListComprehension(item, iters);
        }

        // list_iter: list_for | list_if
        private ComprehensionIterator[] ParseListCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseListCompFor();

            iters.Add(firstFor);

            while (true) {
                ComprehensionIterator iterator;

                if (PeekTokenForOrAsyncFor) {
                    iterator = ParseListCompFor();
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iterator = ParseCompIf();
                } else {
                    break;
                }

                iters.Add(iterator);
            }

            return iters.ToArray();
        }

        // list_for: 'for' target_list 'in' old_expression_list [list_iter]
        private ComprehensionFor ParseListCompFor() {
            bool isAsync = false;
            string firstWhitespace = null, asyncWhitespace = null;
            int start = -1;

            if (MaybeEat(TokenKind.KeywordAsync)) {
                isAsync = true;
                asyncWhitespace = _tokenWhiteSpace;
                start = GetStart();
            }
            Eat(TokenKind.KeywordFor);
            firstWhitespace = _tokenWhiteSpace;
            if (start < 0) {
                start = GetStart();
            }

            bool trailingComma;
            List<string> listWhiteSpace;
            List<Expression> l = ParseExpressionList(out trailingComma, out listWhiteSpace);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, listWhiteSpace, trailingComma, true);
            bool ateIn = Eat(TokenKind.KeywordIn);
            string inWhiteSpace;
            Expression list;

            if (ateIn) {
                inWhiteSpace = _tokenWhiteSpace;
                if (_stubFile || _langVersion.Is3x()) {
                    list = ParseOrTest();
                } else {
                    list = ParseOldExpressionListAsExpr();
                }
            } else {
                inWhiteSpace = null;
                list = Error("");
            }

            ComprehensionFor ret = new ComprehensionFor(lhs, list, isAsync);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, firstWhitespace);
                if (inWhiteSpace != null) {
                    AddSecondPreceedingWhiteSpace(ret, inWhiteSpace);
                }
                if (isAsync) {
                    AddThirdPreceedingWhiteSpace(ret, asyncWhitespace);
                }
                if (!ateIn) {
                    AddErrorIsIncompleteNode(ret);
                }
            }

            ret.SetLoc(start, GetEnd());
            return ret;
        }

        // list_if: 'if' old_test [list_iter]
        // comp_if: 'if' old_test [comp_iter]
        private ComprehensionIf ParseCompIf() {
            var start = GetStart();
            Eat(TokenKind.KeywordIf);
            string ifWhiteSpace = _tokenWhiteSpace;
            Expression expr = ParseOldExpression();
            var end = GetEnd();

            ComprehensionIf ret = new ComprehensionIf(expr);
            ret.HeaderIndex = end;
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, ifWhiteSpace);
            }
            ret.SetLoc(start, end);
            return ret;
        }

        private Expression FinishStringConversion() {
            Debug.Assert(_token.Token.Kind == TokenKind.BackQuote);
            string firstWhiteSpace = _tokenWhiteSpace;
            Expression ret;
            var start = GetStart();
            Expression expr = ParseTestListAsExpr();
            bool ateBackQuote = Eat(TokenKind.BackQuote);
            ret = new BackQuoteExpression(expr);
            if (_verbatim) {
                AddPreceedingWhiteSpace(ret, firstWhiteSpace);
                AddSecondPreceedingWhiteSpace(ret, _tokenWhiteSpace);
                if (!ateBackQuote) {
                    AddErrorMissingCloseGrouping(ret);
                }
            }
            ret.SetLoc(start, GetEnd());
            return ret;
        }

        private Expression MakeTupleOrExpr(List<Expression> l, List<string> itemWhiteSpace, bool trailingComma, bool parenFreeTuple = false) {
            return MakeTupleOrExpr(l, itemWhiteSpace, trailingComma, false, parenFreeTuple);
        }

        private Expression MakeTupleOrExpr(List<Expression> l, List<string> itemWhiteSpace, bool trailingComma, bool expandable, bool parenFreeTuple = false) {
            if (l.Count == 1 && !trailingComma) return l[0];

            Expression[] exprs = l.ToArray();
            TupleExpression te = new TupleExpression(expandable && !trailingComma, exprs);
            if (_verbatim) {
                if (itemWhiteSpace != null) {
                    AddListWhiteSpace(te, itemWhiteSpace.ToArray());
                }
                if (parenFreeTuple) {
                    AddIsAltForm(te);
                }
            }
            if (exprs.Length > 0) {
                te.SetLoc(exprs[0].StartIndex, exprs[exprs.Length - 1].EndIndex);
            }
            return te;
        }

        private static bool NeverTestToken(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual:
                case TokenKind.SubtractEqual:
                case TokenKind.MultiplyEqual:
                case TokenKind.DivideEqual:
                case TokenKind.ModEqual:
                case TokenKind.BitwiseAndEqual:
                case TokenKind.BitwiseOrEqual:
                case TokenKind.ExclusiveOrEqual:
                case TokenKind.LeftShiftEqual:
                case TokenKind.RightShiftEqual:
                case TokenKind.PowerEqual:
                case TokenKind.FloorDivideEqual:

                case TokenKind.Indent:
                case TokenKind.Dedent:
                case TokenKind.NewLine:
                case TokenKind.EndOfFile:
                case TokenKind.Semicolon:

                case TokenKind.Assign:
                case TokenKind.RightBrace:
                case TokenKind.RightBracket:
                case TokenKind.RightParenthesis:

                case TokenKind.Comma:

                case TokenKind.Colon:

                case TokenKind.KeywordFor:
                case TokenKind.KeywordIn:
                case TokenKind.KeywordIf:
                    return true;

                default: return false;
            }
        }

        private FunctionDefinition CurrentFunction {
            get {
                if (_functions != null && _functions.Count > 0) {
                    return _functions.Peek();
                }
                return null;
            }
        }

        private FunctionDefinition PopFunction() {
            if (_functions != null && _functions.Count > 0) {
                return _functions.Pop();
            }
            return null;
        }

        private void PushFunction(FunctionDefinition function) {
            if (_functions == null) {
                _functions = new Stack<FunctionDefinition>();
            }
            _functions.Push(function);
        }

        private CallExpression FinishCallExpr(Expression target, params Arg[] args) {
            bool hasArgsTuple = false;
            bool hasKeywordDict = false;
            int keywordCount = 0;
            int extraArgs = 0;

            foreach (Arg arg in args) {
                if (arg.Name == null) {
                    if (_stubFile || _langVersion >= PythonLanguageVersion.V35) {
                        if (hasKeywordDict) {
                            ReportSyntaxError(arg.StartIndex, arg.EndIndex, "positional argument follows keyword argument unpacking");
                        } else if (keywordCount > 0) {
                            ReportSyntaxError(arg.StartIndex, arg.EndIndex, "positional argument follows keyword argument");
                        }
                    } else if (hasArgsTuple || hasKeywordDict || keywordCount > 0) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "non-keyword arg after keyword arg");
                    }
                } else if (arg.Name == "*") {
                    if (hasArgsTuple || hasKeywordDict) {
                        if (!_stubFile && _langVersion < PythonLanguageVersion.V35) {
                            ReportSyntaxError(arg.StartIndex, arg.EndIndex, "only one * allowed");
                        } else if (hasKeywordDict) {
                            ReportSyntaxError(arg.StartIndex, arg.EndIndex, "iterable argument unpacking follows keyword argument unpacking");
                        }
                    }
                    hasArgsTuple = true; extraArgs++;
                } else if (arg.Name == "**") {
                    if (hasKeywordDict) {
                        if (!_stubFile && _langVersion < PythonLanguageVersion.V35) {
                            ReportSyntaxError(arg.StartIndex, arg.EndIndex, "only one ** allowed");
                        }
                    }
                    hasKeywordDict = true; extraArgs++;
                } else {
                    if (hasKeywordDict && !_stubFile && _langVersion < PythonLanguageVersion.V35) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "keywords must come before ** args");
                    }
                    keywordCount++;
                }
            }

            return new CallExpression(target, args);
        }

        #endregion

        #region Implementation Details

        private PythonAst ParseFileWorker() {
            StartParsing();

            List<Statement> l = new List<Statement>();

            //
            // A future statement must appear near the top of the module. 
            // The only lines that can appear before a future statement are: 
            // - the module docstring (if any), 
            // - comments, 
            // - blank lines, and 
            // - other future statements. 
            // 

            MaybeEatNewLine();

            if (PeekToken(TokenKind.Constant)) {
                Statement s = ParseStmt();
                l.Add(s);
                _fromFutureAllowed = false;
                ExpressionStatement es = s as ExpressionStatement;
                if (es != null) {
                    ConstantExpression ce = es.Expression as ConstantExpression;
                    if (ce != null && IsString(ce)) {
                        // doc string
                        _fromFutureAllowed = true;
                    }
                }
            }

            MaybeEatNewLine();

            // from __future__
            if (_fromFutureAllowed) {
                while (PeekToken(Tokens.KeywordFromToken)) {
                    Statement s = ParseStmt();
                    l.Add(s);
                    FromImportStatement fis = s as FromImportStatement;
                    if (fis != null && !fis.IsFromFuture) {
                        // end of from __future__
                        break;
                    }
                }
            }

            // the end of from __future__ sequence
            _fromFutureAllowed = false;

            while (true) {
                if (MaybeEatEof()) break;
                if (MaybeEatNewLine()) continue;

                Statement s = ParseStmt();
                l.Add(s);
            }

            Statement[] stmts = l.ToArray();

            SuiteStatement ret = new SuiteStatement(stmts);
            AddIsAltForm(ret);
            if (_token.Token != null) {
                ret.SetLoc(0, GetEndForStatement());
            }
            return CreateAst(ret);
        }

        private bool IsString(ConstantExpression ce) {
            if (_langVersion.Is3x()) {
                return ce.Value is string;
            }
            return ce.Value is AsciiString;
        }

        private Statement InternalParseInteractiveInput(out bool parsingMultiLineCmpdStmt, out bool isEmptyStmt) {
            Statement s;
            isEmptyStmt = false;
            parsingMultiLineCmpdStmt = false;

            switch (PeekToken().Kind) {
                case TokenKind.NewLine:
                    MaybeEatNewLine();
                    Eat(TokenKind.EndOfFile);
                    if (_tokenizer.EndContinues) {
                        parsingMultiLineCmpdStmt = true;
                        _errorCode = ErrorCodes.IncompleteStatement;
                    } else {
                        isEmptyStmt = true;
                    }
                    return null;

                case TokenKind.KeywordIf:
                case TokenKind.KeywordWhile:
                case TokenKind.KeywordFor:
                case TokenKind.KeywordTry:
                case TokenKind.At:
                case TokenKind.KeywordDef:
                case TokenKind.KeywordClass:
                case TokenKind.KeywordWith:
                    parsingMultiLineCmpdStmt = true;
                    s = ParseStmt();
                    EatEndOfInput();
                    break;
                case TokenKind.EndOfFile:
                    isEmptyStmt = true;
                    return null;
                default:
                    //  parseSimpleStmt takes care of one or more simple_stmts and the Newline
                    s = ParseSimpleStmt();
                    MaybeEatNewLine();
                    Eat(TokenKind.EndOfFile);
                    break;

            }
            return s;
        }



        private Expression ParseTestListAsExpression() {
            StartParsing();

            Expression expression = ParseTestListAsExpr();
            EatEndOfInput();
            return expression;
        }

        /// <summary>
        /// Maybe eats a new line token returning true if the token was
        /// eaten.
        /// 
        /// Python always tokenizes to have only 1  new line character in a 
        /// row.  But we also craete NLToken's and ignore them except for 
        /// error reporting purposes.  This gives us the same errors as 
        /// CPython and also matches the behavior of the standard library 
        /// tokenize module.  This function eats any present NL tokens and throws
        /// them away.
        /// 
        /// We also need to add the new lines into any proceeding white space
        /// when we're parsing in verbatim mode.
        /// </summary>
        private bool MaybeEatNewLine() {
            string curWhiteSpace = "";
            string newWhiteSpace;
            if (MaybeEatNewLine(out newWhiteSpace)) {
                if (_verbatim) {
                    _lookaheadWhiteSpace = curWhiteSpace + newWhiteSpace + _lookaheadWhiteSpace;
                }
                return true;
            }
            return false;
        }

        private bool MaybeEatNewLine(out string whitespace) {
            whitespace = _verbatim ? "" : null;
            if (MaybeEat(TokenKind.NewLine)) {
                if (whitespace != null) {
                    whitespace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                }
                while (MaybeEat(TokenKind.NLToken)) {
                    if (whitespace != null) {
                        whitespace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Eats a new line token throwing if the next token isn't a new line.  
        /// 
        /// Python always tokenizes to have only 1  new line character in a 
        /// row.  But we also craete NLToken's and ignore them except for 
        /// error reporting purposes.  This gives us the same errors as 
        /// CPython and also matches the behavior of the standard library 
        /// tokenize module.  This function eats any present NL tokens and throws
        /// them away.
        /// </summary>
        private bool EatNewLine(out string whitespace) {
            whitespace = _verbatim ? "" : null;
            if (Eat(TokenKind.NewLine)) {
                if (whitespace != null) {
                    whitespace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                }

                while (MaybeEat(TokenKind.NLToken)) {
                    if (whitespace != null) {
                        whitespace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                    }
                }
                return true;
            }
            return false;
        }

        private Token EatEndOfInput() {
            while (MaybeEatNewLine() || MaybeEat(TokenKind.Dedent)) {
                ;
            }

            Token t = NextToken();
            if (t.Kind != TokenKind.EndOfFile) {
                ReportSyntaxError(_token);
            }
            return t;
        }

        private bool TrueDivision {
            get { return (_languageFeatures & FutureOptions.TrueDivision) == FutureOptions.TrueDivision; }
        }

        private bool AbsoluteImports {
            get { return (_languageFeatures & FutureOptions.AbsoluteImports) == FutureOptions.AbsoluteImports; }
        }

        private void StartParsing() {
            if (_parsingStarted)
                throw new InvalidOperationException("Parsing already started. Use Restart to start again.");

            _parsingStarted = true;

            FetchLookahead();

            string whitespace = _verbatim ? "" : null;
            while (PeekToken().Kind == TokenKind.NLToken) {
                NextToken();

                if (whitespace != null) {
                    whitespace += _tokenWhiteSpace + _token.Token.VerbatimImage;
                }
            }
            _lookaheadWhiteSpace = whitespace + _lookaheadWhiteSpace;
        }

        private int GetEnd() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.End;
        }

        private int GetEndForStatement() {
            Debug.Assert(_token.Token != null, "No token fetched");
            if (_lookahead.Token?.Kind == TokenKind.EndOfFile) {
                return _lookahead.Span.End;
            }
            return _token.Span.End;
        }

        private int GetStart() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.Start;
        }

        private Token NextToken() {
            _token = _lookahead;
            _tokenWhiteSpace = _lookaheadWhiteSpace;
            FetchLookahead();
            return _token.Token;
        }

        private Token PeekToken() {
            return _lookahead.Token;
        }

        private Token PeekToken2() {
            if (_lookahead2.Token == null) {
                _lookahead2 = new TokenWithSpan(_tokenizer.GetNextToken(), _tokenizer.TokenSpan);
                _lookahead2WhiteSpace = _tokenizer.PreceedingWhiteSpace;
            }
            return _lookahead2.Token;
        }

        private void FetchLookahead() {
            if (_lookahead2.Token != null) {
                _lookahead = _lookahead2;
                _lookaheadWhiteSpace = _lookahead2WhiteSpace;
                _lookahead2 = TokenWithSpan.Empty;
                _lookahead2WhiteSpace = null;
            } else {
                _lookahead = new TokenWithSpan(_tokenizer.GetNextToken(), _tokenizer.TokenSpan);
                _lookaheadWhiteSpace = _tokenizer.PreceedingWhiteSpace;
            }
        }

        private bool PeekToken(TokenKind kind) {
            return PeekToken().Kind == kind;
        }

        private bool PeekToken(Token check) {
            return PeekToken() == check;
        }

        private bool Eat(TokenKind kind) {
            Token next = PeekToken();
            if (next.Kind != kind) {
                ReportSyntaxError(_lookahead);
                return false;
            } else {
                NextToken();
                return true;
            }
        }

        private bool EatNoEof(TokenKind kind) {
            Token next = PeekToken();
            if (next.Kind != kind) {
                ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, false);
                return false;
            }
            NextToken();
            return true;
        }

        private bool MaybeEat(TokenKind kind) {
            if (PeekToken().Kind == kind) {
                NextToken();
                return true;
            } else {
                return false;
            }
        }

        private bool MaybeEatName(string name) {
            var peeked = PeekToken();
            if (peeked.Kind == TokenKind.Name && ((NameToken)peeked).Name == name) {
                NextToken();
                return true;
            } else {
                return false;
            }
        }

        private class TokenizerErrorSink : ErrorSink {
            private readonly Parser _parser;

            public TokenizerErrorSink(Parser parser) {
                _parser = parser;
            }

            public override void Add(string message, SourceSpan span, int errorCode, Severity severity) {
                if (_parser._errorCode == 0 && (severity == Severity.Error || severity == Severity.FatalError)) {
                    _parser._errorCode = errorCode;
                }

                _parser.ErrorSink.Add(message, span, errorCode, severity);
            }
        }

        #endregion

        #region Encoding support (PEP 263)

        public static TextReader ReadStreamWithEncoding(Stream stream, PythonLanguageVersion version) {
            var defaultEncoding = version.Is3x() ? new UTF8Encoding(false) : DefaultEncoding;
            return GetStreamReaderWithEncoding(stream, defaultEncoding, ErrorSink.Null);
        }

        /// <summary>
        /// Returns the Encoding that a Python file is written in.  This inspects the BOM and looks for a #coding line
        /// in the provided stream.
        /// 
        /// Returns null if the encoding could not be detected for any reason.
        /// 
        /// New in 1.1.
        /// </summary>
        public static Encoding GetEncodingFromStream(Stream stream) {
            return GetStreamReaderWithEncoding(stream, new UTF8Encoding(false), ErrorSink.Null).CurrentEncoding;
        }

        /// <summary>
        /// Fetches encoding specified in the magic comment as per
        /// https://www.python.org/dev/peps/pep-0263/
        /// </summary>
        /// <param name="text">Chunk of text to analyze (typically first two lines)</param>
        /// <param name="encoding">Encoding if present and valid or null otherwise</param>
        /// <param name="encodingName">Encoding name as specified in the commend (may not be a valid name)</param>
        /// <param name="encodingIndex">Index of the encoding name in the buffer</param>
        public static void GetEncodingFromMagicDesignator(string text, out Encoding encoding, out string encodingName, out int encodingIndex) {
            encoding = null;
            encodingName = null;
            encodingIndex = 0;

            var lineBreakLength = 0;
            var i = 0;
            var lines = new[] { text, string.Empty };

            for (; i < text.Length && lineBreakLength == 0; i++) {
                if (text[i] == '\r') {
                    lineBreakLength++;
                    if (i < text.Length - 1 && text[i + 1] == '\n') {
                        lineBreakLength++;
                    }
                } else if (text[i] == '\n') {
                    lineBreakLength++;
                }
            }

            // magic encoding must be on line 1 or 2
            if (lineBreakLength > 0) {
                lines[0] = text.Substring(0, i + lineBreakLength - 1);
                lines[1] = text.Substring(lines[0].Length);
            }

            for (i = 0; i < lines.Length; i++) {
                TryGetEncoding(lines[i], ref encoding, out encodingName, out encodingIndex);
                if (encoding != null || !string.IsNullOrEmpty(encodingName)) {
                    break;
                }
            }
            encodingIndex += i == 0 ? 0 : lines[0].Length;
        }

        private static StreamReader/*!*/ GetStreamReaderWithEncoding(Stream/*!*/ stream, Encoding/*!*/ defaultEncoding, ErrorSink errors) {
            // Python 2.x should pass ASCII as the default
            // Python 3.x should pass UTF-8
            // A BOM or encoding comment can override the default
            Encoding encoding = defaultEncoding;

            List<byte> readBytes = new List<byte>();
            try {
                byte[] bomBuffer = new byte[3];
                int bomRead = stream.Read(bomBuffer, 0, 3);
                int bytesRead = 0;
                bool isUtf8 = false;
                if (bomRead == 3 && (bomBuffer[0] == 0xef && bomBuffer[1] == 0xbb && bomBuffer[2] == 0xbf)) {
                    isUtf8 = true;
                    bytesRead = 3;
                    readBytes.AddRange(bomBuffer);
                } else {
                    for (int i = 0; i < bomRead; i++) {
                        readBytes.Add(bomBuffer[i]);
                    }
                }

                int lineLength;
                string line = ReadOneLine(readBytes, ref bytesRead, stream, out lineLength);

                bool? gotEncoding = false;
                string encodingName = null;
                // magic encoding must be on line 1 or 2
                int lineNo = 1;
                int encodingIndex = 0;
                if ((gotEncoding = TryGetEncoding(line, ref encoding, out encodingName, out encodingIndex)) == false) {
                    var prevLineLength = lineLength;
                    line = ReadOneLine(readBytes, ref bytesRead, stream, out lineLength);
                    lineNo = 2;
                    gotEncoding = TryGetEncoding(line, ref encoding, out encodingName, out encodingIndex);
                    encodingIndex += prevLineLength;
                }

                if ((gotEncoding == null || gotEncoding == true) && isUtf8 && encodingName != "utf-8") {
                    // we have both a BOM & an encoding type, throw an error
                    errors.Add(
                        "file has both Unicode marker and PEP-263 file encoding.  You must use \"utf-8\" as the encoding name when a BOM is present.",
                        GetEncodingLineNumbers(readBytes),
                        encodingIndex,
                        encodingIndex + encodingName.Length,
                        ErrorCodes.SyntaxError,
                        Severity.FatalError
                    );
                    encoding = Encoding.UTF8;
                } else if (isUtf8) {
                    return new StreamReader(new PartiallyReadStream(readBytes, stream), UTF8Throwing);
                } else if (encoding == null) {
                    if (gotEncoding == null) {
                        // get line number information for the bytes we've read...
                        errors.Add(
                            "encoding problem: unknown encoding (line {0})".FormatUI(lineNo),
                            GetEncodingLineNumbers(readBytes),
                            encodingIndex,
                            encodingIndex + encodingName.Length,
                            ErrorCodes.SyntaxError,
                            Severity.Error
                        );
                    }
                    return new StreamReader(new PartiallyReadStream(readBytes, stream), defaultEncoding);
                }

                // re-read w/ the correct encoding type...
                return new StreamReader(new PartiallyReadStream(readBytes, stream), encoding);
            } catch (EncoderFallbackException ex) {
                errors.Add(
                    ex.Message,
                    new NewLineLocation[0],
                    ex.Index,
                    ex.Index + 1,
                    ErrorCodes.SyntaxError,
                    Severity.FatalError
                );
                return new StreamReader(new PartiallyReadStream(readBytes, stream), encoding);
            }
        }

        private static NewLineLocation[] GetEncodingLineNumbers(IList<byte> readBytes) {
            NewLineLocation[] lineNos = new NewLineLocation[2];
            for (int i = 0, lineCount = 0; i < readBytes.Count && lineCount < 2; i++) {
                if (readBytes[i] == '\r') {
                    if (i + 1 < readBytes.Count && readBytes[i + 1] == '\n') {
                        lineNos[lineCount++] = new NewLineLocation(i, NewLineKind.CarriageReturnLineFeed);
                        i++;
                    } else {
                        lineNos[lineCount++] = new NewLineLocation(i, NewLineKind.CarriageReturn);
                    }
                } else if (readBytes[i] == '\n') {
                    lineNos[lineCount++] = new NewLineLocation(i, NewLineKind.LineFeed);
                }
            }
            return lineNos;
        }

        private static Encoding UTF8Throwing {
            get {
                if (_utf8throwing == null) {
                    var tmp = (Encoding)Encoding.UTF8.Clone();
                    tmp.DecoderFallback = new SourceNonStrictDecoderFallback();
                    _utf8throwing = tmp;
                }
                return _utf8throwing;
            }
        }

        /// <summary>
        /// Attempts to get the encoding from a # coding: line.  
        /// 
        /// Returns true if we successfully parse the encoding line and get the encoding, false if there's no encoding line, or
        /// null if the encoding line exists but the codec is unknown.
        /// </summary>
        internal static bool? TryGetEncoding(string line, ref Encoding enc, out string encName, out int index) {
            // encoding is "# coding: <encoding name>
            // minimum length is 18
            encName = null;
            index = 0;
            if (line.Length < 10) return false;
            if (line[0] != '#') return false;

            // we have magic comment line
            if (_codingRegex == null) {
                // coding regex as specified at http://www.python.org/dev/peps/pep-0263/
                _codingRegex = new Regex("coding[:=]\\s*([-\\w.]+)", RegexOptions.Compiled);
            }

            //int codingIndex;
            Match match;
            if (!(match = _codingRegex.Match(line)).Success) {
                return false;
            }

            // get the encoding string name
            index = match.Groups[1].Index;
            encName = match.Groups[1].Value;

            // and we have the magic ending as well...
            if (TryGetEncoding(encName, out enc)) {
                enc.DecoderFallback = new SourceNonStrictDecoderFallback();
                return true;
            }
            return null;
        }

        internal static bool TryGetEncoding(string name, out Encoding encoding) {
            name = NormalizeEncodingName(name);

            EncodingInfoWrapper encInfo;
            if (CodecsInfo.Codecs.TryGetValue(name, out encInfo)) {
                encoding = (Encoding)encInfo.GetEncoding().Clone();
                return true;
            }

            encoding = null;
            return false;
        }

        static class CodecsInfo {
            public static readonly Dictionary<string, EncodingInfoWrapper> Codecs = MakeCodecsDict();

            private static Dictionary<string, EncodingInfoWrapper> MakeCodecsDict() {
                Dictionary<string, EncodingInfoWrapper> d = new Dictionary<string, EncodingInfoWrapper>();
                EncodingInfo[] encs = Encoding.GetEncodings();
                for (int i = 0; i < encs.Length; i++) {
                    string normalizedName = NormalizeEncodingName(encs[i].Name);

                    // setup well-known mappings, for everything
                    // else we'll store as lower case w/ _                
                    switch (normalizedName) {
                        case "us_ascii":
                            d["cp{0}".FormatInvariant(encs[i].CodePage)] = d[normalizedName] = d["us"] = d["ascii"] = d["646"] = d["us_ascii"] =
                                d["ansi_x3.4_1968"] = d["ansi_x3_4_1968"] = d["ansi_x3.4_1986"] = d["cp367"] = d["csascii"] = d["ibm367"] =
                                d["iso646_us"] = d["iso_646.irv_1991"] = d["iso_ir_6"]
                                = new AsciiEncodingInfoWrapper();
                            continue;
                        case "iso_8859_1":
                            d["iso_ir_100"] = d["iso_8859_1_1987"] = d["iso_8859_1"] = d["iso8859"] = d["ibm819"] = d["csisolatin1"] = d["8859"] =
                            d["latin1"] = d["iso 8859_1"] = d["iso8859_1"] = d["cp819"] = d["819"] = d["latin"] = d["latin1"] = d["l1"] = encs[i];
                            break;
                        case "utf_7":
                            d["u7"] = d["unicode-1-1-utf-7"] = encs[i];
                            break;
                        case "utf_8":
                            d["utf_8_sig"] = encs[i];
                            d["utf_8"] = d["utf8"] = d["u8"] = new EncodingInfoWrapper(encs[i], new byte[0]);
                            continue;
                        case "utf_16":
                            d["utf_16_le"] = d["utf_16le"] = new EncodingInfoWrapper(encs[i], new byte[0]);
                            d["utf16"] = new EncodingInfoWrapper(encs[i], encs[i].GetEncoding().GetPreamble());
                            break;
                        case "unicodefffe": // big endian unicode                    
                                            // strip off the pre-amble, CPython doesn't include it.
                            d["utf_16_be"] = d["utf_16be"] = new EncodingInfoWrapper(encs[i], new byte[0]);
                            break;
                        case "gb2312":
                            d["x_mac_simp_chinese"] = d["936"] = d["ms936"] = d["chinese"] = d["csiso58gb231280"] = d["euc_cn"] = d["euccn"] = d["eucgb2312_cn"] = d["gb2312_1980"] =
                            d["gb2312_80"] = d["iso_ir_58"] = d["gbk"] = encs[i];
                            break;
                        case "big5":
                            d["x_mac_trad_chinese"] = d["big5_tw"] = d["csbig5"] = encs[i];
                            break;
                        case "cp950":
                            d["ms950"] = d["hkscs"] = d["big5_hkscs"] = encs[i];
                            break;
                        case "ibm037":
                            d["cp037"] = d["csibm037"] = d["ebcdic_cp_ca"] = d["ebcdic_cp_nl"] = d["ebcdic_cp_us"] = d["ebcdic_cp_wt"] = d["ibm039"] = encs[i];
                            break;
                        case "gb18030": d["gb18030_2000"] = encs[i]; break;
                    }

                    switch (encs[i].CodePage) {
                        case 500: d["csibm500"] = d["ebcdic_cp_be"] = d["ebcdic_cp_ch"] = encs[i]; break;
                        case 1026: d["csibm1026"] = encs[i]; break;
                        case 1140: d["ibm1140"] = encs[i]; break;
                        case 850: d["cspc850multilingual"] = encs[i]; break;
                        case 852: d["cspcp852"] = encs[i]; break;
                        case 855: d["csibm855"] = encs[i]; break;
                        case 857: d["csibm857"] = encs[i]; break;
                        case 858: d["csibm858"] = d["ibm858"] = encs[i]; break;
                        case 861: d["csibm861"] = d["cp_is"] = encs[i]; break;
                        case 862: d["cspc862latinhebrew"] = encs[i]; break;
                        case 863: d["csibm863"] = encs[i]; break;
                        case 864: d["csibm864"] = encs[i]; break;
                        case 865: d["csibm865"] = encs[i]; break;
                        case 866: d["csibm866"] = encs[i]; break;
                        case 869: d["csibm869"] = d["cp_gr"] = encs[i]; break;
                        case 932: d["csshiftjis"] = d["shiftjis"] = d["sjis"] = d["s_jis"] = d["shiftjis2004"] = d["sjis_2004"] = d["s_jis_2004"] = d["x_mac_japanese"] = d["mskanji"] = d["ms_kanji"] = encs[i]; break;
                        case 949: d["uhc"] = d["ms949"] = encs[i]; break;
                        case 51949: d["euckr"] = d["korean"] = d["ksc5601"] = d["ks_c_5601"] = d["ks_c_5601_1987"] = d["ksx1001"] = d["ks_x_1001"] = d["x_mac_korean"] = encs[i]; break;
                        case 52936: d["hz"] = d["hzgb"] = d["hz_gb"] = encs[i]; break;
                        case 50220: d["iso2022_jp"] = d["iso2022jp"] = encs[i]; break;
                        case 50221: d["iso2022_jp_1"] = d["iso2022jp_1"] = d["iso_2022_jp_1"] = encs[i]; break;
                        case 50222: d["iso2022_jp_2"] = d["iso2022jp_2"] = d["iso_2022_jp_2"] = encs[i]; break;
                        case 50225: d["csiso2022kr"] = d["iso2022kr"] = d["iso_2022_kr"] = encs[i]; break;
                        case 28603: d["iso8859_13"] = d["iso_8859_13"] = d["l7"] = d["latin7"] = encs[i]; break;
                        case 28605: d["iso8859_15"] = d["l9"] = d["latin9"] = encs[i]; break;
                        case 28592: d["csisolatin2"] = d["iso_8859_2_1987"] = d["iso_ir_101"] = d["l2"] = d["latin2"] = encs[i]; break;
                        case 28593: d["csisolatin3"] = d["iso_8859_3_1988"] = d["iso_ir_109"] = d["l3"] = d["latin3"] = encs[i]; break;
                        case 28594: d["csisolatin4"] = d["iso_8859_4_1988"] = d["iso_ir_110"] = d["l4"] = d["latin4"] = encs[i]; break;
                        case 28595: d["csisolatincyrillic"] = d["cyrillic"] = d["iso_8859_5_1988"] = d["iso_ir_144"] = encs[i]; break;
                        case 28596: d["arabic"] = d["asmo_708"] = d["csisolatinarabic"] = d["ecma_114"] = d["iso_8859_6_1987"] = d["iso_ir_127"] = encs[i]; break;
                        case 28597: d["csisolatingreek"] = d["ecma_118"] = d["elot_928"] = d["greek"] = d["greek8"] = d["iso_8859_7_1987"] = d["iso_ir_126"] = encs[i]; break;
                        case 28598: d["csisolatinhebrew"] = d["hebrew"] = d["iso_8859_8_1988"] = d["iso_ir_138"] = encs[i]; break;
                        case 28599: d["csisolatin5"] = d["iso_8859_9_1989"] = d["iso_ir_148"] = d["l5"] = d["latin5"] = encs[i]; break;
                        case 1361: d["ms1361"] = encs[i]; break;
                        case 20866: d["cskoi8r"] = encs[i]; break;
                        case 10006: d["macgreek"] = d["mac_greek"] = encs[i]; break;
                        case 10007: d["mac_cyrillic"] = d["maccyrillic"] = encs[i]; break;
                        case 10079: d["maciceland"] = d["mac_iceland"] = encs[i]; break;
                        case 10081: d["macturkish"] = d["mac_turkish"] = encs[i]; break;
                        case 10010: d["mac_romanian"] = encs[i]; break;
                        case 10004: d["mac_arabic"] = encs[i]; break;
                        case 10082: d["mac_croatian"] = encs[i]; break;
                    }

                    // publish under normalized name (all lower cases, -s replaced with _s)
                    d[normalizedName] = encs[i];
                    // publish under Windows code page as well...                
                    d["windows-{0}".FormatInvariant(encs[i].GetEncoding().WindowsCodePage)] = encs[i];
                    // publish under code page number as well...
                    d["cp{0}".FormatInvariant(encs[i].CodePage)] = d["{0}".FormatInvariant(encs[i].CodePage)] = encs[i];
                }

#if DEBUG
                // all codecs should be stored in lowercase because we only look up from lowercase strings
                foreach (KeyValuePair<string, EncodingInfoWrapper> kvp in d) {
                    Debug.Assert(kvp.Key.ToLowerInvariant() == kvp.Key);
                }
#endif
                return d;
            }
        }

        class EncodingInfoWrapper {
            private EncodingInfo _info;
            private Encoding _encoding;
            private byte[] _preamble;

            public EncodingInfoWrapper(Encoding enc) {
                _encoding = enc;
            }

            public EncodingInfoWrapper(EncodingInfo info) {
                _info = info;
            }

            public EncodingInfoWrapper(EncodingInfo info, byte[] preamble) {
                _info = info;
                _preamble = preamble;
            }

            public virtual Encoding GetEncoding() {
                if (_encoding != null) return _encoding;

                if (_preamble == null) {
                    return _info.GetEncoding();
                }

                return new EncodingWrapper(_info.GetEncoding(), _preamble);
            }

            public static implicit operator EncodingInfoWrapper(EncodingInfo info) {
                return new EncodingInfoWrapper(info);
            }
        }

        class AsciiEncodingInfoWrapper : EncodingInfoWrapper {
            public AsciiEncodingInfoWrapper()
                : base((EncodingInfo)null) {
            }

            public override Encoding GetEncoding() {
                return PythonAsciiEncoding.Instance;
            }
        }

        class EncodingWrapper : Encoding {
            private byte[] _preamble;
            private Encoding _encoding;

            public EncodingWrapper(Encoding encoding, byte[] preamable) {
                _preamble = preamable;
                _encoding = encoding;
            }

            private void SetEncoderFallback() {
                _encoding.EncoderFallback = EncoderFallback;
            }

            private void SetDecoderFallback() {
                _encoding.DecoderFallback = DecoderFallback;
            }

            public override int CodePage {
                get {
                    return _encoding.CodePage;
                }
            }

            public override string EncodingName {
                get {
                    return _encoding.EncodingName;
                }
            }

            public override string WebName {
                get {
                    return _encoding.WebName;
                }
            }

            public override int GetByteCount(char[] chars, int index, int count) {
                SetEncoderFallback();
                return _encoding.GetByteCount(chars, index, count);
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
                SetEncoderFallback();
                return _encoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
            }

            public override int GetCharCount(byte[] bytes, int index, int count) {
                SetDecoderFallback();
                return _encoding.GetCharCount(bytes, index, count);
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
                SetDecoderFallback();
                return _encoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
            }

            public override int GetMaxByteCount(int charCount) {
                SetEncoderFallback();
                return _encoding.GetMaxByteCount(charCount);
            }

            public override int GetMaxCharCount(int byteCount) {
                SetDecoderFallback();
                return _encoding.GetMaxCharCount(byteCount);
            }

            public override byte[] GetPreamble() {
                return _preamble;
            }

            public override Encoder GetEncoder() {
                SetEncoderFallback();
                return _encoding.GetEncoder();
            }

            public override Decoder GetDecoder() {
                SetDecoderFallback();
                return _encoding.GetDecoder();
            }

            public override object Clone() {
                // need to call base.Clone to be marked as read/write
                EncodingWrapper res = (EncodingWrapper)base.Clone();
                res._encoding = (Encoding)_encoding.Clone();
                return res;
            }
        }

        internal static string NormalizeEncodingName(string name) {
            if (name == null) {
                return null;
            }
            return name.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        /// <summary>
        /// Reads one line keeping track of the # of bytes read and saving the bytes that were read
        /// </summary>
        private static string ReadOneLine(List<byte> previewedBytes, ref int curIndex, Stream reader, out int lineLength) {
            lineLength = 0;
            byte[] buffer = new byte[256];
            int bufferReadCount = reader.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < bufferReadCount; i++) {
                previewedBytes.Add(buffer[i]);
            }

            int startIndex = curIndex;
            do {
                for (int i = curIndex; i < previewedBytes.Count; i++) {
                    bool foundEnd = false;

                    if (previewedBytes[i] == '\r') {
                        if (i + 1 < previewedBytes.Count) {
                            if (previewedBytes[i + 1] == '\n') {
                                lineLength = 2;
                                curIndex = i + 2;
                                foundEnd = true;
                            }
                        } else {
                            lineLength = 1;
                            curIndex = i + 1;
                            foundEnd = true;
                        }
                    } else if (previewedBytes[i] == '\n') {
                        lineLength = 1;
                        curIndex = i + 1;
                        foundEnd = true;
                    }

                    if (foundEnd) {
                        var res = MakeString(previewedBytes).Substring(startIndex, i - startIndex);
                        lineLength += res.Length;
                        return res;
                    }
                }

                bufferReadCount = reader.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < bufferReadCount; i++) {
                    previewedBytes.Add(buffer[i]);
                }
            } while (bufferReadCount != 0);

            // no new-line
            curIndex = previewedBytes.Count;
            var noNewlineRes = MakeString(previewedBytes);
            lineLength = noNewlineRes.Length;
            return noNewlineRes;
        }

        /// <summary>
        /// Returns an Encoding object which raises a BadSourceException when invalid characters are encountered.
        /// </summary>
        public static Encoding DefaultEncoding {
            get {
                return PythonAsciiEncoding.SourceEncoding;
            }
        }

        /// <summary>
        /// Returns an Encoding object which will not provide any fallback for invalid characters.
        /// </summary>
        public static Encoding DefaultEncodingNoFallback {
            get {
                return PythonAsciiEncoding.SourceEncodingNoFallback;
            }
        }

        #endregion

        #region Verbatim AST support

        private void AddPreceedingWhiteSpace(Node ret) {
            AddPreceedingWhiteSpace(ret, _tokenWhiteSpace);
        }

        private Dictionary<object, object> GetNodeAttributes(Node node) {
            Dictionary<object, object> attrs;
            if (!_attributes.TryGetValue(node, out attrs)) {
                _attributes[node] = attrs = new Dictionary<object, object>();
            }
            return attrs;
        }

        private void AddVerbatimName(Name name, Node ret) {
            if (_verbatim && name.RealName != name.VerbatimName) {
                GetNodeAttributes(ret)[NodeAttributes.VerbatimImage] = name.VerbatimName;
            }
        }

        private void AddVerbatimImage(Node ret, string image) {
            if (_verbatim) {
                GetNodeAttributes(ret)[NodeAttributes.VerbatimImage] = image;
            }
        }

        private List<string> MakeWhiteSpaceList() {
            return _verbatim ? new List<string>() : null;
        }

        private void AddPreceedingWhiteSpace(Node ret, string whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.PreceedingWhiteSpace] = whiteSpace;
        }

        private void AddSecondPreceedingWhiteSpace(Node ret, string whiteSpace) {
            if (_verbatim) {
                Debug.Assert(_verbatim);
                GetNodeAttributes(ret)[NodeAttributes.SecondPreceedingWhiteSpace] = whiteSpace;
            }
        }

        private void AddThirdPreceedingWhiteSpace(Node ret, string whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.ThirdPreceedingWhiteSpace] = whiteSpace;
        }

        private void AddFourthPreceedingWhiteSpace(Node ret, string whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.FourthPreceedingWhiteSpace] = whiteSpace;
        }

        private void AddFifthPreceedingWhiteSpace(Node ret, string whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.FifthPreceedingWhiteSpace] = whiteSpace;
        }

        private void AddExtraVerbatimText(Node ret, string text) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.ExtraVerbatimText] = text;
        }

        private void AddListWhiteSpace(Node ret, string[] whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.ListWhiteSpace] = whiteSpace;
        }

        private void AddNamesWhiteSpace(Node ret, string[] whiteSpace) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.NamesWhiteSpace] = whiteSpace;
        }

        private void AddVerbatimNames(Node ret, string[] names) {
            Debug.Assert(_verbatim);
            GetNodeAttributes(ret)[NodeAttributes.VerbatimNames] = names;
        }

        private void AddIsAltForm(Node expr) {
            GetNodeAttributes(expr)[NodeAttributes.IsAltFormValue] = NodeAttributes.IsAltFormValue;
        }

        private void AddErrorMissingCloseGrouping(Node expr) {
            GetNodeAttributes(expr)[NodeAttributes.ErrorMissingCloseGrouping] = NodeAttributes.ErrorMissingCloseGrouping;
        }

        private void AddErrorIsIncompleteNode(Node expr) {
            GetNodeAttributes(expr)[NodeAttributes.ErrorIncompleteNode] = NodeAttributes.ErrorIncompleteNode;
        }

        private void MoveNodeAttributes(Node target, Node source, object key) {
            var s = GetNodeAttributes(source);
            if (s.TryGetValue(key, out object o)) {
                GetNodeAttributes(target)[key] = o;
                s.Remove(key);
            }
        }

        #endregion
    }
}
