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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// Represents a command which can be run from a REPL window.
    /// 
    /// This interface is a MEF contract and can be implemented and exported to add commands to the REPL window.
    /// </summary>
    public interface IReplCommand {

        /// <summary>
        /// Asynchronously executes the command with specified arguments and calls back the given completion when finished.
        /// </summary>
        /// <param name="window">The interactive window.</param>
        /// <param name="completion">Callback to invoke on completion. Do not invoke the callback if this method returns false.</param>
        /// <returns>False if completed synchronously (completion won't be invoked).</returns>
        bool Execute(IReplWindow window, string arguments, Action<ExecutionResult> completion);

        /// <summary>
        /// Gets a description of the REPL command which is displayed when the user asks for help.
        /// </summary>
        string Description {
            get;
        }

        /// <summary>
        /// Gets the text for the actual command.
        /// </summary>
        string Command {
            get;
        }

        /// <summary>
        /// Content to be placed in a toolbar button or null if should not be placed on a toolbar.
        /// </summary>
        object ButtonContent {
            get;
        }
    }
}
