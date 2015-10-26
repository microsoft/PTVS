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

using System.ComponentModel;

namespace Microsoft.PythonTools.Parsing {

    /// <summary>
    /// Defines a kind of the source code. The parser sets its initial state accordingly.
    /// </summary>
    enum SourceCodeKind {
        [EditorBrowsable(EditorBrowsableState.Never)]
        Unspecified = 0,

        /// <summary>
        /// The code is an expression.
        /// </summary>
        Expression = 1,

        /// <summary>
        /// The code is a sequence of statements.
        /// </summary>
        Statements = 2,

        /// <summary>
        /// The code is a single statement.
        /// </summary>
        SingleStatement = 3,

        /// <summary>
        /// The code is a content of a file.
        /// </summary>
        File = 4,

        /// <summary>
        /// The code is an interactive command.
        /// </summary>
        InteractiveCode = 5,

        /// <summary>
        /// The language parser auto-detects the kind. A syntax error is reported if it is not able to do so.
        /// </summary>
        AutoDetect = 6
    }
}