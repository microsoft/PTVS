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

namespace Microsoft.PythonTools.Parsing {
    internal struct TokenWithSpan {
        public static readonly TokenWithSpan Empty = new TokenWithSpan();

        private readonly Token _token;
        private readonly IndexSpan _span;

        public TokenWithSpan(Token token, IndexSpan span) {
            _token = token;
            _span = span;
        }

        public IndexSpan Span {
            get { return _span; }
        }

        public Token Token {
            get { return _token; }
        }

    }

    /// <summary>
    /// Summary description for Token.
    /// </summary>
    public abstract class Token {
        private readonly TokenKind _kind;

        internal Token(TokenKind kind) {
            _kind = kind;
        }

        public TokenKind Kind {
            get { return _kind; }
        }

        public virtual object Value {
            get {
                throw new NotSupportedException("no value for this token");
            }
        }

        public override string ToString() {
            return base.ToString() + "(" + _kind + ")";
        }

        /// <summary>
        /// Returns the exact text of the token if it's available.  The text does not
        /// include any leading white space.
        /// </summary>
        public virtual String VerbatimImage {
            get {
                return Image;
            }
        }

        /// <summary>
        /// Returns a user friendly display of the token.
        /// </summary>
        public abstract String Image {
            get;
        }
    }

    internal class ErrorToken : Token {
        private readonly String _message;
        private readonly string _verbatim;

        public ErrorToken(String message, string verbatim)
            : base(TokenKind.Error) {
            _message = message;
            _verbatim = verbatim;
        }

        public String Message {
            get { return _message; }
        }

        public override String Image {
            get { return _message; }
        }

        public override object Value {
            get { return _message; }
        }

        public override string VerbatimImage {
            get {
                return _verbatim;
            }
        }
    }

    internal class IncompleteStringErrorToken : ErrorToken {
        private readonly string _value;

        public IncompleteStringErrorToken(string message, string value)
            : base(message, value) {
            _value = value;
        }

        public override string Image {
            get {
                return _value;
            }
        }

        public override object Value {
            get {
                return _value;
            }
        }
    }

    internal class ConstantValueToken : Token {
        private readonly object _value;

        public ConstantValueToken(object value)
            : base(TokenKind.Constant) {
            _value = value;
        }

        public object Constant {
            get { return this._value; }
        }

        public override object Value {
            get { return _value; }
        }

        public override String Image {
            get {
                return _value == null ? "None" : _value.ToString();
            }
        }
    }

    internal sealed class VerbatimConstantValueToken : ConstantValueToken {
        private readonly string _verbatim;

        public VerbatimConstantValueToken(object value, string verbatim)
            : base(value) {
            _verbatim = verbatim;
        }

        public override string VerbatimImage {
            get {
                return _verbatim;
            }
        }
    }

    class UnicodeStringToken : ConstantValueToken {
        public UnicodeStringToken(object value)
            : base(value) {
        }
    }

    sealed class VerbatimUnicodeStringToken : UnicodeStringToken {
        private readonly string _verbatim;
        
        public VerbatimUnicodeStringToken(object value, string verbatim)
            : base(value) {
                _verbatim = verbatim;
        }

        public override string VerbatimImage {
            get {
                return _verbatim;
            }
        }
    }

    internal sealed class CommentToken : Token {
        private readonly string _comment;

        public CommentToken(string comment)
            : base(TokenKind.Comment) {
            _comment = comment;
        }

        public string Comment {
            get { return _comment; }
        }

        public override string Image {
            get { return _comment; }
        }

        public override object Value {
            get { return _comment; }
        }
    }

    internal class NameToken : Token {
        private readonly string _name;

        public NameToken(string name)
            : base(TokenKind.Name) {
            _name = name;
        }

        public string Name {
            get { return this._name; }
        }

        public override object Value {
            get { return _name; }
        }

        public override String Image {
            get {
                return _name;
            }
        }
    }

    internal sealed class OperatorToken : Token {
        private readonly int _precedence;
        private readonly string _image;

        public OperatorToken(TokenKind kind, string image, int precedence)
            : base(kind) {
            _image = image;
            _precedence = precedence;
        }

        public int Precedence {
            get { return _precedence; }
        }

        public override object Value {
            get { return _image; }
        }

        public override String Image {
            get { return _image; }
        }
    }

    internal class SymbolToken : Token {
        private readonly string _image;

        public SymbolToken(TokenKind kind, String image)
            : base(kind) {
            _image = image;
        }

        public String Symbol {
            get { return _image; }
        }

        public override object Value {
            get { return _image; }
        }

        public override String Image {
            get { return _image; }
        }
    }

    internal sealed class StatementSymbolToken : SymbolToken {
        public StatementSymbolToken(TokenKind kind, String image)
            : base(kind, image) {
        }
    }

    internal class VerbatimToken : SymbolToken {
        private readonly string _verbatimImage;

        public VerbatimToken(TokenKind kind, string verbatimImage, string image)
            : base(kind, image) {
            _verbatimImage = verbatimImage;
        }

        public override string VerbatimImage {
            get {
                return _verbatimImage;
            }
        }
    }

    internal class DentToken : SymbolToken {
        public DentToken(TokenKind kind, String image)
            : base(kind, image) {
        }

        public override string VerbatimImage {
            get {
                // indents are accounted for in whitespace
                return "";
            }
        }
    }
}
