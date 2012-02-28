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

using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Provides an interface for loading/saving of options for the project.
    /// 
    /// New in 1.5:
    ///     Provides access to unevaluated properties
    /// </summary>
    public interface IPythonProject2 : IPythonProject {
        /// <summary>
        /// Gets a property for the project.  Users can get/set their own properties, also these properties
        /// are available:
        /// 
        ///     CommandLineArguments -> arguments to be passed to the debugged program.
        ///     InterpreterPath  -> gets a configured directory where the interpreter should be launched from.
        ///     IsWindowsApplication -> determines whether or not the application is a windows application (for which no console window should be created)
        ///     
        /// The returned property does not have any MSBuild syntax evaluated.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string GetUnevaluatedProperty(string name);
    }
}
