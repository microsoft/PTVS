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
using System.Windows;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// An implementation of a Read Eval Print Loop Window for iteratively developing code.
    /// 
    /// Instances of the repl window can be created by using MEF to import the IReplWindowProvider interface.
    /// </summary>
    public interface IReplWindow {
        /// <summary>
        /// Gets the IWpfTextView in which the REPL window is executing.
        /// </summary>
        IWpfTextView TextView {
            get;
        }

        /// <summary>
        /// The language evaluator used in Repl Window
        /// </summary>
        IReplEvaluator Evaluator {
            get;
        }

        /// <summary>
        /// Title of the Repl Window
        /// </summary>
        string Title {
            get;
        }

        /// <summary>
        /// Clears the REPL window screen.
        /// </summary>
        void ClearScreen();

        /// <summary>
        /// Focuses the window.
        /// </summary>
        void Focus();
        
        /// <summary>
        /// Clears the current input.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Pastes the specified text in as if the user had typed it.
        /// </summary>
        /// <param name="text"></param>
        void PasteText(string text);

        /// <summary>
        /// Resets the execution context clearing all variables.
        /// </summary>
        void Reset();

        /// <summary>
        /// Aborts the current command which is executing.
        /// </summary>
        void AbortCommand();

        /// <summary>
        /// Writes a line into the output buffer as if it was outputted by the program.
        /// </summary>
        /// <param name="text"></param>
        void WriteLine(string text);

        /// <summary>
        /// Writes output to the REPL window.
        /// </summary>
        /// <param name="value"></param>
        void WriteOutput(object value);

        /// <summary>
        /// Writes error output to the REPL window.
        /// </summary>
        /// <param name="value"></param>
        void WriteError(object value);

        /// <summary>
        /// Reads input from the REPL window.
        /// </summary>
        /// <returns></returns>
        string ReadStandardInput();

        /// <summary>
        /// Sets the current value for the specified option.
        /// 
        /// It is safe to call this method from any thread.
        /// </summary>
        void SetOptionValue(ReplOptions option, object value);

        /// <summary>
        /// Gets the current value for the specified option.
        /// 
        /// It is safe to call this method from any thread.
        /// </summary>
        object GetOptionValue(ReplOptions option);
    }
}
