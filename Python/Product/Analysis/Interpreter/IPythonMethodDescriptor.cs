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


namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Represents a method descriptor for an instance of a function.
    /// </summary>
    public interface IPythonMethodDescriptor : IMember {
        /// <summary>
        /// The built-in function that the method descriptor wraps.
        /// </summary>
        IPythonFunction Function {
            get;
        }

        /// <summary>
        /// True if the method is already bound to an instance.
        /// </summary>
        bool IsBound {
            get;
        }
    }
}
