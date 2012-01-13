using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using System.Reflection;

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
