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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides a source of Python interpreters.  This enables a single implementation
    /// to dynamically lookup the installed Python versions and provide multiple interpreters.
    /// </summary>
    public interface IPythonInterpreterFactoryProvider {
        /// <summary>
        /// Returns the interpreter factories that this provider supports.  
        /// 
        /// The factories returned should be the same instances for subsequent calls.  If the number 
        /// of available factories can change at runtime new factories can still be returned but the 
        /// existing instances should not be re-created.
        /// </summary>
        IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories();

        /// <summary>
        /// Raised when the result of calling <see cref="GetInterpreterFactories"/> may have changed.
        /// </summary>
        /// <remarks>New in 2.0.</remarks>
        event EventHandler InterpreterFactoriesChanged;
    }
}
