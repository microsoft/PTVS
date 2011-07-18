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

namespace Microsoft.PythonTools.Parsing {
    public sealed class ParserOptions {
        internal static ParserOptions Default = new ParserOptions();
        public ParserOptions() {
            ErrorSink = ErrorSink.Null;
        }

        public ErrorSink ErrorSink { set; get; }
        public Severity IndentationInconsistencySeverity { set; get; }
        public bool Verbatim { get; set; }

        /// <summary>
        /// True if references to variables should be bound in the AST.  The root node must be
        /// held onto to access the references via GetReference/GetReferences APIs on various 
        /// nodes which reference variables.
        /// </summary>
        public bool BindReferences { get; set; }
    }
}
