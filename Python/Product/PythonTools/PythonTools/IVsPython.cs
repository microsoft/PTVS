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

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides an interface for interacting with Python Tools for Visual
    /// Studio via COM automation.
    /// </summary>
    public interface IVsPython {
        /// <summary>
        /// Opens the Python interactive window with given description.
        /// Equivalent to doing View->Other Windows and selecting the window
        /// with the same name.
        /// </summary>
        void OpenInteractive(string description);
    }
}
