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
    public sealed class ParserOptions {
        public static ParserOptions Default = new ParserOptions();
        public ParserOptions() {
            ErrorSink = ErrorSink.Null;
            InitialSourceLocation = SourceLocation.MinValue;
        }

        public ParserOptions Clone() => (ParserOptions)MemberwiseClone();

        public ErrorSink ErrorSink { get; set; }

        public SourceLocation? InitialSourceLocation { get; set; }

        public Severity IndentationInconsistencySeverity { set; get; }

        public bool Verbatim { get; set; } = false;

        /// <summary>
        /// True if references to variables should be bound in the AST.  The root node must be
        /// held onto to access the references via GetReference/GetReferences APIs on various 
        /// nodes which reference variables.
        /// </summary>
        public bool BindReferences { get; set; }

        /// <summary>
        /// Specifies the class name the parser starts off with for name mangling name expressions.
        /// 
        /// For example __fob would turn into _C__fob if PrivatePrefix is set to C.
        /// </summary>
        public string PrivatePrefix { get; set; }

        /// <summary>
        /// When true, parses with all stub file features.
        /// </summary>
        public bool StubFile { get; set; }

        /// <summary>
        /// When true, Parser behaves as if parsing an f-string expression
        /// </summary>
        public bool ParseFStringExpression { get; set; } = false;
    }

    public class CommentEventArgs : EventArgs {
        public SourceSpan Span { get; private set; }
        public string Text { get; private set; }

        public CommentEventArgs(SourceSpan span, string text) {
            Span = span;
            Text = text;
        }
    }
}
