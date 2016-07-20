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

namespace Microsoft.PythonTools.Parsing {

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dedent")]
    public enum TokenKind {
        EndOfFile = -1,
        Error = 0,
        NewLine = 1,
        Indent = 2,
        Dedent = 3,
        Comment = 4,
        Name = 8,
        Constant = 9,
        Ellipsis = 10,
        Arrow = 11,
        Dot = 31,


        #region Generated Token Kinds

        Add = 32,
        AddEqual = 33,
        Subtract = 34,
        SubtractEqual = 35,
        Power = 36,
        PowerEqual = 37,
        Multiply = 38,
        MultiplyEqual = 39,
        // Defined below
        //MatMultiply = 112,
        //MatMultiplyEqual = 113,
        FloorDivide = 40,
        FloorDivideEqual = 41,
        Divide = 42,
        DivideEqual = 43,
        Mod = 44,
        ModEqual = 45,
        LeftShift = 46,
        LeftShiftEqual = 47,
        RightShift = 48,
        RightShiftEqual = 49,
        BitwiseAnd = 50,
        BitwiseAndEqual = 51,
        BitwiseOr = 52,
        BitwiseOrEqual = 53,
        ExclusiveOr = 54,
        ExclusiveOrEqual = 55,
        LessThan = 56,
        GreaterThan = 57,
        LessThanOrEqual = 58,
        GreaterThanOrEqual = 59,
        Equals = 60,
        NotEquals = 61,
        LessThanGreaterThan = 62,
        LeftParenthesis = 63,
        RightParenthesis = 64,
        LeftBracket = 65,
        RightBracket = 66,
        LeftBrace = 67,
        RightBrace = 68,
        Comma = 69,
        Colon = 70,
        BackQuote = 71,
        Semicolon = 72,
        Assign = 73,
        Twiddle = 74,
        At = 75,

        FirstKeyword = KeywordAnd,
        LastKeyword = KeywordNonlocal,
        KeywordAnd = 76,
        KeywordAssert = 77,
        KeywordBreak = 78,
        KeywordClass = 79,
        KeywordContinue = 80,
        KeywordDef = 81,
        KeywordDel = 82,
        KeywordElseIf = 83,
        KeywordElse = 84,
        KeywordExcept = 85,
        KeywordExec = 86,
        KeywordFinally = 87,
        KeywordFor = 88,
        KeywordFrom = 89,
        KeywordGlobal = 90,
        KeywordIf = 91,
        KeywordImport = 92,
        KeywordIn = 93,
        KeywordIs = 94,
        KeywordLambda = 95,
        KeywordNot = 96,
        KeywordOr = 97,
        KeywordPass = 98,
        KeywordPrint = 99,
        KeywordRaise = 100,
        KeywordReturn = 101,
        KeywordTry = 102,
        KeywordWhile = 103,
        KeywordYield = 104,
        KeywordAs = 105,
        KeywordWith = 106,
        KeywordTrue = 107,
        KeywordFalse = 108,
        KeywordNonlocal = 109,

        #endregion

        NLToken = 110,
        ExplicitLineJoin = 111,
        MatMultiply = 112,
        MatMultiplyEqual = 113,

        KeywordAsync = 114,
        KeywordAwait = 115
    }

    internal static class Tokens {
        public static readonly Token EndOfFileToken = new VerbatimToken(TokenKind.EndOfFile, "", "<eof>");

        public static readonly Token ImpliedNewLineToken = new VerbatimToken(TokenKind.NewLine, "", "<newline>");

        public static readonly Token NewLineToken = new VerbatimToken(TokenKind.NewLine, "\n", "<newline>");
        public static readonly Token NewLineTokenCRLF = new VerbatimToken(TokenKind.NewLine, "\r\n", "<newline>");
        public static readonly Token NewLineTokenCR = new VerbatimToken(TokenKind.NewLine, "\r", "<newline>");
        
        public static readonly Token NLToken = new VerbatimToken(TokenKind.NLToken, "\n", "<NL>");  // virtual token used for error reporting
        public static readonly Token NLTokenCRLF = new VerbatimToken(TokenKind.NLToken, "\r\n", "<NL>");  // virtual token used for error reporting
        public static readonly Token NLTokenCR = new VerbatimToken(TokenKind.NLToken, "\r", "<NL>");  // virtual token used for error reporting

        public static readonly Token IndentToken = new DentToken(TokenKind.Indent, "<indent>");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dedent")]
        public static readonly Token DedentToken = new DentToken(TokenKind.Dedent, "<dedent>");
        public static readonly Token CommentToken = new SymbolToken(TokenKind.Comment, "<comment>");
        public static readonly Token NoneToken = new ConstantValueToken(null);

        public static readonly Token DotToken = new SymbolToken(TokenKind.Dot, ".");

        public static readonly Token Ellipsis = new SymbolToken(TokenKind.Ellipsis, "...");

        private static readonly Token symAddToken = new OperatorToken(TokenKind.Add, "+", 4);
        private static readonly Token symAddEqualToken = new StatementSymbolToken(TokenKind.AddEqual, "+=");
        private static readonly Token symSubtractToken = new OperatorToken(TokenKind.Subtract, "-", 4);
        private static readonly Token symSubtractEqualToken = new StatementSymbolToken(TokenKind.SubtractEqual, "-=");
        private static readonly Token symPowerToken = new OperatorToken(TokenKind.Power, "**", 6);
        private static readonly Token symPowerEqualToken = new StatementSymbolToken(TokenKind.PowerEqual, "**=");
        private static readonly Token symMultiplyToken = new OperatorToken(TokenKind.Multiply, "*", 5);
        private static readonly Token symMultiplyEqualToken = new SymbolToken(TokenKind.MultiplyEqual, "*=");
        private static readonly Token symMatMultiplyToken = new OperatorToken(TokenKind.MatMultiply, "@", 5);
        private static readonly Token symMatMultiplyEqualToken = new SymbolToken(TokenKind.MatMultiplyEqual, "@=");
        private static readonly Token symFloorDivideToken = new OperatorToken(TokenKind.FloorDivide, "//", 5);
        private static readonly Token symFloorDivideEqualToken = new StatementSymbolToken(TokenKind.FloorDivideEqual, "//=");
        private static readonly Token symDivideToken = new OperatorToken(TokenKind.Divide, "/", 5);
        private static readonly Token symDivideEqualToken = new StatementSymbolToken(TokenKind.DivideEqual, "/=");
        private static readonly Token symModToken = new OperatorToken(TokenKind.Mod, "%", 5);
        private static readonly Token symModEqualToken = new StatementSymbolToken(TokenKind.ModEqual, "%=");
        private static readonly Token symLeftShiftToken = new OperatorToken(TokenKind.LeftShift, "<<", 3);
        private static readonly Token symLeftShiftEqualToken = new StatementSymbolToken(TokenKind.LeftShiftEqual, "<<=");
        private static readonly Token symRightShiftToken = new OperatorToken(TokenKind.RightShift, ">>", 3);
        private static readonly Token symRightShiftEqualToken = new StatementSymbolToken(TokenKind.RightShiftEqual, ">>=");
        private static readonly Token symBitwiseAndToken = new OperatorToken(TokenKind.BitwiseAnd, "&", 2);
        private static readonly Token symBitwiseAndEqualToken = new StatementSymbolToken(TokenKind.BitwiseAndEqual, "&=");
        private static readonly Token symBitwiseOrToken = new OperatorToken(TokenKind.BitwiseOr, "|", 0);
        private static readonly Token symBitwiseOrEqualToken = new StatementSymbolToken(TokenKind.BitwiseOrEqual, "|=");
        private static readonly Token symExclusiveOrToken = new OperatorToken(TokenKind.ExclusiveOr, "^", 1);
        private static readonly Token symExclusiveOrEqualToken = new StatementSymbolToken(TokenKind.ExclusiveOrEqual, "^=");
        private static readonly Token symLessThanToken = new OperatorToken(TokenKind.LessThan, "<", -1);
        private static readonly Token symGreaterThanToken = new OperatorToken(TokenKind.GreaterThan, ">", -1);
        private static readonly Token symLessThanOrEqualToken = new OperatorToken(TokenKind.LessThanOrEqual, "<=", -1);
        private static readonly Token symGreaterThanOrEqualToken = new OperatorToken(TokenKind.GreaterThanOrEqual, ">=", -1);
        private static readonly Token symEqualsToken = new OperatorToken(TokenKind.Equals, "==", -1);
        private static readonly Token symNotEqualsToken = new OperatorToken(TokenKind.NotEquals, "!=", -1);
        private static readonly Token symLessThanGreaterThanToken = new SymbolToken(TokenKind.LessThanGreaterThan, "<>");
        private static readonly Token symLeftParenthesisToken = new SymbolToken(TokenKind.LeftParenthesis, "(");
        private static readonly Token symRightParenthesisToken = new SymbolToken(TokenKind.RightParenthesis, ")");
        private static readonly Token symLeftBracketToken = new SymbolToken(TokenKind.LeftBracket, "[");
        private static readonly Token symRightBracketToken = new SymbolToken(TokenKind.RightBracket, "]");
        private static readonly Token symLeftBraceToken = new SymbolToken(TokenKind.LeftBrace, "{");
        private static readonly Token symRightBraceToken = new SymbolToken(TokenKind.RightBrace, "}");
        private static readonly Token symCommaToken = new SymbolToken(TokenKind.Comma, ",");
        private static readonly Token symColonToken = new SymbolToken(TokenKind.Colon, ":");
        private static readonly Token symBackQuoteToken = new SymbolToken(TokenKind.BackQuote, "`");
        private static readonly Token symSemicolonToken = new SymbolToken(TokenKind.Semicolon, ";");
        private static readonly Token symAssignToken = new SymbolToken(TokenKind.Assign, "=");
        private static readonly Token symTwiddleToken = new SymbolToken(TokenKind.Twiddle, "~");
        private static readonly Token symAtToken = new StatementSymbolToken(TokenKind.At, "@");
        private static readonly Token symArrowToken = new SymbolToken(TokenKind.Arrow, "->");

        public static Token AddToken {
            get { return symAddToken; }
        }

        public static Token AddEqualToken {
            get { return symAddEqualToken; }
        }

        public static Token SubtractToken {
            get { return symSubtractToken; }
        }

        public static Token SubtractEqualToken {
            get { return symSubtractEqualToken; }
        }

        public static Token PowerToken {
            get { return symPowerToken; }
        }

        public static Token PowerEqualToken {
            get { return symPowerEqualToken; }
        }

        public static Token MultiplyToken {
            get { return symMultiplyToken; }
        }

        public static Token MultiplyEqualToken {
            get { return symMultiplyEqualToken; }
        }

        public static Token MatMultiplyToken {
            get { return symMatMultiplyToken; }
        }

        public static Token MatMultiplyEqualToken {
            get { return symMatMultiplyEqualToken; }
        }

        public static Token FloorDivideToken {
            get { return symFloorDivideToken; }
        }

        public static Token FloorDivideEqualToken {
            get { return symFloorDivideEqualToken; }
        }

        public static Token DivideToken {
            get { return symDivideToken; }
        }

        public static Token DivideEqualToken {
            get { return symDivideEqualToken; }
        }

        public static Token ModToken {
            get { return symModToken; }
        }

        public static Token ModEqualToken {
            get { return symModEqualToken; }
        }

        public static Token LeftShiftToken {
            get { return symLeftShiftToken; }
        }

        public static Token LeftShiftEqualToken {
            get { return symLeftShiftEqualToken; }
        }

        public static Token RightShiftToken {
            get { return symRightShiftToken; }
        }

        public static Token RightShiftEqualToken {
            get { return symRightShiftEqualToken; }
        }

        public static Token BitwiseAndToken {
            get { return symBitwiseAndToken; }
        }

        public static Token BitwiseAndEqualToken {
            get { return symBitwiseAndEqualToken; }
        }

        public static Token BitwiseOrToken {
            get { return symBitwiseOrToken; }
        }

        public static Token BitwiseOrEqualToken {
            get { return symBitwiseOrEqualToken; }
        }

        public static Token ExclusiveOrToken {
            get { return symExclusiveOrToken; }
        }

        public static Token ExclusiveOrEqualToken {
            get { return symExclusiveOrEqualToken; }
        }

        public static Token LessThanToken {
            get { return symLessThanToken; }
        }

        public static Token GreaterThanToken {
            get { return symGreaterThanToken; }
        }

        public static Token LessThanOrEqualToken {
            get { return symLessThanOrEqualToken; }
        }

        public static Token GreaterThanOrEqualToken {
            get { return symGreaterThanOrEqualToken; }
        }

        public static Token EqualsToken {
            get { return symEqualsToken; }
        }

        public static Token NotEqualsToken {
            get { return symNotEqualsToken; }
        }

        public static Token LessThanGreaterThanToken {
            get { return symLessThanGreaterThanToken; }
        }

        public static Token LeftParenthesisToken {
            get { return symLeftParenthesisToken; }
        }

        public static Token RightParenthesisToken {
            get { return symRightParenthesisToken; }
        }

        public static Token LeftBracketToken {
            get { return symLeftBracketToken; }
        }

        public static Token RightBracketToken {
            get { return symRightBracketToken; }
        }

        public static Token LeftBraceToken {
            get { return symLeftBraceToken; }
        }

        public static Token RightBraceToken {
            get { return symRightBraceToken; }
        }

        public static Token CommaToken {
            get { return symCommaToken; }
        }

        public static Token ColonToken {
            get { return symColonToken; }
        }

        public static Token BackQuoteToken {
            get { return symBackQuoteToken; }
        }

        public static Token SemicolonToken {
            get { return symSemicolonToken; }
        }

        public static Token AssignToken {
            get { return symAssignToken; }
        }

        public static Token TwiddleToken {
            get { return symTwiddleToken; }
        }

        public static Token AtToken {
            get { return symAtToken; }
        }

        public static Token ArrowToken {
            get {
                return symArrowToken;
            }
        }

        private static readonly Token kwAndToken = new SymbolToken(TokenKind.KeywordAnd, "and");
        private static readonly Token kwAsToken = new SymbolToken(TokenKind.KeywordAs, "as");
        private static readonly Token kwAssertToken = new SymbolToken(TokenKind.KeywordAssert, "assert");
        private static readonly Token kwAsyncToken = new SymbolToken(TokenKind.KeywordAsync, "async");
        private static readonly Token kwAwaitToken = new SymbolToken(TokenKind.KeywordAwait, "await");
        private static readonly Token kwBreakToken = new SymbolToken(TokenKind.KeywordBreak, "break");
        private static readonly Token kwClassToken = new SymbolToken(TokenKind.KeywordClass, "class");
        private static readonly Token kwContinueToken = new SymbolToken(TokenKind.KeywordContinue, "continue");
        private static readonly Token kwDefToken = new SymbolToken(TokenKind.KeywordDef, "def");
        private static readonly Token kwDelToken = new SymbolToken(TokenKind.KeywordDel, "del");
        private static readonly Token kwElseIfToken = new SymbolToken(TokenKind.KeywordElseIf, "elif");
        private static readonly Token kwElseToken = new SymbolToken(TokenKind.KeywordElse, "else");
        private static readonly Token kwExceptToken = new SymbolToken(TokenKind.KeywordExcept, "except");
        private static readonly Token kwExecToken = new SymbolToken(TokenKind.KeywordExec, "exec");
        private static readonly Token kwFinallyToken = new SymbolToken(TokenKind.KeywordFinally, "finally");
        private static readonly Token kwForToken = new SymbolToken(TokenKind.KeywordFor, "for");
        private static readonly Token kwFromToken = new SymbolToken(TokenKind.KeywordFrom, "from");
        private static readonly Token kwGlobalToken = new SymbolToken(TokenKind.KeywordGlobal, "global");
        private static readonly Token kwIfToken = new SymbolToken(TokenKind.KeywordIf, "if");
        private static readonly Token kwImportToken = new SymbolToken(TokenKind.KeywordImport, "import");
        private static readonly Token kwInToken = new SymbolToken(TokenKind.KeywordIn, "in");
        private static readonly Token kwIsToken = new SymbolToken(TokenKind.KeywordIs, "is");
        private static readonly Token kwLambdaToken = new SymbolToken(TokenKind.KeywordLambda, "lambda");
        private static readonly Token kwNotToken = new SymbolToken(TokenKind.KeywordNot, "not");
        private static readonly Token kwOrToken = new SymbolToken(TokenKind.KeywordOr, "or");
        private static readonly Token kwPassToken = new SymbolToken(TokenKind.KeywordPass, "pass");
        private static readonly Token kwPrintToken = new SymbolToken(TokenKind.KeywordPrint, "print");
        private static readonly Token kwRaiseToken = new SymbolToken(TokenKind.KeywordRaise, "raise");
        private static readonly Token kwReturnToken = new SymbolToken(TokenKind.KeywordReturn, "return");
        private static readonly Token kwTryToken = new SymbolToken(TokenKind.KeywordTry, "try");
        private static readonly Token kwWhileToken = new SymbolToken(TokenKind.KeywordWhile, "while");
        private static readonly Token kwWithToken = new SymbolToken(TokenKind.KeywordWith, "with");
        private static readonly Token kwYieldToken = new SymbolToken(TokenKind.KeywordYield, "yield");
        private static readonly Token kwTrueToken = new SymbolToken(TokenKind.KeywordTrue, "True");
        private static readonly Token kwFalseToken = new SymbolToken(TokenKind.KeywordFalse, "False");
        private static readonly Token kwNonlocalToken = new SymbolToken(TokenKind.KeywordNonlocal, "nonlocal");


        public static Token KeywordAndToken {
            get { return kwAndToken; }
        }

        public static Token KeywordAsToken {
            get { return kwAsToken; }
        }

        public static Token KeywordAssertToken {
            get { return kwAssertToken; }
        }

        public static Token KeywordAsyncToken {
            get { return kwAsyncToken; }
        }

        public static Token KeywordAwaitToken {
            get { return kwAwaitToken; }
        }

        public static Token KeywordBreakToken {
            get { return kwBreakToken; }
        }

        public static Token KeywordClassToken {
            get { return kwClassToken; }
        }

        public static Token KeywordContinueToken {
            get { return kwContinueToken; }
        }

        public static Token KeywordDefToken {
            get { return kwDefToken; }
        }

        public static Token KeywordDelToken {
            get { return kwDelToken; }
        }

        public static Token KeywordElseIfToken {
            get { return kwElseIfToken; }
        }

        public static Token KeywordElseToken {
            get { return kwElseToken; }
        }

        public static Token KeywordExceptToken {
            get { return kwExceptToken; }
        }

        public static Token KeywordExecToken {
            get { return kwExecToken; }
        }

        public static Token KeywordFinallyToken {
            get { return kwFinallyToken; }
        }

        public static Token KeywordForToken {
            get { return kwForToken; }
        }

        public static Token KeywordFromToken {
            get { return kwFromToken; }
        }

        public static Token KeywordGlobalToken {
            get { return kwGlobalToken; }
        }

        public static Token KeywordIfToken {
            get { return kwIfToken; }
        }

        public static Token KeywordImportToken {
            get { return kwImportToken; }
        }

        public static Token KeywordInToken {
            get { return kwInToken; }
        }

        public static Token KeywordIsToken {
            get { return kwIsToken; }
        }

        public static Token KeywordLambdaToken {
            get { return kwLambdaToken; }
        }

        public static Token KeywordNotToken {
            get { return kwNotToken; }
        }

        public static Token KeywordOrToken {
            get { return kwOrToken; }
        }

        public static Token KeywordPassToken {
            get { return kwPassToken; }
        }

        public static Token KeywordPrintToken {
            get { return kwPrintToken; }
        }

        public static Token KeywordRaiseToken {
            get { return kwRaiseToken; }
        }

        public static Token KeywordReturnToken {
            get { return kwReturnToken; }
        }

        public static Token KeywordTryToken {
            get { return kwTryToken; }
        }

        public static Token KeywordWhileToken {
            get { return kwWhileToken; }
        }

        public static Token KeywordWithToken {
            get { return kwWithToken; }
        }

        public static Token KeywordYieldToken {
            get { return kwYieldToken; }
        }

        public static Token KeywordTrueToken {
            get { return kwTrueToken; }
        }

        public static Token KeywordFalseToken {
            get { return kwFalseToken; }
        }

        public static Token KeywordNonlocalToken {
            get { return kwNonlocalToken; }
        }
    }
}
