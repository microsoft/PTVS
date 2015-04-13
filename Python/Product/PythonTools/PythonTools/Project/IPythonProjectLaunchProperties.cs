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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Defines an interface for providing Python-specific launch parameters.
    /// </summary>
    public interface IPythonProjectLaunchProperties : IProjectLaunchProperties {
        /// <summary>
        /// Gets the path of the interpreter to launch. May throw an exception
        /// if no interpreter is available.
        /// </summary>
        string GetInterpreterPath();

        /// <summary>
        /// Gets the arguments to provide to the interpreter.
        /// </summary>
        string GetInterpreterArguments();
        
        /// <summary>
        /// True if the project does not start with a console window.
        /// </summary>
        bool? GetIsWindowsApplication();

        /// <summary>
        /// True if mixed-mode debugging should be used.
        /// </summary>
        bool? GetIsNativeDebuggingEnabled();
    }
}
