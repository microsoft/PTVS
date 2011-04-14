/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Parsing {

    public class Parser : IDisposable { // TODO: remove IDisposable
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
        private TokenWithSpan _lookahead;
        private Stack<FunctionDefinition> _functions;
        private int _classDepth;
        private bool _fromFutureAllowed;
        private string _privatePrefix;
        private bool _parsingStarted, _allowIncomplete;
        private bool _inLoop, _inFinally, _isGenerator;
        private List<IndexSpan> _returnsWithValue;
        private TextReader _sourceReader;
        private int _errorCode;
        private PythonAst _globalParent;

        private static readonly char[] newLineChar = new char[] { '\n' };
        private static readonly char[] whiteSpace = { ' ', '\t' };

        #region Construction

        private Parser(Tokenizer tokenizer, ErrorSink errorSink, PythonLanguageVersion langVersion) {
            Contract.Assert(tokenizer != null);
            Contract.Assert(errorSink != null);

            tokenizer.ErrorSink = new TokenizerErrorSink(this);

            _tokenizer = tokenizer;
            _errors = errorSink;
            _langVersion = langVersion;

            Reset(FutureOptions.None);
        }

        public static Parser CreateParser(TextReader reader, ErrorSink errors, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Ignore) {
            Tokenizer tokenizer = new Tokenizer(version, errors);

            tokenizer.Initialize(null, reader, SourceLocation.MinValue);
            tokenizer.IndentationInconsistencySeverity = indentationInconsistencySeverity;

            Parser result = new Parser(tokenizer, errors, version);
            result._sourceReader = reader;
            return result;
        }

        /// <summary>
        /// Creates a new parser from a seekable stream including scanning the BOM or looking for a # coding: comment to detect the appropriate coding.
        /// </summary>
        public static Parser CreateParser(Stream stream, ErrorSink errors, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Ignore) {
            var reader = GetStreamReaderWithEncoding(stream, PythonAsciiEncoding.Instance, errors);

            return CreateParser(reader, errors, version, indentationInconsistencySeverity);
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

            _globalParent = new PythonAst();
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

                return FinishParsing(ret);
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

        private PythonAst FinishParsing(Statement ret) {
            var res = _globalParent;
            _globalParent = null;
            var lineLocs = _tokenizer.GetLineLocations();
            res.ParsingFinished(lineLocs, ret);

            PythonNameBinder.BindAst(_langVersion, res, _errors);

            return res;
        }

        public PythonAst ParseSingleStatement() {
            _globalParent = new PythonAst();
            StartParsing();

            MaybeEatNewLine();
            Statement statement = ParseStmt();
            EatEndOfInput();
            return FinishParsing(statement);
        }

        public PythonAst ParseTopExpression() {
            // TODO: move from source unit  .TrimStart(' ', '\t')
            _globalParent = new PythonAst();
            ReturnStatement ret = new ReturnStatement(ParseTestListAsExpression());
            ret.SetLoc(_globalParent, 0, 0);
            return FinishParsing(ret);
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

            string msg = String.Format(System.Globalization.CultureInfo.InvariantCulture, GetErrorMessage(t, errorCode), t.Image);

            ReportSyntaxError(start, end, msg, errorCode);
        }

        private static string GetErrorMessage(Token t, int errorCode) {
            string msg;
            if ((errorCode & ~ErrorCodes.IncompleteMask) == ErrorCodes.IndentationError) {
                msg = "expected an indented block";
            } else if (t.Kind != TokenKind.EndOfFile) {
                msg = "unexpected token '{0}'";
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
            return name.StartsWith("__") && !name.EndsWith("__");
        }

        private string FixName(string name) {
            if (_privatePrefix != null && IsPrivateName(name)) {
                name = "_" + _privatePrefix + name;
            }

            return name;
        }

        private string ReadNameMaybeNone() {
            // peek for better error recovery
            Token t = PeekToken();
            if (t == Tokens.NoneToken) {
                NextToken();
                return "None";
            }

            NameToken n = t as NameToken;
            if (n == null) {
                ReportSyntaxError("syntax error");
                return null;
            }

            NextToken();
            return FixName(n.Name);
        }

        private string ReadName() {
            NameToken n = PeekToken() as NameToken;
            if (n == null) {
                ReportSyntaxError(_lookahead);
                return null;
            }
            NextToken();
            return FixName(n.Name);
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
                    return ParseForStmt();
                case TokenKind.KeywordTry:
                    return ParseTryStatement();
                case TokenKind.At:
                    return ParseDecorated();
                case TokenKind.KeywordDef:
                    return ParseFuncDef();
                case TokenKind.KeywordClass:
                    return ParseClassDef();
                case TokenKind.KeywordWith:
                    return ParseWithStmt();
                default:
                    return ParseSimpleStmt();
            }
        }

        //simple_stmt: small_stmt (';' small_stmt)* [';'] Newline
        private Statement ParseSimpleStmt() {
            Statement s = ParseSmallStmt();
            if (MaybeEat(TokenKind.Semicolon)) {
                var start = s.StartIndex;
                List<Statement> l = new List<Statement>();
                l.Add(s);
                while (true) {
                    if (MaybeEatNewLine() || MaybeEat(TokenKind.EndOfFile)) {
                        break;
                    }

                    l.Add(ParseSmallStmt());

                    if (MaybeEat(TokenKind.EndOfFile)) {
                        // implies a new line
                        break;
                    } else if (!MaybeEat(TokenKind.Semicolon)) {
                        EatNewLine();
                        break;
                    }
                }
                Statement[] stmts = l.ToArray();

                SuiteStatement ret = new SuiteStatement(stmts);
                ret.SetLoc(_globalParent, start, stmts[stmts.Length - 1].EndIndex);
                return ret;
            } else if (!MaybeEat(TokenKind.EndOfFile) && !EatNewLine()) {
                // error handling, make sure we're making forward progress
                NextToken();
            }
            return s;
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
            var start = GetStart();

            if (PeekToken(TokenKind.NewLine) || PeekToken(TokenKind.EndOfFile)) {
                ReportSyntaxError(curLookahead.Span.Start, curLookahead.Span.End, "expected expression after del");
                DelStatement ret = new DelStatement(new Expression[0]);
                ret.SetLoc(_globalParent, start, GetEnd());
                return ret;
            } else {
                List<Expression> l = ParseExprList();
                foreach (Expression e in l) {
                    if (e is ErrorExpression) {
                        continue;
                    }
                    string delError = e.CheckDelete();
                    if (delError != null) {
                        ReportSyntaxError(e.StartIndex, e.EndIndex, delError, ErrorCodes.SyntaxError);
                    }
                }

                DelStatement ret = new DelStatement(l.ToArray());
                ret.SetLoc(_globalParent, start, GetEnd());
                return ret;
            }
        }

        private Statement ParseReturnStmt() {
            if (CurrentFunction == null) {
                ReportSyntaxError("'return' outside function");
            }
            var returnToken = _lookahead;
            NextToken();
            Expression expr = null;
            var start = GetStart();
            if (!NeverTestToken(PeekToken())) {
                expr = ParseTestListAsExpr();
            }

            if (expr != null) {
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
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Statement FinishSmallStmt(Statement stmt) {
            NextToken();
            stmt.SetLoc(_globalParent, GetStart(), GetEnd());
            return stmt;
        }


        private Statement ParseYieldStmt() {
            // For yield statements, continue to enforce that it's currently in a function. 
            // This gives us better syntax error reporting for yield-statements than for yield-expressions.
            FunctionDefinition current = CurrentFunction;
            if (current == null) {
                ReportSyntaxError("misplaced yield");
            }

            _isGenerator = true;
            if (_returnsWithValue != null) {
                foreach (var span in _returnsWithValue) {
                    ReportSyntaxError(span.Start, span.End, "'return' with argument inside generator");
                }
            }

            Eat(TokenKind.KeywordYield);

            // See Pep 342: a yield statement is now just an expression statement around a yield expression.
            Expression e = ParseYieldExpression();
            Debug.Assert(e != null); // caller already verified we have a yield.

            Statement s = new ExpressionStatement(e);
            s.SetLoc(_globalParent, e.IndexSpan);
            return s;
        }

        /// <summary>
        /// Peek if the next token is a 'yield' and parse a yield expression. Else return null.
        /// 
        /// Called w/ yield already eaten.
        /// </summary>
        /// <returns>A yield expression if present, else null. </returns>
        // yield_expression: "yield" [expression_list] 
        private Expression ParseYieldExpression() {
            // Mark that this function is actually a generator.
            // If we're in a generator expression, then we don't have a function yet.
            //    g=((yield i) for i in range(5))
            // In that acse, the genexp will mark IsGenerator. 
            FunctionDefinition current = CurrentFunction;
            if (current != null) {
                current.IsGenerator = true;
            }

            var start = GetStart();

            // Parse expression list after yield. This can be:
            // 1) empty, in which case it becomes 'yield None'
            // 2) a single expression
            // 3) multiple expression, in which case it's wrapped in a tuple.
            Expression yieldResult;

            bool trailingComma;
            List<Expression> l = ParseExpressionList(out trailingComma);
            if (l.Count == 0) {
                if (_langVersion < PythonLanguageVersion.V25) {
                    // 2.4 doesn't allow plain yield
                    ReportSyntaxError("invalid syntax");
                }
                // Check empty expression and convert to 'none'
                yieldResult = new ConstantExpression(null);
            } else if (l.Count != 1) {
                // make a tuple
                yieldResult = MakeTupleOrExpr(l, trailingComma);
            } else {
                // just take the single expression
                yieldResult = l[0];
            }

            Expression yieldExpression = new YieldExpression(yieldResult);

            yieldExpression.SetLoc(_globalParent, start, GetEnd());
            return yieldExpression;

        }

        private Statement FinishAssignments(Expression right) {
            List<Expression> left = null;
            Expression singleLeft = null;

            while (MaybeEat(TokenKind.Assign)) {
                string assignError = right.CheckAssign();
                if (assignError != null) {
                    ReportSyntaxError(right.StartIndex, right.EndIndex, assignError, ErrorCodes.SyntaxError | ErrorCodes.NoCaret);
                }

                if (singleLeft == null) {
                    singleLeft = right;
                } else {
                    if (left == null) {
                        left = new List<Expression>();
                        left.Add(singleLeft);
                    }
                    left.Add(right);
                }

                if (_langVersion >= PythonLanguageVersion.V25 && MaybeEat(TokenKind.KeywordYield)) {
                    right = ParseYieldExpression();
                } else {
                    right = ParseTestListAsExpr();
                }
            }

            if (left != null) {
                Debug.Assert(left.Count > 0);

                AssignmentStatement assign = new AssignmentStatement(left.ToArray(), right);
                assign.SetLoc(_globalParent, left[0].StartIndex, right.EndIndex);
                return assign;
            } else {
                Debug.Assert(singleLeft != null);

                AssignmentStatement assign = new AssignmentStatement(new[] { singleLeft }, right);
                assign.SetLoc(_globalParent, singleLeft.StartIndex, right.EndIndex);
                return assign;
            }
        }

        // expr_stmt: expression_list
        // expression_list: expression ( "," expression )* [","] 
        // assignment_stmt: (target_list "=")+ (expression_list | yield_expression) 
        // augmented_assignment_stmt ::= target augop (expression_list | yield_expression) 
        // augop: '+=' | '-=' | '*=' | '/=' | '%=' | '**=' | '>>=' | '<<=' | '&=' | '^=' | '|=' | '//='
        private Statement ParseExprStmt() {
            Expression ret = ParseTestListAsExpr(true);
            if (ret is ErrorExpression) {
                NextToken();
            }

            if (PeekToken(TokenKind.Assign)) {
                if (_langVersion >= PythonLanguageVersion.V30) {
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

                return FinishAssignments(ret);
            } else {
                PythonOperator op = GetAssignOperator(PeekToken());
                if (op != PythonOperator.None) {
                    NextToken();
                    Expression rhs;

                    if (_langVersion >= PythonLanguageVersion.V25 && MaybeEat(TokenKind.KeywordYield)) {
                        rhs = ParseYieldExpression();
                    } else {
                        rhs = ParseTestListAsExpr();
                    }

                    string assignError = ret.CheckAugmentedAssign();
                    if (assignError != null) {
                        ReportSyntaxError(ret.StartIndex, ret.EndIndex, assignError);
                    }

                    AugmentedAssignStatement aug = new AugmentedAssignStatement(op, ret, rhs);
                    aug.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                    return aug;
                } else {
                    Statement stmt = new ExpressionStatement(ret);
                    stmt.SetLoc(_globalParent, ret.IndexSpan);
                    return stmt;
                }
            }
        }

        private PythonOperator GetAssignOperator(Token t) {
            switch (t.Kind) {
                case TokenKind.AddEqual: return PythonOperator.Add;
                case TokenKind.SubtractEqual: return PythonOperator.Subtract;
                case TokenKind.MultiplyEqual: return PythonOperator.Multiply;
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
            var start = GetStart();

            List<ModuleName> l = new List<ModuleName>();
            List<string> las = new List<string>();
            l.Add(ParseModuleName());
            las.Add(MaybeParseAsName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ParseModuleName());
                las.Add(MaybeParseAsName());
            }
            ModuleName[] names = l.ToArray();
            var asNames = las.ToArray();

            ImportStatement ret = new ImportStatement(names, asNames, AbsoluteImports);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // module: (identifier '.')* identifier
        private ModuleName ParseModuleName() {
            var start = GetStart();
            ModuleName ret = new ModuleName(ReadNames());
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private static string[] EmptyStrings = new string[0];

        // relative_module: "."* module | "."+
        private ModuleName ParseRelativeModuleName() {
            var start = GetStart();

            int dotCount = 0;
            for (; ; ) {
                if (MaybeEat(TokenKind.Dot)) {
                    dotCount++;
                } else if (MaybeEat(TokenKind.Ellipsis)) {
                    dotCount += 3;
                } else {
                    break;
                }
            }

            string[] names = EmptyStrings;
            if (PeekToken() is NameToken) {
                names = ReadNames();
            }

            ModuleName ret;
            if (dotCount > 0) {
                ret = new RelativeModuleName(names, dotCount);
            } else {
                if (names.Length == 0) {
                    ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, "missing module name");
                }
                ret = new ModuleName(names);
            }

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private string[] ReadNames() {
            List<string> l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Dot)) {
                l.Add(ReadName());
            }
            return l.ToArray();
        }


        // 'from' relative_module 'import' identifier ['as' name] (',' identifier ['as' name]) *
        // 'from' relative_module 'import' '(' identifier ['as' name] (',' identifier ['as' name])* [','] ')'        
        // 'from' module 'import' "*"                                        
        private FromImportStatement ParseFromImportStmt() {
            Eat(TokenKind.KeywordFrom);
            var start = GetStart();
            ModuleName dname = ParseRelativeModuleName();

            Eat(TokenKind.KeywordImport);

            bool ateParen = MaybeEat(TokenKind.LeftParenthesis);

            string[] names;
            string[] asNames;
            bool fromFuture = false;

            if (MaybeEat(TokenKind.Multiply)) {
                if (_langVersion.Is3x() && ((_functions != null && _functions.Count > 0) || _classDepth > 0)) {
                    ReportSyntaxError(start, GetEnd(), "import * only allowed at module level");
                }

                names = (string[])FromImportStatement.Star;
                asNames = null;
            } else {
                List<string> l = new List<string>();
                List<string> las = new List<string>();

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    ParseAsNameList(l, las);
                    Eat(TokenKind.RightParenthesis);
                } else {
                    ParseAsNameList(l, las);
                }
                names = l.ToArray();
                asNames = las.ToArray();
            }

            // Process from __future__ statement

            if (dname.Names.Count == 1 && dname.Names[0] == "__future__") {
                if (!_fromFutureAllowed) {
                    ReportSyntaxError(start, GetEnd(), "from __future__ imports must occur at the beginning of the file");
                }
                if (names == FromImportStatement.Star) {
                    ReportSyntaxError(start, GetEnd(), "future statement does not support import *");
                }
                fromFuture = true;
                foreach (string name in names) {
                    if (name == "nested_scopes") {

                        // v2.4
                    } else if (name == "division") {
                        _languageFeatures |= FutureOptions.TrueDivision;
                    } else if (name == "generators") {

                        // v2.5:
                    } else if (_langVersion >= PythonLanguageVersion.V25 && name == "with_statement") {
                        _languageFeatures |= FutureOptions.WithStatement;
                        _tokenizer.WithStatement = true;
                    } else if (_langVersion >= PythonLanguageVersion.V25 && name == "absolute_import") {
                        _languageFeatures |= FutureOptions.AbsoluteImports;

                        // v2.6:
                    } else if (_langVersion >= PythonLanguageVersion.V26 && name == "print_function") {
                        _languageFeatures |= FutureOptions.PrintFunction;
                        _tokenizer.PrintFunction = true;
                    } else if (_langVersion >= PythonLanguageVersion.V26 && name == "unicode_literals") {
                        _tokenizer.UnicodeLiterals = true;
                        _languageFeatures |= FutureOptions.UnicodeLiterals;
                    } else {
                        string strName = name;

                        if (strName != "braces") {
                            ReportSyntaxError(start, GetEnd(), "future feature is not defined: " + strName);
                        } else {
                            // match CPython error message
                            ReportSyntaxError(start, GetEnd(), "not a chance");
                        }
                    }
                }
            }

            if (ateParen) {
                Eat(TokenKind.RightParenthesis);
            }

            FromImportStatement ret = new FromImportStatement(dname, (string[])names, asNames, fromFuture, AbsoluteImports);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // import_as_name (',' import_as_name)*
        private void ParseAsNameList(List<string> l, List<string> las) {
            l.Add(ReadName());
            las.Add(MaybeParseAsName());
            while (MaybeEat(TokenKind.Comma)) {
                if (PeekToken(TokenKind.RightParenthesis)) return;  // the list is allowed to end with a ,
                l.Add(ReadName());
                las.Add(MaybeParseAsName());
            }
        }

        //import_as_name: NAME [NAME NAME]
        //dotted_as_name: dotted_name [NAME NAME]
        private string MaybeParseAsName() {
            if (MaybeEat(TokenKind.KeywordAs) || MaybeEatName("as")) {
                return ReadName();
            }

            return null;
        }

        //exec_stmt: 'exec' expr ['in' expression [',' expression]]
        private ExecStatement ParseExecStmt() {
            Eat(TokenKind.KeywordExec);
            var start = GetStart();
            Expression code, locals = null, globals = null;
            code = ParseExpr();
            if (MaybeEat(TokenKind.KeywordIn)) {
                globals = ParseExpression();
                if (MaybeEat(TokenKind.Comma)) {
                    locals = ParseExpression();
                }
            }
            ExecStatement ret = new ExecStatement(code, locals, globals);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //global_stmt: 'global' NAME (',' NAME)*
        private GlobalStatement ParseGlobalStmt() {
            Eat(TokenKind.KeywordGlobal);
            var start = GetStart();
            List<string> l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ReadName());
            }
            string[] names = l.ToArray();
            GlobalStatement ret = new GlobalStatement(names);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private NonlocalStatement ParseNonlocalStmt() {
            if (_functions.Count == 0 && _classDepth == 0) {
                ReportSyntaxError("nonlocal declaration not allowed at module level");
            }

            Eat(TokenKind.KeywordNonlocal);
            var start = GetStart();
            List<string> l = new List<string>();
            l.Add(ReadName());
            while (MaybeEat(TokenKind.Comma)) {
                l.Add(ReadName());
            }
            string[] names = l.ToArray();
            NonlocalStatement ret = new NonlocalStatement(names);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //raise_stmt: 'raise' [expression [',' expression [',' expression]]]
        private RaiseStatement ParseRaiseStmt() {
            Eat(TokenKind.KeywordRaise);
            var start = GetStart();
            Expression type = null, _value = null, traceback = null, cause = null;

            if (!NeverTestToken(PeekToken())) {
                type = ParseExpression();
                
                if (MaybeEat(TokenKind.Comma)) {
                    var commaStart = GetStart();
                    _value = ParseExpression();
                    if (!_langVersion.Is2x()) {
                        ReportSyntaxError(commaStart, GetEnd(), "invalid syntax, only exception value is allowed in 3.x.");
                    }
                    if (MaybeEat(TokenKind.Comma)) {
                        traceback = ParseExpression();
                    }
                } else if (MaybeEat(TokenKind.KeywordFrom)) {
                    var fromStart = GetStart();
                    cause = ParseExpression();

                    if (!_langVersion.Is3x()) {
                       ReportSyntaxError(fromStart, cause.EndIndex, "invalid syntax, from cause not allowed in 2.x.");
                    }
                }

            }
            RaiseStatement ret = new RaiseStatement(type, _value, traceback, cause);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //assert_stmt: 'assert' expression [',' expression]
        private AssertStatement ParseAssertStmt() {
            Eat(TokenKind.KeywordAssert);
            var start = GetStart();
            Expression expr = ParseExpression();
            Expression message = null;
            if (MaybeEat(TokenKind.Comma)) {
                message = ParseExpression();
            }
            AssertStatement ret = new AssertStatement(expr, message);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //print_stmt: 'print' ( [ expression (',' expression)* [','] ] | '>>' expression [ (',' expression)+ [','] ] )
        private PrintStatement ParsePrintStmt() {
            Eat(TokenKind.KeywordPrint);
            var start = GetStart();
            Expression dest = null;
            PrintStatement ret;

            bool needNonEmptyTestList = false;
            int end = 0;
            if (MaybeEat(TokenKind.RightShift)) {
                dest = ParseExpression();
                if (MaybeEat(TokenKind.Comma)) {
                    needNonEmptyTestList = true;
                    end = GetEnd();
                } else {
                    ret = new PrintStatement(dest, new Expression[0], false);
                    ret.SetLoc(_globalParent, start, GetEnd());
                    return ret;
                }
            }

            bool trailingComma;
            List<Expression> l = ParseExpressionList(out trailingComma);
            if (needNonEmptyTestList && l.Count == 0) {
                ReportSyntaxError(start, end, "print statement expected expression to be printed");
            }
            Expression[] exprs = l.ToArray();
            ret = new PrintStatement(dest, exprs, trailingComma);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private string SetPrivatePrefix(string name) {
            string oldPrefix = _privatePrefix;

            _privatePrefix = GetPrivatePrefix(name);

            return oldPrefix;
        }

        internal static string GetPrivatePrefix(string name) {
            // Remove any leading underscores before saving the prefix
            if (name != null) {
                for (int i = 0; i < name.Length; i++) {
                    if (name[i] != '_') {
                        return name.Substring(i);
                    }
                }
            }
            // Name consists of '_'s only, no private prefix mapping
            return null;
        }

        private ErrorExpression Error() {
            var res = new ErrorExpression();
            res.SetLoc(_globalParent, GetStart(), GetEnd());
            return res;
        }

        private ExpressionStatement ErrorStmt() {
            var res = new ExpressionStatement(Error());
            res.SetLoc(_globalParent, GetStart(), GetEnd());
            return res;
        }

        //classdef: 'class' NAME ['(' testlist ')'] ':' suite
        private ClassDefinition ParseClassDef() {
            Eat(TokenKind.KeywordClass);

            var start = GetStart();
            string name = ReadName();
            if (name == null) {
                // no name, assume there's no class.
                return new ClassDefinition(null, new Expression[0], ErrorStmt(), null);
            }

            Expression[] bases = new Expression[0];
            Dictionary<string, Expression> kwArgs = null;
            if (MaybeEat(TokenKind.LeftParenthesis)) {
                if (_langVersion.Is3x()) {
                    List<Expression> l = new List<Expression>();
                    var args = FinishArgumentList(null);
                    for (int i = 0; i < args.Length; i++) {
                        if (args[i].Name == null) {
                            l.Add(args[i].Expression);
                        } else {
                            if (kwArgs == null) {
                                kwArgs = new Dictionary<string, Expression>();
                            }
                            kwArgs[args[i].Name] = args[i].Expression;
                        }
                    }

                    if (l.Count == 1 && l[0] is ErrorExpression) {
                        // error handling, classes is incomplete.
                        return new ClassDefinition(name, new Expression[0], ErrorStmt(), null);
                    }
                    bases = l.ToArray();
                } else {
                    List<Expression> l = ParseTestList();
                    if (l.Count == 1 && l[0] is ErrorExpression) {
                        // error handling, classes is incomplete.
                        return new ClassDefinition(name, new Expression[0], ErrorStmt(), null);
                    }
                    bases = l.ToArray();
                    Eat(TokenKind.RightParenthesis);
                }
            }
            var mid = GetEnd();

            // Save private prefix
            string savedPrefix = SetPrivatePrefix(name);

            _classDepth++;
            // Parse the class body
            Statement body = ParseClassOrFuncBody();
            _classDepth--;

            // Restore the private prefix
            _privatePrefix = savedPrefix;

            ClassDefinition ret = new ClassDefinition(name, bases, body, kwArgs);
            ret.HeaderIndex = mid;
            ret.SetLoc(_globalParent, start, body.EndIndex);
            return ret;
        }


        //  decorators ::=
        //      decorator+
        //  decorator ::=
        //      "@" dotted_name ["(" [argument_list [","]] ")"] NEWLINE
        private List<Expression> ParseDecorators() {
            List<Expression> decorators = new List<Expression>();

            while (MaybeEat(TokenKind.At)) {
                var start = GetStart();
                Expression decorator = new NameExpression(ReadName());
                decorator.SetLoc(_globalParent, start, GetEnd());
                while (MaybeEat(TokenKind.Dot)) {
                    string name = ReadNameMaybeNone();
                    decorator = new MemberExpression(decorator, name);
                    decorator.SetLoc(_globalParent, GetStart(), GetEnd());
                }
                decorator.SetLoc(_globalParent, start, GetEnd());

                if (MaybeEat(TokenKind.LeftParenthesis)) {
                    Arg[] args = FinishArgumentList(null);
                    decorator = FinishCallExpr(decorator, args);
                }
                decorator.SetLoc(_globalParent, start, GetEnd());
                EatNewLine();

                decorators.Add(decorator);
            }

            return decorators;
        }

        // funcdef: [decorators] 'def' NAME parameters ':' suite
        // 2.6: 
        //  decorated: decorators (classdef | funcdef)
        // this gets called with "@" look-ahead
        private Statement ParseDecorated() {
            List<Expression> decorators = ParseDecorators();

            Statement res;

            if (PeekToken() == Tokens.KeywordDefToken) {
                FunctionDefinition fnc = ParseFuncDef();
                fnc.Decorators = decorators.ToArray();
                res = fnc;
            } else if (PeekToken() == Tokens.KeywordClassToken) {
                if (_langVersion < PythonLanguageVersion.V26) {
                    ReportSyntaxError("invalid syntax, class decorators require 2.6 or later.");
                }
                ClassDefinition cls = ParseClassDef();
                cls.Decorators = decorators.ToArray();
                res = cls;
            } else {
                ReportSyntaxError(_lookahead);
                res = ParseStmt();
            }

            return res;
        }

        // funcdef: [decorators] 'def' NAME parameters ':' suite
        // parameters: '(' [varargslist] ')'
        // this gets called with "def" as the look-ahead
        private FunctionDefinition ParseFuncDef() {
            Eat(TokenKind.KeywordDef);
            var start = GetStart();
            string name = ReadName();

            Eat(TokenKind.LeftParenthesis);

            var lStart = GetStart();
            var lEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;

            Parameter[] parameters = ParseVarArgsList(TokenKind.RightParenthesis, true);
            FunctionDefinition ret;
            if (parameters == null) {
                // error in parameters
                ret = new FunctionDefinition(name, new Parameter[0]);
                ret.SetLoc(_globalParent, start, lEnd);
                return ret;
            }

            var rStart = GetStart();
            var rEnd = GetEnd();

            ret = new FunctionDefinition(name, parameters);
            PushFunction(ret);

            if (MaybeEat(TokenKind.Arrow)) {
                ret.ReturnAnnotation = ParseExpression();
            }

            Statement body = ParseClassOrFuncBody();
            FunctionDefinition ret2 = PopFunction();
            System.Diagnostics.Debug.Assert(ret == ret2);

            ret.Body = body;
            ret.HeaderIndex = rEnd;

            ret.SetLoc(_globalParent, start, body.EndIndex);

            return ret;
        }

        private Parameter ParseParameterName(HashSet<string> names, ParameterKind kind, bool isTyped = false) {
            var start = GetStart();
            string name = ReadName();
            if (name != null) {
                CheckUniqueParameter(start, names, name);
            } else {
                return null;
            }
            Parameter parameter = new Parameter(name, kind);
            parameter.SetLoc(_globalParent, GetStart(), GetEnd());

            start = GetStart();
            if (isTyped && MaybeEat(TokenKind.Colon)) {
                if (_langVersion.Is2x()) {
                    ReportSyntaxError(start, GetEnd(), "invalid syntax, parameter annotations require 3.x");
                }
                parameter.Annotation = ParseExpression();
            }
            return parameter;
        }

        private void CheckUniqueParameter(int start, HashSet<string> names, string name) {
            if (names.Contains(name)) {
                ReportSyntaxError(start, GetEnd(), String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "duplicate argument '{0}' in function definition",
                    name));
            }
            names.Add(name);
        }

        //varargslist: (fpdef ['=' expression ] ',')* ('*' NAME [',' '**' NAME] | '**' NAME) | fpdef ['=' expression] (',' fpdef ['=' expression])* [',']
        //fpdef: NAME | '(' fplist ')'
        //fplist: fpdef (',' fpdef)* [',']
        private Parameter[] ParseVarArgsList(TokenKind terminator, bool isTyped = false) {
            // parameters not doing * or ** today
            List<Parameter> pl = new List<Parameter>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            bool needDefault = false, parsedStarArgs = false;
            for (int position = 0; ; position++) {
                if (MaybeEat(terminator)) {

                    break;
                }

                Parameter parameter;

                var lookahead = _lookahead;
                if (MaybeEat(TokenKind.Multiply)) {
                    if (parsedStarArgs) {
                        ReportSyntaxError(lookahead.Span.Start, GetEnd(), "duplicate * args arguments");
                    }
                    parsedStarArgs = true;

                    if (_langVersion.Is3x()) {
                        if (MaybeEat(TokenKind.Comma)) {
                            // bare *
                            if (MaybeEat(terminator)) {
                                ReportSyntaxError(lookahead.Span.Start, GetEnd(), "named arguments must follow bare *");
                                break;
                            }
                            continue;
                        }
                    }

                    parameter = ParseParameterName(names, ParameterKind.List, isTyped);
                    if (parameter == null) {
                        // no parameter name, syntax error
                        return null;
                    }
                    pl.Add(parameter);

                    if (!MaybeEat(TokenKind.Comma)) {
                        Eat(terminator);
                        break;
                    }
                    continue;
                } else if (MaybeEat(TokenKind.Power)) {
                    parameter = ParseParameterName(names, ParameterKind.Dictionary, isTyped);
                    if (parameter == null) {
                        // no parameter name, syntax error
                        return null;
                    }
                    pl.Add(parameter);
                    Eat(terminator);
                    break;
                }

                //
                //  Parsing defparameter:
                //
                //  defparameter ::=
                //      parameter ["=" expression]

                if ((parameter = ParseParameter(position, names, parsedStarArgs ? ParameterKind.KeywordOnly : ParameterKind.Normal, isTyped)) != null) {
                    pl.Add(parameter);
                    if (MaybeEat(TokenKind.Assign)) {
                        needDefault = true;
                        parameter.DefaultValue = ParseExpression();
                    } else if (needDefault && !parsedStarArgs) {
                        ReportSyntaxError(parameter.StartIndex, parameter.EndIndex, "default value must be specified here");
                    }
                } else {
                    // error recovery, we could have def f(42=abc): ... eat the equals and expression
                    if (MaybeEat(TokenKind.Assign)) {
                        ParseExpression();
                    }
                }

                if (parsedStarArgs && _langVersion.Is2x()) {
                    ReportSyntaxError(parameter.StartIndex, GetEnd(), "positional parameter after * args not allowed");
                }

                if (!MaybeEat(TokenKind.Comma)) {
                    Eat(terminator);
                    break;
                }
            }

            return pl.ToArray();
        }

        //  parameter ::=
        //      identifier | "(" sublist ")"
        private Parameter ParseParameter(int position, HashSet<string> names, ParameterKind kind, bool isTyped = false) {
            Token t = PeekToken();
            Parameter parameter = null;

            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // sublist                    
                    NextToken();
                    var parenStart = GetStart();
                    Expression ret = ParseSublist(names);
                    Eat(TokenKind.RightParenthesis);
                    TupleExpression tret = ret as TupleExpression;
                    NameExpression nameRet;

                    if (tret != null) {
                        parameter = new SublistParameter(position, tret);
                    } else if ((nameRet = ret as NameExpression) != null) {
                        parameter = new Parameter(nameRet.Name, kind);
                    } else {
                        ReportSyntaxError(_lookahead);
                    }

                    if (parameter != null) {
                        parameter.SetLoc(_globalParent, ret.IndexSpan);
                    }
                    if (_langVersion.Is3x()) {
                        ReportSyntaxError(parenStart, GetEnd(), "sublist parameters are not supported in 3.x");
                    }
                    break;

                case TokenKind.Name:  // identifier
                    NextToken();
                    string name = FixName((string)t.Value);
                    parameter = new Parameter(name, kind);
                    if (isTyped && MaybeEat(TokenKind.Colon)) {
                        var start = GetStart();
                        parameter.Annotation = ParseExpression();

                        if (_langVersion.Is2x()) {
                            ReportSyntaxError(start, parameter.Annotation.EndIndex, "invalid syntax, parameter annotations require 3.x");
                        }
                    }
                    CompleteParameterName(parameter, name, names);
                    break;

                default:
                    ReportSyntaxError(_lookahead);
                    NextToken(); // eat the bad token
                    break;
            }

            return parameter;
        }

        private void CompleteParameterName(Node node, string name, HashSet<string> names) {
            CheckUniqueParameter(GetStart(), names, name);
            node.SetLoc(_globalParent, GetStart(), GetEnd());
        }

        //  parameter ::=
        //      identifier | "(" sublist ")"
        private Expression ParseSublistParameter(HashSet<string> names) {
            Token t = NextToken();
            Expression ret = null;
            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // sublist
                    ret = ParseSublist(names);
                    Eat(TokenKind.RightParenthesis);
                    break;
                case TokenKind.Name:  // identifier
                    string name = FixName((string)t.Value);
                    NameExpression ne = new NameExpression(name);
                    CompleteParameterName(ne, name, names);
                    return ne;
                default:
                    ReportSyntaxError(_token);
                    ret = Error();
                    break;
            }
            return ret;
        }

        //  sublist ::=
        //      parameter ("," parameter)* [","]
        private Expression ParseSublist(HashSet<string> names) {
            bool trailingComma;
            List<Expression> list = new List<Expression>();
            for (; ; ) {
                trailingComma = false;
                list.Add(ParseSublistParameter(names));
                if (MaybeEat(TokenKind.Comma)) {
                    trailingComma = true;
                    switch (PeekToken().Kind) {
                        case TokenKind.LeftParenthesis:
                        case TokenKind.Name:
                            continue;
                        default:
                            break;
                    }
                    break;
                } else {
                    trailingComma = false;
                    break;
                }
            }
            return MakeTupleOrExpr(list, trailingComma);
        }

        //Python2.5 -> old_lambdef: 'lambda' [varargslist] ':' old_expression
        private Expression FinishOldLambdef() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseOldExpression();
            return ParseLambdaHelperEnd(func, expr);
        }

        //lambdef: 'lambda' [varargslist] ':' expression
        private Expression FinishLambdef() {
            FunctionDefinition func = ParseLambdaHelperStart(null);
            Expression expr = ParseExpression();
            return ParseLambdaHelperEnd(func, expr);
        }


        // Helpers for parsing lambda expressions. 
        // Usage
        //   FunctionDefinition f = ParseLambdaHelperStart(string);
        //   Expression expr = ParseXYZ();
        //   return ParseLambdaHelperEnd(f, expr);
        private FunctionDefinition ParseLambdaHelperStart(string name) {
            var start = GetStart();
            Parameter[] parameters;
            parameters = ParseVarArgsList(TokenKind.Colon);
            var mid = GetEnd();

            FunctionDefinition func = new FunctionDefinition(name, parameters ?? new Parameter[0]); // new Parameter[0] for error handling of incomplete lambda
            func.HeaderIndex = mid;
            func.StartIndex = start;

            // Push the lambda function on the stack so that it's available for any yield expressions to mark it as a generator.
            PushFunction(func);

            return func;
        }

        private Expression ParseLambdaHelperEnd(FunctionDefinition func, Expression expr) {
            // Pep 342 in Python 2.5 allows Yield Expressions, which can occur inside a Lambda body. 
            // In this case, the lambda is a generator and will yield it's final result instead of just return it.
            Statement body;
            if (func.IsGenerator) {
                YieldExpression y = new YieldExpression(expr);
                y.SetLoc(_globalParent, expr.IndexSpan);
                body = new ExpressionStatement(y);
            } else {
                body = new ReturnStatement(expr);
            }
            body.SetLoc(_globalParent, expr.StartIndex, expr.EndIndex);

            FunctionDefinition func2 = PopFunction();
            System.Diagnostics.Debug.Assert(func == func2);

            func.Body = body;
            func.EndIndex = GetEnd();

            LambdaExpression ret = new LambdaExpression(func);
            func.SetLoc(_globalParent, func.IndexSpan);
            ret.SetLoc(_globalParent, func.IndexSpan);
            return ret;
        }

        //while_stmt: 'while' expression ':' suite ['else' ':' suite]
        private WhileStatement ParseWhileStmt() {
            Eat(TokenKind.KeywordWhile);
            var start = GetStart();
            Expression expr = ParseExpression();
            var mid = GetEnd();
            Statement body = ParseLoopSuite();
            Statement else_ = null;
            if (MaybeEat(TokenKind.KeywordElse)) {
                else_ = ParseSuite();
            }
            WhileStatement ret = new WhileStatement(expr, body, else_);
            ret.SetLoc(_globalParent, start, mid, GetEnd());
            return ret;
        }
        struct WithItem {
            public readonly int Start;
            public readonly Expression ContextManager;
            public readonly Expression Variable;

            public WithItem(int start, Expression contextManager, Expression variable) {
                Start = start;
                ContextManager = contextManager;
                Variable = variable;
            }
        }

        //with_stmt: 'with' with_item (',' with_item)* ':' suite
        //with_item: test ['as' expr]
        private WithStatement ParseWithStmt() {
            var start = _lookahead.Span.Start;
            Eat(TokenKind.KeywordWith);

            var withItem = ParseWithItem();
            List<WithItem> items = null;
            while (MaybeEat(TokenKind.Comma)) {
                if (items == null) {
                    items = new List<WithItem>();
                }

                items.Add(ParseWithItem());
            }


            var header = GetEnd();
            Statement body = ParseSuite();
            if (items != null) {
                for (int i = items.Count - 1; i >= 0; i--) {
                    var curItem = items[i];
                    var innerWith = new WithStatement(curItem.ContextManager, curItem.Variable, body);
                    innerWith.HeaderIndex = header;
                    innerWith.SetLoc(_globalParent, withItem.Start, GetEnd());
                    body = innerWith;
                    header = GetEnd();
                }
            }

            WithStatement ret = new WithStatement(withItem.ContextManager, withItem.Variable, body);
            ret.HeaderIndex = header;
            ret.SetLoc(_globalParent, withItem.Start, GetEnd());
            return ret;
        }

        private WithItem ParseWithItem() {
            var start = GetStart();
            Expression contextManager = ParseExpression();
            Expression var = null;
            if (MaybeEat(TokenKind.KeywordAs)) {
                var = ParseExpression();
            }

            return new WithItem(start, contextManager, var);
        }

        //for_stmt: 'for' target_list 'in' expression_list ':' suite ['else' ':' suite]
        private ForStatement ParseForStmt() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();

            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Expression list;
            Statement body, else_;
            int header;
            if (lhs is ErrorExpression && MaybeEatNewLine()) {
                // error handling
                else_ = null;
                header = GetEnd();
                list = lhs;
                body = ErrorStmt();
            } else {
                Eat(TokenKind.KeywordIn);
                list = ParseTestListAsExpr();
                header = GetEnd();
                body = ParseLoopSuite();
                else_ = null;
                if (MaybeEat(TokenKind.KeywordElse)) {
                    else_ = ParseSuite();
                }
            }
            ForStatement ret = new ForStatement(lhs, list, body, else_);
            ret.HeaderIndex = header;
            ret.SetLoc(_globalParent, start, GetEnd());
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
            var start = GetStart();
            List<IfStatementTest> l = new List<IfStatementTest>();
            l.Add(ParseIfStmtTest());

            while (MaybeEat(TokenKind.KeywordElseIf)) {
                l.Add(ParseIfStmtTest());
            }

            Statement else_ = null;
            if (MaybeEat(TokenKind.KeywordElse)) {
                else_ = ParseSuite();
            }

            IfStatementTest[] tests = l.ToArray();
            IfStatement ret = new IfStatement(tests, else_);
            ret.SetLoc(_globalParent, start, else_ != null ? else_.EndIndex : tests[tests.Length - 1].EndIndex);
            return ret;
        }

        private IfStatementTest ParseIfStmtTest() {
            var start = GetStart();
            Expression expr = ParseExpression();
            var header = GetEnd();
            Statement suite = ParseSuite();
            IfStatementTest ret = new IfStatementTest(expr, suite);
            ret.SetLoc(_globalParent, start, suite.EndIndex);
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
            var start = GetStart();
            var mid = GetEnd();
            Statement body = ParseSuite();
            Statement finallySuite = null;
            Statement elseSuite = null;
            Statement ret;
            int end;

            if (MaybeEat(TokenKind.KeywordFinally)) {
                finallySuite = ParseFinallySuite(finallySuite);
                end = finallySuite.EndIndex;
                TryStatement tfs = new TryStatement(body, null, elseSuite, finallySuite);
                tfs.HeaderIndex = mid;
                ret = tfs;
            } else {
                List<TryStatementHandler> handlers = new List<TryStatementHandler>();
                TryStatementHandler dh = null;
                do {
                    TryStatementHandler handler = ParseTryStmtHandler();
                    end = handler.EndIndex;
                    handlers.Add(handler);

                    if (dh != null) {
                        ReportSyntaxError(dh.StartIndex, dh.HeaderIndex, "default 'except' must be last");
                    }
                    if (handler.Test == null) {
                        dh = handler;
                    }
                } while (PeekToken().Kind == TokenKind.KeywordExcept);

                if (MaybeEat(TokenKind.KeywordElse)) {
                    elseSuite = ParseSuite();
                    end = elseSuite.EndIndex;
                }

                if (MaybeEat(TokenKind.KeywordFinally)) {
                    // If this function has an except block, then it can set the current exception.
                    finallySuite = ParseFinallySuite(finallySuite);
                    end = finallySuite.EndIndex;
                }

                TryStatement ts = new TryStatement(body, handlers.ToArray(), elseSuite, finallySuite);
                ts.HeaderIndex = mid;
                ret = ts;
            }
            ret.SetLoc(_globalParent, start, end);
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
            Eat(TokenKind.KeywordExcept);

            var start = GetStart();
            Expression test1 = null, test2 = null;
            if (PeekToken().Kind != TokenKind.Colon) {
                test1 = ParseExpression();

                // parse the expression even if the syntax isn't allowed so we
                // report better error messages when opening against the wrong Python version
                var lookahead = _lookahead;
                if (MaybeEat(TokenKind.KeywordAs) || MaybeEatName("as")) {
                    if (_langVersion < PythonLanguageVersion.V26) {
                        ReportSyntaxError(lookahead.Span.Start, lookahead.Span.End, "'as' requires Python 2.6 or later");
                    }
                    test2 = ParseExpression();
                } else if (MaybeEat(TokenKind.Comma)) {
                    test2 = ParseExpression();
                    if (_langVersion.Is3x()) {
                        ReportSyntaxError(lookahead.Span.Start, GetEnd(), "\", variable\" not allowed in 3.x - use \"as variable\" instead.");
                    }
                }
            }
            var mid = GetEnd();
            Statement body = ParseSuite();
            TryStatementHandler ret = new TryStatementHandler(test1, test2, body);
            ret.HeaderIndex = mid;
            ret.SetLoc(_globalParent, start, body.EndIndex);
            return ret;
        }

        //suite: simple_stmt NEWLINE | Newline INDENT stmt+ DEDENT
        private Statement ParseSuite() {
            if (!EatNoEof(TokenKind.Colon)) {
                // improve error handling...
                return ErrorStmt();
            }

            TokenWithSpan cur = _lookahead;
            List<Statement> l = new List<Statement>();

            // we only read a real NewLine here because we need to adjust error reporting
            // for the interpreter.
            if (MaybeEat(TokenKind.NewLine)) {
                CheckSuiteEofError(cur);

                // for error reporting we track the NL tokens and report the error on
                // the last one.  This matches CPython.
                cur = _lookahead;
                while (PeekToken(TokenKind.NLToken)) {
                    cur = _lookahead;
                    NextToken();
                }

                if (!MaybeEat(TokenKind.Indent)) {
                    // no indent?  report the indentation error.
                    if (cur.Token.Kind == TokenKind.Dedent) {
                        ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, "expected an indented block", ErrorCodes.SyntaxError | ErrorCodes.IncompleteStatement);
                    } else {
                        ReportSyntaxError(cur, ErrorCodes.IndentationError);
                    }
                    return ErrorStmt();
                }

                while (true) {
                    Statement s = ParseStmt();

                    l.Add(s);
                    if (MaybeEat(TokenKind.Dedent)) break;
                    if (PeekToken().Kind == TokenKind.EndOfFile) {
                        ReportSyntaxError("unexpected end of file");
                        break; // error handling
                    }
                }
                Statement[] stmts = l.ToArray();
                SuiteStatement ret = new SuiteStatement(stmts);
                ret.SetLoc(_globalParent, stmts[0].StartIndex, stmts[stmts.Length - 1].EndIndex);
                return ret;
            } else {
                //  simple_stmt NEWLINE
                //  ParseSimpleStmt takes care of the NEWLINE
                Statement s = ParseSimpleStmt();
                return s;
            }
        }

        private void CheckSuiteEofError(TokenWithSpan cur) {
            if (MaybeEat(TokenKind.EndOfFile)) {
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
                ret.SetLoc(_globalParent, start, GetEnd());
            }

            return ret;
        }

        // or_test: and_test ('or' and_test)*
        private Expression ParseOrTest() {
            Expression ret = ParseAndTest();
            while (MaybeEat(TokenKind.KeywordOr)) {
                var start = ret.StartIndex;
                ret = new OrExpression(ret, ParseAndTest());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }

        private Expression ParseConditionalTest(Expression trueExpr) {
            Expression expr = ParseOrTest();
            Eat(TokenKind.KeywordElse);
            Expression falseExpr = ParseExpression();
            return new ConditionalExpression(expr, trueExpr, falseExpr);
        }

        // and_test: not_test ('and' not_test)*
        private Expression ParseAndTest() {
            Expression ret = ParseNotTest();
            while (MaybeEat(TokenKind.KeywordAnd)) {
                var start = ret.StartIndex;
                ret = new AndExpression(ret, ParseAndTest());
                ret.SetLoc(_globalParent, start, GetEnd());
            }
            return ret;
        }

        //not_test: 'not' not_test | comparison
        private Expression ParseNotTest() {
            if (MaybeEat(TokenKind.KeywordNot)) {
                var start = GetStart();
                Expression ret = new UnaryExpression(PythonOperator.Not, ParseNotTest());
                ret.SetLoc(_globalParent, start, GetEnd());
                return ret;
            } else {
                return ParseComparison();
            }
        }
        //comparison: expr (comp_op expr)*
        //comp_op: '<'|'>'|'=='|'>='|'<='|'<>'|'!='|'in'|'not' 'in'|'is'|'is' 'not'
        private Expression ParseComparison() {
            Expression ret = ParseExpr();
            while (true) {
                PythonOperator op;
                switch (PeekToken().Kind) {
                    case TokenKind.LessThan: NextToken(); op = PythonOperator.LessThan; break;
                    case TokenKind.LessThanOrEqual: NextToken(); op = PythonOperator.LessThanOrEqual; break;
                    case TokenKind.GreaterThan: NextToken(); op = PythonOperator.GreaterThan; break;
                    case TokenKind.GreaterThanOrEqual: NextToken(); op = PythonOperator.GreaterThanOrEqual; break;
                    case TokenKind.Equals: NextToken(); op = PythonOperator.Equal; break;
                    case TokenKind.NotEquals: NextToken(); op = PythonOperator.NotEqual; break;
                    case TokenKind.LessThanGreaterThan: NextToken(); op = PythonOperator.NotEqual; break;

                    case TokenKind.KeywordIn: NextToken(); op = PythonOperator.In; break;
                    case TokenKind.KeywordNot: NextToken(); Eat(TokenKind.KeywordIn); op = PythonOperator.NotIn; break;

                    case TokenKind.KeywordIs:
                        NextToken();
                        if (MaybeEat(TokenKind.KeywordNot)) {
                            op = PythonOperator.IsNot;
                        } else {
                            op = PythonOperator.Is;
                        }
                        break;
                    default:
                        return ret;
                }
                Expression rhs = ParseComparison();
                BinaryExpression be = new BinaryExpression(op, ret, rhs);
                be.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                ret = be;
            }
        }

        /*
        expr: xor_expr ('|' xor_expr)*
        xor_expr: and_expr ('^' and_expr)*
        and_expr: shift_expr ('&' shift_expr)*
        shift_expr: arith_expr (('<<'|'>>') arith_expr)*
        arith_expr: term (('+'|'-') term)*
        term: factor (('*'|'/'|'%'|'//') factor)*
        */
        private Expression ParseExpr() {
            return ParseExpr(0);
        }

        private Expression ParseExpr(int precedence) {
            Expression ret = ParseFactor();
            while (true) {
                Token t = PeekToken();
                OperatorToken ot = t as OperatorToken;
                if (ot == null) return ret;

                int prec = ot.Precedence;
                if (prec >= precedence) {
                    NextToken();
                    Expression right = ParseExpr(prec + 1);
                    var start = ret.StartIndex;
                    ret = new BinaryExpression(GetBinaryOperator(ot), ret, right);
                    ret.SetLoc(_globalParent, start, GetEnd());
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
                    ret = new UnaryExpression(PythonOperator.Pos, ParseFactor());
                    break;
                case TokenKind.Subtract:
                    NextToken();
                    ret = FinishUnaryNegate();
                    break;
                case TokenKind.Twiddle:
                    NextToken();
                    ret = new UnaryExpression(PythonOperator.Invert, ParseFactor());
                    break;
                default:
                    return ParsePower();
            }
            ret.SetLoc(_globalParent, start, GetEnd());
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
                            NextToken();
                            return new ConstantExpression(-2147483648);

                        }
                    }
                }
            }

            return new UnaryExpression(PythonOperator.Negate, ParseFactor());
        }

        // power: atom trailer* ['**' factor]
        private Expression ParsePower() {
            Expression ret = ParsePrimary();
            ret = AddTrailers(ret);
            if (MaybeEat(TokenKind.Power)) {
                var start = ret.StartIndex;
                ret = new BinaryExpression(PythonOperator.Power, ret, ParseFactor());
                ret.SetLoc(_globalParent, start, GetEnd());
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
                case TokenKind.Name:            // identifier
                    NextToken();
                    string name = (string)t.Value;
                    ret = new NameExpression(FixName(name));
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.Ellipsis:
                    NextToken();
                    ret = new ConstantExpression(Ellipsis.Value);
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.KeywordTrue:
                    NextToken();
                    ret = new ConstantExpression(true);
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.KeywordFalse:
                    NextToken();
                    ret = new ConstantExpression(false);
                    ret.SetLoc(_globalParent, GetStart(), GetEnd());
                    return ret;
                case TokenKind.Constant:        // literal
                    NextToken();
                    var start = GetStart();
                    object cv = t.Value;
                    string cvs = cv as string;
                    if (cvs != null) {
                        cv = FinishStringPlus(cvs);
                    } else {
                        AsciiString bytes = cv as AsciiString;
                        if (bytes != null) {
                            cv = FinishBytesPlus(bytes);
                        }
                    }

                    ret = new ConstantExpression(cv);
                    ret.SetLoc(_globalParent, start, GetEnd());
                    return ret;
                default:
                    ReportSyntaxError(_lookahead.Token, _lookahead.Span, ErrorCodes.SyntaxError, _allowIncomplete || _tokenizer.EndContinues);

                    // error node
                    ret = new ErrorExpression();
                    ret.SetLoc(_globalParent, _lookahead.Span.Start, _lookahead.Span.End);
                    return ret;
            }
        }

        private string FinishStringPlus(string s) {
            Token t = PeekToken();
            while (true) {
                if (t is ConstantValueToken) {
                    string cvs;
                    AsciiString bytes;
                    if ((cvs = t.Value as String) != null) {
                        s += cvs;
                        NextToken();
                        t = PeekToken();
                        continue;
                    } else if ((bytes = t.Value as AsciiString) != null) {
                        if (_langVersion.Is3x()) {
                            ReportSyntaxError("cannot mix bytes and nonbytes literals");
                        }

                        s += bytes.String;
                        NextToken();
                        t = PeekToken();
                        continue;
                    } else {
                        // eat the token so we only report the error once
                        ReportSyntaxError("invalid syntax");
                        NextToken();
                    }
                }
                break;
            }
            return s;
        }

        internal static string MakeString(byte[] bytes) {
            StringBuilder res = new StringBuilder(bytes.Length);
            for (int i = 0; i < bytes.Length; i++) {
                res.Append((char)bytes[i]);
            }
            return res.ToString();
        }

        private object FinishBytesPlus(AsciiString s) {
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
                        t = PeekToken();
                        continue;
                    } else if ((str = t.Value as string) != null) {
                        if (_langVersion.Is3x()) {
                            ReportSyntaxError("cannot mix bytes and nonbytes literals");
                        }

                        string final = s.String + str;
                        NextToken();

                        return FinishStringPlus(final);
                    } else {
                        // eat the token so we don't report the error twice
                        ReportSyntaxError("invalid syntax");
                        NextToken();
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
                            Arg[] args = FinishArgListOrGenExpr();
                            CallExpression call;
                            if (args != null) {
                                call = FinishCallExpr(ret, args);
                            } else {
                                call = new CallExpression(ret, new Arg[0]);
                            }

                            call.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = call;
                            break;
                        case TokenKind.LeftBracket:
                            NextToken();
                            Expression index = ParseSubscriptList();
                            IndexExpression ie = new IndexExpression(ret, index);
                            ie.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = ie;
                            break;
                        case TokenKind.Dot:
                            NextToken();
                            string name = ReadNameMaybeNone();
                            MemberExpression fe = new MemberExpression(ret, name);
                            fe.SetLoc(_globalParent, ret.StartIndex, GetEnd());
                            ret = fe;
                            break;
                        case TokenKind.Constant:
                            // abc.1, abc"", abc 1L, abc 0j
                            ReportSyntaxError("invalid syntax");
                            return Error();
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
        private Expression ParseSubscriptList() {
            const TokenKind terminator = TokenKind.RightBracket;
            var start0 = GetStart();
            bool trailingComma = false;

            List<Expression> l = new List<Expression>();
            while (true) {
                Expression e;
                if (MaybeEat(TokenKind.Dot)) {
                    var start = GetStart();
                    Eat(TokenKind.Dot); Eat(TokenKind.Dot);
                    e = new ConstantExpression(Ellipsis.Value);
                    e.SetLoc(_globalParent, start, GetEnd());
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
                    Eat(terminator);
                    trailingComma = false;
                    break;
                }

                trailingComma = true;
                if (MaybeEat(terminator)) {
                    break;
                }
            }
            Expression ret = MakeTupleOrExpr(l, trailingComma, true);
            ret.SetLoc(_globalParent, start0, GetEnd());
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

            switch (PeekToken().Kind) {
                case TokenKind.Comma:
                case TokenKind.RightBracket:
                    break;
                case TokenKind.Colon:
                    // x[?::?]
                    stepProvided = true;
                    NextToken();
                    e2 = ParseSliceEnd();
                    break;
                default:
                    // x[?:val:?]
                    e1 = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) {
                        stepProvided = true;
                        e2 = ParseSliceEnd();
                    }
                    break;
            }
            SliceExpression ret = new SliceExpression(e0, e1, e2, stepProvided);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }


        //exprlist: expr (',' expr)* [',']
        private List<Expression> ParseExprList() {
            List<Expression> l = new List<Expression>();
            while (true) {
                Expression e = ParseExpr();
                l.Add(e);
                if (!MaybeEat(TokenKind.Comma)) {
                    break;
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
        private Arg[] FinishArgListOrGenExpr() {
            Arg a = null;

            Token t = PeekToken();
            if (t.Kind != TokenKind.RightParenthesis && t.Kind != TokenKind.Multiply && t.Kind != TokenKind.Power) {
                Expression e = ParseExpression();
                if (e is ErrorExpression) {
                    return null;
                }

                if (MaybeEat(TokenKind.Assign)) {               //  Keyword argument
                    a = FinishKeywordArgument(e);
                } else if (PeekToken(Tokens.KeywordForToken)) {    //  Generator expression
                    a = new Arg(ParseGeneratorExpression(e));
                    Eat(TokenKind.RightParenthesis);
                    a.SetLoc(_globalParent, e.StartIndex, GetEnd());
                    return new Arg[1] { a };       //  Generator expression is the argument
                } else {
                    a = new Arg(e);
                    a.SetLoc(_globalParent, e.StartIndex, e.EndIndex);
                }

                //  Was this all?
                //
                if (MaybeEat(TokenKind.Comma)) {
                } else {
                    Eat(TokenKind.RightParenthesis);
                    a.SetLoc(_globalParent, e.StartIndex, GetEnd());
                    return new Arg[1] { a };
                }
            }

            return FinishArgumentList(a);
        }

        private Arg FinishKeywordArgument(Expression t) {
            NameExpression n = t as NameExpression;
            string name;
            if (n == null) {
                ReportSyntaxError(t.StartIndex, t.EndIndex, "expected name");
                name = null;
            } else {
                name = n.Name;
            }

            Expression val = ParseExpression();
            Arg arg = new Arg(name, val);
            arg.SetLoc(_globalParent, t.StartIndex, val.EndIndex);
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
        private Arg[] FinishArgumentList(Arg first) {
            const TokenKind terminator = TokenKind.RightParenthesis;
            List<Arg> l = new List<Arg>();

            if (first != null) {
                l.Add(first);
            }

            // Parse remaining arguments
            while (true) {
                if (MaybeEat(terminator)) {
                    break;
                }
                int start;
                Arg a;
                if (MaybeEat(TokenKind.Multiply)) {
                    start = GetStart();
                    Expression t = ParseExpression();
                    a = new Arg("*", t);
                } else if (MaybeEat(TokenKind.Power)) {
                    start = GetStart();
                    Expression t = ParseExpression();
                    a = new Arg("**", t);
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
                a.SetLoc(_globalParent, start, GetEnd());
                l.Add(a);
                if (MaybeEat(TokenKind.Comma)) {
                } else {
                    Eat(terminator);
                    break;
                }
            }

            Arg[] ret = l.ToArray();
            return ret;
        }

        private List<Expression> ParseTestList() {
            bool tmp;
            return ParseExpressionList(out tmp);
        }

        private Expression ParseOldExpressionListAsExpr() {
            bool trailingComma;
            List<Expression> l = ParseOldExpressionList(out trailingComma);
            //  the case when no expression was parsed e.g. when we have an empty expression list
            if (l.Count == 0 && !trailingComma) {
                ReportSyntaxError("invalid syntax");
            }
            return MakeTupleOrExpr(l, trailingComma);
        }

        // old_expression_list: old_expression [(',' old_expression)+ [',']]
        private List<Expression> ParseOldExpressionList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseOldExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }
            return l;
        }

        // target_list: target ("," target)* [","] 
        private List<Expression> ParseTargetList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            while (true) {
                l.Add(ParseTarget());

                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }

                trailingComma = true;

                if (NeverTestToken(PeekToken())) break;
            }

            return l;
        }

        // target: identifier | "(" target_list ")"  | "[" target_list "]"  | attributeref  | subscription  | slicing 
        private Expression ParseTarget() {
            Token t = PeekToken();
            switch (t.Kind) {
                case TokenKind.LeftParenthesis: // parenth_form or generator_expression
                case TokenKind.LeftBracket:     // list_display
                    Eat(t.Kind);

                    bool trailingComma;
                    Expression res = MakeTupleOrExpr(ParseTargetList(out trailingComma), trailingComma);

                    if (t.Kind == TokenKind.LeftParenthesis) {
                        Eat(TokenKind.RightParenthesis);
                    } else {
                        Eat(TokenKind.RightBracket);
                    }

                    return res;
                default:        // identifier, attribute ref, subscription, slicing
                    return AddTrailers(ParsePrimary(), false);
            }
        }

        // expression_list: expression (',' expression)* [',']
        private List<Expression> ParseExpressionList(out bool trailingComma) {
            List<Expression> l = new List<Expression>();
            trailingComma = false;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                l.Add(ParseStarExpression());
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }
            return l;
        }

        // 3.x: star_expr: ['*'] expr
        private Expression ParseStarExpression() {
            
            if (MaybeEat(TokenKind.Multiply)) {
                if (!_langVersion.Is3x()) {
                    ReportSyntaxError("invalid syntax");
                }
                var start = GetStart();
                var expr = ParseExpression();
                var res = new StarredExpression(expr);
                res.SetLoc(_globalParent, start, expr.EndIndex);
                return res;
            }

            return ParseExpression();
        }

        private Expression ParseTestListAsExpr(bool maybeAssign = false) {
            if (!NeverTestToken(PeekToken())) {
                var expr = ParseExpression();
                if (!MaybeEat(TokenKind.Comma)) {
                    return expr;
                }

                return ParseTestListAsExpr(expr, maybeAssign);
            } else {
                return ParseTestListAsExprError();
            }
        }

        private Expression ParseTestListAsExpr(Expression expr, bool maybeAssign) {
            List<Expression> l = new List<Expression>();
            l.Add(expr);

            bool trailingComma = true;
            while (true) {
                if (NeverTestToken(PeekToken())) break;
                if (maybeAssign) {
                    l.Add(ParseStarExpression());
                } else {
                    l.Add(ParseExpression());
                }
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
            }
            return MakeTupleOrExpr(l, trailingComma);
        }

        private Expression ParseTestListAsExprError() {
            if (MaybeEat(TokenKind.Indent)) {
                // the error is on the next token which has a useful location, unlike the indent - note we don't have an
                // indent if we're at an EOF.  It'a also an indentation error instead of a syntax error.
                NextToken();
                ReportSyntaxError(GetStart(), GetEnd(), "unexpected indent", ErrorCodes.IndentationError);
            } else {
                ReportSyntaxError(_lookahead);
            }

            return new ErrorExpression();
        }

        private Expression FinishExpressionListAsExpr(Expression expr) {
            var start = GetStart();
            bool trailingComma = true;
            List<Expression> l = new List<Expression>();
            l.Add(expr);

            while (true) {
                if (NeverTestToken(PeekToken())) break;
                expr = ParseExpression();
                l.Add(expr);
                if (!MaybeEat(TokenKind.Comma)) {
                    trailingComma = false;
                    break;
                }
                trailingComma = true;
            }

            Expression ret = MakeTupleOrExpr(l, trailingComma);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        //
        //  testlist_gexp: expression ( genexpr_for | (',' expression)* [','] )
        //
        private Expression FinishTupleOrGenExp() {
            var lStart = GetStart();
            var lEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;
            bool hasRightParenthesis;

            Expression ret;
            //  Empty tuple
            if (MaybeEat(TokenKind.RightParenthesis)) {
                ret = MakeTupleOrExpr(new List<Expression>(), false);
                hasRightParenthesis = true;
            } else if (MaybeEat(TokenKind.KeywordYield)) {
                ret = ParseYieldExpression();
                Eat(TokenKind.RightParenthesis);
                hasRightParenthesis = true;
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;

                    Expression expr = ParseExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        // "(" expression "," ...
                        ret = FinishExpressionListAsExpr(expr);
                    } else if (PeekToken(Tokens.KeywordForToken)) {
                        // "(" expression "for" ...
                        ret = ParseGeneratorExpression(expr);
                    } else {
                        // "(" expression ")"
                        ret = expr is ParenthesisExpression ? expr : new ParenthesisExpression(expr);
                    }
                    hasRightParenthesis = Eat(TokenKind.RightParenthesis);
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            var rStart = GetStart();
            var rEnd = GetEnd();

            ret.SetLoc(_globalParent, lStart, rEnd);
            return ret;
        }

        //  genexpr_for  ::= "for" target_list "in" or_test [genexpr_iter]
        //  genexpr_iter ::= (genexpr_for | genexpr_if) *
        //
        //  "for" has NOT been eaten before entering this method
        private Expression ParseGeneratorExpression(Expression expr) {
            ForStatement root = ParseGenExprFor();
            Statement current = root;

            for (; ; ) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    current = NestGenExpr(current, ParseGenExprFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    current = NestGenExpr(current, ParseGenExprIf());
                } else {
                    // Generator Expressions have an implicit function definition and yield around their expression.
                    //  (x for i in R)
                    // becomes:
                    //   def f(): 
                    //     for i in R: yield (x)
                    ExpressionStatement ys = new ExpressionStatement(new YieldExpression(expr));
                    ys.Expression.SetLoc(_globalParent, expr.IndexSpan);
                    ys.SetLoc(_globalParent, expr.IndexSpan);
                    NestGenExpr(current, ys);
                    break;
                }
            }

            // We pass the outermost iterable in as a parameter because Python semantics
            // say that this one piece is computed at definition time rather than iteration time
            const string fname = "<genexpr>";
            Parameter parameter = new Parameter("__gen_$_parm__", 0);
            FunctionDefinition func = new FunctionDefinition(fname, new Parameter[] { parameter }, root);
            func.IsGenerator = true;
            func.SetLoc(_globalParent, root.StartIndex, GetEnd());
            func.HeaderIndex = root.EndIndex;

            //  Transform the root "for" statement
            Expression outermost = root.List;
            NameExpression ne = new NameExpression("__gen_$_parm__");
            ne.SetLoc(_globalParent, outermost.IndexSpan);
            root.List = ne;

            GeneratorExpression ret = new GeneratorExpression(func, outermost);
            ret.SetLoc(_globalParent, expr.StartIndex, GetEnd());
            return ret;
        }

        private static Statement NestGenExpr(Statement current, Statement nested) {
            ForStatement fes = current as ForStatement;
            IfStatement ifs;
            if (fes != null) {
                fes.Body = nested;
            } else if ((ifs = current as IfStatement) != null) {
                ifs.Tests[0].Body = nested;
            }
            return nested;
        }

        // "for" target_list "in" or_test
        private ForStatement ParseGenExprFor() {
            var start = GetStart();
            Eat(TokenKind.KeywordFor);
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);
            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression expr = null;
            expr = ParseOrTest();

            ForStatement gef = new ForStatement(lhs, expr, null, null);
            var end = GetEnd();
            gef.SetLoc(_globalParent, start, end);
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
            ist.SetLoc(_globalParent, start, end);
            IfStatement gei = new IfStatement(new IfStatementTest[] { ist }, null);
            gei.SetLoc(_globalParent, start, end);
            return gei;
        }


        // dict_display: '{' [dictorsetmaker] '}'
        // dictorsetmaker: ( (test ':' test (comp_for | (',' test ':' test)* [','])) |
        //                   (test (comp_for | (',' test)* [','])) )


        private Expression FinishDictOrSetValue() {
            var oStart = GetStart();
            var oEnd = GetEnd();

            List<SliceExpression> dictMembers = null;
            List<Expression> setMembers = null;
            bool prevAllow = _allowIncomplete;
            bool reportedError = false;
            try {
                _allowIncomplete = true;
                while (true) {
                    if (MaybeEat(TokenKind.RightBrace)) { // empty dict literal
                        break;
                    }
                    bool first = false;
                    Expression e1 = ParseExpression();
                    if (MaybeEat(TokenKind.Colon)) { // dict literal
                        if (setMembers != null) {
                            ReportSyntaxError("invalid syntax");
                        } else if (dictMembers == null) {
                            dictMembers = new List<SliceExpression>();
                            first = true;
                        }
                        Expression e2 = ParseExpression();

                        if (PeekToken(Tokens.KeywordForToken)) {
                            if (!first || _langVersion < PythonLanguageVersion.V27) {
                                ReportSyntaxError("invalid syntax");
                            }

                            return FinishDictComp(e1, e2);
                        }

                        SliceExpression se = new SliceExpression(e1, e2, null, false);
                        se.SetLoc(_globalParent, e1.StartIndex, e2.EndIndex);
                        dictMembers.Add(se);
                    } else { // set literal
                        if (_langVersion < PythonLanguageVersion.V27 && !reportedError) {
                            ReportSyntaxError("invalid syntax");
                            reportedError = true;
                        }
                        if (dictMembers != null) {
                            ReportSyntaxError("invalid syntax");
                        } else if (setMembers == null) {
                            setMembers = new List<Expression>();
                            first = true;
                        }

                        if (PeekToken(Tokens.KeywordForToken)) {
                            if (!first) {
                                ReportSyntaxError("invalid syntax");
                            }
                            return FinishSetComp(e1);
                        }

                        // error recovery
                        if (setMembers != null) {
                            setMembers.Add(e1);
                        }
                    }

                    if (!MaybeEat(TokenKind.Comma)) {
                        Eat(TokenKind.RightBrace);
                        break;
                    }
                }
            } finally {
                _allowIncomplete = prevAllow;
            }


            var cStart = GetStart();
            var cEnd = GetEnd();

            if (dictMembers != null || setMembers == null) {
                SliceExpression[] exprs;
                if (dictMembers != null) {
                    exprs = dictMembers.ToArray();
                } else {
                    exprs = new SliceExpression[0];
                }
                DictionaryExpression ret = new DictionaryExpression(exprs);
                ret.SetLoc(_globalParent, oStart, cEnd);
                return ret;
            } else {
                SetExpression ret = new SetExpression(setMembers.ToArray());
                ret.SetLoc(_globalParent, oStart, cEnd);
                return ret;
            }
        }

        // comp_iter '}'
        private SetComprehension FinishSetComp(Expression item) {
            ComprehensionIterator[] iters = ParseCompIter();
            Eat(TokenKind.RightBrace);
            return new SetComprehension(item, iters);
        }

        // comp_iter '}'
        private DictionaryComprehension FinishDictComp(Expression key, Expression value) {
            ComprehensionIterator[] iters = ParseCompIter();
            Eat(TokenKind.RightBrace);
            return new DictionaryComprehension(key, value, iters);
        }

        // comp_iter: comp_for | comp_if
        private ComprehensionIterator[] ParseCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseCompFor();
            iters.Add(firstFor);

            while (true) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    iters.Add(ParseCompFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iters.Add(ParseCompIf());
                } else {
                    break;
                }
            }

            return iters.ToArray();
        }

        // comp_for: 'for target_list 'in' or_test [comp_iter]
        private ComprehensionFor ParseCompFor() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            // a
            // a,b
            // a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression list = ParseOrTest();

            ComprehensionFor ret = new ComprehensionFor(lhs, list);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // listmaker: expression ( list_for | (',' expression)* [','] )
        private Expression FinishListValue() {
            var oStart = GetStart();
            var oEnd = GetEnd();
            int grouping = _tokenizer.GroupingLevel;

            Expression ret;
            if (MaybeEat(TokenKind.RightBracket)) {
                ret = new ListExpression();
            } else {
                bool prevAllow = _allowIncomplete;
                try {
                    _allowIncomplete = true;
                    Expression t0 = ParseStarExpression();
                    if (MaybeEat(TokenKind.Comma)) {
                        List<Expression> l = ParseTestList();
                        Eat(TokenKind.RightBracket);
                        l.Insert(0, t0);
                        ret = new ListExpression(l.ToArray());
                    } else if (PeekToken(Tokens.KeywordForToken)) {
                        ret = FinishListComp(t0);
                    } else {
                        Eat(TokenKind.RightBracket);
                        ret = new ListExpression(t0);
                    }
                } finally {
                    _allowIncomplete = prevAllow;
                }
            }

            var cStart = GetStart();
            var cEnd = GetEnd();

            ret.SetLoc(_globalParent, oStart, cEnd);
            return ret;
        }

        // list_iter ']'
        private ListComprehension FinishListComp(Expression item) {
            ComprehensionIterator[] iters = ParseListCompIter();
            Eat(TokenKind.RightBracket);
            return new ListComprehension(item, iters);
        }

        // list_iter: list_for | list_if
        private ComprehensionIterator[] ParseListCompIter() {
            List<ComprehensionIterator> iters = new List<ComprehensionIterator>();
            ComprehensionFor firstFor = ParseListCompFor();
            iters.Add(firstFor);

            while (true) {
                if (PeekToken(Tokens.KeywordForToken)) {
                    iters.Add(ParseListCompFor());
                } else if (PeekToken(Tokens.KeywordIfToken)) {
                    iters.Add(ParseCompIf());
                } else {
                    break;
                }
            }

            return iters.ToArray();
        }

        // list_for: 'for' target_list 'in' old_expression_list [list_iter]
        private ComprehensionFor ParseListCompFor() {
            Eat(TokenKind.KeywordFor);
            var start = GetStart();
            bool trailingComma;
            List<Expression> l = ParseTargetList(out trailingComma);

            // expr list is something like:
            //  ()
            //  a
            //  a,b
            //  a,b,c
            // we either want just () or a or we want (a,b) and (a,b,c)
            // so we can do tupleExpr.EmitSet() or loneExpr.EmitSet()

            Expression lhs = MakeTupleOrExpr(l, trailingComma);
            Eat(TokenKind.KeywordIn);

            Expression list;

            if (_langVersion.Is3x()) {
                list = ParseOrTest();
            } else {
                list = ParseOldExpressionListAsExpr();
            }

            ComprehensionFor ret = new ComprehensionFor(lhs, list);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        // list_if: 'if' old_test [list_iter]
        // comp_if: 'if' old_test [comp_iter]
        private ComprehensionIf ParseCompIf() {
            Eat(TokenKind.KeywordIf);
            var start = GetStart();
            Expression expr = ParseOldExpression();
            ComprehensionIf ret = new ComprehensionIf(expr);

            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Expression FinishStringConversion() {
            Expression ret;
            var start = GetStart();
            Expression expr = ParseTestListAsExpr();
            Eat(TokenKind.BackQuote);
            ret = new BackQuoteExpression(expr);
            ret.SetLoc(_globalParent, start, GetEnd());
            return ret;
        }

        private Expression MakeTupleOrExpr(List<Expression> l, bool trailingComma) {
            return MakeTupleOrExpr(l, trailingComma, false);
        }

        private Expression MakeTupleOrExpr(List<Expression> l, bool trailingComma, bool expandable) {
            if (l.Count == 1 && !trailingComma) return l[0];

            Expression[] exprs = l.ToArray();
            TupleExpression te = new TupleExpression(expandable && !trailingComma, exprs);
            if (exprs.Length > 0) {
                te.SetLoc(_globalParent, exprs[0].StartIndex, exprs[exprs.Length - 1].EndIndex);
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
                    if (hasArgsTuple || hasKeywordDict || keywordCount > 0) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "non-keyword arg after keyword arg");
                    }
                } else if (arg.Name == "*") {
                    if (hasArgsTuple || hasKeywordDict) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "only one * allowed");
                    }
                    hasArgsTuple = true; extraArgs++;
                } else if (arg.Name == "**") {
                    if (hasKeywordDict) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "only one ** allowed");
                    }
                    hasKeywordDict = true; extraArgs++;
                } else {
                    if (hasKeywordDict) {
                        ReportSyntaxError(arg.StartIndex, arg.EndIndex, "keywords must come before ** args");
                    }
                    keywordCount++;
                }
            }

            return new CallExpression(target, args);
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            if (_sourceReader != null) {
                _sourceReader.Close();
            }
        }

        #endregion

        #region Implementation Details

        private PythonAst ParseFileWorker() {
            _globalParent = new PythonAst();
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
                if (MaybeEat(TokenKind.EndOfFile)) break;
                if (MaybeEatNewLine()) continue;

                Statement s = ParseStmt();
                l.Add(s);
            }

            Statement[] stmts = l.ToArray();

            SuiteStatement ret = new SuiteStatement(stmts);
            ret.SetLoc(_globalParent, 0, GetEnd());
            return FinishParsing(ret);
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
        /// </summary>
        private bool MaybeEatNewLine() {
            if (MaybeEat(TokenKind.NewLine)) {
                while (MaybeEat(TokenKind.NLToken)) ;
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
        private bool EatNewLine() {
            bool res = Eat(TokenKind.NewLine);
            while (MaybeEat(TokenKind.NLToken)) ;
            return res;
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
        }

        private int GetEnd() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.End;
        }

        private int GetStart() {
            Debug.Assert(_token.Token != null, "No token fetched");
            return _token.Span.Start;
        }

        private Token NextToken() {
            _token = _lookahead;
            FetchLookahead();
            return _token.Token;
        }

        private Token PeekToken() {
            return _lookahead.Token;
        }

        private void FetchLookahead() {
            _lookahead = new TokenWithSpan(_tokenizer.GetNextToken(), _tokenizer.TokenSpan);
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

            public override void Add(string message, int[] lineLocations, int startIndex, int endIndex, int errorCode, Severity severity) {
                if (_parser._errorCode == 0 && (severity == Severity.Error || severity == Severity.FatalError)) {
                    _parser._errorCode = errorCode;
                }

                _parser.ErrorSink.Add(message, lineLocations, startIndex, endIndex, errorCode, severity);
            }
        }

        #endregion

        #region Encoding support

        private static StreamReader/*!*/ GetStreamReaderWithEncoding(Stream/*!*/ stream, Encoding/*!*/ defaultEncoding, ErrorSink errors) {
            // we choose ASCII by default, if the file has a Unicode pheader though
            // we'll automatically get it as unicode.
            Encoding encoding = PythonAsciiEncoding.SourceEncoding;

            long startPosition = stream.Position;

            StreamReader sr = new StreamReader(stream, PythonAsciiEncoding.SourceEncoding);
            byte[] bomBuffer = new byte[3];
            int bomRead = stream.Read(bomBuffer, 0, 3);
            int bytesRead = 0;
            bool isUtf8 = false;
            if (bomRead == 3 && (bomBuffer[0] == 0xef && bomBuffer[1] == 0xbb && bomBuffer[2] == 0xbf)) {
                isUtf8 = true;
                bytesRead = 3;
            } else {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string line;
            try {
                line = ReadOneLine(sr, ref bytesRead);
            } catch (BadSourceException) {
                errors.Add("failed to read encoding", null, 0, 0, ErrorCodes.SyntaxError, Severity.FatalError);
                return new StreamReader(stream, defaultEncoding);
            }

            bool gotEncoding = false;
            string encodingName = null;
            // magic encoding must be on line 1 or 2
            if (line != null && !(gotEncoding = Tokenizer.TryGetEncoding(defaultEncoding, line, ref encoding, out encodingName))) {
                try {
                    line = ReadOneLine(sr, ref bytesRead);
                } catch (BadSourceException) {
                    errors.Add("failed to read encoding", null, 0, 0, ErrorCodes.SyntaxError, Severity.FatalError);
                    return new StreamReader(stream, defaultEncoding);
                }

                if (line != null) {
                    gotEncoding = Tokenizer.TryGetEncoding(defaultEncoding, line, ref encoding, out encodingName);
                }
            }

            if (gotEncoding && isUtf8 && encodingName != "utf-8") {
                // we have both a BOM & an encoding type, throw an error
                errors.Add("file has both Unicode marker and PEP-263 file encoding.  You can only use \"utf-8\" as the encoding name when a BOM is present.", null, 0, 0, ErrorCodes.SyntaxError, Severity.FatalError);
            } else if (encoding == null) {
                return new StreamReader(stream, defaultEncoding);
            }

            // if we didn't get an encoding seek back to the beginning...
            if (!gotEncoding || stream.Position != stream.Length) {
                stream.Seek(startPosition, SeekOrigin.Begin);
            }

            // re-read w/ the correct encoding type...
            return new StreamReader(stream, encoding);
        }

        /// <summary>
        /// Reads one line keeping track of the # of bytes read
        /// </summary>
        private static string ReadOneLine(StreamReader reader, ref int totalRead) {
            Stream sr = reader.BaseStream;
            byte[] buffer = new byte[256];
            StringBuilder builder = null;

            int bytesRead = sr.Read(buffer, 0, buffer.Length);

            while (bytesRead > 0) {
                totalRead += bytesRead;

                bool foundEnd = false;
                for (int i = 0; i < bytesRead; i++) {
                    if (buffer[i] == '\r') {
                        if (i + 1 < bytesRead) {
                            if (buffer[i + 1] == '\n') {
                                totalRead -= (bytesRead - (i + 2));   // skip cr/lf
                                sr.Seek(i + 2, SeekOrigin.Begin);
                                reader.DiscardBufferedData();
                                foundEnd = true;
                            }
                        } else {
                            totalRead -= (bytesRead - (i + 1)); // skip cr
                            sr.Seek(i + 1, SeekOrigin.Begin);
                            reader.DiscardBufferedData();
                            foundEnd = true;
                        }
                    } else if (buffer[i] == '\n') {
                        totalRead -= (bytesRead - (i + 1)); // skip lf
                        sr.Seek(i + 1, SeekOrigin.Begin);
                        reader.DiscardBufferedData();
                        foundEnd = true;
                    }

                    if (foundEnd) {
                        if (builder != null) {
                            builder.Append(Parser.MakeString(buffer), 0, i);
                            return builder.ToString();
                        }
                        return MakeString(buffer).Substring(0, i);
                    }
                }

                if (builder == null) builder = new StringBuilder();
                builder.Append(MakeString(buffer), 0, bytesRead);
                bytesRead = sr.Read(buffer, 0, buffer.Length);
            }

            // no string
            if (builder == null) {
                return null;
            }

            // no new-line
            return builder.ToString();
        }

        #endregion
    }
}
