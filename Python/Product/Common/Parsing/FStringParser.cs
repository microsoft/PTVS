using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Text;
using Microsoft.PythonTools.Common.Parsing.Ast;

namespace Microsoft.PythonTools.Common.Parsing {
    internal class FStringParser {
        // Readonly parametrized
        private readonly List<Node> _fStringChildren;
        private readonly string _fString;
        private readonly bool _isRaw;
        private readonly ErrorSink _errors;
        private readonly ParserOptions _options;
        private readonly PythonLanguageVersion _langVersion;
        private readonly SourceLocation _start;

        // Nonparametric initialization
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Stack<char> _nestedParens = new Stack<char>();

        // State fields
        private int _position = 0;
        private int _currentLineNumber;
        private int _currentColNumber;
        private bool _hasErrors = false;
        private bool _incomplete = false;

        // Static fields
        private static readonly StringSpan DoubleOpen = new StringSpan("{{");
        private static readonly StringSpan DoubleClose = new StringSpan("}}");
        private static readonly StringSpan NotEqual = new StringSpan("!=");
        private static readonly StringSpan BackslashN = new StringSpan("\\N");

        internal FStringParser(List<Node> fStringChildren, string fString, bool isRaw,
            ParserOptions options, PythonLanguageVersion langVersion) {

            _fString = fString;
            _isRaw = isRaw;
            _fStringChildren = fStringChildren;
            _errors = options.ErrorSink ?? ErrorSink.Null;
            _options = options;
            _langVersion = langVersion;
            _start = options.InitialSourceLocation ?? SourceLocation.MinValue;
            _currentLineNumber = _start.Line;
            _currentColNumber = _start.Column;
        }

        public void Parse() {
            var bufferStartLoc = CurrentLocation;
            while (!EndOfFString) {
                if (IsNext(DoubleOpen)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (IsNext(DoubleClose)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (!_isRaw && IsNext(BackslashN)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    if (CurrentChar == '{') {
                        Read('{');
                        _buffer.Append('{');
                        while (!EndOfFString && CurrentChar != '}') {
                            _buffer.Append(NextChar());
                        }
                        if (Read('}')) {
                            _buffer.Append('}');
                        }
                    } else {
                        _buffer.Append(NextChar());
                    }
                } else if (CurrentChar == '{') {
                    AddBufferedSubstring(bufferStartLoc);
                    ParseInnerExpression();
                    bufferStartLoc = CurrentLocation;
                } else if (CurrentChar == '}') {
                    if (!_incomplete) {
                        ReportSyntaxError(Strings.SingleClosedBraceFStringErrorMsg);
                    }
                    _buffer.Append(NextChar());
                } else {
                    _buffer.Append(NextChar());
                }
            }
            AddBufferedSubstring(bufferStartLoc);
        }

        private bool IsNext(StringSpan span)
            => _fString.Slice(_position, span.Length).Equals(span);

        private void ParseInnerExpression() {
            _fStringChildren.Add(ParseFStringExpression());
        }

        // Inspired on CPython's f-string parsing implementation
        private Node ParseFStringExpression() {
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");

            var startOfFormattedValue = CurrentLocation.Index;
            Read('{');
            var initialPosition = _position;
            SourceLocation initialSourceLocation = CurrentLocation;

            _incomplete = false;

            BufferInnerExpression();
            Expression fStringExpression = null;
            FormattedValue formattedValue;

            if (EndOfFString) {
                if (_nestedParens.Count > 0) {
                    ReportSyntaxError(Strings.UnmatchedFStringErrorMsg.FormatInvariant(_nestedParens.Peek()));
                    _nestedParens.Clear();
                } else {
                    ReportSyntaxError(Strings.ExpectingCharFStringErrorMsg.FormatInvariant('}'));
                }
                if (_buffer.Length != 0) {
                    fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
                    _buffer.Clear();
                } else {
                    fStringExpression = Error(initialPosition);
                }
                formattedValue = new FormattedValue(fStringExpression, null, null);
                formattedValue.SetLoc(new IndexSpan(startOfFormattedValue, CurrentLocation.Index - startOfFormattedValue));
                return formattedValue;
            }
            if (!_hasErrors) {
                fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
                _buffer.Clear();
            } else {
                // Clear and recover
                _buffer.Clear();
            }

            Debug.Assert(CurrentChar == '}' || CurrentChar == '!' || CurrentChar == ':' || CurrentChar == '=');

            MaybeReadEqualSpecifier();
            var conversion = MaybeReadConversionChar();
            var formatSpecifier = MaybeReadFormatSpecifier();

            _incomplete = !Read('}');

            if (fStringExpression == null) {
                return Error(initialPosition);
            }
            formattedValue = new FormattedValue(fStringExpression, conversion, formatSpecifier);
            formattedValue.SetLoc(new IndexSpan(startOfFormattedValue, CurrentLocation.Index - startOfFormattedValue));

            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");
            return formattedValue;
        }

        private SourceLocation CurrentLocation => new SourceLocation(StartIndex + _position, _currentLineNumber, _currentColNumber);

        private void MaybeReadEqualSpecifier() {
            if (_langVersion < PythonLanguageVersion.V38) {
                return;
            }

            if (EndOfFString || CurrentChar != '=') {
                return;
            }

            NextChar();

            while (!EndOfFString && IsAsciiWhiteSpace) {
                NextChar();
            }
        }

        private Expression MaybeReadFormatSpecifier() {
            Debug.Assert(_buffer.Length == 0);

            Expression formatSpecifier = null;
            if (!EndOfFString && CurrentChar == ':') {
                Read(':');
                var position = _position;
                /* Ideally we would just call the FStringParser here. But we are relying on 
                 * an already cut of string, so we need to find the end of the format 
                 * specifier. */
                BufferFormatSpecifier();

                // If we got to the end, there will be an error when we try to read '}'
                if (!EndOfFString) {
                    var options = _options.Clone();
                    options.InitialSourceLocation = new SourceLocation(
                        StartIndex + position,
                        _currentLineNumber,
                        _currentColNumber
                    );
                    var formatStr = _buffer.ToString();
                    _buffer.Clear();
                    var formatSpecifierChildren = new List<Node>();
                    new FStringParser(formatSpecifierChildren, formatStr, _isRaw, options, _langVersion).Parse();
                    formatSpecifier = new FormatSpecifier(formatSpecifierChildren.ToArray(), formatStr);
                    formatSpecifier.SetLoc(new IndexSpan(StartIndex + position, formatStr.Length));
                }
            }

            return formatSpecifier;
        }

        private char? MaybeReadConversionChar() {
            if (!EndOfFString && CurrentChar == '!') {
                Read('!');
                if (EndOfFString) {
                    return null;
                }
                char? conversion = CurrentChar;
                if (conversion == 's' || conversion == 'r' || conversion == 'a') {
                    NextChar();
                    return conversion;
                } else if (conversion == '}' || conversion == ':') {
                    ReportSyntaxError(Strings.InvalidConversionCharacterFStringErrorMsg);
                } else {
                    NextChar();
                    ReportSyntaxError(Strings.InvalidConversionCharacterExpectedFStringErrorMsg.FormatInvariant(conversion));
                }
            }
            return null;
        }

        private void BufferInnerExpression() {
            Debug.Assert(_nestedParens.Count == 0);

            char? quoteChar = null;
            int stringType = 0;

            while (!EndOfFString) {
                var ch = CurrentChar;
                var appendExtra = false;

                if (!quoteChar.HasValue && _nestedParens.Count == 0) {
                    switch (ch) {
                        case '=':
                        case '!':
                            if (!IsEqualsAfterNext) {
                                return;
                            }
                            appendExtra = true;
                            break;

                        case '<':
                        case '>':
                            appendExtra = IsEqualsAfterNext;
                            break;

                        case '}':
                        case ':':
                            return;
                    }
                }

                if (ch == '\\') {
                    ReportSyntaxError(Strings.BackslashFStringExpressionErrorMsg);
                    _buffer.Append(NextChar());
                    continue;
                }

                if (quoteChar.HasValue) {
                    HandleInsideString(ref quoteChar, ref stringType);
                } else {
                    HandleInnerExprOutsideString(ref quoteChar, ref stringType);
                }

                if (appendExtra) {
                    _buffer.Append(NextChar());
                }
            }
        }

        private void BufferFormatSpecifier() {
            Debug.Assert(_nestedParens.Count == 0);

            char? quoteChar = null;
            var stringType = 0;

            while (!EndOfFString) {
                var ch = CurrentChar;
                if (!quoteChar.HasValue && _nestedParens.Count == 0 && (ch == '}')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(NotEqual)) {
                        break;
                    }
                }

                if (quoteChar.HasValue) {
                    /* We're inside a string. See if we're at the end. */
                    HandleInsideString(ref quoteChar, ref stringType);
                } else {
                    HandleFormatSpecOutsideString(ref quoteChar, ref stringType);
                }
            }
        }
        private void HandleFormatSpecOutsideString(ref char? quoteChar, ref int stringType) {
            Debug.Assert(!quoteChar.HasValue);

            var ch = CurrentChar;
            if (ch == '\'' || ch == '"') {
                /* Is this a triple quoted string? */
                quoteChar = ch;
                if (IsNext(new StringSpan(new string(ch, 3)))) {
                    stringType = 3;
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    return;
                } else {
                    /* Start of a normal string. */
                    stringType = 1;
                }
                /* Start looking for the end of the string. */
            } else if ((ch == ')' || ch == '}' || ch == ']') && _nestedParens.Count > 0) {
                var opening = _nestedParens.Pop();
                if (!IsOpeningOf(opening, ch)) {
                    ReportSyntaxError(Strings.ClosingParensNotMatchFStringErrorMsg.FormatInvariant(ch, opening));
                }
            } else if ((ch == ')' || ch == '}' || ch == ']') && _nestedParens.Count == 0) {
                ReportSyntaxError(Strings.UnmatchedFStringErrorMsg.FormatInvariant(ch));
            } else if (ch == '(' || ch == '{' || ch == '[') {
                _nestedParens.Push(ch);
            }

            _buffer.Append(NextChar());
        }

        private void HandleInnerExprOutsideString(ref char? quoteChar, ref int stringType) {
            Debug.Assert(!quoteChar.HasValue);

            var ch = CurrentChar;
            if (ch == '\'' || ch == '"') {
                /* Is this a triple quoted string? */
                quoteChar = ch;
                if (IsNext(new StringSpan(new string(ch, 3)))) {
                    stringType = 3;
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    return;
                } else {
                    /* Start of a normal string. */
                    stringType = 1;
                }
                /* Start looking for the end of the string. */
            } else if (ch == '#') {
                ReportSyntaxError(Strings.NumberSignFStringExpressionErrorMsg);
            } else if ((ch == ')' || ch == '}' || ch == ']') && _nestedParens.Count > 0) {
                var opening = _nestedParens.Pop();
                if (!IsOpeningOf(opening, ch)) {
                    ReportSyntaxError(Strings.ClosingParensNotMatchFStringErrorMsg.FormatInvariant(ch, opening));
                }
            } else if ((ch == ')' || ch == '}' || ch == ']') && _nestedParens.Count == 0) {
                ReportSyntaxError(Strings.UnmatchedFStringErrorMsg.FormatInvariant(ch));
            } else if (ch == '(' || ch == '{' || ch == '[') {
                _nestedParens.Push(ch);
            }

            _buffer.Append(NextChar());
        }

        private static bool IsOpeningOf(char opening, char ch) {
            switch (opening) {
                case '(' when ch == ')':
                case '{' when ch == '}':
                case '[' when ch == ']':
                    return true;
                default:
                    return false;
            }
        }

        private void HandleInsideString(ref char? quoteChar, ref int stringType) {
            Debug.Assert(quoteChar.HasValue);

            var ch = CurrentChar;
            /* We're inside a string. See if we're at the end. */
            if (ch == quoteChar.Value) {
                /* Does this match the string_type (single or triple
                   quoted)? */
                if (stringType == 3) {
                    if (IsNext(new StringSpan(new string(ch, 3)))) {
                        /* We're at the end of a triple quoted string. */
                        _buffer.Append(NextChar());
                        _buffer.Append(NextChar());
                        _buffer.Append(NextChar());
                        stringType = 0;
                        quoteChar = null;
                        return;
                    }
                } else {
                    /* We're at the end of a normal string. */
                    quoteChar = null;
                    stringType = 0;
                }
            }
            _buffer.Append(NextChar());
        }

        private Expression CreateExpression(string subExprStr, SourceLocation initialSourceLocation) {
            if (subExprStr.IsNullOrEmpty()) {
                ReportSyntaxError(Strings.EmptyExpressionFStringErrorMsg);
                return new ErrorExpression(subExprStr, null);
            }
            var parser = Parser.CreateParser(new StringReader(subExprStr), _langVersion, new ParserOptions() {
                ErrorSink = _errors,
                InitialSourceLocation = initialSourceLocation,
                ParseFStringExpression = true
            });
            var expr = parser.ParseFStrSubExpr();
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError(Strings.InvalidExpressionFStringErrorMsg);
                return Error(_position - subExprStr.Length);
            }
            return expr;
        }

        private bool Read(char nextChar) {
            if (EndOfFString) {
                ReportSyntaxError(Strings.ExpectingCharFStringErrorMsg.FormatInvariant(nextChar));
                return false;
            }

            var expected = CurrentChar == nextChar;
            if (!expected) {
                ReportSyntaxError(Strings.ExpectingCharButFoundFStringErrorMsg.FormatInvariant(nextChar, CurrentChar));
            }

            NextChar();
            return expected;
        }

        private void AddBufferedSubstring(SourceLocation bufferStartLoc) {
            if (_buffer.Length == 0) {
                return;
            }
            var s = _buffer.ToString();
            _buffer.Clear();
            var contents = "";
            try {
                contents = LiteralParser.ParseString(s.ToCharArray(),
                0, s.Length, _isRaw, isUni: true, normalizeLineEndings: true, allowTrailingBackslash: true);
            } catch (DecoderFallbackException e) {
                var span = new SourceSpan(bufferStartLoc, CurrentLocation);
                _errors.Add(e.Message, span, ErrorCodes.SyntaxError, Severity.Error);
            } finally {
                var expr = new ConstantExpression(contents);
                expr.SetLoc(new IndexSpan(bufferStartLoc.Index, s.Length));
                _fStringChildren.Add(expr);
            }
        }

        private char NextChar() {
            var prev = CurrentChar;
            _position++;
            _currentColNumber++;
            if (IsLineEnding(prev)) {
                _currentColNumber = 1;
                _currentLineNumber++;
            }
            return prev;
        }

        private int StartIndex => _start.Index;

        private bool IsLineEnding(char prev) => prev == '\n' || (prev == '\\' && IsNext(new StringSpan("n")));

        private char CurrentChar => _fString[_position];

        private bool EndOfFString => _position >= _fString.Length;

        private void ReportSyntaxError(string message) {
            _hasErrors = true;
            var span = new SourceSpan(new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber),
            new SourceLocation(StartIndex + _position + 1, _currentLineNumber, _currentColNumber + 1));
            _errors.Add(message, span, ErrorCodes.SyntaxError, Severity.Error);
        }

        private ErrorExpression Error(int startPos, string verbatimImage = null, Expression preceding = null) {
            verbatimImage = verbatimImage ?? (_fString.Substring(startPos, _position - startPos));
            var expr = new ErrorExpression(verbatimImage, preceding);
            expr.SetLoc(StartIndex + startPos, StartIndex + _position);
            return expr;
        }

        private bool IsAsciiWhiteSpace {
            get {
                switch (CurrentChar) {
                    case '\t':
                    case '\n':
                    case '\v':
                    case '\f':
                    case '\r':
                    case ' ':
                        return true;
                }
                return false;
            }
        }

        private bool IsEqualsAfterNext => _position + 1 < _fString.Length && _fString[_position + 1] == '=';
    }
}
