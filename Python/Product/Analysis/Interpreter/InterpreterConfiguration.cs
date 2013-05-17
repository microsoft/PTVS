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
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    public abstract class InterpreterConfiguration {
        /// <summary>
        /// Returns the path to the interpreter executable for launching Python applications.
        /// </summary>
        public abstract string InterpreterPath {
            get;
        }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python applications
        /// which are windows applications (pythonw.exe, ipyw.exe)
        /// </summary>
        public abstract string WindowsInterpreterPath {
            get;
        }

        /// <summary>
        /// Gets the environment variable which should be used to set sys.path.
        /// </summary>
        public abstract string PathEnvironmentVariable {
            get;
        }

        public abstract ProcessorArchitecture Architecture {
            get;
        }

        /// <summary>
        /// The language version of the interpreter (e.g. 2.7).
        /// </summary>
        public abstract Version Version {
            get;
        }

    }
}
