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
    public class ErrorResult {
        private readonly string _message;
        private readonly SourceSpan _span;

        public ErrorResult(string message, SourceSpan span) {
            _message = message;
            _span = span;
        }

        public string Message {
            get {
                return _message;
            }
        }

        public SourceSpan Span {
            get {
                return _span;
            }
        }
    }
}
