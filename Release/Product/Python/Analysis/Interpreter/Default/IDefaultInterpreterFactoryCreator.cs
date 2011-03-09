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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter.Default {
    /// <summary>
    /// Provides a factory for creating a default Python interpreter factory which is configured to run against
    /// a custom Python interpreter.  By default the interpreter factory picks up all interpreters registered
    /// in the registry.  This provides a mechanism to create interpreters whose configuration is stored elsewhere.
    /// </summary>
    public interface IDefaultInterpreterFactoryCreator {
        /// <summary>
        /// Creates a new interpreter factory with the specified options.
        /// </summary>
        IPythonInterpreterFactory CreateInterpreterFactory(Dictionary<InterpreterFactoryOptions, object> options);
    }
}
