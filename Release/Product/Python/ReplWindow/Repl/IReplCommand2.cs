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
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
#endif

    /// <summary>
    /// Represents a command which can be run from a REPL window.
    /// 
    /// This interface is a MEF contract and can be implemented and exported to add commands to the REPL window.
    /// This is new in v1.5.
    /// </summary>
#if INTERACTIVE_WINDOW
    public interface IInteractiveWindowCommand2 {
#else
    public interface IReplCommand2 : IReplCommand {
#endif
        /// <summary>
        /// Gets a list of aliases for the command.
        /// </summary>
        IEnumerable<string> Aliases {
            get;
        }
    }
}
