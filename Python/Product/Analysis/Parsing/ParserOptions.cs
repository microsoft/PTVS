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
    public sealed class ParserOptions {
        internal static ParserOptions Default = new ParserOptions();
        public ParserOptions() {
            ErrorSink = ErrorSink.Null;
        }

        public ParserOptions Clone() {
            return (ParserOptions)MemberwiseClone();
        }

        public ErrorSink ErrorSink { get; set; }

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
        /// An event that is raised for every comment in the source as it is parsed.
        /// </summary>
        public event EventHandler<CommentEventArgs> ProcessComment;

        internal void RaiseProcessComment(object sender, CommentEventArgs e) {
            var handler = ProcessComment;
            if (handler != null) {
                handler(sender, e);
            }
        }
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
