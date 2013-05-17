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


namespace Microsoft.PythonTools.Interpreter.Default {
    public enum InterpreterFactoryOptions {
        /// <summary>
        /// Specifies the version of the language which this interpreter uses.  Can be a string ("2.7") or a System.Version object.
        /// 
        /// Default value is 2.7.
        /// </summary>
        Version,
        /// <summary>
        /// Specified a unique identifier for the interpreter.  If one is not provided a new Guid will be created.  Can be a string
        /// or a .NET guid object.
        /// </summary>
        Guid, 
        /// <summary>
        /// Provides a description of the interpreter.  Value must be a string.
        /// </summary>
        Description, 
        /// <summary>
        /// Specifies the path for the Python executable.  Value must be a string.
        /// </summary>
        PythonPath, 
        /// <summary>
        /// Specifies the path for the Python executable for starting GUI applications.  Value must be a string.
        /// </summary>
        PythonWindowsPath, 
        /// <summary>
        /// Specifies the environment variable used for setting sys.path.  Value must be a string, default is "PYTHONPATH".
        /// </summary>
        PathEnvVar, 
        /// <summary>
        /// Specifies the processor architecture of the Python interpreter.  Value can be a string or a System.Reflection.ProcessorArchitecture enum value.  Default is X86.
        /// </summary>
        ProcessorArchitecture
    }
}
