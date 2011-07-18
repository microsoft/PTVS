using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using System.Reflection;

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
