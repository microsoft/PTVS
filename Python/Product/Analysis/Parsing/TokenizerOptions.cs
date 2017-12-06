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
    [Flags]
    public enum TokenizerOptions {
        None,
        /// <summary>
        /// Tokenizer is in verbatim mode and will track extra information including white space proceeding tokens, the exact representation of constant tokens, etc...
        /// 
        /// Despite the presence of extra information no tokens will be reported which aren't normally reported.  Instead items which would be reported when 
        /// VerbatimCommentsAndLineJoins is also specified will be included in the white space tracking strings.
        /// </summary>
        Verbatim = 0x01,
        /// <summary>
        /// Tokenizer will report comment tokens and explicit line join tokens in addition to the normal tokens it produces.
        /// </summary>
        VerbatimCommentsAndLineJoins = 0x02,
        /// <summary>
        /// Tokenizer will attempt to recover from groupings which contain invalid characters
        /// by ending the grouping automatically.  This allows code like:
        /// 
        /// x = f(
        /// 
        /// def g():
        ///     pass
        ///     
        /// To successfully parse the function defintion even though we have no idea we should
        /// be looking at indents/dedents after the open grouping starts.
        /// </summary>
        GroupingRecovery = 0x04,
        /// <summary>
        /// Enables parsing of stub files. Stub files act like Python 3.6 or later, regardless
        /// of the specified version.
        /// </summary>
        StubFile = 0x08,
    }
}
