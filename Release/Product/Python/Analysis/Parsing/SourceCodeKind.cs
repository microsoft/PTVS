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