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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing {
    public struct TokenWithSpan {
        public static readonly TokenWithSpan Empty = new TokenWithSpan();

        public TokenWithSpan(Token token, IndexSpan span) {
            Token = token;
            Span = span;
        }

        public IndexSpan Span { get; }
        public Token Token { get; }
    }

    /// <summary>
    /// Summary description for Token.
    /// </summary>
    public abstract class Token {
        public Token(TokenKind kind) {
            Kind = kind;
        }

        public TokenKind Kind { get; }

        public virtual object Value => throw new NotSupportedException("no value for this token");

        public override string ToString() => base.ToString() + "(" + Kind + ")";

        /// <summary>
        /// Returns the exact text of the token if it's available.  The text does not
        /// include any leading white space.
        /// </summary>
        public virtual string VerbatimImage => Image;

        /// <summary>
        /// Returns a user friendly display of the token.
        /// </summary>
        public abstract string Image {
            get;
        }
    }

    public class ErrorToken : Token {
        public ErrorToken(string message, string verbatim)
            : base(TokenKind.Error) {
            Message = message;
            VerbatimImage = verbatim;
        }

        public string Message { get; }

        public override string Image => Message;

        public override object Value => Message;

        public override string VerbatimImage { get; }
    }

    public class IncompleteStringErrorToken : ErrorToken {
        private readonly string _value;

        public IncompleteStringErrorToken(string message, string value)
            : base(message, value) {
            _value = value;
        }

        public override string Image => _value;

        public override object Value => _value;
    }

    public class ConstantValueToken : Token {
        public ConstantValueToken(object value)
            : base(TokenKind.Constant) {
            Value = value;
        }

        public override object Value { get; }

        public override string Image => Value == null ? "None" : Value.ToString();
    }

    public sealed class VerbatimConstantValueToken : ConstantValueToken {
        public VerbatimConstantValueToken(object value, string verbatim)
            : base(value) {
            VerbatimImage = verbatim;
        }

        public override string VerbatimImage { get; }
    }

    public class UnicodeStringToken : ConstantValueToken {
        public UnicodeStringToken(object value)
            : base(value) {
        }
    }

    public sealed class VerbatimUnicodeStringToken : UnicodeStringToken {
        public VerbatimUnicodeStringToken(object value, string verbatim)
            : base(value) {
            VerbatimImage = verbatim;
        }

        public override string VerbatimImage { get; }
    }

    public class FStringToken : Token {
        public FStringToken(string value, string openQuote, bool isTriple, bool isRaw)
            : base(TokenKind.FString) {
            Value = value;
            OpenQuotes = openQuote;
            IsRaw = isRaw;
        }

        public override object Value { get; }

        public string OpenQuotes { get; }

        public bool IsRaw { get; }

        public string Text => (string)Value;

        public override string Image => Value == null ? "None" : $"f{OpenQuotes}{Value.ToString()}{OpenQuotes}";
    }

    public sealed class VerbatimFStringToken : FStringToken {
        public VerbatimFStringToken(string value, string openQuotes, bool isTriple, bool isRaw, string verbatim)
            : base(value, openQuotes, isTriple, isRaw) {
            VerbatimImage = verbatim;
        }

        public override string VerbatimImage { get; }
    }

    public sealed class CommentToken : Token {
        public CommentToken(string comment)
            : base(TokenKind.Comment) {
            Comment = comment;
        }

        public string Comment { get; }
        public override string Image => Comment;
        public override object Value => Comment;
    }

    public class NameToken : Token {
        public NameToken(string name)
            : base(TokenKind.Name) {
            Name = name;
        }

        public string Name { get; }

        public override object Value => Name;

        public override string Image => Name;
    }

    public sealed class OperatorToken : Token {
        public OperatorToken(TokenKind kind, string image, int precedence)
            : base(kind) {
            Image = image;
            Precedence = precedence;
        }

        public int Precedence { get; }

        public override object Value => Image;

        public override string Image { get; }
    }

    public class SymbolToken : Token {
        public SymbolToken(TokenKind kind, string image)
            : base(kind) {
            Image = image;
        }

        public string Symbol => Image;

        public override object Value => Image;

        public override string Image { get; }
    }

    public sealed class StatementSymbolToken : SymbolToken {
        public StatementSymbolToken(TokenKind kind, string image)
            : base(kind, image) {
        }
    }

    public class VerbatimToken : SymbolToken {
        public VerbatimToken(TokenKind kind, string verbatimImage, string image)
            : base(kind, image) {
            VerbatimImage = verbatimImage;
        }

        public override string VerbatimImage { get; }
    }

    public class DentToken : SymbolToken {
        public DentToken(TokenKind kind, string image)
            : base(kind, image) {
        }

        public override string VerbatimImage =>
                // indents are accounted for in whitespace
                "";
    }
}
