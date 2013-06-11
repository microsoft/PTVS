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

using System;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// The options that may be passed to
    /// <see cref="IInterpreterWithCompletionDatabase.GenerateCompletionDatabase"/>
    /// </summary>
    [Flags]
    public enum GenerateDatabaseOptions {
        /// <summary>
        /// Runs a full analysis for the interpreter's standard library and
        /// installed packages.
        /// </summary>
        None,
        /// <summary>
        /// Skips analysis if the modification time of every file in a package
        /// is earlier than the database's time. This option prefers false
        /// negatives (that is, analyze something that did not need it) if it is
        /// likely that the results could be outdated.
        /// </summary>
        SkipUnchanged
    }
}
